using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Controllers;

[Route("cash-handover")]
public sealed class CashHandoversController(
    AppDbContext dbContext,
    ICashHandoverService cashHandoverService,
    IBusinessClock clock) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var selectedDate = date ?? clock.Today;
        return View(await BuildPageAsync(selectedDate, cancellationToken));
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        CashHandoverInputModel input,
        CancellationToken cancellationToken)
    {
        var selectedDate = input.Date == default ? clock.Today : input.Date;
        if (input.Date != default &&
            await cashHandoverService.IsClosedAsync(input.Date, cancellationToken))
        {
            TempData["Error"] =
                "Teslim edilmiş günlük kasa kaydı yeniden açılamaz veya değiştirilemez.";
            return RedirectToAction(
                nameof(Index),
                new { date = input.Date.ToString("yyyy-MM-dd") });
        }

        CashHandoverStatus? status = input.Status switch
        {
            "Taslak" => CashHandoverStatus.Draft,
            "Teslim Edildi" => CashHandoverStatus.Delivered,
            _ => null
        };
        if (status is null)
        {
            ModelState.AddModelError(
                nameof(input.Status),
                "Geçerli bir kasa durumu seçilmelidir.");
        }

        if (!ModelState.IsValid)
        {
            var model = await BuildPageAsync(selectedDate, cancellationToken);
            model.ManualItems = input.Items.Select(item => new CashHandoverItemViewModel
            {
                SubscriberName = item.SubscriberName,
                Amount = item.Amount,
                Description = item.Description,
                IsAutomatic = false
            }).ToList();
            return View(nameof(Index), model);
        }

        var requestedStatus = status.GetValueOrDefault();
        try
        {
            var items = input.Items
                .Where(value => !string.IsNullOrWhiteSpace(value.SubscriberName))
                .Select(value => new CashHandoverItemInput(
                    value.SubscriberName.Trim(),
                    value.Amount,
                    value.Description?.Trim()))
                .ToArray();

            await cashHandoverService.SaveDailyAsync(
                input.Date,
                new CashHandoverUpdate(items, requestedStatus),
                cancellationToken);
            TempData["Notice"] = requestedStatus == CashHandoverStatus.Delivered
                ? "Günlük kasa teslim edildi."
                : "Günlük kasa taslağı kaydedildi.";
        }
        catch (DomainValidationException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { date = input.Date.ToString("yyyy-MM-dd") });
    }

    private async Task<CashHandoverPageViewModel> BuildPageAsync(
        DateOnly selectedDate,
        CancellationToken cancellationToken)
    {
        var daily = await cashHandoverService.GetDailyAsync(
            selectedDate,
            cancellationToken);
        var monthly = await cashHandoverService.GetMonthlyAsync(
            selectedDate.Year,
            selectedDate.Month,
            cancellationToken);
        var subscribers = await dbContext.Subscribers
            .AsNoTracking()
            .Where(value => value.IsActive)
            .OrderBy(value => value.Name)
            .Select(value => value.Name)
            .ToListAsync(cancellationToken);

        return new CashHandoverPageViewModel
        {
            Date = selectedDate,
            Month = selectedDate.ToString("yyyy-MM"),
            Status = daily.Status == CashHandoverStatus.Delivered
                ? "Teslim Edildi"
                : "Taslak",
            DeliveredAt = daily.DeliveredAt,
            MonthlyDeliveredTotal = monthly.Total,
            MonthlyDeliveredDayCount = monthly.Records.Count,
            SubscriberSuggestions = subscribers,
            AutomaticItems = daily.AutomaticItems.Select(ToItemViewModel).ToList(),
            ManualItems = daily.ManualItems.Select(ToItemViewModel).ToList()
        };
    }

    private static CashHandoverItemViewModel ToItemViewModel(CashHandoverLine line) =>
        new()
        {
            SubscriberName = line.SubscriberName,
            Amount = line.Amount,
            Description = line.Description,
            IsAutomatic = line.IsAutomatic,
            PaymentMethod = line.PaymentMethod switch
            {
                SubscriberPaymentMethod.Cash => "Nakit",
                SubscriberPaymentMethod.Card => "Kart",
                SubscriberPaymentMethod.Transfer => "Havale/EFT",
                _ => ""
            }
        };
}
