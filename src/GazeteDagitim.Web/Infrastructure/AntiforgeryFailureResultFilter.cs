using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GazeteDagitim.Web.Infrastructure;

/// <summary>
/// Turns an expired form token into a recoverable response without replaying
/// the rejected write operation.
/// </summary>
public sealed class AntiforgeryFailureResultFilter : IAlwaysRunResultFilter
{
    private const string ExpiredFormMessage =
        "Formun güvenlik süresi doldu. Sayfayı yenileyip işlemi yeniden deneyin.";

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not AntiforgeryValidationFailedResult)
        {
            return;
        }

        var httpContext = context.HttpContext;
        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        httpContext.Response.Headers.Pragma = "no-cache";

        var logger = httpContext.RequestServices
            .GetRequiredService<ILogger<AntiforgeryFailureResultFilter>>();
        logger.LogWarning(
            "Antiforgery validation failed for {Method} {Path}. Trace identifier: {TraceIdentifier}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        if (ExpectsHtml(httpContext.Request))
        {
            var returnUrl = GetSameHostReturnUrl(httpContext.Request);
            var destination = "/form-expired";
            if (returnUrl is not null)
            {
                destination += QueryString.Create("returnUrl", returnUrl);
            }

            // A normal redirect intentionally changes POST to GET. The rejected
            // write must never be replayed automatically.
            context.Result = new RedirectResult(destination);
            return;
        }

        context.Result = new JsonResult(new
        {
            success = false,
            code = "form_expired",
            message = ExpiredFormMessage
        })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static bool ExpectsHtml(HttpRequest request)
    {
        if (string.Equals(
                request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return request.Headers.Accept.Any(value =>
            value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string? GetSameHostReturnUrl(HttpRequest request)
    {
        var referrer = request.Headers.Referer.ToString();
        if (!Uri.TryCreate(referrer, UriKind.Absolute, out var referrerUri)
            || (!string.Equals(referrerUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(referrerUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            || !string.Equals(
                referrerUri.Host,
                request.Host.Host,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return referrerUri.PathAndQuery;
    }
}
