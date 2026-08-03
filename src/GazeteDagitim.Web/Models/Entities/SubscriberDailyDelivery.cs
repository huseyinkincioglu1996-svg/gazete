using System.ComponentModel.DataAnnotations;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class SubscriberDailyDelivery : EntityBase
{
    public int SubscriberId { get; set; }

    public Subscriber Subscriber { get; set; } = null!;

    public int? DistributorId { get; set; }

    public Distributor? Distributor { get; set; }

    [StringLength(120)]
    public string DistributorName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public int NewspaperCount { get; set; } = 1;

    public bool IsDelivered { get; set; }

    public bool IsCollected { get; set; }

    public DateTimeOffset? CollectedAt { get; set; }

    public decimal Amount { get; set; }

    public SubscriberPaymentMethod PaymentMethod { get; set; } = SubscriberPaymentMethod.Cash;

    [StringLength(120)]
    public string CollectionPeriodName { get; set; } = string.Empty;

    public int? CollectionDayCount { get; set; }

    public ICollection<SubscriberDailyDeliveryCoveredDate> CoveredDates { get; set; } = [];
}

public sealed class SubscriberDailyDeliveryCoveredDate
{
    public int SubscriberDailyDeliveryId { get; set; }

    public SubscriberDailyDelivery SubscriberDailyDelivery { get; set; } = null!;

    public DateOnly CoveredDate { get; set; }
}
