using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GazeteDagitim.Web.Models.ViewModels.Distributors;

public sealed class DistributorIndexViewModel
{
    public bool IncludeInactive { get; init; }
    public IReadOnlyList<DistributorListItemViewModel> Items { get; init; } = [];
}

public sealed class DistributorListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Zone { get; init; } = string.Empty;
    public string PaymentType { get; init; } = string.Empty;
    public decimal NewspaperPrice { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> DistributionDays { get; init; } = [];
    public IReadOnlyList<string> PaymentDays { get; init; } = [];
}

public sealed class DistributorFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Dağıtıcı adı zorunludur.")]
    [StringLength(120, ErrorMessage = "Dağıtıcı adı en fazla 120 karakter olabilir.")]
    [Display(Name = "Dağıtıcı adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adres zorunludur.")]
    [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
    [Display(Name = "Adres")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon zorunludur.")]
    [StringLength(40, ErrorMessage = "Telefon en fazla 40 karakter olabilir.")]
    [Display(Name = "Telefon")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bölge seçimi zorunludur.")]
    [Display(Name = "Bölge")]
    public string Zone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ödeme tipi seçimi zorunludur.")]
    [Display(Name = "Ödeme tipi")]
    public string PaymentType { get; set; } = "Daily";

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Dağıtıcı birim maliyeti sıfır veya daha büyük olmalıdır.")]
    [Display(Name = "Dağıtıcı birim maliyeti")]
    public decimal NewspaperPrice { get; set; } = 5;

    [Display(Name = "Dağıtım günleri")]
    public List<string> DistributionDays { get; set; } = [];

    [Display(Name = "Haftalık ödeme günleri")]
    public List<string> WeeklyPaymentDays { get; set; } = [];

    [Display(Name = "Aylık ödeme günleri")]
    public List<int> MonthlyPaymentDays { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<SelectListItem> ZoneOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var paymentType = PaymentType?.Trim();

        if (string.Equals(paymentType, "Weekly", StringComparison.OrdinalIgnoreCase)
            && WeeklyPaymentDays.Count == 0)
        {
            yield return new ValidationResult(
                "Haftalık ödeme için en az bir ödeme günü seçin.",
                [nameof(WeeklyPaymentDays)]);
        }

        if (string.Equals(paymentType, "Monthly", StringComparison.OrdinalIgnoreCase)
            && MonthlyPaymentDays.Count == 0)
        {
            yield return new ValidationResult(
                "Aylık ödeme için en az bir ay günü seçin.",
                [nameof(MonthlyPaymentDays)]);
        }

        if (MonthlyPaymentDays.Any(day => day is < 1 or > 31))
        {
            yield return new ValidationResult(
                "Aylık ödeme günleri 1 ile 31 arasında olmalıdır.",
                [nameof(MonthlyPaymentDays)]);
        }
    }
}
