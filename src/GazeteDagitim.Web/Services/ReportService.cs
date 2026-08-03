using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Services;

public sealed class ReportService(AppDbContext dbContext) : IReportService
{
    private static readonly TimeZoneInfo BusinessTimeZone =
        ResolveBusinessTimeZone();

    public async Task<ReportSummary> GetSummaryAsync(
        DateOnly start,
        DateOnly end,
        int? distributorId = null,
        DistributorZone? zone = null,
        CancellationToken cancellationToken = default)
    {
        DomainRules.EnsureValidRange(start, end);
        if (distributorId is not null && zone is not null)
        {
            throw new DomainValidationException(
                "Dağıtıcı ve bölge filtreleri aynı anda kullanılamaz.");
        }
        if (distributorId is not null &&
            !await dbContext.Distributors.AnyAsync(
                value => value.Id == distributorId.Value,
                cancellationToken))
        {
            throw new EntityNotFoundException("Dağıtıcı bulunamadı.");
        }
        if (zone is not null && !Enum.IsDefined(zone.Value))
        {
            throw new DomainValidationException("Bölge değeri geçersizdir.");
        }

        var deliveryQuery = dbContext.Deliveries
            .AsNoTracking()
            .Where(value => value.Date >= start && value.Date <= end);
        var paymentQuery = dbContext.Payments
            .AsNoTracking()
            .Where(value => value.Date >= start && value.Date <= end);

        if (distributorId is not null)
        {
            deliveryQuery = deliveryQuery.Where(
                value => value.DistributorId == distributorId.Value);
            paymentQuery = paymentQuery.Where(
                value => value.DistributorId == distributorId.Value);
        }
        else if (zone is not null)
        {
            deliveryQuery = deliveryQuery.Where(
                value => value.Distributor.Zone == zone.Value);
            paymentQuery = paymentQuery.Where(
                value => value.Distributor.Zone == zone.Value);
        }

        var deliveries = await deliveryQuery.ToListAsync(cancellationToken);
        var payments = await paymentQuery.ToListAsync(cancellationToken);
        var totalNewspapers = deliveries.Sum(value => value.NewspaperCount);
        var totalAmount = SumAmounts(payments.Select(value => value.Amount));
        var paidAmount = SumAmounts(payments
            .Where(value => value.Status == PaymentStatus.Paid)
            .Select(value => value.Amount));
        var pendingAmount = DomainRules.RoundCurrency(totalAmount - paidAmount);
        var collectionRate = totalAmount > 0m
            ? Math.Round(
                paidAmount / totalAmount * 100m,
                2,
                MidpointRounding.AwayFromZero)
            : 0m;

        return new ReportSummary(
            start,
            end,
            totalNewspapers,
            totalAmount,
            paidAmount,
            pendingAmount,
            collectionRate);
    }

    public async Task<SubscriberCollectionSummary>
        GetSubscriberCollectionSummaryAsync(
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken = default)
    {
        DomainRules.EnsureValidRange(start, end);

        var subscribers = await dbContext.Subscribers
            .AsNoTracking()
            .Include(value => value.PaymentPeriod)
            .Include(value => value.PaymentDeferrals)
            .Where(value => value.PaymentPeriodId != null)
            .ToListAsync(cancellationToken);
        var dueTotal = 0m;
        foreach (var subscriber in subscribers)
        {
            var activeDeferrals = subscriber.PaymentDeferrals
                .Where(value => value.CancelledAt is null)
                .ToDictionary(
                    value => value.OriginalDueDate,
                    value => value.DeferredUntil);
            var scheduledPayments =
                SubscriberPaymentScheduleRules.GetScheduledPayments(
                    subscriber,
                    end,
                    start);
            foreach (var scheduledPayment in scheduledPayments)
            {
                var effectiveDueDate = activeDeferrals.GetValueOrDefault(
                    scheduledPayment.OriginalDueDate,
                    scheduledPayment.OriginalDueDate);
                if (effectiveDueDate >= start && effectiveDueDate <= end)
                {
                    dueTotal += scheduledPayment.Amount;
                }
            }
        }

        var startUtc = ToUtcBoundary(start);
        var endExclusive = end == DateOnly.MaxValue
            ? DateTimeOffset.MaxValue
            : ToUtcBoundary(end.AddDays(1));
        var collectedTotal = await dbContext.SubscriberDailyDeliveries
            .AsNoTracking()
            .Where(value =>
                value.IsCollected &&
                ((value.CollectedAt.HasValue &&
                  value.CollectedAt.Value >= startUtc &&
                  value.CollectedAt.Value < endExclusive) ||
                 (!value.CollectedAt.HasValue &&
                  value.Date >= start &&
                  value.Date <= end)))
            .SumAsync(
                value => (decimal?)value.Amount,
                cancellationToken) ?? 0m;

        return new SubscriberCollectionSummary(
            start,
            end,
            DomainRules.RoundCurrency(dueTotal),
            DomainRules.RoundCurrency(collectedTotal));
    }

    private static decimal SumAmounts(IEnumerable<decimal> amounts) =>
        DomainRules.RoundCurrency(amounts.Sum());

    private static DateTimeOffset ToUtcBoundary(DateOnly date)
    {
        var local = date.ToDateTime(
            TimeOnly.MinValue,
            DateTimeKind.Unspecified);
        var offset = BusinessTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next platform-specific identifier.
            }
            catch (InvalidTimeZoneException)
            {
                // Try the next platform-specific identifier.
            }
        }

        return TimeZoneInfo.Utc;
    }
}
