using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Models.ViewModels.Subscribers;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Controllers;

[Route("subscribers")]
public sealed class SubscribersController(
    AppDbContext dbContext,
    IBusinessClock businessClock)
    : Controller
{
    private readonly AppDbContext _dbContext = dbContext;

    [HttpGet("")]
    public async Task<IActionResult> Index(string status = "all", CancellationToken cancellationToken = default)
    {
        status = NormalizeStatus(status);
        var query = _dbContext.Subscribers
            .AsNoTracking()
            .Include(subscriber => subscriber.PaymentPeriod)
            .Include(subscriber => subscriber.Distributor)
            .Include(subscriber => subscriber.NewspaperDays)
            .AsQueryable();

        query = status switch
        {
            "active" => query.Where(subscriber => subscriber.IsActive),
            "inactive" => query.Where(subscriber => !subscriber.IsActive),
            _ => query
        };

        var subscribers = await query
            .OrderBy(subscriber => subscriber.Name)
            .ToListAsync(cancellationToken);

        return View(new SubscriberIndexViewModel
        {
            Status = status,
            Items = subscribers.Select(subscriber => new SubscriberListItemViewModel
            {
                Id = subscriber.Id,
                Name = subscriber.Name,
                Phone = subscriber.Phone,
                Address = subscriber.Address,
                MonthlyFee = subscriber.MonthlyFee,
                IsActive = subscriber.IsActive,
                PaymentPeriodName = subscriber.PaymentPeriod?.Name,
                DistributorName = subscriber.Distributor?.Name,
                Latitude = subscriber.Latitude,
                Longitude = subscriber.Longitude,
                NewspaperDays = subscriber.NewspaperDays
                    .OrderBy(day => (int)day.Day)
                    .Select(day => ManagementOptions.DayLabel(day.Day.ToString()))
                    .ToArray()
            }).ToArray()
        });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new SubscriberFormViewModel();
        await PopulateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SubscriberFormViewModel model,
        CancellationToken cancellationToken)
    {
        var days = ParseNewspaperDays(model.NewspaperDays);
        await ValidateReferencesAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var subscriber = new Subscriber
        {
            Name = model.Name.Trim(),
            Phone = model.Phone?.Trim() ?? string.Empty,
            Address = model.Address?.Trim() ?? string.Empty,
            MonthlyFee = model.MonthlyFee,
            Notes = model.Notes?.Trim() ?? string.Empty,
            IsActive = model.IsActive,
            DeactivatedAt = model.IsActive ? null : businessClock.UtcNow,
            PaymentPeriodId = model.PaymentPeriodId,
            PaymentPeriodStartedOn = model.PaymentPeriodId.HasValue
                ? businessClock.Today
                : null,
            DistributorId = model.DistributorId,
            Latitude = model.Latitude,
            Longitude = model.Longitude
        };

        foreach (var day in days)
        {
            subscriber.NewspaperDays.Add(new SubscriberPublicationDay
            {
                Subscriber = subscriber,
                Day = day
            });
        }

        _dbContext.Subscribers.Add(subscriber);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = $"{subscriber.Name} adlı abone oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var subscriber = await _dbContext.Subscribers
            .AsNoTracking()
            .Include(item => item.NewspaperDays)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (subscriber is null)
        {
            return NotFound();
        }

        var model = new SubscriberFormViewModel
        {
            Id = subscriber.Id,
            Name = subscriber.Name,
            Phone = subscriber.Phone,
            Address = subscriber.Address,
            MonthlyFee = subscriber.MonthlyFee,
            Notes = subscriber.Notes,
            IsActive = subscriber.IsActive,
            PaymentPeriodId = subscriber.PaymentPeriodId,
            DistributorId = subscriber.DistributorId,
            Latitude = subscriber.Latitude,
            Longitude = subscriber.Longitude,
            NewspaperDays = subscriber.NewspaperDays
                .OrderBy(day => (int)day.Day)
                .Select(day => day.Day.ToString())
                .ToList()
        };

        await PopulateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        SubscriberFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Id.HasValue && model.Id.Value != id)
        {
            return BadRequest();
        }

        var subscriber = await _dbContext.Subscribers
            .Include(item => item.NewspaperDays)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (subscriber is null)
        {
            return NotFound();
        }

        var days = ParseNewspaperDays(model.NewspaperDays);
        await ValidateReferencesAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        subscriber.Name = model.Name.Trim();
        subscriber.Phone = model.Phone?.Trim() ?? string.Empty;
        subscriber.Address = model.Address?.Trim() ?? string.Empty;
        subscriber.MonthlyFee = model.MonthlyFee;
        subscriber.Notes = model.Notes?.Trim() ?? string.Empty;
        SynchronizeActivationState(subscriber, model.IsActive);
        if (subscriber.PaymentPeriodId != model.PaymentPeriodId)
        {
            subscriber.PaymentPeriodStartedOn = model.PaymentPeriodId.HasValue
                ? businessClock.Today
                : null;
        }
        subscriber.PaymentPeriodId = model.PaymentPeriodId;
        subscriber.DistributorId = model.DistributorId;
        subscriber.Latitude = model.Latitude;
        subscriber.Longitude = model.Longitude;
        SynchronizeNewspaperDays(subscriber, days);

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = $"{subscriber.Name} adlı abone güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(
        int id,
        bool isActive,
        string status = "all",
        CancellationToken cancellationToken = default)
    {
        var subscriber = await _dbContext.Subscribers
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (subscriber is null)
        {
            TempData["Error"] = "Abone bulunamadı.";
            return RedirectToAction(nameof(Index), new { status = NormalizeStatus(status) });
        }

        SynchronizeActivationState(subscriber, isActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = $"{subscriber.Name} {(isActive ? "aktifleştirildi" : "pasife alındı")}.";
        return RedirectToAction(nameof(Index), new { status = NormalizeStatus(status) });
    }

    private List<NewspaperDay> ParseNewspaperDays(IEnumerable<string>? values)
    {
        var days = new List<NewspaperDay>();

        foreach (var value in values ?? [])
        {
            if (!Enum.TryParse<NewspaperDay>(value, true, out var day)
                || !Enum.IsDefined(day))
            {
                ModelState.AddModelError(
                    nameof(SubscriberFormViewModel.NewspaperDays),
                    "Geçersiz bir gazete günü seçildi.");
                continue;
            }

            if (!days.Contains(day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    private async Task ValidateReferencesAsync(
        SubscriberFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PaymentPeriodId.HasValue
            && !await _dbContext.PaymentPeriods.AnyAsync(
                period => period.Id == model.PaymentPeriodId.Value,
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(model.PaymentPeriodId),
                "Seçilen ödeme periyodu bulunamadı.");
        }

        if (model.DistributorId.HasValue
            && !await _dbContext.Distributors.AnyAsync(
                distributor => distributor.Id == model.DistributorId.Value,
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(model.DistributorId),
                "Seçilen dağıtıcı bulunamadı.");
        }
    }

    private async Task PopulateOptionsAsync(
        SubscriberFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.PaymentPeriodOptions = await _dbContext.PaymentPeriods
            .AsNoTracking()
            .Where(period => period.IsActive || period.Id == model.PaymentPeriodId)
            .OrderBy(period => period.Name)
            .Select(period => new SelectListItem
            {
                Value = period.Id.ToString(),
                Text = period.Name + (period.IsActive ? string.Empty : " (Pasif)"),
                Selected = period.Id == model.PaymentPeriodId
            })
            .ToListAsync(cancellationToken);

        model.DistributorOptions = await _dbContext.Distributors
            .AsNoTracking()
            .Where(distributor => distributor.IsActive || distributor.Id == model.DistributorId)
            .OrderBy(distributor => distributor.Name)
            .Select(distributor => new SelectListItem
            {
                Value = distributor.Id.ToString(),
                Text = distributor.Name + (distributor.IsActive ? string.Empty : " (Pasif)"),
                Selected = distributor.Id == model.DistributorId
            })
            .ToListAsync(cancellationToken);
    }

    private void SynchronizeNewspaperDays(Subscriber subscriber, IReadOnlyCollection<NewspaperDay> days)
    {
        var selected = days.ToHashSet();
        var removed = subscriber.NewspaperDays
            .Where(item => !selected.Contains(item.Day))
            .ToArray();

        _dbContext.Set<SubscriberPublicationDay>().RemoveRange(removed);

        var existing = subscriber.NewspaperDays
            .Select(item => item.Day)
            .ToHashSet();

        foreach (var day in selected.Where(day => !existing.Contains(day)))
        {
            subscriber.NewspaperDays.Add(new SubscriberPublicationDay
            {
                SubscriberId = subscriber.Id,
                Day = day
            });
        }
    }

    private static string NormalizeStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "active" => "active",
            "inactive" => "inactive",
            _ => "all"
        };

    private void SynchronizeActivationState(
        Subscriber subscriber,
        bool isActive)
    {
        if (subscriber.IsActive == isActive &&
            (isActive || subscriber.DeactivatedAt.HasValue))
        {
            return;
        }

        subscriber.IsActive = isActive;
        subscriber.DeactivatedAt = isActive ? null : businessClock.UtcNow;
    }
}
