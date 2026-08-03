using System.ComponentModel.DataAnnotations;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class PaymentPeriod : EntityBase
{
    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 365)]
    public int DayCount { get; set; }

    public PaymentPeriodFrequency Frequency { get; set; } =
        PaymentPeriodFrequency.Monthly;

    public int? CollectionDayOfMonth { get; set; }

    public TimeOnly? CollectionTime { get; set; }

    public decimal? CollectionAmount { get; set; }

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Subscriber> Subscribers { get; set; } = [];
}
