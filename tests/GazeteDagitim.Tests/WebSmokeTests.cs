using GazeteDagitim.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GazeteDagitim.Tests;

public sealed class WebSmokeTests : IClassFixture<GazeteWebFactory>
{
    private readonly HttpClient _client;

    public WebSmokeTests(GazeteWebFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Theory]
    [InlineData("/menu")]
    [InlineData("/menu/company")]
    [InlineData("/subscribers")]
    [InlineData("/settings")]
    [InlineData("/distributors")]
    [InlineData("/deliveries?date=2026-07-28")]
    [InlineData("/payments?month=2026-07")]
    [InlineData("/cash-handover?date=2026-07-28")]
    [InlineData("/reports?date=2026-07-28")]
    [InlineData("/menu/company/settings")]
    public async Task MainRoutes_ReturnSuccessfulHtml(string url)
    {
        var response = await _client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<main", html, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class GazeteWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrations"] = "false",
                    ["ConnectionStrings:GazeteDagitim"] =
                        "Server=(local);Database=Unused;Trusted_Connection=True;TrustServerCertificate=True"
                });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            var databaseName = $"gazete-web-tests-{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
        });
    }
}
