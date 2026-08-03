using System.Data;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Services;

public sealed class NewspaperCashSaleService(
    AppDbContext dbContext,
    IBusinessClock clock) : INewspaperCashSaleService
{
    public async Task<DailyNewspaperCashSalesResult> GetDailyAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var configuredUnitPrice = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(value => value.SingletonKey == "company")
            .Select(value => value.NewspaperUnitPrice)
            .SingleOrDefaultAsync(cancellationToken);
        var unitPrice = configuredUnitPrice is > 0
            ? DomainRules.RoundCurrency(configuredUnitPrice.Value)
            : 0m;
        var distributors = await dbContext.Distributors
            .AsNoTracking()
            .Where(value => value.IsActive)
            .OrderBy(value => value.Name)
            .Select(value => new NewspaperCashSaleDistributorOption(
                value.Id,
                value.Name,
                unitPrice))
            .ToListAsync(cancellationToken);
        var sales = await dbContext.NewspaperCashSales
            .AsNoTracking()
            .Where(value => value.Date == date && value.CancelledAt == null)
            .OrderByDescending(value => value.CreatedAt)
            .ThenByDescending(value => value.Id)
            .Select(value => new NewspaperCashSaleRow(
                value.Id,
                value.Date,
                value.DistributorId,
                value.DistributorName,
                value.Quantity,
                value.UnitPrice,
                value.Amount,
                value.CreatedAt))
            .ToListAsync(cancellationToken);

        return new DailyNewspaperCashSalesResult(date, distributors, sales);
    }

    public async Task<NewspaperCashSaleRow> CreateAsync(
        DateOnly date,
        int distributorId,
        int quantity,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateInput(date, distributorId, quantity, idempotencyKey);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var firstAttempt = true;
        var saleId = await strategy.ExecuteAsync(async () =>
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
                    "Teslim edilmiş günlük kasaya nakit satış eklenemez.");
            }

            var existing = await dbContext.NewspaperCashSales
                .SingleOrDefaultAsync(
                    value => value.IdempotencyKey == idempotencyKey,
                    cancellationToken);
            if (existing is not null)
            {
                EnsureIdempotentRequestMatches(
                    existing,
                    date,
                    distributorId,
                    quantity);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return existing.Id;
            }

            var distributor = await dbContext.Distributors
                .SingleOrDefaultAsync(
                    value => value.Id == distributorId,
                    cancellationToken);
            if (distributor is null)
            {
                throw new EntityNotFoundException("Dağıtıcı bulunamadı.");
            }
            if (!distributor.IsActive)
            {
                throw new DomainConflictException(
                    "Pasif dağıtıcı için nakit satış kaydedilemez.");
            }

            var configuredUnitPrice = await dbContext.CompanySettings
                .Where(value => value.SingletonKey == "company")
                .Select(value => value.NewspaperUnitPrice)
                .SingleOrDefaultAsync(cancellationToken);
            if (configuredUnitPrice is null || configuredUnitPrice <= 0)
            {
                throw new DomainValidationException(
                    "Nakit satış için Firma Ayarları bölümünde gazete birim satış fiyatı belirlenmelidir.");
            }

            var unitPrice = DomainRules.RoundCurrency(configuredUnitPrice.Value);
            var amount = DomainRules.RoundCurrency(quantity * unitPrice);
            var sale = new NewspaperCashSale
            {
                Date = date,
                DistributorId = distributor.Id,
                Distributor = distributor,
                DistributorName = distributor.Name,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Amount = amount,
                IdempotencyKey = idempotencyKey
            };
            dbContext.NewspaperCashSales.Add(sale);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return sale.Id;
        });

        return await LoadRowAsync(saleId, cancellationToken);
    }

    public async Task<NewspaperCashSaleRow> CancelAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new DomainValidationException("Nakit satış kaydı geçersizdir.");
        }

        var saleDate = await dbContext.NewspaperCashSales
            .AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => (DateOnly?)value.Date)
            .SingleOrDefaultAsync(cancellationToken);
        if (saleDate is null)
        {
            throw new EntityNotFoundException("Nakit satış kaydı bulunamadı.");
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
                saleDate.Value,
                cancellationToken);
            if (lockedHandover?.Status == CashHandoverStatus.Delivered)
            {
                throw new DomainConflictException(
                    "Teslim edilmiş günlük kasadaki nakit satış geri alınamaz.");
            }

            var sale = await dbContext.NewspaperCashSales
                .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
            if (sale is null)
            {
                throw new EntityNotFoundException("Nakit satış kaydı bulunamadı.");
            }

            sale.CancelledAt ??= clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        });

        return await LoadRowAsync(id, cancellationToken);
    }

    private async Task<NewspaperCashSaleRow> LoadRowAsync(
        int id,
        CancellationToken cancellationToken) =>
        await dbContext.NewspaperCashSales
            .AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new NewspaperCashSaleRow(
                value.Id,
                value.Date,
                value.DistributorId,
                value.DistributorName,
                value.Quantity,
                value.UnitPrice,
                value.Amount,
                value.CreatedAt))
            .SingleAsync(cancellationToken);

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

    private static void ValidateCreateInput(
        DateOnly date,
        int distributorId,
        int quantity,
        Guid idempotencyKey)
    {
        if (date == default)
        {
            throw new DomainValidationException("Geçerli bir satış tarihi seçilmelidir.");
        }
        if (distributorId <= 0)
        {
            throw new DomainValidationException("Gazete veya dağıtıcı seçilmelidir.");
        }
        if (quantity is < 1 or > 1000)
        {
            throw new DomainValidationException(
                "Nakit satış adedi 1 ile 1000 arasında olmalıdır.");
        }
        if (idempotencyKey == Guid.Empty)
        {
            throw new DomainValidationException("Nakit satış işlem anahtarı geçersizdir.");
        }
    }

    private static void EnsureIdempotentRequestMatches(
        NewspaperCashSale existing,
        DateOnly date,
        int distributorId,
        int quantity)
    {
        if (existing.Date != date ||
            existing.DistributorId != distributorId ||
            existing.Quantity != quantity)
        {
            throw new DomainConflictException(
                "Nakit satış işlemi başka bir kayıtla çakıştı. Sayfayı yenileyip tekrar deneyin.");
        }
    }
}
