using System.ComponentModel.DataAnnotations;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class SubscriberPaymentDeferral : EntityBase
{
    public int SubscriberId { get; set; }

    public Subscriber Subscriber { get; set; } = null!;

    public DateOnly OriginalDueDate { get; set; }

    public DateOnly PreviousDueDate { get; set; }

    public DateOnly DeferredUntil { get; set; }

    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset? CancelledAt { get; set; }
}
