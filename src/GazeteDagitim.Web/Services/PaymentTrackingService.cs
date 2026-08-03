using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Services;

public sealed class PaymentTrackingService(AppDbContext dbContext)
    : IPaymentTrackingService
{
    public async Task<PaymentTrackingResult> GetMonthlyAsync(
        int year,
        int month,
        int? distributorId = null,
        CancellationToken cancellationToken = default)
    {
        var (start, endExclusive) = DomainRules.MonthRange(year, month);
        if (distributorId is not null &&
            !await dbContext.Distributors.AnyAsync(
                value => value.Id == distributorId.Value,
                cancellationToken))
        {
            throw new EntityNotFoundException("Dağıtıcı bulunamadı.");
        }

        var paymentQuery = dbContext.Payments
            .AsNoTracking()
            .Where(value => value.Date >= start && value.Date < endExclusive);
        if (distributorId is not null)
        {
            paymentQuery = paymentQuery.Where(
                value => value.DistributorId == distributorId.Value);
        }

        var payments = await paymentQuery
            .Include(value => value.Distributor)
            .OrderByDescending(value => value.Date)
            .ThenByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        var collectionQuery = dbContext.SubscriberDailyDeliveries
            .AsNoTracking()
            .Where(value =>
                value.Date >= start &&
                value.Date < endExclusive &&
                value.IsCollected &&
                value.PaymentMethod == SubscriberPaymentMethod.Cash);
        if (distributorId is not null)
        {
            collectionQuery = collectionQuery.Where(value =>
                value.DistributorId == distributorId.Value);
        }

        var collectionEntities = await collectionQuery
            .Include(value => value.Distributor)
            .Include(value => value.Subscriber)
            .OrderByDescending(value => value.Date)
            .ThenByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        var cashSaleQuery = dbContext.NewspaperCashSales
            .AsNoTracking()
            .Where(value =>
                value.Date >= start &&
                value.Date < endExclusive &&
                value.CancelledAt == null);
        if (distributorId is not null)
        {
            cashSaleQuery = cashSaleQuery.Where(
                value => value.DistributorId == distributorId.Value);
        }

        var cashSaleEntities = await cashSaleQuery
            .OrderByDescending(value => value.Date)
            .ThenByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        var subscriberCollectionRows = collectionEntities
            .Select(ToCashCollectionRow)
            .Where(value =>
                distributorId is null ||
                value.DistributorId == distributorId.Value)
            .ToArray();
        var cashSaleRows = cashSaleEntities
            .Select(ToCashSaleCollectionRow)
            .ToArray();
        var collectionRows = subscriberCollectionRows
            .Concat(cashSaleRows)
            .OrderByDescending(value => value.Date)
            .ThenByDescending(value => value.Id)
            .ToArray();
        var paymentRows = payments.Select(value => new PaymentTrackingPaymentRow(
            Id: value.Id,
            DistributorId: value.DistributorId,
            DistributorName: value.Distributor?.Name ?? string.Empty,
            Amount: value.Amount,
            Date: value.Date,
            PeriodStart: value.PeriodStart,
            PeriodEnd: value.PeriodEnd,
            PaymentType: value.PaymentType,
            Status: value.Status,
            PaidAt: value.PaidAt,
            Description: value.Description)).ToArray();

        var paymentTotal = SumAmounts(payments.Select(value => value.Amount));
        var paidTotal = SumAmounts(payments
            .Where(value => value.Status == PaymentStatus.Paid)
            .Select(value => value.Amount));
        var pendingTotal = SumAmounts(payments
            .Where(value => value.Status == PaymentStatus.Pending)
            .Select(value => value.Amount));
        var cashTotal = SumAmounts(collectionRows.Select(value => value.Amount));

        return new PaymentTrackingResult(
            year,
            month,
            distributorId,
            new PaymentTrackingSummary(
                DistributorPaymentTotal: paymentTotal,
                PaidTotal: paidTotal,
                PendingTotal: pendingTotal,
                CashCollectionTotal: cashTotal,
                CashCollectionCount: collectionRows.Length),
            paymentRows,
            collectionRows);
    }

    private static CashCollectionRow ToCashCollectionRow(
        SubscriberDailyDelivery delivery)
    {
        var distributorName = delivery.DistributorName;
        if (string.IsNullOrWhiteSpace(distributorName))
        {
            distributorName = delivery.Distributor?.Name ?? string.Empty;
        }

        return new CashCollectionRow(
            Id: delivery.Id,
            SubscriberId: delivery.SubscriberId,
            SubscriberName: delivery.Subscriber?.Name ?? "Bilinmeyen abone",
            Date: delivery.Date,
            Amount: delivery.Amount,
            PaymentMethod: delivery.PaymentMethod,
            DistributorId: delivery.DistributorId,
            DistributorName: distributorName,
            IsCashSale: false,
            Description: "Abone tahsilatı");
    }

    private static CashCollectionRow ToCashSaleCollectionRow(
        NewspaperCashSale sale) =>
        new(
            Id: sale.Id,
            SubscriberId: null,
            SubscriberName: "Nakit gazete satışı",
            Date: sale.Date,
            Amount: sale.Amount,
            PaymentMethod: SubscriberPaymentMethod.Cash,
            DistributorId: sale.DistributorId,
            DistributorName: sale.DistributorName,
            IsCashSale: true,
            Description:
                $"{sale.Quantity} gazete × {sale.UnitPrice:0.00} ₺");

    private static decimal SumAmounts(IEnumerable<decimal> amounts) =>
        DomainRules.RoundCurrency(amounts.Sum());
}
