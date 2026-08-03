using System.ComponentModel.DataAnnotations;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class NewspaperCashSale : EntityBase
{
    public DateOnly Date { get; set; }

    public int DistributorId { get; set; }

    public Distributor Distributor { get; set; } = null!;

    [Required, StringLength(120)]
    public string DistributorName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public Guid IdempotencyKey { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
}
