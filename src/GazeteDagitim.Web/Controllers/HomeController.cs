using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models;
using GazeteDagitim.Web.Models.ViewModels;

namespace GazeteDagitim.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _dbContext;

    public HomeController(
        ILogger<HomeController> logger,
        AppDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    [HttpGet("")]
    [HttpGet("menu")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new MenuPageViewModel
        {
            Eyebrow = "GAZETE DAĞITIM",
            Title = "ANA MENÜ",
            Icon = "📰",
            Branding = await ResolveBrandingAsync(cancellationToken),
            Items =
            [
                new("/deliveries", "DAĞITIMLAR", "🗞️", "Günlük teslimat ve tahsilat listesi", true),
                new("/reports", "RAPORLAR", "📊", "Güncel sonuçları inceleyin"),
                new("/subscribers", "ABONELER", "👥", "Abone kayıtlarını yönetin"),
                new("/menu/company", "GAZETE FİRMASI", "🏢", "Firma menüsünü aç"),
                new("/payments", "ÖDEMELER", "₺", "Tahsilatları takip edin"),
                new("/cash-handover", "KASA TESLİMİ", "💵", "Günlük kasayı teslim edin"),
                new("/settings", "AYARLAR", "⚙️", "Uygulama seçeneklerini belirleyin")
            ]
        };

        return View(model);
    }

    [HttpGet("menu/company")]
    public async Task<IActionResult> Company(CancellationToken cancellationToken)
    {
        var model = new MenuPageViewModel
        {
            Eyebrow = "GAZETE FİRMASI",
            Title = "FİRMA MENÜSÜ",
            Icon = "🏢",
            IsCompanyMenu = true,
            ParentMenuUrl = "/menu",
            Branding = await ResolveBrandingAsync(cancellationToken),
            Items =
            [
                new("/distributors", "DAĞITICILAR", "◉", "Dağıtıcı kayıtları ve ödeme planları"),
                new("/menu/company/settings", "FİRMA AYARLARI", "⚙️", "Logo, profil ve gazete satış fiyatı")
            ]
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<BrandingViewModel> ResolveBrandingAsync(
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.CompanySettings
            .AsNoTracking()
            .Include(value => value.FeaturedDistributor)
            .SingleOrDefaultAsync(
                value => value.SingletonKey == "company",
                cancellationToken);

        return settings is null
            ? BrandingViewModel.Empty
            : new BrandingViewModel
            {
                CompanyLogoUrl = settings.LogoDataUrl,
                DistributorName = settings.FeaturedDistributor?.Name,
                DistributorProfileImageUrl =
                    settings.FeaturedDistributor?.ProfileImageDataUrl
            };
    }
}
