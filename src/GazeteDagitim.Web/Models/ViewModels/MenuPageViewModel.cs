namespace GazeteDagitim.Web.Models.ViewModels;

public sealed record MenuItemViewModel(
    string Url,
    string Label,
    string Icon,
    string Description,
    bool IsFeatured = false);

public sealed class MenuPageViewModel
{
    public required string Eyebrow { get; init; }

    public required string Title { get; init; }

    public required string Icon { get; init; }

    public bool IsCompanyMenu { get; init; }

    public string? ParentMenuUrl { get; init; }

    public BrandingViewModel Branding { get; init; } = BrandingViewModel.Empty;

    public IReadOnlyList<MenuItemViewModel> Items { get; init; } = [];
}
