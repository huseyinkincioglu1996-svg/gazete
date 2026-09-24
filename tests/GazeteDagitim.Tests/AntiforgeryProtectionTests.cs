using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text.Json;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GazeteDagitim.Tests;

public sealed class AntiforgeryProtectionTests
{
    [Fact]
    public async Task HtmlForm_WithMissingToken_RedirectsToRecoverablePageWithoutSaving()
    {
        await using var factory = new GazeteWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/distributors/create")
        {
            Content = ValidDistributorForm()
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Referrer = new Uri(
            "http://localhost/distributors/create",
            UriKind.Absolute);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith(
            "/form-expired?returnUrl=",
            response.Headers.Location!.OriginalString,
            StringComparison.Ordinal);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        using var expiredPage = await client.GetAsync(response.Headers.Location);
        expiredPage.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await expiredPage.Content.ReadAsStringAsync());
        Assert.Contains("Formun süresi doldu", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/distributors/create\"", html, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.Distributors.ToListAsync());
    }

    [Fact]
    public async Task JsonRequest_WithMissingToken_ReturnsActionableBadRequestWithoutSaving()
    {
        await using var factory = new GazeteWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/distributors/create")
        {
            Content = ValidDistributorForm()
        };
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("form_expired", json.RootElement.GetProperty("code").GetString());
        Assert.Contains(
            "Formun güvenlik süresi doldu",
            json.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.Distributors.ToListAsync());
    }

    [Fact]
    public async Task FormToken_RemainsValidAfterApplicationRestart()
    {
        var sharedKeysPath = Path.Combine(
            Path.GetTempPath(),
            "GazeteDagitim.Tests",
            $"restart-{Guid.NewGuid():N}",
            "DataProtection-Keys");
        string requestToken;
        string antiforgeryCookie;

        await using (var firstFactory = new GazeteWebFactory(sharedKeysPath))
        {
            using var firstClient = CreateCookieTransparentClient(firstFactory);
            using var formPage = await firstClient.GetAsync("/distributors/create");
            formPage.EnsureSuccessStatusCode();
            var html = await formPage.Content.ReadAsStringAsync();
            requestToken = ExtractRequestToken(html);
            antiforgeryCookie = ExtractAntiforgeryCookie(formPage);
        }

        Assert.True(Directory.Exists(sharedKeysPath));
        Assert.NotEmpty(
            Directory.EnumerateFiles(
                sharedKeysPath,
                "key-*.xml"));

        await using var secondFactory = new GazeteWebFactory(sharedKeysPath);
        using var secondClient = CreateCookieTransparentClient(secondFactory);
        using var post = new HttpRequestMessage(HttpMethod.Post, "/distributors/create")
        {
            Content = ValidDistributorForm(requestToken)
        };
        post.Headers.Add("Cookie", antiforgeryCookie);

        using var response = await secondClient.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/distributors", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task SubscriberEditToken_RemainsValidAfterApplicationRestart()
    {
        var sharedKeysPath = Path.Combine(
            Path.GetTempPath(),
            "GazeteDagitim.Tests",
            $"subscriber-edit-restart-{Guid.NewGuid():N}",
            "DataProtection-Keys");
        string requestToken;
        string antiforgeryCookie;
        int subscriberId;

        await using (var firstFactory = new GazeteWebFactory(sharedKeysPath))
        {
            subscriberId = await SeedSubscriberAsync(firstFactory, "Eski Abone Adı");
            using var firstClient = CreateCookieTransparentClient(firstFactory);
            using var formPage = await firstClient.GetAsync(
                $"/subscribers/{subscriberId}/edit");
            formPage.EnsureSuccessStatusCode();
            var html = await formPage.Content.ReadAsStringAsync();
            requestToken = ExtractRequestToken(html);
            antiforgeryCookie = ExtractAntiforgeryCookie(formPage);
        }

        await using var secondFactory = new GazeteWebFactory(sharedKeysPath);
        var restartedSubscriberId = await SeedSubscriberAsync(
            secondFactory,
            "Eski Abone Adı");
        Assert.Equal(subscriberId, restartedSubscriberId);

        using var secondClient = CreateCookieTransparentClient(secondFactory);
        using var post = new HttpRequestMessage(
            HttpMethod.Post,
            $"/subscribers/{subscriberId}/edit")
        {
            Content = ValidSubscriberEditForm(subscriberId, requestToken)
        };
        post.Headers.Add("Cookie", antiforgeryCookie);

        using var response = await secondClient.SendAsync(post);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/subscribers", response.Headers.Location?.OriginalString);

        await using var scope = secondFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscriber = await dbContext.Subscribers
            .AsNoTracking()
            .SingleAsync(value => value.Id == subscriberId);
        Assert.Equal("Güncellenen Abone", subscriber.Name);
        Assert.Equal(175.50m, subscriber.MonthlyFee);
    }

    [Fact]
    public void ProductionAntiforgeryCookie_AlwaysRequiresHttps()
    {
        using var factory = new ProductionGazeteWebFactory();
        var options = factory.Services
            .GetRequiredService<IOptions<AntiforgeryOptions>>()
            .Value;

        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
    }

    private static HttpClient CreateCookieTransparentClient(
        WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private static FormUrlEncodedContent ValidDistributorForm(
        string? antiforgeryToken = null)
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("Name", "Güvenli Dağıtıcı"),
            new("Address", "Test adresi"),
            new("Phone", "5550000000"),
            new("Zone", "Region1"),
            new("PaymentType", "Daily"),
            new("NewspaperPrice", "5.50"),
            new("DistributionDays", "Monday")
        };
        if (antiforgeryToken is not null)
        {
            values.Insert(
                0,
                new KeyValuePair<string, string>(
                    "__RequestVerificationToken",
                    antiforgeryToken));
        }

        return new FormUrlEncodedContent(values);
    }

