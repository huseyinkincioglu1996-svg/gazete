using System.ComponentModel.DataAnnotations;

namespace GazeteDagitim.Web.Models.ViewModels.Subscribers;

public sealed class SubscriberPaymentDetailsPageViewModel
{
    public int SubscriberId { get; init; }
    public string SubscriberName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string DistributorName { get; init; } = "Dağıtıcı atanmamış";
    public bool IsActive { get; init; }

    public bool HasPaymentPlan { get; init; }
    public string PaymentPlanName { get; init; } = "Ödeme planı tanımlanmamış";
    public string PaymentScheduleLabel { get; init; } = "Ödeme günü belirlenmemiş";
    public DateOnly? PlanStartedOn { get; init; }
    public int? CoveredDayCount { get; init; }
    public decimal? ScheduledAmount { get; init; }
    public TimeOnly? ScheduledTime { get; init; }

    public DateOnly? NextOriginalDueDate { get; init; }
    public DateOnly? NextEffectiveDueDate { get; init; }
    public DateOnly EarliestDeferralDate { get; init; }
    public DateOnly LatestDeferralDate { get; init; }
    public int? ActiveDeferralId { get; init; }
    public string ActiveDeferralReason { get; init; } = string.Empty;

    public decimal ExpectedTotal { get; init; }
    public decimal CollectedTotal { get; init; }
    public decimal OutstandingBalance { get; init; }
    public decimal AdvanceBalance { get; init; }
    public decimal OverdueBalance { get; init; }

    public IReadOnlyList<SubscriberCollectionHistoryItemViewModel> Collections { get; init; } = [];
    public IReadOnlyList<SubscriberPaymentDeferralHistoryItemViewModel> Deferrals { get; init; } = [];
    public IReadOnlyList<SubscriberPaymentMovementViewModel> Movements { get; init; } = [];

    public SubscriberPaymentDeferralInputModel DeferralInput { get; set; } = new();

    public bool HasActiveDeferral =>
        ActiveDeferralId.HasValue &&
        NextOriginalDueDate.HasValue &&
        NextEffectiveDueDate.HasValue &&
        NextEffectiveDueDate != NextOriginalDueDate;

    public int ActiveDeferralCount => Deferrals.Count(value => value.IsActive);

    public bool HasOutstandingBalance => OutstandingBalance > 0;
}

public sealed class SubscriberCollectionHistoryItemViewModel
{
    public DateOnly Date { get; init; }
    public TimeOnly? Time { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string DistributorName { get; init; } = "Dağıtıcı atanmamış";
    public string PaymentPeriodName { get; init; } = string.Empty;
    public int? CoveredDayCount { get; init; }
    public bool IsLegacyTimestamp { get; init; }
}

public sealed class SubscriberPaymentDeferralHistoryItemViewModel
{
    public int Id { get; init; }
    public DateOnly OriginalDueDate { get; init; }
    public DateOnly PreviousDueDate { get; init; }
    public DateOnly DeferredUntil { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public bool IsActive => CancelledAt is null;
}

public sealed class SubscriberPaymentMovementViewModel
{
    public DateOnly Date { get; init; }
    public TimeOnly? Time { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public bool ReducesBalance { get; init; }
    public string Status { get; init; } = string.Empty;
    public string CssClass { get; init; } = string.Empty;
}

public sealed class SubscriberPaymentDeferralInputModel : IValidatableObject
{
    [Required(ErrorMessage = "Ertelenecek ödeme günü bulunamadı.")]
    public DateOnly? OriginalDueDate { get; set; }

    [Required(ErrorMessage = "Yeni ödeme tarihi zorunludur.")]
    [Display(Name = "Yeni ödeme tarihi")]
    public DateOnly? DeferredUntil { get; set; }

    [StringLength(500, ErrorMessage = "Erteleme nedeni en fazla 500 karakter olabilir.")]
    [Display(Name = "Erteleme nedeni")]
    public string? Reason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OriginalDueDate.HasValue &&
            DeferredUntil.HasValue &&
            DeferredUntil <= OriginalDueDate)
        {
            yield return new ValidationResult(
                "Yeni ödeme tarihi mevcut ödeme gününden sonra olmalıdır.",
                [nameof(DeferredUntil)]);
        }
    }
}
