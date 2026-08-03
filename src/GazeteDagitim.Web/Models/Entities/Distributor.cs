using System.ComponentModel.DataAnnotations;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class Distributor : EntityBase
{
    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Phone { get; set; } = string.Empty;

    public string? ProfileImageDataUrl { get; set; }

    public DistributorZone Zone { get; set; }

    public PaymentType PaymentType { get; set; } = PaymentType.Daily;

    public decimal NewspaperPrice { get; set; } = 5m;

    public bool IsActive { get; set; } = true;

    public ICollection<DistributorDistributionDay> DistributionDays { get; set; } = [];

    public ICollection<DistributorWeeklyPaymentDay> WeeklyPaymentDays { get; set; } = [];

    public ICollection<DistributorMonthlyPaymentDay> MonthlyPaymentDays { get; set; } = [];

    public ICollection<Delivery> Deliveries { get; set; } = [];

    public ICollection<Payment> Payments { get; set; } = [];

    public ICollection<Subscriber> Subscribers { get; set; } = [];

    public ICollection<SubscriberDailyDelivery> SubscriberDailyDeliveries { get; set; } = [];

    public ICollection<NewspaperCashSale> NewspaperCashSales { get; set; } = [];
}

public sealed class DistributorDistributionDay
{
    public int DistributorId { get; set; }

    public Distributor Distributor { get; set; } = null!;

    public BusinessDay Day { get; set; }
}

public sealed class DistributorWeeklyPaymentDay
{
    public int DistributorId { get; set; }

    public Distributor Distributor { get; set; } = null!;

    public BusinessDay Day { get; set; }
}

public sealed class DistributorMonthlyPaymentDay
{
    public int DistributorId { get; set; }

    public Distributor Distributor { get; set; } = null!;

    public int DayOfMonth { get; set; }
}
