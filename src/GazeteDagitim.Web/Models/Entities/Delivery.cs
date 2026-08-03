using System.ComponentModel.DataAnnotations;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class Delivery : EntityBase
{
    public int DistributorId { get; set; }

    public Distributor Distributor { get; set; } = null!;

    public DateOnly Date { get; set; }

    public BusinessDay Day { get; set; }

    public int NewspaperCount { get; set; }

    public decimal Amount { get; set; }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;
}
