using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Controllers;

[Route("deliveries")]
public sealed class DeliveriesController(
    ISubscriberDeliveryService deliveryService,
    ICashHandoverService cashHandoverService,
    INewspaperCashSaleService cashSaleService,
    IBusinessClock clock,
    AppDbContext dbContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var selectedDate = date ?? clock.Today;
        return View(await BuildPageAsync(selectedDate, cancellationToken));
    }

    [HttpGet("delivered")]
    public async Task<IActionResult> Delivered(
        DateOnly? date,
        string? list,
        CancellationToken cancellationToken)
    {
        var selectedDate = date ?? clock.Today;
        var result = await deliveryService.GetDailyAsync(
            selectedDate,
            cancellationToken);
        var rows = MapDeliveryRows(result.Records);
        var showAllSubscribers = string.Equals(
            list,
            "all",
            StringComparison.OrdinalIgnoreCase);

        return View(new DeliveredSubscribersPageViewModel
        {
            Date = selectedDate,
            ShowAllSubscribers = showAllSubscribers,
            DeliveredCount = rows.Count(value => value.Delivered),
            SubscriberCount = rows.Count,
            Rows = showAllSubscribers
                ? rows
                : rows.Where(value => value.Delivered).ToList()
        });
    }

    [HttpGet("collections")]
    public async Task<IActionResult> Collections(
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var selectedDate = date ?? clock.Today;
        var result = await deliveryService.GetDailyAsync(
            selectedDate,
            cancellationToken);
        var cashSales = await cashSaleService.GetDailyAsync(
            selectedDate,
            cancellationToken);
        var isCashLocked = await cashHandoverService.IsClosedAsync(
            selectedDate,
            cancellationToken);

        return View(new DailyCollectionsPageViewModel
        {
            Date = selectedDate,
            IsCashLocked = isCashLocked,
            Rows = MapDeliveryRows(result.Records)
                .Where(value => value.ShowPaymentControls)
                .ToList(),
            CashSales = MapCashSales(cashSales.Records)
        });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        DailyDeliveriesInputModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var selectedDate = input.Date == default ? clock.Today : input.Date;
            var model = await BuildPageAsync(selectedDate, cancellationToken);
            OverlayPostedRows(model, input.Rows);
            return View(nameof(Index), model);
        }

        try
        {
            var updates = input.Rows.Select(row =>
            {
                if (row.Collected && row.Amount <= 0)
                {
                    throw new DomainValidationException(
                        "Ödeme alınan satırlarda tutar sıfırdan büyük olmalıdır.");
                }

                return new SubscriberDeliveryUpdate(
                    row.SubscriberId,
                    row.Delivered,
                    row.Collected,
                    row.Amount,
                    ParsePaymentMethod(row.PaymentMethod));
            }).ToArray();

            await deliveryService.SaveDailyAsync(input.Date, updates, cancellationToken);
            TempData["Notice"] = "Günlük dağıtım ve tahsilat kayıtları kaydedildi.";
        }
        catch (DomainValidationException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { date = input.Date.ToString("yyyy-MM-dd") });
    }

    [HttpPost("save-row")]
    [ValidateAntiForgeryToken]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<IActionResult> SaveRow(
        [FromForm] DailyDeliveryRowAutosaveInputModel input,
        CancellationToken cancellationToken)
    {
        if (input.Date == default)
        {
            ModelState.AddModelError(
                nameof(input.Date),
                "Geçerli bir dağıtım tarihi seçilmelidir.");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(CreateAutosaveError(GetFirstModelStateError()));
        }

        try
        {
            SubscriberPaymentMethod? paymentMethod = input.PaymentMethod is null
                ? null
                : ParsePaymentMethod(input.PaymentMethod);
            var result = await deliveryService.SaveDailyRowAsync(
                input.Date,
                new SubscriberDeliveryPatch(
                    input.SubscriberId,
                    input.Delivered,
                    input.Collected,
                    input.Amount,
                    paymentMethod),
                cancellationToken);
            var persistedRow = result.Records.Single(
                value => value.SubscriberId == input.SubscriberId);

            return Ok(new DailyDeliveryRowAutosaveResponseModel
            {
                Success = true,
                Message = "Değişiklik otomatik olarak kaydedildi.",
                Row = new DailyDeliveryRowAutosaveStateViewModel
                {
                    SubscriberId = persistedRow.SubscriberId,
                    Delivered = persistedRow.IsDelivered,
                    Collected = persistedRow.IsCollected,
                    Amount = persistedRow.Amount,
                    PaymentMethod = ToDisplayName(persistedRow.PaymentMethod)
                },
                Summary = new DailyDeliveryAutosaveSummaryViewModel
                {
                    DeliveredCount = result.Records.Count(value => value.IsDelivered),
                    CollectedCount = result.Records.Count(value => value.IsCollected),
                    CollectedTotal = result.Records
                        .Where(value => value.IsCollected)
                        .Sum(value => value.Amount)
                }
            });
        }
        catch (EntityNotFoundException exception)
        {
            return NotFound(CreateAutosaveError(exception.Message));
        }
        catch (DomainConflictException exception)
        {
            return Conflict(CreateAutosaveError(exception.Message));
        }
        catch (DomainValidationException exception)
        {
            return BadRequest(CreateAutosaveError(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(CreateAutosaveError(
                "Kayıt başka bir işlem tarafından değiştirildi. Lütfen yeniden deneyin."));
        }
        catch (DbUpdateException)
        {
            return Conflict(CreateAutosaveError(
                "Değişiklik aynı anda yapılan başka bir işlemle çakıştı. Lütfen yeniden deneyin."));
        }
    }

    [HttpPost("cash-sale")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCashSale(
        DailyNewspaperCashSaleInputModel input,
        CancellationToken cancellationToken)
    {
        var selectedDate = input.Date == default ? clock.Today : input.Date;
        if (input.IdempotencyKey == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(input.IdempotencyKey),
                "Nakit satış işlem anahtarı geçersizdir.");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = GetFirstModelStateError();
            return RedirectToAction(
                nameof(Index),
                new { date = selectedDate.ToString("yyyy-MM-dd") });
        }

        try
        {
            var sale = await cashSaleService.CreateAsync(
                input.Date,
                input.DistributorId,
                input.Quantity,
                input.IdempotencyKey,
                cancellationToken);
            TempData["Notice"] =
                $"{sale.Quantity} adet gazete, {sale.Amount:0.00} ₺ olarak tahsilata eklendi.";
        }
        catch (EntityNotFoundException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (DomainConflictException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (DomainValidationException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(
            nameof(Index),
            new { date = selectedDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost("cash-sale/{id:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelCashSale(
        int id,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var selectedDate = date ?? clock.Today;
        try
        {
            var sale = await cashSaleService.CancelAsync(id, cancellationToken);
            selectedDate = sale.Date;
            TempData["Notice"] = "Nakit satış tahsilattan geri alındı.";
        }
        catch (EntityNotFoundException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (DomainConflictException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (DomainValidationException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(
            nameof(Index),
            new { date = selectedDate.ToString("yyyy-MM-dd") });
    }

    private async Task<DailyDeliveriesPageViewModel> BuildPageAsync(
        DateOnly selectedDate,
        CancellationToken cancellationToken)
    {
        var result = await deliveryService.GetDailyAsync(selectedDate, cancellationToken);
        var cashSales = await cashSaleService.GetDailyAsync(
            selectedDate,
            cancellationToken);
        var isCashLocked = await cashHandoverService.IsClosedAsync(
            selectedDate,
            cancellationToken);
        var showDistributorAndCoverage = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(value => value.SingletonKey == "company")
            .Select(value => (bool?)value.ShowDistributorAndCoverage)
            .SingleOrDefaultAsync(cancellationToken) ?? true;
        return new DailyDeliveriesPageViewModel
        {
            Date = selectedDate,
            IsCashLocked = isCashLocked,
            ShowDistributorAndCoverage = showDistributorAndCoverage,
            CashSaleDistributors = cashSales.Distributors.Select(value =>
                new NewspaperCashSaleDistributorViewModel
                {
                    Id = value.Id,
                    Name = value.Name,
                    UnitPrice = value.UnitPrice
                }).ToList(),
            CashSales = MapCashSales(cashSales.Records),
            Rows = MapDeliveryRows(result.Records)
        };
    }

    private static List<DailyDeliveryRowViewModel> MapDeliveryRows(
        IEnumerable<DailySubscriberDeliveryRow> records) =>
        records.Select(record => new DailyDeliveryRowViewModel
        {
            SubscriberId = record.SubscriberId,
            SubscriberName = record.SubscriberName,
            DistributorName = string.IsNullOrWhiteSpace(record.DistributorName)
                    ? "Atanmamış"
                    : record.DistributorName,
            HasDelivery = record.HasDelivery,
            IsScheduled = record.IsScheduled,
            NewspaperCount = record.NewspaperCount,
            CoverageLabel = string.Join(
                    " + ",
                    record.CoveredDates.Select(value => value.ToString("dd.MM.yyyy"))),
            Delivered = record.IsDelivered,
            Collected = record.IsCollected,
            IsPaymentDue = record.IsPaymentDue,
            Amount = record.Amount,
            PaymentMethod = ToDisplayName(record.PaymentMethod)
        })
            .ToList();

    private static List<NewspaperCashSaleViewModel> MapCashSales(
        IEnumerable<NewspaperCashSaleRow> records) =>
        records.Select(value => new NewspaperCashSaleViewModel
        {
            Id = value.Id,
            DistributorName = value.DistributorName,
            Quantity = value.Quantity,
            UnitPrice = value.UnitPrice,
            Amount = value.Amount,
            CreatedAt = value.CreatedAt
        })
            .ToList();

    private static void OverlayPostedRows(
        DailyDeliveriesPageViewModel model,
        IEnumerable<DailyDeliveryRowInputModel> postedRows)
    {
        var postedBySubscriber = postedRows
            .GroupBy(row => row.SubscriberId)
            .ToDictionary(group => group.Key, group => group.Last());

        foreach (var row in model.Rows)
        {
            if (!postedBySubscriber.TryGetValue(row.SubscriberId, out var posted))
            {
                continue;
            }

            row.Delivered = posted.Delivered;
            row.Collected = posted.Collected;
            row.Amount = posted.Amount;
            if (!string.IsNullOrWhiteSpace(posted.PaymentMethod))
            {
                row.PaymentMethod = posted.PaymentMethod;
            }
        }
    }

    private static SubscriberPaymentMethod ParsePaymentMethod(string? value) =>
        value switch
        {
            "Nakit" => SubscriberPaymentMethod.Cash,
            "Kart" => SubscriberPaymentMethod.Card,
            "Havale/EFT" => SubscriberPaymentMethod.Transfer,
            _ => throw new DomainValidationException("Geçerli bir ödeme yöntemi seçilmelidir.")
        };

    private static string ToDisplayName(SubscriberPaymentMethod value) =>
        value switch
        {
            SubscriberPaymentMethod.Cash => "Nakit",
            SubscriberPaymentMethod.Card => "Kart",
            SubscriberPaymentMethod.Transfer => "Havale/EFT",
            _ => "Nakit"
        };

    private string GetFirstModelStateError() =>
        ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ??
        "Gönderilen satır bilgileri geçersizdir.";

    private static DailyDeliveryRowAutosaveResponseModel CreateAutosaveError(
        string message) =>
        new()
        {
            Success = false,
            Message = message
        };
}
