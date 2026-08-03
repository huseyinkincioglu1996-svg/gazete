using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class WebInputSecurityTests
{
    private static readonly DateOnly TestDate = new(2026, 7, 28);

    [Fact]
    public async Task DeliveryAmount_WithHtmlDecimalPoint_PersistsFractionalValue()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var subscriberId = await SeedAsync(
            factory,
            dbContext =>
            {
                var subscriber = new Subscriber
                {
                    Name = "Ondalıklı Teslimat",
                    IsActive = true
                };
                dbContext.Subscribers.Add(subscriber);
                return subscriber;
            });
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        using var response = await client.PostAsync(
            "/deliveries/save",
            Form(
                token,
                ("Date", TestDate.ToString("yyyy-MM-dd")),
                ("Rows[0].SubscriberId", subscriberId.ToString()),
                ("Rows[0].Delivered", "true"),
                ("Rows[0].Collected", "true"),
                ("Rows[0].Amount", "125.50"),
                ("Rows[0].PaymentMethod", "Nakit")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var delivery = await dbContext.SubscriberDailyDeliveries
            .AsNoTracking()
            .SingleAsync(value => value.SubscriberId == subscriberId);
        Assert.Equal(125.50m, delivery.Amount);
    }

    [Fact]
    public async Task CashAmount_WithHtmlDecimalPoint_PersistsFractionalValue()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/cash-handover?date={TestDate:yyyy-MM-dd}");

        using var response = await client.PostAsync(
            "/cash-handover/save",
            Form(
                token,
                ("Date", TestDate.ToString("yyyy-MM-dd")),
                ("Status", "Taslak"),
                ("Items[0].SubscriberName", "Manuel Tahsilat"),
                ("Items[0].Amount", "125.50"),
                ("Items[0].Description", "")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await dbContext.CashHandoverItems.AsNoTracking().SingleAsync();
        Assert.Equal(125.50m, item.Amount);
    }

    [Fact]
    public async Task DeliveredCashHandover_HasNoEditor_AndPostCannotReopenOrDeleteItems()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        await SeedAsync(
            factory,
            dbContext =>
            {
                var handover = new CashHandover
                {
                    Date = TestDate,
                    Status = CashHandoverStatus.Delivered,
                    DeliveredAt = DateTimeOffset.UtcNow,
                    Total = 75m
                };
                handover.Items.Add(new CashHandoverItem
                {
                    SubscriberName = "Korunan Kalem",
                    Amount = 75m,
                    Description = "Teslim edilmiş"
                });
                dbContext.CashHandovers.Add(handover);
                return handover;
            });

        var html = await client.GetStringAsync(
            $"/cash-handover?date={TestDate:yyyy-MM-dd}");
        Assert.DoesNotContain("id=\"cash-form\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-remove-cash-row", html, StringComparison.Ordinal);

        var token = await GetAntiforgeryTokenAsync(client, "/subscribers/create");
        using var response = await client.PostAsync(
            "/cash-handover/save",
            Form(
                token,
                ("Date", TestDate.ToString("yyyy-MM-dd")),
                ("Status", "Taslak")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var handover = await dbContext.CashHandovers
            .AsNoTracking()
            .Include(value => value.Items)
            .SingleAsync(value => value.Date == TestDate);
        Assert.Equal(CashHandoverStatus.Delivered, handover.Status);
        Assert.Single(handover.Items);
        Assert.Equal(75m, handover.Items.Single().Amount);
    }

    [Fact]
    public async Task Payments_WithOneActiveDistributor_KeepAllFilter_AndUnknownIdIsNotFound()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var distributorId = await SeedAsync(
            factory,
            dbContext =>
            {
                var distributor = new Distributor
                {
                    Name = "Tek Dağıtıcı",
                    Address = "Adres",
                    Phone = "555",
                    IsActive = true,
                    NewspaperPrice = 5m
                };
                dbContext.Distributors.Add(distributor);
                return distributor;
            });

        var html = await client.GetStringAsync("/payments?month=2026-07");
        Assert.DoesNotMatch(
            new Regex(
                $"<option[^>]*value=\"{distributorId}\"[^>]*selected",
                RegexOptions.IgnoreCase),
            html);

        using var response = await client.GetAsync(
            "/payments?month=2026-07&distributorId=987654");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubscriberOptionalStrings_AreOptional_AndSundayMondayConflictIsRejected()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client, "/subscribers/create");

        using var validResponse = await client.PostAsync(
            "/subscribers/create",
            Form(
                token,
                ("Name", "İsteğe Bağlı Alanlar"),
                ("Phone", ""),
                ("Address", ""),
                ("Notes", ""),
                ("MonthlyFee", "0.00"),
                ("IsActive", "true")));
        Assert.Equal(HttpStatusCode.Redirect, validResponse.StatusCode);

        token = await GetAntiforgeryTokenAsync(client, "/subscribers/create");
        using var invalidResponse = await client.PostAsync(
            "/subscribers/create",
            Form(
                token,
                ("Name", "Çakışan Günler"),
                ("MonthlyFee", "0.00"),
                ("IsActive", "true"),
                ("NewspaperDays", "SundayMonday"),
                ("NewspaperDays", "Sunday")));
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();
        Assert.Contains(
            "Pazar Pazartesi",
            WebUtility.HtmlDecode(invalidHtml),
            StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await dbContext.Subscribers.CountAsync());
    }

    [Fact]
    public async Task PaymentPeriodSchedule_WithHtmlInputs_PersistsDayTimeCoverageAndAmount()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client, "/settings/create");

        using var response = await client.PostAsync(
            "/settings/create",
            Form(
                token,
                ("Name", "Ay Ortası Tahsilatı"),
                ("CollectionDayOfMonth", "15"),
                ("CollectionTime", "14:30"),
                ("DayCount", "30"),
                ("CollectionAmount", "450.75"),
                ("Description", "Otuz günlük abonelik bedeli"),
                ("IsActive", "true")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var period = await dbContext.PaymentPeriods.AsNoTracking().SingleAsync();
        Assert.Equal("Ay Ortası Tahsilatı", period.Name);
        Assert.Equal(15, period.CollectionDayOfMonth);
        Assert.Equal(new TimeOnly(14, 30), period.CollectionTime);
        Assert.Equal(30, period.DayCount);
        Assert.Equal(450.75m, period.CollectionAmount);
        Assert.True(period.IsActive);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/settings"));
        Assert.Contains("Ayın 15. günü", html, StringComparison.Ordinal);
        Assert.Contains("14:30", html, StringComparison.Ordinal);
        Assert.Contains("450,75", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaymentPeriodSchedule_RejectsInvalidDayAndNonPositiveAmount()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client, "/settings/create");

        using var response = await client.PostAsync(
            "/settings/create",
            Form(
                token,
                ("Name", "Geçersiz Tahsilat"),
                ("CollectionDayOfMonth", "32"),
                ("CollectionTime", "09:00"),
                ("DayCount", "30"),
                ("CollectionAmount", "100"),
                ("Description", ""),
                ("IsActive", "true")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            "Ödeme alınacak gün 1 ile 31 arasında olmalıdır.",
            html,
            StringComparison.Ordinal);

        token = await GetAntiforgeryTokenAsync(client, "/settings/create");
        using var amountResponse = await client.PostAsync(
            "/settings/create",
            Form(
                token,
                ("Name", "Geçersiz Tutar"),
                ("CollectionDayOfMonth", "10"),
                ("CollectionTime", "09:00"),
                ("DayCount", "30"),
                ("CollectionAmount", "0"),
                ("Description", ""),
                ("IsActive", "true")));

        Assert.Equal(HttpStatusCode.OK, amountResponse.StatusCode);
        html = WebUtility.HtmlDecode(await amountResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            "Alınacak tutar sıfırdan büyük olmalıdır.",
            html,
            StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.PaymentPeriods.ToListAsync());
    }

    [Fact]
    public async Task CompanyLogo_WithSpoofedContentType_IsRejectedWithoutMutation()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        const string originalLogo = "data:image/png;base64,b3JpZ2luYWw=";
        await SeedAsync(
            factory,
            dbContext =>
            {
                var settings = new CompanySettings { LogoDataUrl = originalLogo };
                dbContext.CompanySettings.Add(settings);
                return settings;
            });
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/menu/company/settings");

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(token), "__RequestVerificationToken");
        multipart.Add(new StringContent("true"), "RemoveCompanyLogo");
        using var fakeImage = new ByteArrayContent(
            Encoding.UTF8.GetBytes("this is not a png image"));
        fakeImage.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(fakeImage, "CompanyLogo", "spoofed.png");

        using var response = await client.PostAsync(
            "/menu/company/settings/save",
            multipart);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Dosya içeriği", html, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await dbContext.CompanySettings.AsNoTracking().SingleAsync();
        Assert.Equal(originalLogo, settings.LogoDataUrl);
    }

    private static HttpClient CreateClient(GazeteWebFactory factory) =>
        factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

    private static async Task<int> SeedAsync<TEntity>(
        GazeteWebFactory factory,
        Func<AppDbContext, TEntity> create)
        where TEntity : EntityBase
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = create(dbContext);
        await dbContext.SaveChangesAsync();
        return entity.Id;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string path)
    {
        var html = await client.GetStringAsync(path);
        var input = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"Anti-forgery input was not found at {path}.");

        var value = Regex.Match(
            input.Value,
            "value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(value.Success, $"Anti-forgery value was not found at {path}.");
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Key, string Value)[] values) =>
        new(
            new[]
            {
                new KeyValuePair<string, string>(
                    "__RequestVerificationToken",
                    antiforgeryToken)
            }.Concat(
                values.Select(
                    value => new KeyValuePair<string, string>(
                        value.Key,
                        value.Value))));
}
