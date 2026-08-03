using System.ComponentModel.DataAnnotations;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GazeteDagitim.Web.Models.ViewModels.PaymentPeriods;

public static class PaymentPeriodScheduleTypes
{
    public const string Monthly = "monthly";
    public const string Daily = "daily";
}

public sealed class PaymentPeriodIndexViewModel
{
    public string Status { get; init; } = "all";
    public IReadOnlyList<PaymentPeriodListItemViewModel> Items { get; init; } = [];
}

public sealed class PaymentPeriodListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int DayCount { get; init; }
    public int? CollectionDayOfMonth { get; init; }
    public TimeOnly? CollectionTime { get; init; }
    public decimal? CollectionAmount { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public PaymentPeriodFrequency Frequency { get; init; }
    public bool IsDaily => Frequency == PaymentPeriodFrequency.Daily;
}

public sealed class PaymentPeriodFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Periyot adı zorunludur.")]
    [StringLength(120, ErrorMessage = "Periyot adı en fazla 120 karakter olabilir.")]
    [Display(Name = "Periyot adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tahsilat sıklığı zorunludur.")]
    [RegularExpression(
        "^(monthly|daily)$",
        ErrorMessage = "Tahsilat sıklığı geçersizdir.")]
    [Display(Name = "Tahsilat sıklığı")]
    public string ScheduleType { get; set; } = PaymentPeriodScheduleTypes.Monthly;

    [Range(1, 365, ErrorMessage = "Ödeme kapsamı 1 ile 365 gün arasında olmalıdır.")]
    [Display(Name = "Kaç günlük ödeme alınacak?")]
    public int DayCount { get; set; } = 30;

    [Range(1, 31, ErrorMessage = "Ödeme alınacak gün 1 ile 31 arasında olmalıdır.")]
    [Display(Name = "Ödeme alınacak gün")]
    public int? CollectionDayOfMonth { get; set; } = 1;

    [Required(ErrorMessage = "Ödeme alınacak saat zorunludur.")]
    [Display(Name = "Ödeme alınacak saat")]
    public TimeOnly? CollectionTime { get; set; } = new(9, 0);

    [Required(ErrorMessage = "Alınacak tutar zorunludur.")]
    [Range(
        typeof(decimal),
        "0",
        "9999999999999999",
        ErrorMessage = "Alınacak tutar geçersizdir.")]
    [ModelBinder(BinderType = typeof(InvariantDecimalModelBinder))]
    [Display(Name = "Alınacak tutar")]
    public decimal? CollectionAmount { get; set; }

    [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Aktif ödeme periyodu")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScheduleType == PaymentPeriodScheduleTypes.Monthly &&
            CollectionDayOfMonth is null)
        {
            yield return new ValidationResult(
                "Ödeme alınacak gün zorunludur.",
                [nameof(CollectionDayOfMonth)]);
        }

        if (CollectionAmount is <= 0)
        {
            yield return new ValidationResult(
                "Alınacak tutar sıfırdan büyük olmalıdır.",
                [nameof(CollectionAmount)]);
        }
    }
}
