using System.Data;
using System.Globalization;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Services;

public sealed class SubscriberDeliveryService(
    AppDbContext dbContext,
    IBusinessClock? businessClock = null)
    : ISubscriberDeliveryService
{
    private static readonly StringComparer TurkishNameComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

    public SubscriberDeliveryPlan? PlanDailyDelivery(Subscriber subscriber, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var selectedDays = subscriber.NewspaperDays
            .Select(value => value.Day)
            .ToHashSet();

        if (selectedDays.Count == 0)
        {
            return new SubscriberDeliveryPlan(
                IsScheduled: false,
                CoveredDates: [date],
                NewspaperCount: 1);
        }

        var businessDay = DomainRules.ToBusinessDay(date);
        if (businessDay == BusinessDay.Monday &&
            selectedDays.Contains(NewspaperDay.SundayMonday))
        {
            return new SubscriberDeliveryPlan(
                IsScheduled: true,
                CoveredDates: [date.AddDays(-1), date],
                NewspaperCount: 2);
        }

        if (!selectedDays.Contains(DomainRules.ToNewspaperDay(businessDay)))
        {
            return null;
        }

        return new SubscriberDeliveryPlan(
            IsScheduled: true,
            CoveredDates: [date],
            NewspaperCount: 1);
    }

    public async Task<DailySubscriberDeliveryResult> GetDailyAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var activeSubscribers = await dbContext.Subscribers
            .AsNoTracking()
            .AsSplitQuery()
            .Where(value => value.IsActive)
            .Include(value => value.NewspaperDays)
            .Include(value => value.Distributor)
            .Include(value => value.PaymentPeriod)
            .Include(value => value.PaymentDeferrals)
            .ToListAsync(cancellationToken);

        var existingDeliveries = await dbContext.SubscriberDailyDeliveries
            .AsNoTracking()
            .AsSplitQuery()
            .Where(value => value.Date == date)
            .Include(value => value.CoveredDates)
            .Include(value => value.Distributor)
            .Include(value => value.Subscriber)
                .ThenInclude(value => value.NewspaperDays)
            .Include(value => value.Subscriber)
                .ThenInclude(value => value.Distributor)
            .Include(value => value.Subscriber)
                .ThenInclude(value => value.PaymentPeriod)
            .Include(value => value.Subscriber)
                .ThenInclude(value => value.PaymentDeferrals)
            .ToListAsync(cancellationToken);

        var existingBySubscriber = existingDeliveries
            .ToDictionary(value => value.SubscriberId);
        var usedSubscriberIds = new HashSet<int>();
        var rows = new List<DailySubscriberDeliveryRow>();

        foreach (var subscriber in activeSubscribers)
        {
            existingBySubscriber.TryGetValue(subscriber.Id, out var existing);
            var plan = PlanDailyDelivery(subscriber, date);
            var paymentDue = SubscriberPaymentScheduleRules.GetDailyPaymentDue(
                subscriber,
                date,
                date);
            if (plan is null && existing is null && paymentDue is null)
            {
                continue;
            }

            rows.Add(BuildRow(existing, subscriber, plan, date));
            usedSubscriberIds.Add(subscriber.Id);
        }

        foreach (var existing in existingDeliveries)
        {
            if (!usedSubscriberIds.Add(existing.SubscriberId))
            {
                continue;
            }

            var plan = existing.Subscriber.IsActive
                ? PlanDailyDelivery(existing.Subscriber, date)
                : null;
            rows.Add(BuildRow(existing, existing.Subscriber, plan, date));
        }

        rows.Sort((left, right) =>
            TurkishNameComparer.Compare(left.SubscriberName, right.SubscriberName));

        return new DailySubscriberDeliveryResult(date, rows);
    }

    public async Task<DailySubscriberDeliveryResult> SaveDailyAsync(
        DateOnly date,
        IReadOnlyCollection<SubscriberDeliveryUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);

        if (await dbContext.CashHandovers.AnyAsync(
                value => value.Date == date &&
                         value.Status == CashHandoverStatus.Delivered,
                cancellationToken))
        {
            throw new DomainConflictException(
                "Teslim edilmiş günlük kasa kaydı değiştirilemez.");
        }

        ValidateUpdates(updates);
        if (updates.Count == 0)
        {
            return await GetDailyAsync(date, cancellationToken);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var firstAttempt = true;
        await strategy.ExecuteAsync(async () =>
        {
            if (!firstAttempt)
            {
                dbContext.ChangeTracker.Clear();
            }

            firstAttempt = false;
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken)
                : null;
            var lockedHandover = await LockCashHandoverDateAsync(
                date,
                cancellationToken);
            if (lockedHandover?.Status == CashHandoverStatus.Delivered)
            {
                throw new DomainConflictException(
                    "Teslim edilmiş günlük kasa kaydı değiştirilemez.");
            }

            var subscriberIds = updates.Select(value => value.SubscriberId).ToArray();
            var subscribers = await dbContext.Subscribers
                .Where(value => subscriberIds.Contains(value.Id))
                .Include(value => value.NewspaperDays)
                .Include(value => value.Distributor)
                .Include(value => value.PaymentPeriod)
                .Include(value => value.PaymentDeferrals)
                .ToListAsync(cancellationToken);
            var subscriberById = subscribers.ToDictionary(value => value.Id);

            var existingDeliveries = await dbContext.SubscriberDailyDeliveries
                .Where(value => value.Date == date && subscriberIds.Contains(value.SubscriberId))
                .Include(value => value.CoveredDates)
                .ToListAsync(cancellationToken);
            var existingBySubscriber = existingDeliveries
                .ToDictionary(value => value.SubscriberId);

            foreach (var update in updates)
            {
                if (!subscriberById.TryGetValue(update.SubscriberId, out var subscriber))
                {
                    throw new EntityNotFoundException(
                        $"Abone bulunamadı: {update.SubscriberId}");
                }

                existingBySubscriber.TryGetValue(update.SubscriberId, out var delivery);
                var isNewDelivery = delivery is null;
                var plan = subscriber.IsActive
                    ? PlanDailyDelivery(subscriber, date)
                    : null;
                var paymentDue = SubscriberPaymentScheduleRules.GetDailyPaymentDue(
                    subscriber,
                    date,
                    date);
                if (plan is null && delivery is null && paymentDue is null)
                {
                    throw new DomainConflictException(
                        $"{subscriber.Name} bu tarih için planlı değildir.");
                }
                if (plan is null && delivery is null && update.IsDelivered)
                {
                    throw new DomainConflictException(
                        $"{subscriber.Name} için bu tarihte gazete teslimatı planlı değildir.");
                }

                var wasCollected = delivery?.IsCollected ?? false;
                if (delivery is null)
                {
                    delivery = new SubscriberDailyDelivery
                    {
                        SubscriberId = subscriber.Id,
                        Subscriber = subscriber,
                        Date = date,
                        DistributorId = subscriber.DistributorId,
                        DistributorName = subscriber.Distributor?.Name ?? string.Empty
                    };
                    dbContext.SubscriberDailyDeliveries.Add(delivery);
                }
                else if (delivery.DistributorId is null &&
                         !wasCollected &&
                         update.IsCollected &&
                         subscriber.DistributorId is not null)
                {
                    delivery.DistributorId = subscriber.DistributorId;
                    delivery.DistributorName = subscriber.Distributor?.Name ?? string.Empty;
                }

                var coveredDates = isNewDelivery
                    ? plan?.CoveredDates ?? [date]
                    : delivery.CoveredDates
                        .Select(value => value.CoveredDate)
                        .Order()
                        .ToArray();
                if (coveredDates.Count is < 1 or > 2)
                {
                    throw new DomainValidationException(
                        "Kapsanan tarihler bir veya iki tarih içermelidir.");
                }

                var newspaperCount = isNewDelivery
                    ? plan?.NewspaperCount ?? 1
                    : delivery.NewspaperCount;
                if (coveredDates.Distinct().Count() != newspaperCount)
                {
                    throw new DomainValidationException(
                        "Kapsanan tarih sayısı gazete adedi ile aynı olmalıdır.");
                }

                delivery.NewspaperCount = newspaperCount;
                ReplaceCoveredDates(delivery, coveredDates);
                delivery.IsDelivered = update.IsDelivered;
                SynchronizeCollectionSnapshot(
                    delivery,
                    subscriber,
                    wasCollected,
                    update.IsCollected,
                    date);
                delivery.IsCollected = update.IsCollected;
                delivery.Amount = DomainRules.RoundCurrency(update.Amount);
                delivery.PaymentMethod = update.PaymentMethod;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await SynchronizeDistributorDeliveryTotalsAsync(date, cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

        });

        return await GetDailyAsync(date, cancellationToken);
    }

    public async Task<DailySubscriberDeliveryResult> SaveDailyRowAsync(
        DateOnly date,
        SubscriberDeliveryPatch patch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ValidatePatch(patch);

        if (await dbContext.CashHandovers.AnyAsync(
                value => value.Date == date &&
                         value.Status == CashHandoverStatus.Delivered,
                cancellationToken))
        {
            throw new DomainConflictException(
                "Teslim edilmiş günlük kasa kaydı değiştirilemez.");
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var firstAttempt = true;
        await strategy.ExecuteAsync(async () =>
        {
            if (!firstAttempt)
            {
                dbContext.ChangeTracker.Clear();
            }

            firstAttempt = false;
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken)
                : null;

            var lockedHandover = await LockCashHandoverDateAsync(
                date,
                cancellationToken);
            if (lockedHandover?.Status == CashHandoverStatus.Delivered)
            {
                throw new DomainConflictException(
                    "Teslim edilmiş günlük kasa kaydı değiştirilemez.");
            }

            var subscriber = await dbContext.Subscribers
                .Include(value => value.NewspaperDays)
                .Include(value => value.Distributor)
                .Include(value => value.PaymentPeriod)
                .Include(value => value.PaymentDeferrals)
                .SingleOrDefaultAsync(
                    value => value.Id == patch.SubscriberId,
                    cancellationToken);
            if (subscriber is null)
            {
                throw new EntityNotFoundException(
                    $"Abone bulunamadı: {patch.SubscriberId}");
            }

            var delivery = await LockSubscriberDeliveryAsync(
                date,
                patch.SubscriberId,
                cancellationToken);
            var plan = subscriber.IsActive
                ? PlanDailyDelivery(subscriber, date)
                : null;
            var paymentDue = SubscriberPaymentScheduleRules.GetDailyPaymentDue(
                subscriber,
                date,
                date);
            if (plan is null && delivery is null && paymentDue is null)
            {
                throw new DomainConflictException(
                    $"{subscriber.Name} bu tarih için planlı değildir.");
            }

            var effectiveUpdate = new SubscriberDeliveryUpdate(
                patch.SubscriberId,
                patch.IsDelivered ?? delivery?.IsDelivered ?? false,
                patch.IsCollected ?? delivery?.IsCollected ?? false,
                patch.Amount ??
                delivery?.Amount ??
                DefaultCollectionAmount(subscriber, date),
                patch.PaymentMethod ??
                delivery?.PaymentMethod ??
                SubscriberPaymentMethod.Cash);
            ValidateUpdates([effectiveUpdate]);
            if (plan is null &&
                delivery is null &&
                effectiveUpdate.IsDelivered)
            {
                throw new DomainConflictException(
                    $"{subscriber.Name} için bu tarihte gazete teslimatı planlı değildir.");
            }

            var isNewDelivery = delivery is null;
            var wasCollected = delivery?.IsCollected ?? false;
            if (delivery is null)
            {
                delivery = new SubscriberDailyDelivery
                {
                    SubscriberId = subscriber.Id,
                    Subscriber = subscriber,
                    Date = date,
                    DistributorId = subscriber.DistributorId,
                    DistributorName = subscriber.Distributor?.Name ?? string.Empty
                };
                dbContext.SubscriberDailyDeliveries.Add(delivery);
            }
            else if (delivery.DistributorId is null &&
                     !wasCollected &&
                     effectiveUpdate.IsCollected &&
                     subscriber.DistributorId is not null)
            {
                delivery.DistributorId = subscriber.DistributorId;
                delivery.DistributorName = subscriber.Distributor?.Name ?? string.Empty;
            }

            IReadOnlyCollection<DateOnly> coveredDates = isNewDelivery
                ? plan?.CoveredDates.ToArray() ?? [date]
                : delivery.CoveredDates
                    .Select(value => value.CoveredDate)
                    .Order()
                    .ToArray();
            if (coveredDates.Count is < 1 or > 2)
            {
                throw new DomainValidationException(
                    "Kapsanan tarihler bir veya iki tarih içermelidir.");
            }

            var newspaperCount = isNewDelivery
                ? plan?.NewspaperCount ?? 1
                : delivery.NewspaperCount;
            if (coveredDates.Distinct().Count() != newspaperCount)
            {
                throw new DomainValidationException(
                    "Kapsanan tarih sayısı gazete adedi ile aynı olmalıdır.");
            }

            delivery.NewspaperCount = newspaperCount;
            ReplaceCoveredDates(delivery, coveredDates);
            delivery.IsDelivered = effectiveUpdate.IsDelivered;
            SynchronizeCollectionSnapshot(
                delivery,
                subscriber,
                wasCollected,
                effectiveUpdate.IsCollected,
                date);
            delivery.IsCollected = effectiveUpdate.IsCollected;
            delivery.Amount = DomainRules.RoundCurrency(effectiveUpdate.Amount);
            delivery.PaymentMethod = effectiveUpdate.PaymentMethod;

            await dbContext.SaveChangesAsync(cancellationToken);
            await SynchronizeDistributorDeliveryTotalsAsync(date, cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        });

        return await GetDailyAsync(date, cancellationToken);
    }

    private async Task<CashHandover?> LockCashHandoverDateAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName?.Contains(
                "SqlServer",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return await dbContext.CashHandovers
                .FromSqlInterpolated(
                    $"""
                     SELECT *
                     FROM [CashHandovers] WITH (UPDLOCK, HOLDLOCK)
                     WHERE [Date] = {date}
                     """)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await dbContext.CashHandovers
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Date == date, cancellationToken);
    }

    private async Task<SubscriberDailyDelivery?> LockSubscriberDeliveryAsync(
        DateOnly date,
        int subscriberId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName?.Contains(
                "SqlServer",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return await dbContext.SubscriberDailyDeliveries
                .FromSqlInterpolated(
                    $"""
                     SELECT *
                     FROM [SubscriberDailyDeliveries] WITH (UPDLOCK, HOLDLOCK)
                     WHERE [Date] = {date} AND [SubscriberId] = {subscriberId}
                     """)
                .Include(value => value.CoveredDates)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await dbContext.SubscriberDailyDeliveries
            .Include(value => value.CoveredDates)
            .SingleOrDefaultAsync(
                value => value.Date == date &&
                         value.SubscriberId == subscriberId,
                cancellationToken);
    }

    private async Task SynchronizeDistributorDeliveryTotalsAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var subscriberDeliveries = await dbContext.SubscriberDailyDeliveries
            .AsNoTracking()
            .Where(value => value.Date == date && value.DistributorId != null)
            .ToListAsync(cancellationToken);
        var distributorIds = subscriberDeliveries
            .Select(value => value.DistributorId!.Value)
            .Distinct()
            .ToArray();
        if (distributorIds.Length == 0)
        {
            return;
        }

        var distributors = await dbContext.Distributors
            .Where(value => distributorIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var existing = await dbContext.Deliveries
            .Where(value => value.Date == date &&
                            distributorIds.Contains(value.DistributorId))
            .ToDictionaryAsync(value => value.DistributorId, cancellationToken);

        foreach (var group in subscriberDeliveries.GroupBy(
                     value => value.DistributorId!.Value))
        {
            if (!distributors.TryGetValue(group.Key, out var distributor))
            {
                continue;
            }

            var newspaperCount = group
                .Where(value => value.IsDelivered)
                .Sum(value => value.NewspaperCount);
            if (!existing.TryGetValue(group.Key, out var delivery))
            {
                delivery = new Delivery
                {
                    DistributorId = group.Key,
                    Date = date,
                    Day = DomainRules.ToBusinessDay(date)
                };
                dbContext.Deliveries.Add(delivery);
            }
            else if (delivery.Status == DeliveryStatus.Cancelled)
            {
                continue;
            }

            delivery.NewspaperCount = newspaperCount;
            delivery.Amount = DomainRules.RoundCurrency(
                newspaperCount * distributor.NewspaperPrice);
            delivery.Status = newspaperCount > 0
                ? DeliveryStatus.Completed
                : DeliveryStatus.Pending;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void ReplaceCoveredDates(
        SubscriberDailyDelivery delivery,
        IReadOnlyCollection<DateOnly> coveredDates)
    {
        var normalized = coveredDates.Distinct().Order().ToArray();
        if (!normalized.Contains(delivery.Date))
        {
            throw new DomainValidationException(
                "Kapsanan tarihler teslimat tarihini içermelidir.");
        }

        var desiredDates = normalized.ToHashSet();
        var existingDates = delivery.CoveredDates
            .Select(value => value.CoveredDate)
            .ToHashSet();

        foreach (var obsolete in delivery.CoveredDates
                     .Where(value => !desiredDates.Contains(value.CoveredDate))
                     .ToArray())
        {
            dbContext.SubscriberDailyDeliveryCoveredDates.Remove(obsolete);
            delivery.CoveredDates.Remove(obsolete);
        }

        foreach (var coveredDate in normalized.Where(
                     value => !existingDates.Contains(value)))
        {
            delivery.CoveredDates.Add(new SubscriberDailyDeliveryCoveredDate
            {
                SubscriberDailyDelivery = delivery,
                CoveredDate = coveredDate
            });
        }
    }

    private static void ValidateUpdates(
        IReadOnlyCollection<SubscriberDeliveryUpdate> updates)
    {
        var duplicate = updates
            .GroupBy(value => value.SubscriberId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainValidationException(
                "Aynı abone birden fazla gönderilemez.");
        }

        foreach (var update in updates)
        {
            var roundedAmount = DomainRules.RoundCurrency(update.Amount);
            if (update.SubscriberId <= 0)
            {
                throw new DomainValidationException("Geçerli bir abone seçilmelidir.");
            }
            if (update.Amount < 0)
            {
                throw new DomainValidationException("Tutar negatif olamaz.");
            }
            if (update.IsCollected && roundedAmount <= 0)
            {
                throw new DomainValidationException(
                    "Tahsil edildi işaretli kaydın tutarı sıfırdan büyük olmalıdır.");
            }
            if (!Enum.IsDefined(update.PaymentMethod))
            {
                throw new DomainValidationException("Ödeme yöntemi geçersizdir.");
            }
        }
    }

    private static void ValidatePatch(SubscriberDeliveryPatch patch)
    {
        if (patch.SubscriberId <= 0)
        {
            throw new DomainValidationException("Geçerli bir abone seçilmelidir.");
        }
        if (patch.IsDelivered is null &&
            patch.IsCollected is null &&
            patch.Amount is null &&
            patch.PaymentMethod is null)
        {
            throw new DomainValidationException(
                "Kaydedilecek en az bir değişiklik gönderilmelidir.");
        }
        if (patch.Amount < 0)
        {
            throw new DomainValidationException("Tutar negatif olamaz.");
        }
        if (patch.PaymentMethod is { } paymentMethod &&
            !Enum.IsDefined(paymentMethod))
        {
            throw new DomainValidationException("Ödeme yöntemi geçersizdir.");
        }
    }

    private static DailySubscriberDeliveryRow BuildRow(
        SubscriberDailyDelivery? delivery,
        Subscriber subscriber,
        SubscriberDeliveryPlan? plan,
        DateOnly date)
    {
        var coveredDates = delivery?.CoveredDates
                .Select(value => value.CoveredDate)
                .Order()
                .ToArray() ??
            plan?.CoveredDates ??
            [date];

        var distributorId = delivery?.DistributorId ?? subscriber.DistributorId;
        var distributorName = delivery?.DistributorName;
        if (string.IsNullOrWhiteSpace(distributorName))
        {
            distributorName = delivery?.Distributor?.Name ??
                              subscriber.Distributor?.Name ??
                              string.Empty;
        }

        var paymentDue = SubscriberPaymentScheduleRules.GetDailyPaymentDue(
            subscriber,
            date,
            date);
        var hasDelivery = plan is not null || delivery?.IsDelivered == true;

        return new DailySubscriberDeliveryRow(
            Id: delivery?.Id,
            SubscriberId: subscriber.Id,
            SubscriberName: subscriber.Name,
            NewspaperDays: subscriber.NewspaperDays
                .Select(value => value.Day)
                .ToArray(),
            HasDelivery: hasDelivery,
            IsScheduled: plan?.IsScheduled ?? false,
            CoveredDates: coveredDates,
            NewspaperCount: delivery?.NewspaperCount ?? plan?.NewspaperCount ?? 1,
            IsDelivered: delivery?.IsDelivered ?? false,
            IsCollected: delivery?.IsCollected ?? false,
            IsPaymentDue: paymentDue is not null,
            Amount: delivery?.Amount ??
                    paymentDue?.Amount ??
                    DefaultCollectionAmount(subscriber, date),
            PaymentMethod: delivery?.PaymentMethod ?? SubscriberPaymentMethod.Cash,
            DistributorId: distributorId,
            DistributorName: distributorName);
    }

    private static decimal DefaultCollectionAmount(
        Subscriber subscriber,
        DateOnly date) =>
        SubscriberPaymentScheduleRules.GetDailyPaymentDue(
            subscriber,
            date,
            date)?.Amount ??
        (subscriber.PaymentPeriod?.CollectionAmount is > 0
            ? subscriber.PaymentPeriod.CollectionAmount.Value
            : subscriber.MonthlyFee);

    private void SynchronizeCollectionSnapshot(
        SubscriberDailyDelivery delivery,
        Subscriber subscriber,
        bool wasCollected,
        bool isCollected,
        DateOnly deliveryDate)
    {
        if (!isCollected)
        {
            delivery.CollectedAt = null;
            delivery.CollectionPeriodName = string.Empty;
            delivery.CollectionDayCount = null;
            return;
        }

        if (wasCollected)
        {
            return;
        }

        delivery.CollectedAt = businessClock?.UtcNow ??
                              new DateTimeOffset(
                                  deliveryDate.ToDateTime(TimeOnly.MinValue),
                                  TimeSpan.Zero);
        delivery.CollectionPeriodName = subscriber.PaymentPeriod?.Name ?? string.Empty;
        delivery.CollectionDayCount =
            SubscriberPaymentScheduleRules.GetDailyPaymentDue(
                subscriber,
                deliveryDate,
                deliveryDate)?.CoveredDayCount ??
            subscriber.PaymentPeriod?.DayCount;
    }
}
