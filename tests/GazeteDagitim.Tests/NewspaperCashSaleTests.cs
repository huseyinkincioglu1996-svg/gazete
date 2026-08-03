using System.Net;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class NewspaperCashSaleTests
{
    private static readonly DateOnly TestDate = new(2026, 7, 31);

    [Fact]
    public async Task CreateAsync_SnapshotsPriceAndDistributor_AndDoesNotChangeDeliveryCount()
    {
        await using var context = CreateContext();
        var distributor = CreateDistributor("Central Distributor", 3.10m);
        var companySettings = CreateCompanySettings(7.255m);
        var delivery = new Delivery
        {
            Distributor = distributor,
            Date = TestDate,
            Day = BusinessDay.Friday,
            NewspaperCount = 12,
            Amount = 87m,
            Status = DeliveryStatus.Completed
        };
        context.AddRange(delivery, companySettings);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var created = await service.CreateAsync(
            TestDate,
            distributor.Id,
            quantity: 3,
            Guid.NewGuid());

        Assert.Equal(TestDate, created.Date);
        Assert.Equal(distributor.Id, created.DistributorId);
        Assert.Equal("Central Distributor", created.DistributorName);
        Assert.Equal(3, created.Quantity);
        Assert.Equal(7.26m, created.UnitPrice);
        Assert.Equal(21.78m, created.Amount);

        distributor.Name = "Renamed Distributor";
        distributor.NewspaperPrice = 99.50m;
        companySettings.NewspaperUnitPrice = 11m;
        await context.SaveChangesAsync();

        var daily = await service.GetDailyAsync(TestDate);
        var snapshot = Assert.Single(daily.Records);
        Assert.Equal("Central Distributor", snapshot.DistributorName);
        Assert.Equal(7.26m, snapshot.UnitPrice);
        Assert.Equal(21.78m, snapshot.Amount);
        Assert.Equal(
            11m,
            Assert.Single(daily.Distributors).UnitPrice);
        Assert.Equal(
            12,
            (await context.Deliveries.AsNoTracking().SingleAsync()).NewspaperCount);
    }

    [Fact]
    public async Task CreateAsync_ReusingIdempotencyKey_ReturnsOriginalSaleOnlyOnce()
    {
        await using var context = CreateContext();
        var distributor = CreateDistributor("Idempotent Distributor", 1.25m);
        var companySettings = CreateCompanySettings(6.40m);
        context.AddRange(distributor, companySettings);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var idempotencyKey = Guid.NewGuid();

        var first = await service.CreateAsync(
            TestDate,
            distributor.Id,
            quantity: 2,
            idempotencyKey);
        companySettings.NewspaperUnitPrice = 8.90m;
        await context.SaveChangesAsync();
        var repeated = await service.CreateAsync(
            TestDate,
            distributor.Id,
            quantity: 2,
            idempotencyKey);
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.CreateAsync(
                TestDate,
                distributor.Id,
                quantity: 3,
                idempotencyKey));

        Assert.Equal(first.Id, repeated.Id);
        Assert.Equal(6.40m, first.UnitPrice);
        Assert.Equal(first.Amount, repeated.Amount);
        Assert.Equal(1, await context.NewspaperCashSales.CountAsync());
        Assert.Equal(12.80m, (await service.GetDailyAsync(TestDate)).Total);
    }

    [Fact]
    public async Task CreateAsync_WithoutCompanyUnitPrice_IsRejectedEvenWhenDistributorHasCost()
    {
        await using var context = CreateContext();
        var distributor = CreateDistributor("Unconfigured Distributor", 125m);
        context.Distributors.Add(distributor);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var daily = await service.GetDailyAsync(TestDate);
        Assert.Equal(0m, Assert.Single(daily.Distributors).UnitPrice);

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CreateAsync(
                TestDate,
                distributor.Id,
                quantity: 1,
                Guid.NewGuid()));

        Assert.Contains(
            "Firma Ayarlar",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.NewspaperCashSales);
    }

    [Fact]
    public async Task CancelAsync_MarksSaleCancelled_AndRemovesItFromDailyTotals()
    {
        await using var context = CreateContext();
        var distributor = CreateDistributor("Cancellation Distributor", 2m);
        context.AddRange(distributor, CreateCompanySettings(8m));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var sale = await service.CreateAsync(
            TestDate,
            distributor.Id,
            quantity: 2,
            Guid.NewGuid());

        var cancelled = await service.CancelAsync(sale.Id);

        Assert.Equal(sale.Id, cancelled.Id);
        var persisted = await context.NewspaperCashSales
            .AsNoTracking()
            .SingleAsync();
        Assert.NotNull(persisted.CancelledAt);
        var daily = await service.GetDailyAsync(TestDate);
        Assert.Empty(daily.Records);
        Assert.Equal(0, daily.TotalQuantity);
        Assert.Equal(0m, daily.Total);
    }

    [Fact]
    public async Task DeliveredCashHandover_BlocksCreatingAndCancellingSales()
    {
        await using var context = CreateContext();
        var distributor = CreateDistributor("Locked Distributor", 1m);
        context.AddRange(distributor, CreateCompanySettings(5m));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var existing = await service.CreateAsync(
            TestDate,
            distributor.Id,
            quantity: 1,
            Guid.NewGuid());
        context.CashHandovers.Add(new CashHandover
        {
            Date = TestDate,
            Status = CashHandoverStatus.Delivered,
            Total = existing.Amount,
            DeliveredAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.CreateAsync(
                TestDate,
                distributor.Id,
                quantity: 1,
                Guid.NewGuid()));
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.CancelAsync(existing.Id));

        Assert.Null(
            (await context.NewspaperCashSales.AsNoTracking().SingleAsync())
            .CancelledAt);
        Assert.Equal(1, await context.NewspaperCashSales.CountAsync());
    }

    [Fact]
    public async Task CashHandover_IncludesActiveCashSalesInAutomaticAndGrandTotals()
    {
        await using var context = CreateContext();
        var distributor = CreateDistributor("Cash Distributor", 0.75m);
        var subscriber = new Subscriber
        {
            Name = "Cash Subscriber",
            Distributor = distributor,
            MonthlyFee = 40m
        };
        context.AddRange(subscriber, CreateCompanySettings(7.50m));
        await context.SaveChangesAsync();
        context.SubscriberDailyDeliveries.Add(new SubscriberDailyDelivery
        {
            SubscriberId = subscriber.Id,
            DistributorId = distributor.Id,
            DistributorName = distributor.Name,
            Date = TestDate,
            NewspaperCount = 1,
            IsCollected = true,
            Amount = 40m,
            PaymentMethod = SubscriberPaymentMethod.Cash,
            CoveredDates =
            [
                new SubscriberDailyDeliveryCoveredDate { CoveredDate = TestDate }
            ]
        });
        await context.SaveChangesAsync();
        var sale = await CreateService(context).CreateAsync(
            TestDate,
            distributor.Id,
            quantity: 2,
            Guid.NewGuid());
        var handoverService = new CashHandoverService(
            context,
            new FixedBusinessClock(TestDate));

        var draft = await handoverService.GetDailyAsync(TestDate);

        Assert.Equal(55m, draft.AutomaticTotal);
        Assert.Equal(55m, draft.Total);
        Assert.Equal(2, draft.AutomaticItems.Count);
        var saleLine = Assert.Single(
            draft.AutomaticItems,
            value => value.SourceCashSaleId == sale.Id);
        Assert.Equal(15m, saleLine.Amount);
        Assert.Null(saleLine.SourceDeliveryId);
        Assert.Equal(SubscriberPaymentMethod.Cash, saleLine.PaymentMethod);

        var delivered = await handoverService.SaveDailyAsync(
            TestDate,
            new CashHandoverUpdate(
                [new CashHandoverItemInput("Manual cash", 5m)],
                CashHandoverStatus.Delivered));

        Assert.Equal(5m, delivered.ManualTotal);
        Assert.Equal(55m, delivered.AutomaticTotal);
        Assert.Equal(60m, delivered.Total);
        Assert.Equal(
            60m,
            (await context.CashHandovers.AsNoTracking().SingleAsync()).Total);
    }

    [Fact]
    public async Task PaymentTracking_IncludesOnlyActiveSalesInMonthAndDistributorFilter()
    {
        await using var context = CreateContext();
        var firstDistributor = CreateDistributor("First Distributor", 1.50m);
        var secondDistributor = CreateDistributor("Second Distributor", 99m);
        var subscriber = new Subscriber
        {
            Name = "Tracked Subscriber",
            Distributor = firstDistributor,
            MonthlyFee = 40m
        };
        context.AddRange(
            subscriber,
            secondDistributor,
            CreateCompanySettings(6m));
        await context.SaveChangesAsync();
        context.SubscriberDailyDeliveries.Add(new SubscriberDailyDelivery
        {
            SubscriberId = subscriber.Id,
            DistributorId = firstDistributor.Id,
            DistributorName = firstDistributor.Name,
            Date = TestDate,
            NewspaperCount = 1,
            IsCollected = true,
            Amount = 40m,
            PaymentMethod = SubscriberPaymentMethod.Cash,
            CoveredDates =
            [
                new SubscriberDailyDeliveryCoveredDate { CoveredDate = TestDate }
            ]
        });
        await context.SaveChangesAsync();
        var saleService = CreateService(context);
        var firstSale = await saleService.CreateAsync(
            TestDate,
            firstDistributor.Id,
            quantity: 2,
            Guid.NewGuid());
        var secondSale = await saleService.CreateAsync(
            TestDate,
            secondDistributor.Id,
            quantity: 1,
            Guid.NewGuid());
        await saleService.CreateAsync(
            TestDate.AddMonths(-1),
            firstDistributor.Id,
            quantity: 1,
            Guid.NewGuid());
        var cancelledSale = await saleService.CreateAsync(
            TestDate.AddDays(-1),
            firstDistributor.Id,
            quantity: 3,
            Guid.NewGuid());
        await saleService.CancelAsync(cancelledSale.Id);
        var trackingService = new PaymentTrackingService(context);

        var all = await trackingService.GetMonthlyAsync(2026, 7);
        var filtered = await trackingService.GetMonthlyAsync(
            2026,
            7,
            firstDistributor.Id);

        Assert.Equal(58m, all.Summary.CashCollectionTotal);
        Assert.Equal(3, all.Summary.CashCollectionCount);
        var allSales = all.CashCollections
            .Where(value => value.IsCashSale)
            .ToArray();
        Assert.Equal(2, allSales.Length);
        Assert.Contains(allSales, value =>
            value.Id == firstSale.Id &&
            value.Amount == 12m &&
            value.SubscriberId is null &&
            value.DistributorId == firstDistributor.Id);
        Assert.Contains(allSales, value =>
            value.Id == secondSale.Id &&
            value.Amount == 6m &&
            value.SubscriberId is null &&
            value.DistributorId == secondDistributor.Id);
        Assert.DoesNotContain(
            all.CashCollections,
            value => value.IsCashSale && value.Id == cancelledSale.Id);

        Assert.Equal(52m, filtered.Summary.CashCollectionTotal);
        Assert.Equal(2, filtered.Summary.CashCollectionCount);
        var filteredSale = Assert.Single(
            filtered.CashCollections,
            value => value.IsCashSale);
        Assert.Equal(firstSale.Id, filteredSale.Id);
        Assert.Equal(firstDistributor.Id, filteredSale.DistributorId);
    }

    private static NewspaperCashSaleService CreateService(AppDbContext context) =>
        new(context, new FixedBusinessClock(TestDate));

    private static Distributor CreateDistributor(string name, decimal price) =>
        new()
        {
            Name = name,
            Address = "Test address",
            Phone = "5550000000",
            Zone = DistributorZone.Region1,
            NewspaperPrice = price,
            IsActive = true
        };

    private static CompanySettings CreateCompanySettings(decimal unitPrice) =>
        new()
        {
            NewspaperUnitPrice = unitPrice
        };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"gazete-cash-sale-tests-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FixedBusinessClock(DateOnly today) : IBusinessClock
    {
        public DateTimeOffset UtcNow { get; } =
            new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        public DateOnly Today { get; } = today;
    }
}

