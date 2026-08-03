using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels.PaymentPeriods;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Controllers;

[Route("settings")]
public sealed class PaymentPeriodsController(
    AppDbContext dbContext,
    IBusinessClock businessClock) : Controller
{
    private readonly AppDbContext _dbContext = dbContext;

    [HttpGet("")]
    public async Task<IActionResult> Index(string status = "all", CancellationToken cancellationToken = default)
    {
        status = NormalizeStatus(status);
        var query = _dbContext.PaymentPeriods.AsNoTracking();

        query = status switch
        {
            "active" => query.Where(period => period.IsActive),
            "inactive" => query.Where(period => !period.IsActive),
            _ => query
        };

        var items = await query
            .OrderBy(period => period.Name)
            .Select(period => new PaymentPeriodListItemViewModel
            {
                Id = period.Id,
                Name = period.Name,
                DayCount = period.DayCount,
                CollectionDayOfMonth = period.CollectionDayOfMonth,
                CollectionTime = period.CollectionTime,
                CollectionAmount = period.CollectionAmount,
                Description = period.Description,
                IsActive = period.IsActive,
                Frequency = period.Frequency
            })
            .ToListAsync(cancellationToken);

        return View(new PaymentPeriodIndexViewModel
        {
            Status = status,
            Items = items
        });
    }

    [HttpGet("create")]
    public IActionResult Create() => View(new PaymentPeriodFormViewModel());

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PaymentPeriodFormViewModel model,
        CancellationToken cancellationToken)
    {
        NormalizeScheduleInput(model);
        await ValidateUniqueNameAsync(model.Name, null, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var period = new PaymentPeriod
        {
            Name = model.Name.Trim(),
            DayCount = model.DayCount,
            Frequency = NormalizeFrequency(model),
            CollectionDayOfMonth = NormalizeCollectionDay(model),
            CollectionTime = model.CollectionTime,
            CollectionAmount = model.CollectionAmount,
            Description = model.Description?.Trim() ?? string.Empty,
            IsActive = model.IsActive
        };

        _dbContext.PaymentPeriods.Add(period);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = $"{period.Name} ödeme periyodu oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var period = await _dbContext.PaymentPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (period is null)
        {
            return NotFound();
        }

        return View(new PaymentPeriodFormViewModel
        {
            Id = period.Id,
            Name = period.Name,
            ScheduleType = period.Frequency == PaymentPeriodFrequency.Daily
                ? PaymentPeriodScheduleTypes.Daily
                : PaymentPeriodScheduleTypes.Monthly,
            DayCount = period.DayCount,
            CollectionDayOfMonth = period.CollectionDayOfMonth,
            CollectionTime = period.CollectionTime,
            CollectionAmount = period.CollectionAmount,
            Description = period.Description,
            IsActive = period.IsActive
        });
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        PaymentPeriodFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Id.HasValue && model.Id.Value != id)
        {
            return BadRequest();
        }

        NormalizeScheduleInput(model);
        var period = await _dbContext.PaymentPeriods
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (period is null)
        {
            return NotFound();
        }

        await ValidateUniqueNameAsync(model.Name, id, cancellationToken);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        var frequency = NormalizeFrequency(model);
        if (period.Frequency != frequency)
        {
            var subscribers = await _dbContext.Subscribers
                .Where(value => value.PaymentPeriodId == period.Id)
                .ToListAsync(cancellationToken);
            foreach (var subscriber in subscribers)
            {
                subscriber.PaymentPeriodStartedOn = businessClock.Today;
            }
        }

        period.Name = model.Name.Trim();
        period.DayCount = model.DayCount;
        period.Frequency = frequency;
        period.CollectionDayOfMonth = NormalizeCollectionDay(model);
        period.CollectionTime = model.CollectionTime;
        period.CollectionAmount = model.CollectionAmount;
        period.Description = model.Description?.Trim() ?? string.Empty;
        period.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = $"{period.Name} ödeme periyodu güncellendi.";
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
        var period = await _dbContext.PaymentPeriods
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (period is null)
        {
            TempData["Error"] = "Ödeme periyodu bulunamadı.";
            return RedirectToAction(nameof(Index), new { status = NormalizeStatus(status) });
        }

        period.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["Notice"] = $"{period.Name} {(isActive ? "aktifleştirildi" : "pasife alındı")}.";
        return RedirectToAction(nameof(Index), new { status = NormalizeStatus(status) });
    }

    private async Task ValidateUniqueNameAsync(
        string? name,
        int? ignoredId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        var exists = await _dbContext.PaymentPeriods
            .AnyAsync(
                period => period.Id != ignoredId && period.Name == normalizedName,
                cancellationToken);

        if (exists)
        {
            ModelState.AddModelError(
                nameof(PaymentPeriodFormViewModel.Name),
                "Bu adla bir ödeme periyodu zaten var.");
        }
    }

    private static string NormalizeStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "active" => "active",
            "inactive" => "inactive",
            _ => "all"
        };

    private static int? NormalizeCollectionDay(
        PaymentPeriodFormViewModel model) =>
        model.ScheduleType == PaymentPeriodScheduleTypes.Daily
            ? 1
            : model.DayCount == 10
                ? 10
                : model.CollectionDayOfMonth;

    private static PaymentPeriodFrequency NormalizeFrequency(
        PaymentPeriodFormViewModel model) =>
        model.ScheduleType == PaymentPeriodScheduleTypes.Daily
            ? PaymentPeriodFrequency.Daily
            : PaymentPeriodFrequency.Monthly;

    private void NormalizeScheduleInput(PaymentPeriodFormViewModel model)
    {
        if (model.ScheduleType != PaymentPeriodScheduleTypes.Daily)
        {
            return;
        }

        model.DayCount = 1;
        model.CollectionDayOfMonth = 1;
        ModelState.Remove(nameof(PaymentPeriodFormViewModel.DayCount));
        ModelState.Remove(nameof(PaymentPeriodFormViewModel.CollectionDayOfMonth));
    }
}
