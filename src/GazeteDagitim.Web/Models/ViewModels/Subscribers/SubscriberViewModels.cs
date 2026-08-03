using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GazeteDagitim.Web.Models.ViewModels.Subscribers;

public sealed class SubscriberIndexViewModel
{
    public string Status { get; init; } = "all";
    public IReadOnlyList<SubscriberListItemViewModel> Items { get; init; } = [];
}

public sealed class SubscriberListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public decimal MonthlyFee { get; init; }
    public bool IsActive { get; init; }
    public string? PaymentPeriodName { get; init; }
    public string? DistributorName { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public IReadOnlyList<string> NewspaperDays { get; init; } = [];
}

public sealed class SubscriberFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Abone adı zorunludur.")]
    [StringLength(160, ErrorMessage = "Abone adı en fazla 160 karakter olabilir.")]
    [Display(Name = "Abone adı")]
    public string Name { get; set; } = string.Empty;

    [StringLength(40, ErrorMessage = "Telefon en fazla 40 karakter olabilir.")]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
    [Display(Name = "Teslimat adresi")]
    public string? Address { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Aylık ücret sıfır veya daha büyük olmalıdır.")]
    [Display(Name = "Aylık ücret")]
    public decimal MonthlyFee { get; set; }

    [StringLength(1000, ErrorMessage = "Notlar en fazla 1000 karakter olabilir.")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    [Display(Name = "Aktif abone")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Ödeme periyodu")]
    public int? PaymentPeriodId { get; set; }

    [Display(Name = "Dağıtıcı")]
    public int? DistributorId { get; set; }

    [Range(typeof(decimal), "-90", "90", ErrorMessage = "Enlem -90 ile 90 arasında olmalıdır.")]
    [Display(Name = "Enlem")]
    public decimal? Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180", ErrorMessage = "Boylam -180 ile 180 arasında olmalıdır.")]
    [Display(Name = "Boylam")]
    public decimal? Longitude { get; set; }

    [Display(Name = "Gazete alınacak günler")]
    public List<string> NewspaperDays { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<SelectListItem> PaymentPeriodOptions { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<SelectListItem> DistributorOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var selectedDays = NewspaperDays
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedDays.Contains("SundayMonday")
            && (selectedDays.Contains("Sunday") || selectedDays.Contains("Monday")))
        {
            yield return new ValidationResult(
                "Pazar Pazartesi seçeneği, Pazar veya Pazartesi ile birlikte seçilemez.",
                [nameof(NewspaperDays)]);
        }

        if (Latitude.HasValue != Longitude.HasValue)
        {
            yield return new ValidationResult(
                "Konum kaydetmek için enlem ve boylam birlikte girilmelidir.",
                [nameof(Latitude), nameof(Longitude)]);
        }
    }
}
