using System.Globalization;

namespace GazeteDagitim.Web.Models.ViewModels;

public sealed class BrandingViewModel
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static BrandingViewModel Empty => new();

    public string CompanyName { get; init; } = "Gazete Dağıtım";

    public string? CompanyLogoUrl { get; init; }

    public string? DistributorName { get; init; }

    public string? DistributorProfileImageUrl { get; init; }

    public string DistributorInitials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DistributorName))
            {
                return "D";
            }

            var initials = DistributorName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(2)
                .Select(part => char.ToUpper(part[0], TurkishCulture));

            return string.Concat(initials);
        }
    }
}

public sealed record BrandingDisplayViewModel(
    BrandingViewModel Branding,
    bool IsCorporate = false);
