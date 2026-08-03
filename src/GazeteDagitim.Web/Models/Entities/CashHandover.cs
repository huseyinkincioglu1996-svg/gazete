using System.ComponentModel.DataAnnotations;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class CashHandover : EntityBase
{
    public DateOnly Date { get; set; }

    public decimal Total { get; set; }

    public CashHandoverStatus Status { get; set; } = CashHandoverStatus.Draft;

    public DateTimeOffset? DeliveredAt { get; set; }

    public ICollection<CashHandoverItem> Items { get; set; } = [];
}

public sealed class CashHandoverItem
{
    public int Id { get; set; }

    public int CashHandoverId { get; set; }

    public CashHandover CashHandover { get; set; } = null!;

    [Required, StringLength(200)]
    public string SubscriberName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
}
