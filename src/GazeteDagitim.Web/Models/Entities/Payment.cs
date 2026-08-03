using System.ComponentModel.DataAnnotations;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class Payment : EntityBase
{
    public int DistributorId { get; set; }

    public Distributor Distributor { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public PaymentType PaymentType { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTimeOffset? PaidAt { get; set; }
}
