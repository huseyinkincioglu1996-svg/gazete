using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Controllers;

[Route("menu/company/settings")]
public sealed class CompanySettingsController(AppDbContext dbContext) : Controller
{
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    private const long MaximumImageBytes = 2 * 1024 * 1024;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildPageAsync(null, null, false, cancellationToken));

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Save(
        CompanySettingsInputModel input,
        CancellationToken cancellationToken)
    {
        var distributorExists = !input.FeaturedDistributorId.HasValue ||
            await dbContext.Distributors
                .AsNoTracking()
                .AnyAsync(
                    value => value.Id == input.FeaturedDistributorId.Value,
                    cancellationToken);
        if (!distributorExists)
        {
            ModelState.AddModelError(
                nameof(input.FeaturedDistributorId),
                "Seçilen dağıtıcı bulunamadı.");
        }

        if (input.DistributorProfileImage is not null &&
            !input.FeaturedDistributorId.HasValue)
        {
            ModelState.AddModelError(
                nameof(input.DistributorProfileImage),
                "Profil görseli için önce vitrin dağıtıcısı seçilmelidir.");
        }

        string? companyLogoDataUrl = null;
        string? distributorProfileDataUrl = null;
        if (input.CompanyLogo is not null)
        {
            try
            {
                companyLogoDataUrl = await ReadImageDataUrlAsync(
                    input.CompanyLogo,
                    cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                ModelState.AddModelError(nameof(input.CompanyLogo), exception.Message);
            }
        }

        if (input.DistributorProfileImage is not null)
        {
            try
            {
                distributorProfileDataUrl = await ReadImageDataUrlAsync(
                    input.DistributorProfileImage,
                    cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                ModelState.AddModelError(
                    nameof(input.DistributorProfileImage),
                    exception.Message);
            }
        }

        if (!ModelState.IsValid)
        {
            return View(
                nameof(Index),
                await BuildPageAsync(
                    input.FeaturedDistributorId,
                    input.NewspaperUnitPrice,
                    true,
                    cancellationToken));
        }

        var settings = await dbContext.CompanySettings
            .SingleOrDefaultAsync(
                value => value.SingletonKey == "company",
                cancellationToken);
        if (settings is null)
        {
            settings = new CompanySettings();
            dbContext.CompanySettings.Add(settings);
        }

        var distributor = input.FeaturedDistributorId.HasValue
            ? await dbContext.Distributors.FindAsync(
                [input.FeaturedDistributorId.Value],
                cancellationToken)
            : null;

        settings.FeaturedDistributorId = input.FeaturedDistributorId;
        settings.NewspaperUnitPrice = input.NewspaperUnitPrice.HasValue
            ? DomainRules.RoundCurrency(input.NewspaperUnitPrice.Value)
            : null;
        if (input.RemoveCompanyLogo)
        {
            settings.LogoDataUrl = null;
        }
        if (companyLogoDataUrl is not null)
        {
            settings.LogoDataUrl = companyLogoDataUrl;
        }

        if (input.RemoveDistributorProfileImage && distributor is not null)
        {
            distributor.ProfileImageDataUrl = null;
        }
        if (distributorProfileDataUrl is not null && distributor is not null)
        {
            distributor.ProfileImageDataUrl = distributorProfileDataUrl;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["Notice"] = "Firma ayarları ve gazete birim satış fiyatı kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CompanySettingsPageViewModel> BuildPageAsync(
        int? requestedFeaturedDistributorId,
        decimal? requestedNewspaperUnitPrice,
        bool useRequestedValues,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.CompanySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.SingletonKey == "company",
                cancellationToken);
        var distributors = await dbContext.Distributors
            .AsNoTracking()
            .OrderByDescending(value => value.IsActive)
            .ThenBy(value => value.Name)
            .ToListAsync(cancellationToken);
        var featuredDistributorId = useRequestedValues
            ? requestedFeaturedDistributorId
            : settings?.FeaturedDistributorId;
        var newspaperUnitPrice = useRequestedValues
            ? requestedNewspaperUnitPrice
            : settings?.NewspaperUnitPrice;
        var featuredDistributor = distributors
            .SingleOrDefault(value => value.Id == featuredDistributorId);

        return new CompanySettingsPageViewModel
        {
            FeaturedDistributorId = featuredDistributorId,
            NewspaperUnitPrice = newspaperUnitPrice,
            CompanyLogoDataUrl = settings?.LogoDataUrl,
            DistributorProfileImageDataUrl = featuredDistributor?.ProfileImageDataUrl,
            Distributors = distributors.Select(value => new LookupOptionViewModel
            {
                Id = value.Id,
                Name = value.Name,
                IsActive = value.IsActive
            }).ToList()
        };
    }

    private static async Task<string> ReadImageDataUrlAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var declaredContentType = file.ContentType
            .Split(';', 2, StringSplitOptions.TrimEntries)[0]
            .ToLowerInvariant();
        if (!AllowedImageTypes.Contains(declaredContentType))
        {
            throw new InvalidOperationException(
                "Yalnızca PNG, JPEG veya WebP görseller yüklenebilir.");
        }
        if (file.Length is <= 0 or > MaximumImageBytes)
        {
            throw new InvalidOperationException("Görsel boyutu en fazla 2 MB olabilir.");
        }

        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, cancellationToken);
        if (stream.Length is <= 0 or > MaximumImageBytes)
        {
            throw new InvalidOperationException("Görsel boyutu en fazla 2 MB olabilir.");
        }

        var bytes = stream.ToArray();
        var detectedContentType = DetectImageContentType(bytes);
        if (detectedContentType is null ||
            !string.Equals(
                detectedContentType,
                declaredContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Dosya içeriği seçilen PNG, JPEG veya WebP biçimiyle eşleşmiyor.");
        }

        return $"data:{detectedContentType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static string? DetectImageContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xD8 &&
            bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' &&
            bytes[1] == (byte)'I' &&
            bytes[2] == (byte)'F' &&
            bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' &&
            bytes[9] == (byte)'E' &&
            bytes[10] == (byte)'B' &&
            bytes[11] == (byte)'P')
        {
            return "image/webp";
        }

        return null;
    }
}
