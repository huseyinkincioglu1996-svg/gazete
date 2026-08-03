using System.ComponentModel.DataAnnotations;

namespace GazeteDagitim.Web.Models.Entities;

public sealed class CompanySettings : EntityBase
{
    [Required, StringLength(32)]
    public string SingletonKey { get; set; } = "company";

    public string? LogoDataUrl { get; set; }

    public decimal? NewspaperUnitPrice { get; set; }

    public int? FeaturedDistributorId { get; set; }

    public Distributor? FeaturedDistributor { get; set; }
}
