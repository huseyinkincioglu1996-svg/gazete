using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class DeliveryDetailPageTests
{
    private static readonly DateOnly TestDate = new(2026, 7, 28);

    [Fact]
    public async Task DeliveriesPage_SummaryCardsLinkToDatedDetailPages()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            $"/deliveries/delivered?date={TestDate:yyyy-MM-dd}",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            $"/deliveries/collections?date={TestDate:yyyy-MM-dd}",
            html,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("delivered")]
    public async Task DeliveredPage_DefaultAndDeliveredList_ShowOnlyDeliveredSubscribers(
        string? list)
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var seeded = await SeedDetailScenarioAsync(factory);
        var path = $"/deliveries/delivered?date={TestDate:yyyy-MM-dd}" +
                   (list is null ? "" : $"&list={list}");

        using var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);
        Assert.Contains(
            seeded.DeliveredSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            seeded.DueSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            seeded.CollectedSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            seeded.NonDueSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        AssertDeliveredPageHasNoMutationControls(html);
    }

    [Fact]
    public async Task DeliveredPage_AllList_ShowsEveryDailySubscriberReadOnly()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var seeded = await SeedDetailScenarioAsync(factory);

        using var response = await client.GetAsync(
            $"/deliveries/delivered?date={TestDate:yyyy-MM-dd}&list=all");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);
        Assert.Contains(
            seeded.DeliveredSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            seeded.DueSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            seeded.CollectedSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            seeded.NonDueSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        AssertDeliveredPageHasNoMutationControls(html);
    }

    [Fact]
    public async Task DeliveredPage_SummaryCardsLinkToDeliveredAndAllListsWithDate()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        await SeedDetailScenarioAsync(factory);

        using var response = await client.GetAsync(
            $"/deliveries/delivered?date={TestDate:yyyy-MM-dd}&list=all");

        response.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        AssertSummaryCardLink(
            html,
            $"/deliveries/delivered?date={TestDate:yyyy-MM-dd}&list=delivered");
        AssertSummaryCardLink(
            html,
            $"/deliveries/delivered?date={TestDate:yyyy-MM-dd}&list=all");
    }

    private static void AssertDeliveredPageHasNoMutationControls(string html)
    {
        Assert.DoesNotContain(
            "data-delivered-toggle",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "data-collected-toggle",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "/deliveries/save-row",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectionsPage_ShowsDueCollectedAndActiveCashSale_Only()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var seeded = await SeedDetailScenarioAsync(factory);

        using var response = await client.GetAsync(
            $"/deliveries/collections?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);
        Assert.Contains(
            seeded.DueSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            seeded.CollectedSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            seeded.ActiveCashSaleDistributorName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            seeded.NonDueSubscriberName,
            decodedHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            seeded.CancelledCashSaleDistributorName,
            decodedHtml,
            StringComparison.Ordinal);

        var dueRow = ExtractDeliveryRow(html, seeded.DueSubscriberId);
        Assert.Contains(
            "data-collected-toggle",
            dueRow,
            StringComparison.Ordinal);
        Assert.Contains("data-amount", dueRow, StringComparison.Ordinal);
        Assert.Contains(
            "data-payment-field",
            dueRow,
            StringComparison.Ordinal);

        var collectedRow = ExtractDeliveryRow(
            html,
            seeded.CollectedSubscriberId);
        Assert.Contains(
            "data-collected-toggle",
            collectedRow,
            StringComparison.Ordinal);
        Assert.Contains(
            "aria-pressed=\"true\"",
            collectedRow,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-autosave-url=\"/deliveries/save-row\"",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectionsPage_PaymentPostPreservesDeliveryState()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var seeded = await SeedDetailScenarioAsync(factory);
        var pagePath =
            $"/deliveries/collections?date={TestDate:yyyy-MM-dd}";
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, pagePath);

        using var response = await client.PostAsync(
            "/deliveries/save-row",
            PartialAutosaveForm(
                antiforgeryToken,
                seeded.DueSubscriberId,
                ("Collected", "true"),
                ("Amount", "280.00"),
                ("PaymentMethod", "Kart")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = Assert.IsType<DailyDeliveryRowAutosaveResponseModel>(
            await response.Content
                .ReadFromJsonAsync<DailyDeliveryRowAutosaveResponseModel>());
        Assert.True(saved.Success);
        Assert.NotNull(saved.Row);
        Assert.True(saved.Row.Delivered);
        Assert.True(saved.Row.Collected);
        Assert.Equal(280m, saved.Row.Amount);
        Assert.Equal("Kart", saved.Row.PaymentMethod);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbContext.SubscriberDailyDeliveries
            .AsNoTracking()
            .SingleAsync(value =>
                value.SubscriberId == seeded.DueSubscriberId &&
                value.Date == TestDate);
        Assert.True(persisted.IsDelivered);
        Assert.True(persisted.IsCollected);
        Assert.Equal(280m, persisted.Amount);
        Assert.Equal(SubscriberPaymentMethod.Card, persisted.PaymentMethod);
    }

    [Fact]
    public async Task CollectionsPage_WithDeliveredCash_DisablesPaymentControls()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var seeded = await SeedDetailScenarioAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.CashHandovers.Add(new CashHandover
            {
                Date = TestDate,
                Status = CashHandoverStatus.Delivered,
                Total = 199m,
                DeliveredAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.GetAsync(
            $"/deliveries/collections?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var row = ExtractDeliveryRow(html, seeded.DueSubscriberId);
        AssertMarkedTagIsDisabled(row, "button", "data-collected-toggle");
        AssertMarkedTagIsDisabled(row, "input", "data-amount");
        AssertMarkedTagIsDisabled(row, "select", "data-payment-field");
    }

    private static HttpClient CreateClient(GazeteWebFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

    private static async Task<DeliveryDetailSeed> SeedDetailScenarioAsync(
        GazeteWebFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var planStart = new DateTimeOffset(
            2026,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        var distributor = new Distributor
        {
            Name = "Detay Gazete Bayisi",
            Address = "Test adresi",
            Phone = "0555 000 00 10",
            Zone = DistributorZone.Region1,
            NewspaperPrice = 2m,
            IsActive = true
        };
        var cancelledSaleDistributor = new Distributor
        {
            Name = "İptal Edilmiş Satış Bayisi",
            Address = "İptal test adresi",
            Phone = "0555 000 00 11",
            Zone = DistributorZone.Region1,
            NewspaperPrice = 2m,
            IsActive = true
        };
        var deliveredSubscriber = CreateSubscriber(
            "Salt Okunur Teslim Abonesi",
            collectionDayOfMonth: 27,
            amount: 120m,
            planStart,
            distributor);
        var dueSubscriber = CreateSubscriber(
            "Bugün Ödemeli Abone",
            collectionDayOfMonth: 28,
            amount: 280m,
            planStart,
            distributor);
        var collectedSubscriber = CreateSubscriber(
            "Ödemesi Alınmış Abone",
            collectionDayOfMonth: 27,
            amount: 175m,
            planStart,
            distributor);
        var nonDueSubscriber = CreateSubscriber(
            "Vadesiz Ödenmemiş Abone",
            collectionDayOfMonth: 27,
            amount: 90m,
            planStart,
            distributor);

        dbContext.AddRange(
            deliveredSubscriber,
            dueSubscriber,
            collectedSubscriber,
            nonDueSubscriber,
            cancelledSaleDistributor);
        await dbContext.SaveChangesAsync();

        dbContext.SubscriberDailyDeliveries.AddRange(
            CreateDelivery(
                deliveredSubscriber,
                distributor,
                delivered: true,
                collected: false,
                amount: 120m),
            CreateDelivery(
                dueSubscriber,
                distributor,
                delivered: true,
                collected: false,
                amount: 280m),
            CreateDelivery(
                collectedSubscriber,
                distributor,
                delivered: false,
                collected: true,
                amount: 175m));
        var cashSaleCreatedAt = new DateTimeOffset(
            2026,
            7,
            28,
            12,
            0,
            0,
            TimeSpan.Zero);
        dbContext.NewspaperCashSales.AddRange(
            new NewspaperCashSale
            {
                Date = TestDate,
                DistributorId = distributor.Id,
                Distributor = distributor,
                DistributorName = distributor.Name,
                Quantity = 2,
                UnitPrice = 12m,
                Amount = 24m,
                IdempotencyKey = Guid.NewGuid(),
                CreatedAt = cashSaleCreatedAt
            },
            new NewspaperCashSale
            {
                Date = TestDate,
                DistributorId = cancelledSaleDistributor.Id,
                Distributor = cancelledSaleDistributor,
                DistributorName = cancelledSaleDistributor.Name,
                Quantity = 1,
                UnitPrice = 12m,
                Amount = 12m,
                IdempotencyKey = Guid.NewGuid(),
                CreatedAt = cashSaleCreatedAt,
                CancelledAt = cashSaleCreatedAt.AddMinutes(1)
            });
        await dbContext.SaveChangesAsync();

        return new DeliveryDetailSeed(
            deliveredSubscriber.Name,
            dueSubscriber.Id,
            dueSubscriber.Name,
            collectedSubscriber.Id,
            collectedSubscriber.Name,
            nonDueSubscriber.Name,
            distributor.Name,
            cancelledSaleDistributor.Name);
    }

    private static Subscriber CreateSubscriber(
        string name,
        int collectionDayOfMonth,
        decimal amount,
        DateTimeOffset planStart,
        Distributor distributor) =>
        new()
        {
            Name = name,
            MonthlyFee = amount,
            IsActive = true,
            CreatedAt = planStart,
            Distributor = distributor,
            PaymentPeriod = new PaymentPeriod
            {
                Name = $"{name} Ödeme Planı",
                DayCount = 30,
                CollectionDayOfMonth = collectionDayOfMonth,
                CollectionTime = new TimeOnly(10, 0),
                CollectionAmount = amount,
                IsActive = true
            }
        };

    private static SubscriberDailyDelivery CreateDelivery(
        Subscriber subscriber,
        Distributor distributor,
        bool delivered,
        bool collected,
        decimal amount) =>
        new()
        {
            SubscriberId = subscriber.Id,
            Subscriber = subscriber,
            DistributorId = distributor.Id,
            Distributor = distributor,
            DistributorName = distributor.Name,
            Date = TestDate,
            NewspaperCount = 1,
            IsDelivered = delivered,
            IsCollected = collected,
            CollectedAt = collected
                ? new DateTimeOffset(
                    2026,
                    7,
                    28,
                    10,
                    30,
                    0,
                    TimeSpan.Zero)
                : null,
            Amount = amount,
            PaymentMethod = SubscriberPaymentMethod.Cash,
            CollectionPeriodName = collected ? subscriber.PaymentPeriod!.Name : "",
            CollectionDayCount = collected ? 30 : null,
            CoveredDates =
            [
                new SubscriberDailyDeliveryCoveredDate
                {
                    CoveredDate = TestDate
                }
            ]
        };

    private static string ExtractDeliveryRow(string html, int subscriberId)
    {
        var row = Regex.Match(
            html,
            $"""
             <tr\b
             (?=[^>]*\bdata-subscriber-id="{subscriberId}")
             [^>]*>
             .*?
             </tr>
             """,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.CultureInvariant);
        Assert.True(
            row.Success,
            $"Subscriber {subscriberId} detail row was not found.");
        return row.Value;
    }

    private static void AssertMarkedTagIsDisabled(
        string html,
        string tagName,
        string markerAttribute)
    {
        var tag = Regex.Match(
            html,
            $"""
             <{Regex.Escape(tagName)}\b
             (?=[^>]*\b{Regex.Escape(markerAttribute)}(?:\s|=|/?>))
             [^>]*>
             """,
            RegexOptions.IgnoreCase |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.CultureInvariant);
        Assert.True(
            tag.Success,
            $"{tagName} with {markerAttribute} was not found.");
        Assert.Matches(
            @"\bdisabled(?:\s|=|/?>)",
            tag.Value);
    }

    private static void AssertSummaryCardLink(
        string html,
        string expectedHref)
    {
        var link = Regex.Matches(
                html,
                @"<a\b[^>]*>",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant)
            .Cast<Match>()
            .SingleOrDefault(value =>
                value.Value.Contains(
                    $"href=\"{expectedHref}\"",
                    StringComparison.Ordinal) &&
                Regex.IsMatch(
                    value.Value,
                    @"\bclass=""[^""]*\bsummary-card\b[^""]*""",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant));
        Assert.NotNull(link);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string path)
    {
        var html = await client.GetStringAsync(path);
        var input = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(input.Success, $"Anti-forgery input was not found at {path}.");

        var value = Regex.Match(
            input.Value,
            "value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(value.Success, $"Anti-forgery value was not found at {path}.");
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private static FormUrlEncodedContent PartialAutosaveForm(
        string antiforgeryToken,
        int subscriberId,
        params (string Key, string Value)[] changes) =>
        new(
            new[]
            {
                new KeyValuePair<string, string>(
                    "__RequestVerificationToken",
                    antiforgeryToken),
                new KeyValuePair<string, string>(
                    "Date",
                    TestDate.ToString("yyyy-MM-dd")),
                new KeyValuePair<string, string>(
                    "SubscriberId",
                    subscriberId.ToString())
            }.Concat(
                changes.Select(change =>
                    new KeyValuePair<string, string>(
                        change.Key,
                        change.Value))));

    private sealed record DeliveryDetailSeed(
        string DeliveredSubscriberName,
        int DueSubscriberId,
        string DueSubscriberName,
        int CollectedSubscriberId,
        string CollectedSubscriberName,
        string NonDueSubscriberName,
        string ActiveCashSaleDistributorName,
        string CancelledCashSaleDistributorName);
}