public sealed class NewspaperCashSaleEndpointTests
{
    private static readonly DateOnly TestDate = new(2026, 7, 31);

    [Fact]
    public async Task DeliveriesPage_RendersCashSaleDialogWithServerUnitPrice()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var distributorId = await SeedDistributorAsync(
            factory,
            "Web Cash Distributor",
            distributorCost: 2.10m,
            companyUnitPrice: 7.35m);

        using var response = await client.GetAsync(
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-cash-sale-open", html, StringComparison.Ordinal);
        Assert.Contains("data-cash-sale-dialog", html, StringComparison.Ordinal);
        Assert.Contains(
            "action=\"/deliveries/cash-sale\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains("Nakit Satış", html, StringComparison.Ordinal);
        Assert.Contains(
            $"value=\"{distributorId}\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-unit-price=\"7.35\"",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CashSalePost_UsesServerPriceAndRedirects_ThenClosedCashShowsError()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var distributorId = await SeedDistributorAsync(
            factory,
            "Posted Cash Distributor",
            distributorCost: 1.75m,
            companyUnitPrice: 9.15m);
        var pagePath = $"/deliveries?date={TestDate:yyyy-MM-dd}";
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, pagePath);

        using var response = await client.PostAsync(
            "/deliveries/cash-sale",
            CashSaleForm(
                antiforgeryToken,
                distributorId,
                quantity: 3,
                Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            pagePath,
            response.Headers.Location?.OriginalString);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persisted = await dbContext.NewspaperCashSales
                .AsNoTracking()
                .SingleAsync();
            Assert.Equal(TestDate, persisted.Date);
            Assert.Equal(distributorId, persisted.DistributorId);
            Assert.Equal(3, persisted.Quantity);
            Assert.Equal(9.15m, persisted.UnitPrice);
            Assert.Equal(27.45m, persisted.Amount);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.CashHandovers.Add(new CashHandover
            {
                Date = TestDate,
                Status = CashHandoverStatus.Delivered,
                Total = 27.45m,
                DeliveredAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        antiforgeryToken = await GetAntiforgeryTokenAsync(client, pagePath);
        using var blockedResponse = await client.PostAsync(
            "/deliveries/cash-sale",
            CashSaleForm(
                antiforgeryToken,
                distributorId,
                quantity: 1,
                Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Redirect, blockedResponse.StatusCode);
        Assert.Equal(
            pagePath,
            blockedResponse.Headers.Location?.OriginalString);
        using var errorPage = await client.GetAsync(
            blockedResponse.Headers.Location);
        errorPage.EnsureSuccessStatusCode();
        var errorHtml = await errorPage.Content.ReadAsStringAsync();
        Assert.Contains(
            "class=\"feedback error\"",
            errorHtml,
            StringComparison.Ordinal);
        Assert.Contains("kasa", errorHtml, StringComparison.OrdinalIgnoreCase);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await dbContext.NewspaperCashSales.CountAsync());
        }
    }

    [Fact]
    public async Task CompanySettingsPage_RendersPersistedNewspaperUnitPrice()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.CompanySettings.Add(new CompanySettings
            {
                NewspaperUnitPrice = 13.40m
            });
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.GetAsync("/menu/company/settings");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "name=\"NewspaperUnitPrice\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "value=\"13.40\"",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompanySettingsPost_ParsesInvariantPriceAndPersistsRoundedValue()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        const string pagePath = "/menu/company/settings";
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, pagePath);

        using var response = await client.PostAsync(
            "/menu/company/settings/save",
            CompanySettingsForm(antiforgeryToken, "12.345"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(pagePath, response.Headers.Location?.OriginalString);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await dbContext.CompanySettings
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(12.35m, settings.NewspaperUnitPrice);
    }

    [Fact]
    public async Task CompanySettingsPost_RejectsZeroPrice_AndPreservesStoredValue()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.CompanySettings.Add(new CompanySettings
            {
                NewspaperUnitPrice = 11.20m
            });
            await dbContext.SaveChangesAsync();
        }

        const string pagePath = "/menu/company/settings";
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, pagePath);
        using var response = await client.PostAsync(
            "/menu/company/settings/save",
            CompanySettingsForm(antiforgeryToken, "0"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "Gazete birim sat",
            html,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "value=\"0.00\"",
            html,
            StringComparison.Ordinal);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext =
            verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await verificationContext.CompanySettings
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(11.20m, settings.NewspaperUnitPrice);
    }

    [Fact]
    public async Task CompanySettingsPost_RequiresNewspaperUnitPrice()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        const string pagePath = "/menu/company/settings";
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, pagePath);
        using var response = await client.PostAsync(
            "/menu/company/settings/save",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", antiforgeryToken)
            ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
        Assert.Contains(
            "Gazete birim satış fiyatı zorunludur.",
            html,
            StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.CompanySettings.AsNoTracking().ToListAsync());
    }

