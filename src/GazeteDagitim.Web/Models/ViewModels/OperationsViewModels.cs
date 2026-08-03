using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace GazeteDagitim.Web.Models.ViewModels;

public sealed class DailyDeliveriesPageViewModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool IsCashLocked { get; set; }
    public List<DailyDeliveryRowViewModel> Rows { get; set; } = [];
    public List<NewspaperCashSaleDistributorViewModel> CashSaleDistributors { get; set; } = [];
    public List<NewspaperCashSaleViewModel> CashSales { get; set; } = [];
    public Guid CashSaleRequestId { get; set; } = Guid.NewGuid();
    public bool ShowDistributorAndCoverage { get; set; } = true;

    public int DeliveredCount => Rows.Count(row => row.Delivered);
    public int CollectedCount => Rows.Count(row => row.Collected);
    public decimal SubscriberCollectedTotal =>
        Rows.Where(row => row.Collected).Sum(row => row.Amount);
    public decimal CashSaleTotal => CashSales.Sum(value => value.Amount);
    public int CashSaleQuantity => CashSales.Sum(value => value.Quantity);
    public decimal CollectedTotal => SubscriberCollectedTotal + CashSaleTotal;
    public bool CanCreateCashSale =>
        !IsCashLocked && CashSaleDistributors.Any(value => value.UnitPrice > 0);
}

public sealed class DeliveredSubscribersPageViewModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool ShowAllSubscribers { get; set; }
    public int DeliveredCount { get; set; }
    public int SubscriberCount { get; set; }
    public List<DailyDeliveryRowViewModel> Rows { get; set; } = [];
}

public sealed class DailyCollectionsPageViewModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool IsCashLocked { get; set; }
    public List<DailyDeliveryRowViewModel> Rows { get; set; } = [];
    public List<NewspaperCashSaleViewModel> CashSales { get; set; } = [];

    public int CollectedCount => Rows.Count(value => value.Collected);
    public int PendingCount => Rows.Count(value => !value.Collected);
    public decimal SubscriberCollectedTotal =>
        Rows.Where(value => value.Collected).Sum(value => value.Amount);
    public decimal CashSaleTotal => CashSales.Sum(value => value.Amount);
    public int CashSaleQuantity => CashSales.Sum(value => value.Quantity);
    public decimal CollectedTotal => SubscriberCollectedTotal + CashSaleTotal;
}

public sealed class DailyDeliveryRowViewModel
{
    public int SubscriberId { get; set; }
    public string SubscriberName { get; set; } = "";
    public string DistributorName { get; set; } = "Atanmamış";
    public bool HasDelivery { get; set; }
    public bool IsScheduled { get; set; }
    public int NewspaperCount { get; set; } = 1;
    public string CoverageLabel { get; set; } = "";
    public bool Delivered { get; set; }
    public bool Collected { get; set; }
    public bool IsPaymentDue { get; set; }
    public bool ShowPaymentControls => IsPaymentDue || Collected;

    [Range(typeof(decimal), "0", "9999999999999999")]
    [ModelBinder(BinderType = typeof(InvariantDecimalModelBinder))]
    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = "Nakit";
}

public sealed class DailyDeliveriesInputModel
{
    [Required]
    public DateOnly Date { get; set; }

    public List<DailyDeliveryRowInputModel> Rows { get; set; } = [];
}

public sealed class DailyDeliveryRowInputModel
{
    public int SubscriberId { get; set; }
    public bool Delivered { get; set; }
    public bool Collected { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [ModelBinder(BinderType = typeof(InvariantDecimalModelBinder))]
    public decimal Amount { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = "Nakit";
}

public sealed class DailyDeliveryRowAutosaveInputModel : IValidatableObject
{
    [Required]
    public DateOnly Date { get; set; }

    [Range(1, int.MaxValue)]
    public int SubscriberId { get; set; }

    public bool? Delivered { get; set; }

    public bool? Collected { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [ModelBinder(BinderType = typeof(InvariantDecimalModelBinder))]
    public decimal? Amount { get; set; }

    [StringLength(30)]
    public string? PaymentMethod { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Delivered is null &&
            Collected is null &&
            Amount is null &&
            PaymentMethod is null)
        {
            yield return new ValidationResult(
                "Kaydedilecek en az bir değişiklik gönderilmelidir.",
                [
                    nameof(Delivered),
                    nameof(Collected),
                    nameof(Amount),
                    nameof(PaymentMethod)
                ]);
        }
    }
}

public sealed class DailyNewspaperCashSaleInputModel
{
    [Required]
    public DateOnly Date { get; set; }

    [Range(1, int.MaxValue)]
    public int DistributorId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;

    public Guid IdempotencyKey { get; set; }
}

public sealed class NewspaperCashSaleDistributorViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public decimal UnitPrice { get; init; }
}

public sealed class NewspaperCashSaleViewModel
{
    public int Id { get; init; }
    public string DistributorName { get; init; } = "";
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class DailyDeliveryRowAutosaveResponseModel
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public DailyDeliveryRowAutosaveStateViewModel? Row { get; init; }
    public DailyDeliveryAutosaveSummaryViewModel? Summary { get; init; }
}

public sealed class DailyDeliveryRowAutosaveStateViewModel
{
    public int SubscriberId { get; init; }
    public bool Delivered { get; init; }
    public bool Collected { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = "Nakit";
}

public sealed class DailyDeliveryAutosaveSummaryViewModel
{
    public int DeliveredCount { get; init; }
    public int CollectedCount { get; init; }
    public decimal CollectedTotal { get; init; }
}

public sealed class PaymentsPageViewModel
{
    public string Month { get; set; } = DateTime.Today.ToString("yyyy-MM");
    public int? DistributorId { get; set; }
    public List<LookupOptionViewModel> Distributors { get; set; } = [];
    public List<CashCollectionViewModel> CashCollections { get; set; } = [];
    public List<DistributorPaymentViewModel> Payments { get; set; } = [];
    public decimal CashCollectedTotal { get; set; }
    public decimal PaymentTotal { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal PendingTotal { get; set; }
}

public sealed class LookupOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class CashCollectionViewModel
{
    public int Id { get; set; }
    public string SubscriberName { get; set; } = "";
    public string DistributorName { get; set; } = "Atanmamış";
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Nakit";
    public bool IsCashSale { get; set; }
    public string Description { get; set; } = "";
}

public sealed class DistributorPaymentViewModel
{
    public int Id { get; set; }
    public string DistributorName { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly DueDate { get; set; }
    public string PaymentType { get; set; } = "";
    public bool IsPaid { get; set; }
}

public sealed class CashHandoverPageViewModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Month { get; set; } = DateTime.Today.ToString("yyyy-MM");
    public string Status { get; set; } = "Taslak";
    public DateTimeOffset? DeliveredAt { get; set; }
    public List<CashHandoverItemViewModel> AutomaticItems { get; set; } = [];
    public List<CashHandoverItemViewModel> ManualItems { get; set; } = [];
    public List<string> SubscriberSuggestions { get; set; } = [];
    public decimal MonthlyDeliveredTotal { get; set; }
    public int MonthlyDeliveredDayCount { get; set; }

