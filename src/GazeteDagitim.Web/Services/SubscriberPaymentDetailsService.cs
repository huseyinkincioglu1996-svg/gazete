using System.Data;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Services;

public enum SubscriberPaymentMovementType
{
    Due,
    Collection,
    Deferral,
    DeferralCancellation
}

public sealed record SubscriberPaymentPlanResult(
    string Name,
    DateOnly StartedOn,
    int CollectionDayOfMonth,
    TimeOnly CollectionTime,
    int CoveredDayCount,
    decimal Amount,
    string ScheduleLabel);

public sealed record SubscriberPaymentDueRow(
    DateOnly OriginalDueDate,
    DateOnly EffectiveDueDate,
    decimal Amount,
    int CoveredDayCount,
    decimal Balance,
    string Status,
    bool IsDeferred);

public sealed record SubscriberCollectionHistoryRow(
    DateOnly Date,
    TimeOnly? Time,
    decimal Amount,
    SubscriberPaymentMethod PaymentMethod,
    string DistributorName,
    string PaymentPeriodName,
    int? CoveredDayCount,
    bool IsLegacyTimestamp);

public sealed record SubscriberPaymentDeferralRow(
    int Id,
    DateOnly OriginalDueDate,
    DateOnly PreviousDueDate,
    DateOnly DeferredUntil,
    string Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt);

public sealed record SubscriberPaymentMovementRow(
    DateOnly Date,
    TimeOnly? Time,
    SubscriberPaymentMovementType Type,
    string Title,
    string Description,
    decimal? Amount,
    bool ReducesBalance,
    string Status);

public sealed record SubscriberPaymentDetailsResult(
    int SubscriberId,
    string SubscriberName,
    string Phone,
    string Address,
    string DistributorName,
    bool IsActive,
    SubscriberPaymentPlanResult? Plan,
    SubscriberPaymentDueRow? NextDue,
    SubscriberPaymentDeferralRow? ActiveDeferral,
    DateOnly EarliestDeferralDate,
    DateOnly LatestDeferralDate,
    decimal ExpectedTotal,
    decimal CollectedTotal,
    decimal OutstandingBalance,
    decimal AdvanceBalance,
    decimal OverdueBalance,
    IReadOnlyList<SubscriberCollectionHistoryRow> Collections,
    IReadOnlyList<SubscriberPaymentDeferralRow> Deferrals,
    IReadOnlyList<SubscriberPaymentMovementRow> Movements);

public interface ISubscriberPaymentDetailsService
{
    Task<SubscriberPaymentDetailsResult> GetAsync(
        int subscriberId,
        CancellationToken cancellationToken = default);

