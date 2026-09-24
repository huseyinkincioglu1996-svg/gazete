namespace GazeteDagitim.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

public sealed class FormExpiredViewModel
{
    public string ReturnUrl { get; init; } = "/menu";
}