    public bool IsDelivered => Status == "Teslim Edildi";
    public decimal AutomaticTotal => AutomaticItems.Sum(item => item.Amount);
    public decimal ManualTotal => ManualItems.Sum(item => item.Amount);
    public decimal DailyTotal => AutomaticTotal + ManualTotal;
}

public sealed class CashHandoverItemViewModel
{
    public string SubscriberName { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string PaymentMethod { get; set; } = "Nakit";
    public bool IsAutomatic { get; set; }
}

public sealed class CashHandoverInputModel
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public string Status { get; set; } = "Taslak";

    public List<CashHandoverManualItemInputModel> Items { get; set; } = [];
}

public sealed class CashHandoverManualItemInputModel
{
    [Required, StringLength(200)]
    public string SubscriberName { get; set; } = "";

    [Range(typeof(decimal), "0", "9999999999999999")]
    [ModelBinder(BinderType = typeof(InvariantDecimalModelBinder))]
    public decimal Amount { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}

public sealed class ReportsPageViewModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int NewspaperTotal { get; set; }
    public int CompletedDeliveryCount { get; set; }
    public int PendingDeliveryCount { get; set; }
    public decimal PaymentTotal { get; set; }
    public decimal PendingPaymentTotal { get; set; }
    public decimal CollectionRate { get; set; }
    public decimal SubscriberDueTotal { get; set; }
    public decimal SubscriberCollectedTotal { get; set; }
    public List<ReportDeliveryViewModel> Deliveries { get; set; } = [];
    public List<DistributorPaymentViewModel> Payments { get; set; } = [];

    public decimal SubscriberRemainingTotal =>
        Math.Max(0m, SubscriberDueTotal - SubscriberCollectedTotal);

    public decimal SubscriberCollectionRate =>
        SubscriberDueTotal > 0m
            ? Math.Min(
                100m,
                Math.Round(
                    SubscriberCollectedTotal / SubscriberDueTotal * 100m,
                    2,
                    MidpointRounding.AwayFromZero))
            : 0m;
}

public sealed class ReportDeliveryViewModel
{
    public string DistributorName { get; set; } = "";
    public int NewspaperCount { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}

public sealed class CompanySettingsPageViewModel
{
    public int? FeaturedDistributorId { get; set; }
    public decimal? NewspaperUnitPrice { get; set; }
    public bool ShowDistributorAndCoverage { get; set; } = true;
    public string? CompanyLogoDataUrl { get; set; }
    public string? DistributorProfileImageDataUrl { get; set; }
    public List<LookupOptionViewModel> Distributors { get; set; } = [];
}

public sealed class CompanySettingsInputModel
{
    public int? FeaturedDistributorId { get; set; }
    public bool ShowDistributorAndCoverage { get; set; } = true;

    [Required(ErrorMessage = "Gazete birim satış fiyatı zorunludur.")]
    [Range(
        typeof(decimal),
        "0.01",
        "999999999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Gazete birim satış fiyatı sıfırdan büyük olmalıdır.")]
    [ModelBinder(BinderType = typeof(InvariantDecimalModelBinder))]
    public decimal? NewspaperUnitPrice { get; set; }

    public IFormFile? CompanyLogo { get; set; }
    public IFormFile? DistributorProfileImage { get; set; }
    public bool RemoveCompanyLogo { get; set; }
    public bool RemoveDistributorProfileImage { get; set; }
}