    private static HttpClient CreateClient(GazeteWebFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

    private static async Task<int> SeedDistributorAsync(
        GazeteWebFactory factory,
        string name,
        decimal distributorCost,
        decimal companyUnitPrice)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var distributor = new Distributor
        {
            Name = name,
            Address = "HTTP test address",
            Phone = "5550000001",
            Zone = DistributorZone.Region1,
            NewspaperPrice = distributorCost,
            IsActive = true
        };
        dbContext.AddRange(
            distributor,
            new CompanySettings
            {
                NewspaperUnitPrice = companyUnitPrice
            });
        await dbContext.SaveChangesAsync();
        return distributor.Id;
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

    private static FormUrlEncodedContent CashSaleForm(
        string antiforgeryToken,
        int distributorId,
        int quantity,
        Guid idempotencyKey) =>
        new(
        [
            new("__RequestVerificationToken", antiforgeryToken),
            new("Date", TestDate.ToString("yyyy-MM-dd")),
            new("DistributorId", distributorId.ToString()),
            new("Quantity", quantity.ToString()),
            new("IdempotencyKey", idempotencyKey.ToString())
        ]);

    private static FormUrlEncodedContent CompanySettingsForm(
        string antiforgeryToken,
        string newspaperUnitPrice) =>
        new(
        [
            new("__RequestVerificationToken", antiforgeryToken),
            new("NewspaperUnitPrice", newspaperUnitPrice)
        ]);
}