    private static FormUrlEncodedContent ValidSubscriberEditForm(
        int subscriberId,
        string antiforgeryToken) =>
        new(
        [
            new("__RequestVerificationToken", antiforgeryToken),
            new("Id", subscriberId.ToString()),
            new("Name", "Güncellenen Abone"),
            new("MonthlyFee", "175.50"),
            new("FirstDeliveryDate", "2026-09-20"),
            new("IsActive", "true"),
            new("NewspaperDays", "Monday")
        ]);

    private static async Task<int> SeedSubscriberAsync(
        GazeteWebFactory factory,
        string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscriber = new Subscriber
        {
            Name = name,
            MonthlyFee = 150m,
            IsActive = true,
            FirstDeliveryDate = new DateOnly(2026, 9, 20)
        };
        dbContext.Subscribers.Add(subscriber);
        await dbContext.SaveChangesAsync();
        return subscriber.Id;
    }

    private static string ExtractRequestToken(string html)
    {
        var input = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(input.Success, "Anti-forgery input was not found.");
        var value = Regex.Match(
            input.Value,
            "value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(value.Success, "Anti-forgery value was not found.");
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private static string ExtractAntiforgeryCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith(
                ".GazeteDagitim.Antiforgery=",
                StringComparison.Ordinal));
        return setCookie.Split(';', 2)[0];
    }

    private sealed class ProductionGazeteWebFactory : WebApplicationFactory<Program>
    {
        private readonly string _keysPath = Path.Combine(
            Path.GetTempPath(),
            "GazeteDagitim.Tests",
            $"production-{Guid.NewGuid():N}",
            "DataProtection-Keys");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Database:ApplyMigrations"] = "false",
                        ["DataProtection:KeysPath"] = _keysPath,
                        ["ConnectionStrings:GazeteDagitim"] =
                            "Server=(local);Database=Unused;Trusted_Connection=True;TrustServerCertificate=True"
                    });
            });
            builder.ConfigureServices(services =>
            {
                Directory.CreateDirectory(_keysPath);
                services
                    .AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(_keysPath))
                    .SetApplicationName("GazeteDagitim.Web");
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(
                        $"gazete-production-tests-{Guid.NewGuid():N}"));
            });
        }
    }
}
