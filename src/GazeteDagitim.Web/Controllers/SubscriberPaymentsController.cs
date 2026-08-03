using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels.Subscribers;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GazeteDagitim.Web.Controllers;

[Route("subscribers/{subscriberId:int}/payments")]
public sealed class SubscriberPaymentsController(
    ISubscriberPaymentDetailsService paymentDetailsService)
    : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Details(
        int subscriberId,
        CancellationToken cancellationToken)
    {
        try
        {
            var details = await paymentDetailsService.GetAsync(
                subscriberId,
                cancellationToken);
            return View(Map(details));
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("defer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Defer(
        int subscriberId,
        [Bind(Prefix = nameof(SubscriberPaymentDetailsPageViewModel.DeferralInput))]
        SubscriberPaymentDeferralInputModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid ||
            !model.OriginalDueDate.HasValue ||
            !model.DeferredUntil.HasValue)
        {
            return await RenderDetailsAsync(
                subscriberId,
                model,
                cancellationToken);
        }

        try
        {
            await paymentDetailsService.DeferAsync(
                subscriberId,
                model.OriginalDueDate.Value,
                model.DeferredUntil.Value,
                model.Reason,
                cancellationToken);
            TempData["Notice"] = "Ödeme tarihi ertelendi.";
            return RedirectToAction(nameof(Details), new { subscriberId });
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (DomainValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await RenderDetailsAsync(
                subscriberId,
                model,
                cancellationToken);
        }
    }

    [HttpPost("deferrals/{deferralId:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelDeferral(
        int subscriberId,
        int deferralId,
        CancellationToken cancellationToken)
    {
        try
        {
            await paymentDetailsService.CancelDeferralAsync(
                subscriberId,
                deferralId,
                cancellationToken);
            TempData["Notice"] = "Ödeme ertelemesi geri alındı.";
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (DomainValidationException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { subscriberId });
    }

    private async Task<IActionResult> RenderDetailsAsync(
        int subscriberId,
        SubscriberPaymentDeferralInputModel input,
        CancellationToken cancellationToken)
    {
        try
        {
            var details = await paymentDetailsService.GetAsync(
                subscriberId,
                cancellationToken);
            return View(nameof(Details), Map(details, input));
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    private static SubscriberPaymentDetailsPageViewModel Map(
        SubscriberPaymentDetailsResult details,
        SubscriberPaymentDeferralInputModel? input = null)
    {
        var plan = details.Plan;
        var nextDue = details.NextDue;
        var activeDeferral = details.ActiveDeferral;

        return new SubscriberPaymentDetailsPageViewModel
        {
            SubscriberId = details.SubscriberId,
            SubscriberName = details.SubscriberName,
            Phone = details.Phone,
            Address = details.Address,
            DistributorName = details.DistributorName,
            IsActive = details.IsActive,
            HasPaymentPlan = plan is not null,
            PaymentPlanName = plan?.Name ?? "Ödeme planı tanımlanmamış",
            PaymentScheduleLabel = plan?.ScheduleLabel ?? "Ödeme günü belirlenmemiş",
            PlanStartedOn = plan?.StartedOn,
            CoveredDayCount = plan?.CoveredDayCount,
            ScheduledAmount = plan?.Amount,
            ScheduledTime = plan?.CollectionTime,
            NextOriginalDueDate = nextDue?.OriginalDueDate,
            NextEffectiveDueDate = nextDue?.EffectiveDueDate,
            EarliestDeferralDate = details.EarliestDeferralDate,
            LatestDeferralDate = details.LatestDeferralDate,
            ActiveDeferralId = activeDeferral?.Id,
            ActiveDeferralReason = activeDeferral?.Reason ?? string.Empty,
            ExpectedTotal = details.ExpectedTotal,
            CollectedTotal = details.CollectedTotal,
            OutstandingBalance = details.OutstandingBalance,
            AdvanceBalance = details.AdvanceBalance,
            OverdueBalance = details.OverdueBalance,
            Collections = details.Collections
                .Select(value => new SubscriberCollectionHistoryItemViewModel
                {
                    Date = value.Date,
                    Time = value.Time,
                    Amount = value.Amount,
                    PaymentMethod = PaymentMethodLabel(value.PaymentMethod),
                    DistributorName = value.DistributorName,
                    PaymentPeriodName = value.PaymentPeriodName,
                    CoveredDayCount = value.CoveredDayCount,
                    IsLegacyTimestamp = value.IsLegacyTimestamp
                })
                .ToArray(),
            Deferrals = details.Deferrals
                .Select(value => new SubscriberPaymentDeferralHistoryItemViewModel
                {
                    Id = value.Id,
                    OriginalDueDate = value.OriginalDueDate,
                    PreviousDueDate = value.PreviousDueDate,
                    DeferredUntil = value.DeferredUntil,
                    Reason = value.Reason,
                    CreatedAt = value.CreatedAt,
                    CancelledAt = value.CancelledAt
                })
                .ToArray(),
            Movements = details.Movements
                .Select(MapMovement)
                .ToArray(),
            DeferralInput = input ?? new SubscriberPaymentDeferralInputModel
            {
                OriginalDueDate = nextDue?.OriginalDueDate
            }
        };
    }

    private static SubscriberPaymentMovementViewModel MapMovement(
        SubscriberPaymentMovementRow movement)
    {
        var kind = movement.Type switch
        {
            SubscriberPaymentMovementType.Collection => "collection",
            SubscriberPaymentMovementType.Deferral => "deferral",
            SubscriberPaymentMovementType.DeferralCancellation => "cancellation",
            _ => "due"
        };
        var cssClass = movement.Type == SubscriberPaymentMovementType.Due &&
                       movement.Status == "Gecikmiş"
            ? "overdue"
            : kind;

        return new SubscriberPaymentMovementViewModel
        {
            Date = movement.Date,
            Time = movement.Time,
            Kind = kind,
            Title = movement.Title,
            Description = movement.Description,
            Amount = movement.Amount,
            ReducesBalance = movement.ReducesBalance,
            Status = movement.Status,
            CssClass = cssClass
        };
    }

    private static string PaymentMethodLabel(
        SubscriberPaymentMethod paymentMethod) =>
        paymentMethod switch
        {
            SubscriberPaymentMethod.Cash => "Nakit",
            SubscriberPaymentMethod.Card => "Kart",
            SubscriberPaymentMethod.Transfer => "Havale/EFT",
            _ => "Bilinmeyen yöntem"
        };
}
