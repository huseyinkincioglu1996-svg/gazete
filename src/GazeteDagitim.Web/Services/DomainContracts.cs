using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Services;

public sealed record SubscriberDeliveryPlan(
    bool IsScheduled,
    IReadOnlyList<DateOnly> CoveredDates,
    int NewspaperCount);

public sealed record SubscriberDeliveryUpdate(
    int SubscriberId,
    bool IsDelivered,
    bool IsCollected,
    decimal Amount,
    SubscriberPaymentMethod PaymentMethod);

public sealed record SubscriberDeliveryPatch(
    int SubscriberId,
    bool? IsDelivered = null,
    bool? IsCollected = null,
    decimal? Amount = null,
    SubscriberPaymentMethod? PaymentMethod = null);

public sealed record DailySubscriberDeliveryRow(
    int? Id,
    int SubscriberId,
    string SubscriberName,
    IReadOnlyList<NewspaperDay> NewspaperDays,
    bool HasDelivery,
    bool IsScheduled,
    IReadOnlyList<DateOnly> CoveredDates,
    int NewspaperCount,
    bool IsDelivered,
    bool IsCollected,
    bool IsPaymentDue,
    decimal Amount,
    SubscriberPaymentMethod PaymentMethod,
    int? DistributorId,
    string DistributorName)
{
    public bool ShowPaymentControls => IsPaymentDue || IsCollected;
}

public sealed record DailySubscriberDeliveryResult(
    DateOnly Date,
    IReadOnlyList<DailySubscriberDeliveryRow> Records);

public interface ISubscriberDeliveryService
{
    SubscriberDeliveryPlan? PlanDailyDelivery(Subscriber subscriber, DateOnly date);

    Task<DailySubscriberDeliveryResult> GetDailyAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<DailySubscriberDeliveryResult> SaveDailyAsync(
        DateOnly date,
        IReadOnlyCollection<SubscriberDeliveryUpdate> updates,
        CancellationToken cancellationToken = default);

    Task<DailySubscriberDeliveryResult> SaveDailyRowAsync(
        DateOnly date,
        SubscriberDeliveryPatch patch,
        CancellationToken cancellationToken = default);
}

public sealed record NewspaperCashSaleDistributorOption(
    int Id,
    string Name,
    decimal UnitPrice);

public sealed record NewspaperCashSaleRow(
    int Id,
    DateOnly Date,
    int DistributorId,
    string DistributorName,
    int Quantity,
    decimal UnitPrice,
    decimal Amount,
    DateTimeOffset CreatedAt);

public sealed record DailyNewspaperCashSalesResult(
    DateOnly Date,
    IReadOnlyList<NewspaperCashSaleDistributorOption> Distributors,
    IReadOnlyList<NewspaperCashSaleRow> Records)
{
    public int TotalQuantity => Records.Sum(value => value.Quantity);

    public decimal Total => DomainRules.RoundCurrency(
        Records.Sum(value => value.Amount));
}

public interface INewspaperCashSaleService
{
    Task<DailyNewspaperCashSalesResult> GetDailyAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<NewspaperCashSaleRow> CreateAsync(
        DateOnly date,
        int distributorId,
        int quantity,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<NewspaperCashSaleRow> CancelAsync(
        int id,
        CancellationToken cancellationToken = default);
}

public sealed record CashHandoverItemInput(
    string SubscriberName,
    decimal Amount,
    string? Description = null);

public sealed record CashHandoverUpdate(
    IReadOnlyCollection<CashHandoverItemInput>? Items = null,
    CashHandoverStatus? Status = null);

public sealed record CashHandoverLine(
    int? Id,
    string SubscriberName,
    decimal Amount,
    string Description,
    bool IsAutomatic,
    int? SourceDeliveryId,
    SubscriberPaymentMethod? PaymentMethod,
    int? SourceCashSaleId = null);

public sealed record DailyCashHandoverResult(
    int? Id,
    DateOnly Date,
    CashHandoverStatus Status,
    DateTimeOffset? DeliveredAt,
    IReadOnlyList<CashHandoverLine> ManualItems,
    IReadOnlyList<CashHandoverLine> AutomaticItems,
    decimal ManualTotal,
    decimal AutomaticTotal,
    decimal Total);

public sealed record MonthlyCashHandoverRow(
    int Id,
    DateOnly Date,
    decimal ManualTotal,
    decimal AutomaticTotal,
    decimal Total,
    int ManualItemCount,
    int AutomaticItemCount,
    DateTimeOffset DeliveredAt);

public sealed record MonthlyCashHandoverResult(
    int Year,
    int Month,
    decimal Total,
    IReadOnlyList<MonthlyCashHandoverRow> Records);

public interface ICashHandoverService
{
    Task<bool> IsClosedAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<DailyCashHandoverResult> GetDailyAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<DailyCashHandoverResult> SaveDailyAsync(
        DateOnly date,
        CashHandoverUpdate update,
        CancellationToken cancellationToken = default);

    Task<MonthlyCashHandoverResult> GetMonthlyAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentTrackingSummary(
    decimal DistributorPaymentTotal,
    decimal PaidTotal,
    decimal PendingTotal,
    decimal CashCollectionTotal,
    int CashCollectionCount);

public sealed record PaymentTrackingPaymentRow(
    int Id,
    int DistributorId,
    string DistributorName,
    decimal Amount,
    DateOnly Date,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    PaymentType PaymentType,
    PaymentStatus Status,
    DateTimeOffset? PaidAt,
    string Description);

public sealed record CashCollectionRow(
    int Id,
    int? SubscriberId,
    string SubscriberName,
    DateOnly Date,
    decimal Amount,
    SubscriberPaymentMethod PaymentMethod,
    int? DistributorId,
    string DistributorName,
    bool IsCashSale,
    string Description);

public sealed record PaymentTrackingResult(
    int Year,
    int Month,
    int? DistributorId,
    PaymentTrackingSummary Summary,
    IReadOnlyList<PaymentTrackingPaymentRow> Payments,
    IReadOnlyList<CashCollectionRow> CashCollections);

public interface IPaymentTrackingService
{
    Task<PaymentTrackingResult> GetMonthlyAsync(
        int year,
        int month,
        int? distributorId = null,
        CancellationToken cancellationToken = default);
}

public sealed record ReportSummary(
    DateOnly Start,
    DateOnly End,
    int TotalNewspapers,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal PendingAmount,
    decimal CollectionRate);

public sealed record SubscriberCollectionSummary(
    DateOnly Start,
    DateOnly End,
    decimal DueTotal,
    decimal CollectedTotal);

public interface IReportService
{
    Task<ReportSummary> GetSummaryAsync(
        DateOnly start,
        DateOnly end,
        int? distributorId = null,
        DistributorZone? zone = null,
        CancellationToken cancellationToken = default);

    Task<SubscriberCollectionSummary> GetSubscriberCollectionSummaryAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);
}

public sealed record PeriodicJobSummary(
    int Created,
    int Existing,
    int Skipped,
    int Failed);

public interface IPeriodicPaymentService
{
    Task<PeriodicJobSummary> CreateScheduledDeliveriesAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<PeriodicJobSummary> CreateScheduledPaymentsAsync(
        PaymentType paymentType,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
