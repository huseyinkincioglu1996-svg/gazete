using GazeteDagitim.Web.Data;
using Microsoft.AspNetCore.Mvc;

namespace GazeteDagitim.Web.Controllers;

[ApiController]
[Route("health")]
[Route("api/health")]
public sealed class HealthController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseAvailable = await dbContext.Database.CanConnectAsync(cancellationToken);
        var payload = new
        {
            status = databaseAvailable ? "healthy" : "unhealthy",
            framework = ".NET 9",
            database = "Microsoft SQL Server",
            databaseAvailable,
            checkedAt = DateTimeOffset.UtcNow
        };

        return databaseAvailable
            ? Ok(payload)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
    }
}
