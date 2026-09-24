using System.ComponentModel.DataAnnotations;

namespace GazeteDagitim.Web.Models.ViewModels;

/// <summary>
/// Prevents malformed or impractically distant dates from reaching services
/// that build day-by-day or month-by-month schedules.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SupportedDateAttribute : ValidationAttribute
{
    public static readonly DateOnly Minimum = new(1900, 1, 1);
    public static readonly DateOnly Maximum = new(2100, 12, 31);

    public SupportedDateAttribute()
        : base("Tarih 01.01.1900 ile 31.12.2100 arasında olmalıdır.")
    {
    }

    public override bool IsValid(object? value) =>
        value is null ||
        value is DateOnly date && date >= Minimum && date <= Maximum;
}