    Task<SubscriberPaymentDetailsResult> DeferAsync(
        int subscriberId,
        DateOnly originalDueDate,
        DateOnly deferredUntil,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<SubscriberPaymentDetailsResult> CancelDeferralAsync(
        int subscriberId,
        int deferralId,
        CancellationToken cancellationToken = default);
}

public sealed class SubscriberPaymentDetailsService(
    AppDbContext dbContext,
    IBusinessClock businessClock)
    : ISubscriberPaymentDetailsService
{
    private readonly TimeZoneInfo _businessTimeZone = ResolveBusinessTimeZone();

    public async Task<SubscriberPaymentDetailsResult> GetAsync(
        int subscriberId,
        CancellationToken cancellationToken = default)
    {
        var subscriber = await LoadSubscriberAsync(
            subscriberId,
            tracking: false,
            cancellationToken);
        return BuildDetails(subscriber);
    }

    public async Task<SubscriberPaymentDetailsResult> DeferAsync(
        int subscriberId,
        DateOnly originalDueDate,
        DateOnly deferredUntil,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length > 500)
        {
            throw new DomainValidationException(
                "Erteleme nedeni en fazla 500 karakter olabilir.");
        }
        var latestAllowedDate = businessClock.Today.AddYears(5);
        if (deferredUntil <= businessClock.Today)
        {
            throw new DomainValidationException(
                "Yeni ödeme tarihi bugünden sonra olmalıdır.");
        }
        if (deferredUntil > latestAllowedDate)
        {
            throw new DomainValidationException(
                $"Ödeme tarihi en geç {latestAllowedDate:dd.MM.yyyy} tarihine ertelenebilir.");
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using var transaction = dbContext.Database.IsRelational()
                    ? await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken)
                    : null;

                var subscriber = await LoadSubscriberAsync(
                    subscriberId,
                    tracking: true,
                    cancellationToken);
                var plan = BuildPlan(subscriber);
                if (plan is null)
                {
                    throw new DomainConflictException(
                        "Abonenin ertelenebilir bir ödeme planı bulunmuyor.");
                }

                if (SubscriberPaymentScheduleRules.GetScheduledPaymentOn(
                        subscriber,
                        originalDueDate,
                        plan.StartedOn) is null)
                {
                    throw new DomainValidationException(
                        "Seçilen tarih abonenin ödeme planına ait bir vade değildir.");
                }

                var activeDeferral = subscriber.PaymentDeferrals
                    .SingleOrDefault(value =>
                        value.OriginalDueDate == originalDueDate &&
                        value.CancelledAt is null);
                var currentDueDate = activeDeferral?.DeferredUntil ?? originalDueDate;
                if (deferredUntil <= currentDueDate)
                {
                    throw new DomainValidationException(
                        "Yeni ödeme tarihi mevcut ödeme gününden sonra olmalıdır.");
                }

                var dueRows = BuildDueRows(
                    subscriber,
                    plan,
                    endDate: MaxDate(
                        businessClock.Today.AddYears(1),
                        deferredUntil.AddMonths(1)));
                var due = dueRows.SingleOrDefault(
                    value => value.OriginalDueDate == originalDueDate);
                if (due is null)
                {
                    throw new DomainValidationException(
                        "Ertelenecek ödeme vadesi bulunamadı.");
                }
                if (due.Balance <= 0)
                {
                    throw new DomainConflictException(
                        "Tamamı ödenmiş bir ödeme vadesi ertelenemez.");
                }
                var nextOpenDue = dueRows
                    .Where(value => value.Balance > 0)
                    .OrderBy(value => value.EffectiveDueDate)
                    .ThenBy(value => value.OriginalDueDate)
                    .First();
                if (nextOpenDue.OriginalDueDate != originalDueDate)
                {
                    throw new DomainValidationException(
                        "Yalnızca sıradaki açık ödeme vadesi ertelenebilir.");
                }

                if (activeDeferral is not null)
                {
                    activeDeferral.CancelledAt = businessClock.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                subscriber.PaymentDeferrals.Add(new SubscriberPaymentDeferral
                {
                    SubscriberId = subscriber.Id,
                    OriginalDueDate = originalDueDate,
                    PreviousDueDate = currentDueDate,
                    DeferredUntil = deferredUntil,
                    Reason = normalizedReason
                });

                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            });
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            throw new DomainConflictException(
                "Ödeme ertelemesi eş zamanlı başka bir işlem nedeniyle kaydedilemedi.");
        }

        dbContext.ChangeTracker.Clear();
        return await GetAsync(subscriberId, cancellationToken);
    }

    public async Task<SubscriberPaymentDetailsResult> CancelDeferralAsync(
        int subscriberId,
        int deferralId,
        CancellationToken cancellationToken = default)
    {
        var subscriberExists = await dbContext.Subscribers
            .AsNoTracking()
            .AnyAsync(value => value.Id == subscriberId, cancellationToken);
        if (!subscriberExists)
        {
            throw new EntityNotFoundException($"Abone bulunamadı: {subscriberId}");
        }

        var deferral = await dbContext.SubscriberPaymentDeferrals
            .SingleOrDefaultAsync(
                value =>
                    value.Id == deferralId &&
                    value.SubscriberId == subscriberId,
                cancellationToken);
        if (deferral is null)
        {
            throw new EntityNotFoundException("Ödeme ertelemesi bulunamadı.");
        }
        if (deferral.CancelledAt is not null)
        {
            throw new DomainConflictException("Ödeme ertelemesi zaten geri alınmış.");
        }

        deferral.CancelledAt = businessClock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return await GetAsync(subscriberId, cancellationToken);
    }

    private async Task<Subscriber> LoadSubscriberAsync(
        int subscriberId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Subscriber> query = dbContext.Subscribers
            .Include(value => value.PaymentPeriod)
            .Include(value => value.Distributor)
            .Include(value => value.DailyDeliveries)
            .Include(value => value.PaymentDeferrals);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(
                   value => value.Id == subscriberId,
                   cancellationToken) ??
               throw new EntityNotFoundException($"Abone bulunamadı: {subscriberId}");
    }

    private SubscriberPaymentDetailsResult BuildDetails(Subscriber subscriber)
    {
        var plan = BuildPlan(subscriber);
        var collections = BuildCollections(subscriber);
        var deferrals = subscriber.PaymentDeferrals
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new SubscriberPaymentDeferralRow(
                value.Id,
                value.OriginalDueDate,
                value.PreviousDueDate,
                value.DeferredUntil,
                value.Reason,
                ToBusinessTimestamp(value.CreatedAt),
                value.CancelledAt.HasValue
                    ? ToBusinessTimestamp(value.CancelledAt.Value)
                    : null))
            .ToArray();

        var activeDeferralHorizon = subscriber.PaymentDeferrals
            .Where(value => value.CancelledAt is null)
            .Select(value => (DateOnly?)value.DeferredUntil)
            .Max();
        var dueHorizon = activeDeferralHorizon.HasValue
            ? MaxDate(
                businessClock.Today.AddYears(1),
                activeDeferralHorizon.Value.AddMonths(1))
            : businessClock.Today.AddYears(1);
        IReadOnlyList<SubscriberPaymentDueRow> dueRows = plan is null
            ? []
            : BuildDueRows(
                subscriber,
                plan,
                dueHorizon);
        var dueThroughToday = dueRows
            .Where(value => value.OriginalDueDate <= businessClock.Today)
            .ToArray();
        var expectedTotal = DomainRules.RoundCurrency(
            dueThroughToday.Sum(value => value.Amount));
        var collectedTotal = DomainRules.RoundCurrency(
            collections.Sum(value => value.Amount));
        var outstanding = DomainRules.RoundCurrency(
            dueThroughToday.Sum(value => value.Balance));
        var overdue = DomainRules.RoundCurrency(
            dueRows
                .Where(value =>
                    value.EffectiveDueDate < businessClock.Today &&
                    value.Balance > 0)
                .Sum(value => value.Balance));
        var advance = plan is null
            ? 0m
            : DomainRules.RoundCurrency(
                Math.Max(0m, collectedTotal - expectedTotal));
        var nextDue = dueRows
            .Where(value => value.Balance > 0)
            .OrderBy(value => value.EffectiveDueDate)
            .ThenBy(value => value.OriginalDueDate)
            .FirstOrDefault();
        var activeDeferral = nextDue is null
            ? null
            : deferrals.SingleOrDefault(value =>
                value.OriginalDueDate == nextDue.OriginalDueDate &&
                value.CancelledAt is null);
        var movements = BuildMovements(
            plan,
            dueRows,
            collections,
            deferrals);

        return new SubscriberPaymentDetailsResult(
            subscriber.Id,
            subscriber.Name,
            subscriber.Phone,
            subscriber.Address,
            subscriber.Distributor?.Name ?? "Dağıtıcı atanmamış",
            subscriber.IsActive,
            plan,
            nextDue,
            activeDeferral,
            MaxDate(
                businessClock.Today.AddDays(1),
                (nextDue?.EffectiveDueDate ?? businessClock.Today).AddDays(1)),
            businessClock.Today.AddYears(5),
            expectedTotal,
            collectedTotal,
            outstanding,
            advance,
            overdue,
            collections,
            deferrals,
            movements);
    }

    private SubscriberPaymentPlanResult? BuildPlan(Subscriber subscriber)
    {
        var period = subscriber.PaymentPeriod;
        if (!SubscriberPaymentScheduleRules.HasCompletePlan(period))
        {
            return null;
        }

        var collectionDay = period!.CollectionDayOfMonth!.Value;
        var collectionTime = period.CollectionTime!.Value;
        var startedOn = SubscriberPaymentScheduleRules.GetPlanStartDate(
            subscriber,
            businessClock.Today);
        return new SubscriberPaymentPlanResult(
            period.Name,
            startedOn,
            collectionDay,
            collectionTime,
            period.DayCount,
            DomainRules.RoundCurrency(period.CollectionAmount!.Value),
            SubscriberPaymentScheduleRules.IsDailyPlan(period)
                ? $"Her gün · {collectionTime:HH\\:mm}"
                : period.DayCount == 10
                    ? $"Ayın 10., 20. ve son günü · {collectionTime:HH\\:mm}"
                    : $"Her ayın {collectionDay}. günü · {collectionTime:HH\\:mm}");
    }

    private IReadOnlyList<SubscriberPaymentDueRow> BuildDueRows(
        Subscriber subscriber,
        SubscriberPaymentPlanResult plan,
        DateOnly endDate)
    {
        var scheduledPayments = SubscriberPaymentScheduleRules.GetScheduledPayments(
            subscriber,
            endDate,
            plan.StartedOn);
        var activeDeferrals = subscriber.PaymentDeferrals
            .Where(value => value.CancelledAt is null)
            .ToDictionary(value => value.OriginalDueDate);
        var collectedRemaining = DomainRules.RoundCurrency(
            subscriber.DailyDeliveries
                .Where(value => value.IsCollected)
                .Sum(value => value.Amount));

        var scheduled = scheduledPayments
            .Select(payment =>
            {
                activeDeferrals.TryGetValue(
                    payment.OriginalDueDate,
                    out var deferral);
                return new
                {
                    Payment = payment,
                    EffectiveDate =
                        deferral?.DeferredUntil ?? payment.OriginalDueDate,
                    IsDeferred = deferral is not null
                };
            })
            .OrderBy(value => value.EffectiveDate)
            .ThenBy(value => value.Payment.OriginalDueDate)
            .ToArray();
        var rows = new List<SubscriberPaymentDueRow>(scheduled.Length);

        foreach (var due in scheduled)
        {
            var allocated = Math.Min(collectedRemaining, due.Payment.Amount);
            var balance = DomainRules.RoundCurrency(
                due.Payment.Amount - allocated);
            collectedRemaining = DomainRules.RoundCurrency(collectedRemaining - allocated);
            var status = balance <= 0
                ? "Ödendi"
                : due.EffectiveDate < businessClock.Today
                    ? "Gecikmiş"
                    : due.EffectiveDate == businessClock.Today
                        ? "Bugün ödenecek"
                        : due.IsDeferred
                            ? "Ertelendi"
                            : "Planlandı";
            rows.Add(new SubscriberPaymentDueRow(
                due.Payment.OriginalDueDate,
                due.EffectiveDate,
                due.Payment.Amount,
                due.Payment.CoveredDayCount,
                balance,
                status,
                due.IsDeferred));
        }

        return rows;
    }

    private IReadOnlyList<SubscriberCollectionHistoryRow> BuildCollections(
        Subscriber subscriber) =>
        subscriber.DailyDeliveries
            .Where(value => value.IsCollected)
            .OrderByDescending(value => value.CollectedAt ?? value.CreatedAt)
            .ThenByDescending(value => value.Date)
            .Select(value =>
            {
                var localTimestamp = value.CollectedAt.HasValue
                    ? ToBusinessDateAndTime(value.CollectedAt.Value)
                    : ((DateOnly Date, TimeOnly Time)?)null;
                var distributorName = string.IsNullOrWhiteSpace(value.DistributorName)
                    ? "Dağıtıcı atanmamış"
                    : value.DistributorName;
                return new SubscriberCollectionHistoryRow(
                    localTimestamp?.Date ?? value.Date,
                    localTimestamp?.Time,
                    DomainRules.RoundCurrency(value.Amount),
                    value.PaymentMethod,
                    distributorName,
                    value.CollectionPeriodName,
                    value.CollectionDayCount,
                    IsLegacyTimestamp: value.CollectedAt is null);
            })
            .ToArray();

    private IReadOnlyList<SubscriberPaymentMovementRow> BuildMovements(
        SubscriberPaymentPlanResult? plan,
        IReadOnlyList<SubscriberPaymentDueRow> dues,
        IReadOnlyList<SubscriberCollectionHistoryRow> collections,
        IReadOnlyList<SubscriberPaymentDeferralRow> deferrals)
    {
        var movements = new List<SubscriberPaymentMovementRow>();
        var movementWindowStart = businessClock.Today.AddYears(-1);

        var nextUpcomingDue = dues
            .Where(value =>
                value.EffectiveDueDate > businessClock.Today &&
                value.Balance > 0)
            .OrderBy(value => value.EffectiveDueDate)
            .ThenBy(value => value.OriginalDueDate)
            .FirstOrDefault();
        foreach (var due in dues.Where(value =>
                     value.EffectiveDueDate <= businessClock.Today &&
                     (value.OriginalDueDate >= movementWindowStart ||
                      value.Balance > 0) ||
                     value == nextUpcomingDue))
        {
            var description = due.IsDeferred
                ? $"{due.OriginalDueDate:dd.MM.yyyy} tarihli vade " +
                  $"{due.EffectiveDueDate:dd.MM.yyyy} tarihine ertelendi."
                : plan is null
                    ? "Planlanan ödeme vadesi"
                    : $"{due.CoveredDayCount} günlük {plan.Name} ödemesi";
            if (due.Balance > 0 && due.Balance < due.Amount)
            {
                description +=
                    $" · {due.Amount:C2} planlandı, {due.Balance:C2} kaldı";
            }
            movements.Add(new SubscriberPaymentMovementRow(
                due.EffectiveDueDate,
                plan?.CollectionTime,
                SubscriberPaymentMovementType.Due,
                "Planlanan ödeme",
                description,
                due.Balance > 0 ? due.Balance : null,
                ReducesBalance: false,
                due.Status));
        }

        movements.AddRange(collections.Select(value =>
            new SubscriberPaymentMovementRow(
                value.Date,
                value.Time,
                SubscriberPaymentMovementType.Collection,
                "Ödeme alındı",
                $"{PaymentMethodLabel(value.PaymentMethod)}" +
                (value.CoveredDayCount.HasValue
                    ? $" · {value.CoveredDayCount} günlük ödeme"
                    : string.Empty),
                value.Amount,
                ReducesBalance: true,
                value.IsLegacyTimestamp ? "Eski kayıt" : "Tahsil edildi")));

        foreach (var deferral in deferrals)
        {
            var created = ToBusinessDateAndTime(deferral.CreatedAt);
            movements.Add(new SubscriberPaymentMovementRow(
                created.Date,
                created.Time,
                SubscriberPaymentMovementType.Deferral,
                "Ödeme ertelendi",
                $"{deferral.PreviousDueDate:dd.MM.yyyy} tarihinden " +
                $"{deferral.DeferredUntil:dd.MM.yyyy} tarihine" +
                (string.IsNullOrWhiteSpace(deferral.Reason)
                    ? string.Empty
                    : $" · {deferral.Reason}"),
                Amount: null,
                ReducesBalance: false,
                deferral.CancelledAt is null ? "Aktif" : "Geçmiş"));

            if (deferral.CancelledAt.HasValue)
            {
                var cancelled = ToBusinessDateAndTime(deferral.CancelledAt.Value);
                movements.Add(new SubscriberPaymentMovementRow(
                    cancelled.Date,
                    cancelled.Time,
                    SubscriberPaymentMovementType.DeferralCancellation,
                    "Erteleme geri alındı",
                    $"{deferral.DeferredUntil:dd.MM.yyyy} tarihli erteleme iptal edildi.",
                    Amount: null,
                    ReducesBalance: false,
                    "Geri alındı"));
            }
        }

        return movements
            .OrderBy(value => value.Date > businessClock.Today)
            .ThenByDescending(value => value.Date)
            .ThenByDescending(value => value.Time)
            .ThenByDescending(value => value.Type)
            .Take(120)
            .ToArray();
    }

    private (DateOnly Date, TimeOnly Time) ToBusinessDateAndTime(
        DateTimeOffset timestamp)
    {
        var local = ToBusinessTimestamp(timestamp);
        return (
            DateOnly.FromDateTime(local.DateTime),
            TimeOnly.FromDateTime(local.DateTime));
    }

    private DateTimeOffset ToBusinessTimestamp(DateTimeOffset timestamp) =>
        TimeZoneInfo.ConvertTime(timestamp, _businessTimeZone);

    private static DateOnly MaxDate(DateOnly first, DateOnly second) =>
        first >= second ? first : second;

    private static string PaymentMethodLabel(SubscriberPaymentMethod paymentMethod) =>
        paymentMethod switch
        {
            SubscriberPaymentMethod.Cash => "Nakit",
            SubscriberPaymentMethod.Card => "Kart",
            SubscriberPaymentMethod.Transfer => "Havale/EFT",
            _ => "Bilinmeyen yöntem"
        };

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
