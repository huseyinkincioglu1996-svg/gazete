using System.Net;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Controllers;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class ReportsSubscriberPaymentTotalsTests
{
    private static readonly DateOnly JulyDate = new(2026, 7, 15);
    private static readonly DateOnly AugustDate = new(2026, 8, 15);

    [Fact]
    public async Task ReportModel_SumsDueAmountsAcrossSchedulesWithinSelectedMonth()
    {
        await using var context = CreateContext();
        await SeedScheduleScenarioAsync(context);
        var controller = CreateController(context);

        var july = await GetModelAsync(controller, JulyDate);
        var august = await GetModelAsync(controller, AugustDate);

        Assert.Equal(794.50m, july.SubscriberDueTotal);
        Assert.Equal(1_294.75m, august.SubscriberDueTotal);
    }

    [Fact]
    public async Task ReportModel_UsesActiveDeferralEffectiveDateForDueMonth()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriber(
            "Ertelenen Aylık Abone",
            new DateOnly(2026, 7, 1),
            CreateMonthlyPeriod("Aylık 450", 450m));
        subscriber.PaymentDeferrals.Add(new SubscriberPaymentDeferral
        {
            OriginalDueDate = new DateOnly(2026, 7, 15),
            PreviousDueDate = new DateOnly(2026, 7, 15),
            DeferredUntil = new DateOnly(2026, 8, 5),
            Reason = "Ağustos ayına ertelendi"
        });
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var july = await GetModelAsync(controller, JulyDate);
        var august = await GetModelAsync(controller, AugustDate);

        Assert.Equal(0m, july.SubscriberDueTotal);
        Assert.Equal(900m, august.SubscriberDueTotal);
    }

    [Fact]
    public async Task ReportModel_SumsCollectionsByBusinessMonthAndIncreasesWithNewRecord()
    {
        await using var context = CreateContext();
        var subscriber = await SeedCollectionScenarioAsync(context);
        var controller = CreateController(context);

        var julyBefore = await GetModelAsync(controller, JulyDate);
        var august = await GetModelAsync(controller, AugustDate);

        Assert.Equal(123.45m, julyBefore.SubscriberCollectedTotal);
        Assert.Equal(300m, august.SubscriberCollectedTotal);

        context.SubscriberDailyDeliveries.Add(CreateCollection(
            subscriber,
            new DateOnly(2026, 7, 25),
            40m,
            SubscriberPaymentMethod.Transfer,
            new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.Zero)));
        await context.SaveChangesAsync();

        var julyAfter = await GetModelAsync(controller, JulyDate);

        Assert.Equal(163.45m, julyAfter.SubscriberCollectedTotal);
    }

    [Fact]
    public async Task ReportsView_RendersSubscriberMonthlyTotalsInStableTestElements()
    {
        await using var factory = new GazeteWebFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SeedScheduleScenarioAsync(context);
            await SeedCollectionScenarioAsync(context);
        }
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            $"/reports?date={JulyDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var collectedPosition = html.IndexOf(
            "data-testid=\"subscriber-monthly-collected\"",
            StringComparison.Ordinal);
        var duePosition = html.IndexOf(
            "data-testid=\"subscriber-monthly-due\"",
            StringComparison.Ordinal);
        var dueText = ExtractDataTestText(html, "subscriber-monthly-due");
        var collectedText = ExtractDataTestText(
            html,
            "subscriber-monthly-collected");
        Assert.True(
            collectedPosition >= 0 && collectedPosition < duePosition,
            "Toplanan tutar, toplanması gereken tutarın üstünde görünmelidir.");
        Assert.Contains("794,50", dueText, StringComparison.Ordinal);
        Assert.Contains("123,45", collectedText, StringComparison.Ordinal);
    }

    private static ReportsController CreateController(AppDbContext context) =>
        new(
            context,
            new ReportService(context),
            new FixedBusinessClock(JulyDate));

    private static async Task<ReportsPageViewModel> GetModelAsync(
        ReportsController controller,
        DateOnly date)
    {
        var result = await controller.Index(date, CancellationToken.None);
        var view = Assert.IsType<ViewResult>(result);
        return Assert.IsType<ReportsPageViewModel>(view.Model);
    }

    private static async Task SeedScheduleScenarioAsync(AppDbContext context)
    {
        context.Subscribers.AddRange(
            CreateSubscriber(
                "Günlük Abone",
                new DateOnly(2026, 7, 30),
                CreateDailyPeriod("Günlük 17,25", 17.25m)),
            CreateSubscriber(
                "On Günlük Abone",
                new DateOnly(2026, 7, 1),
                CreateTenDayPeriod("On Günlük 100", 100m)),
            CreateSubscriber(
                "Aylık Abone",
                new DateOnly(2026, 7, 1),
                CreateMonthlyPeriod("Aylık 450", 450m)));
        await context.SaveChangesAsync();
    }

    private static async Task<Subscriber> SeedCollectionScenarioAsync(
        AppDbContext context)
    {
        var subscriber = new Subscriber
        {
            Name = $"Tahsilat Abonesi {Guid.NewGuid():N}",
            MonthlyFee = 0m,
            IsActive = true,
            CreatedAt = new DateTimeOffset(
                new DateOnly(2026, 7, 1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero)
        };
        subscriber.DailyDeliveries.Add(CreateCollection(
            subscriber,
            new DateOnly(2026, 6, 30),
            100m,
            SubscriberPaymentMethod.Cash,
            new DateTimeOffset(2026, 6, 30, 22, 30, 0, TimeSpan.Zero)));
        subscriber.DailyDeliveries.Add(CreateCollection(
            subscriber,
            new DateOnly(2026, 7, 20),
            23.45m,
            SubscriberPaymentMethod.Card,
            collectedAt: null));
        subscriber.DailyDeliveries.Add(CreateCollection(
            subscriber,
            new DateOnly(2026, 7, 31),
            300m,
            SubscriberPaymentMethod.Cash,
            new DateTimeOffset(2026, 7, 31, 21, 30, 0, TimeSpan.Zero)));
        subscriber.DailyDeliveries.Add(new SubscriberDailyDelivery
        {
            Subscriber = subscriber,
            Date = new DateOnly(2026, 7, 5),
            NewspaperCount = 1,
            IsCollected = false,
            Amount = 999m,
            PaymentMethod = SubscriberPaymentMethod.Cash
        });

        var distributor = new Distributor
        {
            Name = $"Nakit Satış Dağıtıcısı {Guid.NewGuid():N}",
            Phone = "555",
            Address = "Adres",
            IsActive = true,
            NewspaperPrice = 5m
        };
        context.NewspaperCashSales.Add(new NewspaperCashSale
        {
            Date = new DateOnly(2026, 7, 12),
            Distributor = distributor,
            DistributorName = distributor.Name,
            Quantity = 1,
            UnitPrice = 777m,
            Amount = 777m,
            IdempotencyKey = Guid.NewGuid()
        });
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        return subscriber;
    }

    private static Subscriber CreateSubscriber(
        string name,
        DateOnly paymentPeriodStartedOn,
        PaymentPeriod paymentPeriod) =>
        new()
        {
            Name = name,
            MonthlyFee = paymentPeriod.CollectionAmount ?? 0m,
            IsActive = true,
            PaymentPeriodStartedOn = paymentPeriodStartedOn,
            CreatedAt = new DateTimeOffset(
                paymentPeriodStartedOn.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero),
            PaymentPeriod = paymentPeriod
        };

    private static PaymentPeriod CreateDailyPeriod(
        string name,
        decimal collectionAmount) =>
        new()
        {
            Name = name,
            Frequency = PaymentPeriodFrequency.Daily,
            DayCount = 1,
            CollectionDayOfMonth = 1,
            CollectionTime = new TimeOnly(9, 0),
            CollectionAmount = collectionAmount,
            IsActive = true
        };

    private static PaymentPeriod CreateTenDayPeriod(
        string name,
        decimal collectionAmount) =>
        new()
        {
            Name = name,
            Frequency = PaymentPeriodFrequency.Monthly,
            DayCount = 10,
            CollectionDayOfMonth = 10,
            CollectionTime = new TimeOnly(9, 0),
            CollectionAmount = collectionAmount,
            IsActive = true
        };

    private static PaymentPeriod CreateMonthlyPeriod(
        string name,
        decimal collectionAmount) =>
        new()
        {
            Name = name,
            Frequency = PaymentPeriodFrequency.Monthly,
            DayCount = 30,
            CollectionDayOfMonth = 15,
            CollectionTime = new TimeOnly(9, 0),
            CollectionAmount = collectionAmount,
            IsActive = true
        };

    private static SubscriberDailyDelivery CreateCollection(
        Subscriber subscriber,
        DateOnly date,
        decimal amount,
        SubscriberPaymentMethod paymentMethod,
        DateTimeOffset? collectedAt) =>
        new()
        {
            Subscriber = subscriber,
            Date = date,
            NewspaperCount = 1,
            IsCollected = true,
            CollectedAt = collectedAt,
            Amount = amount,
            PaymentMethod = paymentMethod
        };

    private static string ExtractDataTestText(string html, string testId)
    {
        var match = Regex.Match(
            html,
            $"""
             <(?<tag>[a-z][a-z0-9]*)\b
             [^>]*\bdata-testid="{Regex.Escape(testId)}"[^>]*
             >
             (?<content>.*?)
             </\k<tag>>
             """,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"data-testid '{testId}' was not found.");
        var withoutTags = Regex.Replace(
            match.Groups["content"].Value,
            "<[^>]+>",
            " ",
            RegexOptions.CultureInvariant);
        return Regex.Replace(
                WebUtility.HtmlDecode(withoutTags),
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"gazete-report-subscriber-totals-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FixedBusinessClock(DateOnly today) : IBusinessClock
    {
        public DateTimeOffset UtcNow { get; } =
            new(
                today.ToDateTime(new TimeOnly(9, 0)),
                TimeSpan.Zero);

        public DateOnly Today { get; } = today;
    }
}
