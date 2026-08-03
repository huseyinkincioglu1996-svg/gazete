using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Services;

public sealed class PeriodicPaymentService(AppDbContext dbContext)
    : IPeriodicPaymentService
{
    public async Task<PeriodicJobSummary> CreateScheduledDeliveriesAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var businessDay = DomainRules.ToBusinessDay(date);
        var scheduledDistributors = await dbContext.Distributors
            .Where(value =>
                value.IsActive &&
                value.DistributionDays.Any(day => day.Day == businessDay))
            .ToListAsync(cancellationToken);
        var scheduledIds = scheduledDistributors.Select(value => value.Id).ToArray();
        var existingIds = await dbContext.Deliveries
            .Where(value =>
                value.Date == date &&
                scheduledIds.Contains(value.DistributorId))
            .Select(value => value.DistributorId)
            .ToListAsync(cancellationToken);
        var existingSet = existingIds.ToHashSet();

        foreach (var distributor in scheduledDistributors)
        {
            if (existingSet.Contains(distributor.Id))
            {
                continue;
            }

            dbContext.Deliveries.Add(new Delivery
            {
                DistributorId = distributor.Id,
                Date = date,
                Day = businessDay,
                NewspaperCount = 0,
                Amount = 0m,
                Status = DeliveryStatus.Pending
            });
        }

        var created = scheduledDistributors.Count - existingSet.Count;
        try
        {
            if (created > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return new PeriodicJobSummary(
                Created: created,
                Existing: existingSet.Count,
                Skipped: 0,
                Failed: 0);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in dbContext.ChangeTracker
                         .Entries<Delivery>()
                         .Where(value => value.State == EntityState.Added))
            {
                entry.State = EntityState.Detached;
            }

            var nowExisting = await dbContext.Deliveries
                .AsNoTracking()
                .CountAsync(
                    value => value.Date == date &&
                             scheduledIds.Contains(value.DistributorId),
                    cancellationToken);
            return new PeriodicJobSummary(
                Created: 0,
                Existing: nowExisting,
                Skipped: 0,
                Failed: Math.Max(0, scheduledDistributors.Count - nowExisting));
        }
    }

    public async Task<PeriodicJobSummary> CreateScheduledPaymentsAsync(
        PaymentType paymentType,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(paymentType))
        {
            throw new DomainValidationException("Ödeme türü geçersizdir.");
        }

        var distributors = await dbContext.Distributors
            .Where(value => value.IsActive && value.PaymentType == paymentType)
            .Include(value => value.WeeklyPaymentDays)
            .Include(value => value.MonthlyPaymentDays)
            .ToListAsync(cancellationToken);
        distributors = distributors
            .Where(value => IsScheduledPaymentDay(value, paymentType, date))
            .ToList();

        var created = 0;
        var existing = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var distributor in distributors)
        {
            try
            {
                var existingPayment = await dbContext.Payments
                    .SingleOrDefaultAsync(
                        value =>
                            value.DistributorId == distributor.Id &&
                            value.PaymentType == paymentType &&
                            value.PeriodEnd == date,
                        cancellationToken);
                var period = existingPayment is null
                    ? await NextPaymentPeriodAsync(
                        distributor.Id,
                        paymentType,
                        date,
                        cancellationToken)
                    : (
                        Start: existingPayment.PeriodStart,
                        End: existingPayment.PeriodEnd);
                if (period is null)
                {
                    existing++;
                    continue;
                }

                var deliveries = await dbContext.Deliveries
                    .AsNoTracking()
                    .Where(value =>
                        value.DistributorId == distributor.Id &&
                        value.Date >= period.Value.Start &&
                        value.Date <= period.Value.End &&
                        value.Status == DeliveryStatus.Completed)
                    .ToListAsync(cancellationToken);
                if (deliveries.Count == 0 && existingPayment is null)
                {
                    skipped++;
                    continue;
                }

                if (distributor.NewspaperPrice < 0)
                {
                    throw new DomainValidationException(
                        "Dağıtıcı gazete fiyatı negatif olamaz.");
                }

                var totalNewspapers = deliveries.Sum(value => value.NewspaperCount);
                if (totalNewspapers < 0)
                {
                    throw new DomainValidationException(
                        "Tamamlanan dağıtımdaki gazete sayısı geçersizdir.");
                }

                var amount = DomainRules.RoundCurrency(
                    totalNewspapers * distributor.NewspaperPrice);
                var description =
                    $"{PaymentTypeLabel(paymentType)}: {totalNewspapers} gazete × " +
                    $"{distributor.NewspaperPrice:0.##}₺ = {amount:0.##}₺";

                if (existingPayment is not null)
                {
                    if (!Enum.IsDefined(existingPayment.Status))
                    {
                        failed++;
                        continue;
                    }

                    if (existingPayment.Status == PaymentStatus.Paid)
                    {
                        if (existingPayment.PaidAt is null ||
                            existingPayment.Amount != amount)
                        {
                            failed++;
                        }
                        else
                        {
                            existing++;
                        }

                        continue;
                    }

                    if (existingPayment.PaidAt is not null)
                    {
                        failed++;
                        continue;
                    }

                    existingPayment.Amount = amount;
                    existingPayment.Description = description;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    existing++;
                    continue;
                }

                var payment = new Payment
                {
                    DistributorId = distributor.Id,
                    PaymentType = paymentType,
                    PeriodStart = period.Value.Start,
                    PeriodEnd = period.Value.End,
                    Date = date,
                    Amount = amount,
                    Description =
                        $"{PaymentTypeLabel(paymentType)}: {totalNewspapers} gazete × " +
                        $"{distributor.NewspaperPrice:0.##}₺ = {amount:0.##}₺",
                    Status = PaymentStatus.Pending,
                    PaidAt = null
                };
                payment.Description = description;
                dbContext.Payments.Add(payment);

                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    created++;
                }
                catch (DbUpdateException)
                {
                    dbContext.Entry(payment).State = EntityState.Detached;
                    var nowExists = await dbContext.Payments
                        .AsNoTracking()
                        .AnyAsync(value =>
                                value.DistributorId == distributor.Id &&
                                value.PaymentType == paymentType &&
                                value.PeriodStart == period.Value.Start &&
                                value.PeriodEnd == period.Value.End,
                            cancellationToken);
                    if (nowExists)
                    {
                        existing++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                foreach (var entry in dbContext.ChangeTracker
                             .Entries<Payment>()
                             .Where(value =>
                                 value.State is EntityState.Added or EntityState.Modified))
                {
                    entry.State = EntityState.Detached;
                }

                failed++;
            }
        }

        return new PeriodicJobSummary(created, existing, skipped, failed);
    }

    private async Task<(DateOnly Start, DateOnly End)?> NextPaymentPeriodAsync(
        int distributorId,
        PaymentType paymentType,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        var alreadyClosed = await dbContext.Payments
            .AsNoTracking()
            .AnyAsync(value =>
                    value.DistributorId == distributorId &&
                    value.PaymentType == paymentType &&
                    value.PeriodEnd == periodEnd,
                cancellationToken);
        if (alreadyClosed)
        {
            return null;
        }

        if (paymentType == PaymentType.Daily)
        {
            return (periodEnd, periodEnd);
        }

        var previousEnd = await dbContext.Payments
            .AsNoTracking()
            .Where(value =>
                value.DistributorId == distributorId &&
                value.PaymentType == paymentType &&
                value.PeriodEnd < periodEnd)
            .OrderByDescending(value => value.PeriodEnd)
            .Select(value => (DateOnly?)value.PeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);

        var periodStart = previousEnd?.AddDays(1) ??
                          InitialPeriodStart(paymentType, periodEnd);
        return periodStart > periodEnd
            ? null
            : (periodStart, periodEnd);
    }

    private static DateOnly InitialPeriodStart(
        PaymentType paymentType,
        DateOnly periodEnd) =>
        paymentType switch
        {
            PaymentType.Daily => periodEnd,
            PaymentType.Weekly => periodEnd.AddDays(-6),
            PaymentType.Monthly => periodEnd.AddDays(1).AddMonths(-1),
            _ => throw new DomainValidationException("Ödeme türü geçersizdir.")
        };

    private static bool IsScheduledPaymentDay(
        Distributor distributor,
        PaymentType paymentType,
        DateOnly date)
    {
        if (paymentType == PaymentType.Daily)
        {
            return true;
        }

        if (paymentType == PaymentType.Weekly)
        {
            var day = DomainRules.ToBusinessDay(date);
            return distributor.WeeklyPaymentDays.Any(value => value.Day == day);
        }

        var lastDay = DateTime.DaysInMonth(date.Year, date.Month);
        return date.Day == lastDay
            ? distributor.MonthlyPaymentDays.Any(
                value => value.DayOfMonth >= date.Day)
            : distributor.MonthlyPaymentDays.Any(
                value => value.DayOfMonth == date.Day);
    }

    private static string PaymentTypeLabel(PaymentType paymentType) =>
        paymentType switch
        {
            PaymentType.Daily => "Günlük",
            PaymentType.Weekly => "Haftalık",
            PaymentType.Monthly => "Aylık",
            _ => "Ödeme"
        };
}
