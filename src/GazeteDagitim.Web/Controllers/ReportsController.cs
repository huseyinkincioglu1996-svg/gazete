using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Controllers;

[Route("reports")]
public sealed class ReportsController(
    AppDbContext dbContext,
    IReportService reportService,
    IBusinessClock clock) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var selectedDate = date ?? clock.Today;
        var monthStart = new DateOnly(
            selectedDate.Year,
            selectedDate.Month,
            1);
        var monthEnd = new DateOnly(
            selectedDate.Year,
            selectedDate.Month,
            DateTime.DaysInMonth(selectedDate.Year, selectedDate.Month));
        var deliveries = await dbContext.Deliveries
            .AsNoTracking()
            .Include(value => value.Distributor)
            .Where(value => value.Date == selectedDate)
            .OrderBy(value => value.Distributor.Name)
            .ToListAsync(cancellationToken);
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Include(value => value.Distributor)
            .Where(value => value.Date == selectedDate)
            .OrderBy(value => value.Distributor.Name)
            .ToListAsync(cancellationToken);
        var summary = await reportService.GetSummaryAsync(
            selectedDate,
            selectedDate,
            cancellationToken: cancellationToken);
        var subscriberCollection =
            await reportService.GetSubscriberCollectionSummaryAsync(
                monthStart,
                monthEnd,
                cancellationToken);

        var model = new ReportsPageViewModel
        {
            Date = selectedDate,
            NewspaperTotal = summary.TotalNewspapers,
            CompletedDeliveryCount = deliveries.Count(
                value => value.Status == DeliveryStatus.Completed),
            PendingDeliveryCount = deliveries.Count(
                value => value.Status == DeliveryStatus.Pending),
            PaymentTotal = summary.TotalAmount,
            PendingPaymentTotal = summary.PendingAmount,
            CollectionRate = summary.CollectionRate,
            SubscriberDueTotal = subscriberCollection.DueTotal,
            SubscriberCollectedTotal = subscriberCollection.CollectedTotal,
            Deliveries = deliveries.Select(value => new ReportDeliveryViewModel
            {
                DistributorName = value.Distributor?.Name ?? "Silinmiş dağıtıcı",
                NewspaperCount = value.NewspaperCount,
                Amount = value.Amount,
                Status = value.Status switch
                {
                    DeliveryStatus.Completed => "Tamamlandı",
                    DeliveryStatus.Cancelled => "İptal",
                    _ => "Beklemede"
                }
            }).ToList(),
            Payments = payments.Select(value => new DistributorPaymentViewModel
            {
                Id = value.Id,
                DistributorName = value.Distributor?.Name ?? "Silinmiş dağıtıcı",
                Amount = value.Amount,
                PeriodStart = value.PeriodStart,
                PeriodEnd = value.PeriodEnd,
                DueDate = value.Date,
                PaymentType = value.PaymentType switch
                {
                    PaymentType.Daily => "Günlük",
                    PaymentType.Weekly => "Haftalık",
                    PaymentType.Monthly => "Aylık",
                    _ => value.PaymentType.ToString()
                },
                IsPaid = value.Status == PaymentStatus.Paid
            }).ToList()
        };

        return View(model);
    }
}
