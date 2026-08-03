using System.Data;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Services;

public sealed class CashHandoverService(
    AppDbContext dbContext,
    IBusinessClock clock) : ICashHandoverService
{
    public Task<bool> IsClosedAsync(
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        dbContext.CashHandovers.AnyAsync(
            value => value.Date == date &&
                     value.Status == CashHandoverStatus.Delivered,
            cancellationToken);

    public async Task<DailyCashHandoverResult> GetDailyAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var handover = await dbContext.CashHandovers
            .AsNoTracking()
            .Include(value => value.Items)
            .SingleOrDefaultAsync(value => value.Date == date, cancellationToken);
        var automaticItems = await LoadAutomaticItemsAsync(
            [date],
            cancellationToken);

        return BuildDailyResult(
            handover,
            date,
            automaticItems.GetValueOrDefault(date, []));
    }

    public async Task<DailyCashHandoverResult> SaveDailyAsync(
        DateOnly date,
        CashHandoverUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
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
                    "Teslim edilmiş kasa kaydı yeniden açılamaz veya değiştirilemez.");
            }

            if (update.Items is null && update.Status is null)
            {
                throw new DomainValidationException(
                    "Kalemler veya durum alanlarından en az biri belirtilmelidir.");
            }

            var handover = await dbContext.CashHandovers
                .Include(value => value.Items)
                .SingleOrDefaultAsync(value => value.Date == date, cancellationToken);
            if (handover is null)
            {
                handover = new CashHandover { Date = date };
                dbContext.CashHandovers.Add(handover);
            }

            if (update.Items is not null)
            {
                var preparedItems = update.Items
                    .Select(ValidateAndNormalizeItem)
                    .ToArray();

                dbContext.CashHandoverItems.RemoveRange(handover.Items);
                handover.Items.Clear();
                foreach (var item in preparedItems)
                {
                    handover.Items.Add(new CashHandoverItem
                    {
                        CashHandover = handover,
                        SubscriberName = item.SubscriberName,
                        Amount = item.Amount,
                        Description = item.Description ?? string.Empty
                    });
                }

                handover.Total = SumAmounts(preparedItems.Select(value => value.Amount));
            }

            if (update.Status is not null)
            {
                if (!Enum.IsDefined(update.Status.Value))
                {
                    throw new DomainValidationException("Kasa teslim durumu geçersizdir.");
                }

                handover.Status = update.Status.Value;
                if (handover.Status == CashHandoverStatus.Draft)
                {
                    handover.DeliveredAt = null;
                }
                else
                {
                    handover.DeliveredAt ??= clock.UtcNow;
                }
            }

            if (handover.Status == CashHandoverStatus.Draft)
            {
                handover.DeliveredAt = null;
            }
            else
            {
                handover.DeliveredAt ??= clock.UtcNow;
            }

            var automaticItems = await LoadAutomaticItemsAsync(
                [date],
                cancellationToken);
            var manualTotal = SumAmounts(handover.Items.Select(value => value.Amount));
            var automaticTotal = SumAmounts(
                automaticItems
                    .GetValueOrDefault(date, [])
                    .Select(value => value.Amount));
            handover.Total = DomainRules.RoundCurrency(manualTotal + automaticTotal);

            await dbContext.SaveChangesAsync(cancellationToken);
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

    public async Task<MonthlyCashHandoverResult> GetMonthlyAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var (start, endExclusive) = DomainRules.MonthRange(year, month);
        var handovers = await dbContext.CashHandovers
            .AsNoTracking()
            .Where(value =>
                value.Date >= start &&
                value.Date < endExclusive &&
                value.Status == CashHandoverStatus.Delivered)
            .Include(value => value.Items)
            .OrderBy(value => value.Date)
            .ToListAsync(cancellationToken);

        var handoverDates = handovers.Select(value => value.Date).ToArray();
        var automaticItemsByDate = await LoadAutomaticItemsAsync(
            handoverDates,
            cancellationToken);

        var rows = handovers.Select(handover =>
        {
            var automaticItems = automaticItemsByDate.GetValueOrDefault(
                handover.Date,
                []);
            var manualTotal = SumAmounts(handover.Items.Select(value => value.Amount));
            var automaticTotal = SumAmounts(automaticItems.Select(value => value.Amount));
            return new MonthlyCashHandoverRow(
                Id: handover.Id,
                Date: handover.Date,
                ManualTotal: manualTotal,
                AutomaticTotal: automaticTotal,
                Total: DomainRules.RoundCurrency(manualTotal + automaticTotal),
                ManualItemCount: handover.Items.Count,
                AutomaticItemCount: automaticItems.Count,
                DeliveredAt: handover.DeliveredAt ?? handover.UpdatedAt);
        }).ToArray();

        return new MonthlyCashHandoverResult(
            year,
            month,
            SumAmounts(rows.Select(value => value.Total)),
            rows);
    }

    private async Task<Dictionary<DateOnly, IReadOnlyList<CashHandoverLine>>>
        LoadAutomaticItemsAsync(
            IReadOnlyCollection<DateOnly> dates,
            CancellationToken cancellationToken)
    {
        if (dates.Count == 0)
        {
            return [];
        }

        var deliveries = await dbContext.SubscriberDailyDeliveries
            .AsNoTracking()
            .Where(value =>
                dates.Contains(value.Date) &&
                value.IsCollected &&
                value.PaymentMethod == SubscriberPaymentMethod.Cash)
            .Include(value => value.Subscriber)
            .OrderBy(value => value.Date)
            .ThenBy(value => value.Subscriber.Name)
            .ToListAsync(cancellationToken);
        var cashSales = await dbContext.NewspaperCashSales
            .AsNoTracking()
            .Where(value =>
                dates.Contains(value.Date) &&
                value.CancelledAt == null)
            .OrderBy(value => value.Date)
            .ThenBy(value => value.DistributorName)
            .ThenBy(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        var lines = deliveries
            .Select(value => (
                value.Date,
                Line: new CashHandoverLine(
                    Id: null,
                    SubscriberName: value.Subscriber?.Name ?? "Bilinmeyen abone",
                    Amount: value.Amount,
                    Description: "Günlük abone tahsilatı",
                    IsAutomatic: true,
                    SourceDeliveryId: value.Id,
                    PaymentMethod: value.PaymentMethod)))
            .Concat(cashSales.Select(value => (
                value.Date,
                Line: new CashHandoverLine(
                    Id: null,
                    SubscriberName: $"Nakit satış · {value.DistributorName}",
                    Amount: value.Amount,
                    Description:
                        $"{value.Quantity} gazete × {value.UnitPrice:0.00} ₺",
                    IsAutomatic: true,
                    SourceDeliveryId: null,
                    PaymentMethod: SubscriberPaymentMethod.Cash,
                    SourceCashSaleId: value.Id))))
            .OrderBy(value => value.Date)
            .ThenBy(value => value.Line.SubscriberName)
            .ToArray();

        return lines
            .GroupBy(value => value.Date)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CashHandoverLine>)group
                    .Select(value => value.Line)
                    .ToArray());
    }

    private static DailyCashHandoverResult BuildDailyResult(
        CashHandover? handover,
        DateOnly date,
        IReadOnlyList<CashHandoverLine> automaticItems)
    {
        var manualItems = handover?.Items
                .Select(value => new CashHandoverLine(
                    Id: value.Id,
                    SubscriberName: value.SubscriberName,
                    Amount: value.Amount,
                    Description: value.Description,
                    IsAutomatic: false,
                    SourceDeliveryId: null,
                    PaymentMethod: null))
                .ToArray() ??
            [];
        var manualTotal = SumAmounts(manualItems.Select(value => value.Amount));
        var automaticTotal = SumAmounts(automaticItems.Select(value => value.Amount));

        return new DailyCashHandoverResult(
            Id: handover?.Id,
            Date: date,
            Status: handover?.Status ?? CashHandoverStatus.Draft,
            DeliveredAt: handover?.DeliveredAt,
            ManualItems: manualItems,
            AutomaticItems: automaticItems,
            ManualTotal: manualTotal,
            AutomaticTotal: automaticTotal,
            Total: DomainRules.RoundCurrency(manualTotal + automaticTotal));
    }

    private static CashHandoverItemInput ValidateAndNormalizeItem(
        CashHandoverItemInput item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var subscriberName = item.SubscriberName?.Trim() ?? string.Empty;
        var description = item.Description?.Trim() ?? string.Empty;

        if (subscriberName.Length == 0)
        {
            throw new DomainValidationException("Kasa kalemi abone adı boş olamaz.");
        }
        if (subscriberName.Length > 200)
        {
            throw new DomainValidationException(
                "Kasa kalemi abone adı en fazla 200 karakter olabilir.");
        }
        if (description.Length > 1000)
        {
            throw new DomainValidationException(
                "Kasa kalemi açıklaması en fazla 1000 karakter olabilir.");
        }
        if (item.Amount < 0)
        {
            throw new DomainValidationException("Kasa kalemi tutarı negatif olamaz.");
        }

        return item with
        {
            SubscriberName = subscriberName,
            Amount = DomainRules.RoundCurrency(item.Amount),
            Description = description
        };
    }

    private static decimal SumAmounts(IEnumerable<decimal> amounts) =>
        DomainRules.RoundCurrency(amounts.Sum());
}
