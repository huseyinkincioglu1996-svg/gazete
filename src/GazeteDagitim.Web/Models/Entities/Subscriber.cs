using System.ComponentModel.DataAnnotations;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class Subscriber : EntityBase
{
    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(500)]
    public string Address { get; set; } = string.Empty;

    public decimal MonthlyFee { get; set; }

    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? DeactivatedAt { get; set; }

    public int? PaymentPeriodId { get; set; }

    public PaymentPeriod? PaymentPeriod { get; set; }

    public DateOnly? PaymentPeriodStartedOn { get; set; }

    public int? DistributorId { get; set; }

    public Distributor? Distributor { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public ICollection<SubscriberPublicationDay> NewspaperDays { get; set; } = [];

    public ICollection<SubscriberDailyDelivery> DailyDeliveries { get; set; } = [];

    public ICollection<SubscriberPaymentDeferral> PaymentDeferrals { get; set; } = [];
}

public sealed class SubscriberPublicationDay
{
    public int SubscriberId { get; set; }

    public Subscriber Subscriber { get; set; } = null!;

    public NewspaperDay Day { get; set; }
}
