using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Controllers;

[Route("payments")]
public sealed class PaymentsController(
    AppDbContext dbContext,
    IPaymentTrackingService paymentTrackingService,
    IBusinessClock clock) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? month,
        int? distributorId,
        CancellationToken cancellationToken)
    {
        var (year, monthNumber, monthKey) = ParseMonth(month, clock.Today);
        var distributors = await dbContext.Distributors
            .AsNoTracking()
            .OrderByDescending(value => value.IsActive)
            .ThenBy(value => value.Name)
            .Select(value => new LookupOptionViewModel
            {
                Id = value.Id,
                Name = value.Name,
                IsActive = value.IsActive
            })
            .ToListAsync(cancellationToken);

        if (distributorId.HasValue &&
            distributors.All(value => value.Id != distributorId.Value))
        {
            return NotFound();
        }

        var result = await paymentTrackingService.GetMonthlyAsync(
            year,
            monthNumber,
            distributorId,
            cancellationToken);

        var model = new PaymentsPageViewModel
        {
            Month = monthKey,
            DistributorId = distributorId,
            Distributors = distributors,
            CashCollectedTotal = result.Summary.CashCollectionTotal,
            PaymentTotal = result.Summary.DistributorPaymentTotal,
            PaidTotal = result.Summary.PaidTotal,
            PendingTotal = result.Summary.PendingTotal,
            CashCollections = result.CashCollections.Select(item => new CashCollectionViewModel
            {
                Id = item.Id,
                SubscriberName = item.SubscriberName,
                DistributorName = string.IsNullOrWhiteSpace(item.DistributorName)
                    ? "Atanmamış"
                    : item.DistributorName,
                Date = item.Date,
                Amount = item.Amount,
                PaymentMethod = ToDisplayName(item.PaymentMethod),
                IsCashSale = item.IsCashSale,
                Description = item.Description
            }).ToList(),
            Payments = result.Payments.Select(ToPaymentViewModel).ToList()
        };

        return View(model);
    }

    [HttpPost("mark-paid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(
        int id,
        string? month,
        int? distributorId,
        CancellationToken cancellationToken)
    {
        if (distributorId.HasValue &&
            !await dbContext.Distributors
                .AsNoTracking()
                .AnyAsync(
                    value => value.Id == distributorId.Value,
                    cancellationToken))
        {
            return NotFound();
        }

        var payment = await dbContext.Payments.FindAsync([id], cancellationToken);
        if (payment is null)
        {
            TempData["Error"] = "Ödeme kaydı bulunamadı.";
        }
        else if (distributorId.HasValue &&
                 payment.DistributorId != distributorId.Value)
        {
            return BadRequest();
        }
        else if (payment.Status == PaymentStatus.Paid)
        {
            TempData["Notice"] = "Bu ödeme daha önce tamamlanmış.";
        }
        else
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            TempData["Notice"] = "Dağıtıcı ödemesi tamamlandı olarak işaretlendi.";
        }

        return RedirectToAction(nameof(Index), new { month, distributorId });
    }

    private static DistributorPaymentViewModel ToPaymentViewModel(
        PaymentTrackingPaymentRow payment) =>
        new()
        {
            Id = payment.Id,
            DistributorName = payment.DistributorName,
            Amount = payment.Amount,
            PeriodStart = payment.PeriodStart,
            PeriodEnd = payment.PeriodEnd,
            DueDate = payment.Date,
            PaymentType = ToDisplayName(payment.PaymentType),
            IsPaid = payment.Status == PaymentStatus.Paid
        };

    private static (int Year, int Month, string Key) ParseMonth(
        string? value,
        DateOnly fallback)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            DateOnly.TryParseExact(
                $"{value}-01",
                "yyyy-MM-dd",
                out var parsed))
        {
            return (parsed.Year, parsed.Month, value);
        }

        return (fallback.Year, fallback.Month, fallback.ToString("yyyy-MM"));
    }

    private static string ToDisplayName(SubscriberPaymentMethod value) =>
        value switch
        {
            SubscriberPaymentMethod.Cash => "Nakit",
            SubscriberPaymentMethod.Card => "Kart",
            SubscriberPaymentMethod.Transfer => "Havale/EFT",
            _ => "Nakit"
        };

    private static string ToDisplayName(PaymentType value) =>
        value switch
        {
            PaymentType.Daily => "Günlük",
            PaymentType.Weekly => "Haftalık",
            PaymentType.Monthly => "Aylık",
            _ => value.ToString()
        };
}
