using System.Net;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GazeteDagitim.Tests;

public sealed class DailyPaymentScheduleDomainTests
{
    private static readonly DateOnly PlanStart = new(2026, 7, 31);

    [Theory]
    [InlineData(2026, 7, 31)]
    [InlineData(2026, 8, 1)]
    [InlineData(2026, 8, 2)]
    [InlineData(2026, 8, 3)]
    public async Task DailySchedule_IsDueEveryCalendarDayAcrossWeekendAndMonthBoundary(
        int year,
        int month,
        int day)
    {
        await using var context = CreateContext();
        var subscriber = CreateDailySubscriber(
            PlanStart,
            newspaperDay: NewspaperDay.Monday);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);
        var selectedDate = new DateOnly(year, month, day);

        var row = Assert.Single(
            (await service.GetDailyAsync(selectedDate)).Records);

        Assert.True(row.IsPaymentDue);
        Assert.True(row.ShowPaymentControls);
        Assert.Equal(17.25m, row.Amount);
    }

    [Fact]
    public async Task DailySchedule_BeginsOnAssignedPaymentPeriodStart()
    {
        await using var context = CreateContext();
        var subscriber = CreateDailySubscriber(
            new DateOnly(2026, 7, 1),
            newspaperDay: NewspaperDay.Monday);
        subscriber.PaymentPeriodStartedOn = PlanStart;
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        var beforeStart = Assert.Single(
            (await service.GetDailyAsync(new DateOnly(2026, 7, 27))).Records);
        var onStart = Assert.Single(
            (await service.GetDailyAsync(PlanStart)).Records);

        Assert.False(beforeStart.IsPaymentDue);
        Assert.False(beforeStart.ShowPaymentControls);
        Assert.True(onStart.IsPaymentDue);
        Assert.True(onStart.ShowPaymentControls);
        Assert.Equal(17.25m, onStart.Amount);
    }

    [Fact]
    public async Task DailySchedule_ListsPaymentControlsWithoutNewspaperDelivery()
    {
        await using var context = CreateContext();
        var subscriber = CreateDailySubscriber(
            PlanStart,
            newspaperDay: NewspaperDay.Monday);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);
        var sunday = new DateOnly(2026, 8, 2);

        var row = Assert.Single((await service.GetDailyAsync(sunday)).Records);

        Assert.False(row.HasDelivery);
        Assert.False(row.IsScheduled);
        Assert.False(row.IsDelivered);
        Assert.True(row.IsPaymentDue);
        Assert.True(row.ShowPaymentControls);
        Assert.Equal(17.25m, row.Amount);

        var collected = await service.SaveDailyRowAsync(
            sunday,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: true));

        Assert.Equal(17.25m, Assert.Single(collected.Records).Amount);
        var persisted = await context.SubscriberDailyDeliveries
            .AsNoTracking()
            .SingleAsync();
        Assert.True(persisted.IsCollected);
        Assert.Equal(17.25m, persisted.Amount);
        Assert.Equal(1, persisted.CollectionDayCount);
        Assert.Equal("Günlük Tahsilat", persisted.CollectionPeriodName);
    }

    private static Subscriber CreateDailySubscriber(
        DateOnly createdOn,
        NewspaperDay newspaperDay)
    {
        var subscriber = new Subscriber
        {
            Name = "Günlük Ödeme Abonesi",
            MonthlyFee = 517.50m,
            IsActive = true,
            CreatedAt = new DateTimeOffset(
                createdOn.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero),
            PaymentPeriod = new PaymentPeriod
            {
                Name = "Günlük Tahsilat",
                Frequency = PaymentPeriodFrequency.Daily,
                DayCount = 1,
                CollectionDayOfMonth = 1,
                CollectionTime = new TimeOnly(8, 45),
                CollectionAmount = 17.25m,
                IsActive = true
            }
        };
        subscriber.NewspaperDays.Add(new SubscriberPublicationDay
        {
            Day = newspaperDay
        });
        return subscriber;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"gazete-daily-payment-tests-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }
}

public sealed class DailyPaymentScheduleEndpointTests
{
    [Fact]
    public async Task CreateDailyPeriod_AllowsBlankCollectionDay_AndNormalizesSchedule()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(
            client,
            "/settings/create");

        using var response = await client.PostAsync(
            "/settings/create",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", antiforgeryToken),
                new("Name", "HTTP Günlük Tahsilat"),
                new("ScheduleType", "daily"),
                new("CollectionDayOfMonth", ""),
                new("CollectionTime", "08:45"),
                new("DayCount", "30"),
                new("CollectionAmount", "17.25"),
                new("Description", "Her takvim günü tahsilat"),
                new("IsActive", "true")
            ]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var period = await dbContext.PaymentPeriods
                .AsNoTracking()
                .SingleAsync();
            Assert.Equal("HTTP Günlük Tahsilat", period.Name);
            Assert.Equal(PaymentPeriodFrequency.Daily, period.Frequency);
            Assert.Equal(1, period.DayCount);
            Assert.Equal(1, period.CollectionDayOfMonth);
            Assert.Equal(new TimeOnly(8, 45), period.CollectionTime);
            Assert.Equal(17.25m, period.CollectionAmount);
            Assert.True(period.IsActive);
        }

        var indexHtml = WebUtility.HtmlDecode(
            await client.GetStringAsync("/settings"));
        Assert.Contains(
            "HTTP Günlük Tahsilat",
            indexHtml,
            StringComparison.Ordinal);
        Assert.Contains("Her gün", indexHtml, StringComparison.Ordinal);
        Assert.Contains("08:45", indexHtml, StringComparison.Ordinal);
        Assert.Contains("17,25", indexHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubscriberCreateAndEdit_SynchronizePaymentPeriodStartDate()
    {
        var today = new DateOnly(2026, 8, 2);
        await using var sourceFactory = new GazeteWebFactory();
        await using var factory = sourceFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBusinessClock>();
                services.AddSingleton<IBusinessClock>(
                    new FixedBusinessClock(today));
            });
        });
        using var client = CreateClient(factory);
        int firstPeriodId;
        int secondPeriodId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var firstPeriod = CreateDailyPeriod("Birinci Günlük Periyot");
            var secondPeriod = CreateDailyPeriod("İkinci Günlük Periyot");
            dbContext.PaymentPeriods.AddRange(firstPeriod, secondPeriod);
            await dbContext.SaveChangesAsync();
            firstPeriodId = firstPeriod.Id;
            secondPeriodId = secondPeriod.Id;
        }

        var antiforgeryToken = await GetAntiforgeryTokenAsync(
            client,
            "/subscribers/create");
        using var createResponse = await client.PostAsync(
            "/subscribers/create",
            SubscriberForm(
                antiforgeryToken,
                ("Name", "Başlangıç Tarihli Abone"),
                ("MonthlyFee", "517.50"),
                ("IsActive", "true"),
                ("PaymentPeriodId", firstPeriodId.ToString())));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        int subscriberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscriber = await dbContext.Subscribers.SingleAsync();
            Assert.Equal(firstPeriodId, subscriber.PaymentPeriodId);
            Assert.Equal(today, subscriber.PaymentPeriodStartedOn);
            subscriberId = subscriber.Id;
            subscriber.PaymentPeriodStartedOn = today.AddDays(-10);
            await dbContext.SaveChangesAsync();
        }

        antiforgeryToken = await GetAntiforgeryTokenAsync(
            client,
            $"/subscribers/{subscriberId}/edit");
        using var changeResponse = await client.PostAsync(
            $"/subscribers/{subscriberId}/edit",
            SubscriberForm(
                antiforgeryToken,
                ("Id", subscriberId.ToString()),
                ("Name", "Başlangıç Tarihli Abone"),
                ("MonthlyFee", "517.50"),
                ("IsActive", "true"),
                ("PaymentPeriodId", secondPeriodId.ToString())));

        Assert.Equal(HttpStatusCode.Redirect, changeResponse.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscriber = await dbContext.Subscribers
                .AsNoTracking()
                .SingleAsync();
            Assert.Equal(secondPeriodId, subscriber.PaymentPeriodId);
            Assert.Equal(today, subscriber.PaymentPeriodStartedOn);
        }

        antiforgeryToken = await GetAntiforgeryTokenAsync(
            client,
            $"/subscribers/{subscriberId}/edit");
        using var clearResponse = await client.PostAsync(
            $"/subscribers/{subscriberId}/edit",
            SubscriberForm(
                antiforgeryToken,
                ("Id", subscriberId.ToString()),
                ("Name", "Başlangıç Tarihli Abone"),
                ("MonthlyFee", "517.50"),
                ("IsActive", "true"),
                ("PaymentPeriodId", string.Empty)));

        Assert.Equal(HttpStatusCode.Redirect, clearResponse.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscriber = await dbContext.Subscribers
                .AsNoTracking()
                .SingleAsync();
            Assert.Null(subscriber.PaymentPeriodId);
            Assert.Null(subscriber.PaymentPeriodStartedOn);
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

    private static PaymentPeriod CreateDailyPeriod(string name) =>
        new()
        {
            Name = name,
            Frequency = PaymentPeriodFrequency.Daily,
            DayCount = 1,
            CollectionDayOfMonth = 1,
            CollectionTime = new TimeOnly(8, 45),
            CollectionAmount = 17.25m,
            IsActive = true
        };

    private static FormUrlEncodedContent SubscriberForm(
        string antiforgeryToken,
        params (string Key, string Value)[] values) =>
        new(
            new[]
            {
                new KeyValuePair<string, string>(
                    "__RequestVerificationToken",
                    antiforgeryToken)
            }.Concat(
                values.Select(value =>
                    new KeyValuePair<string, string>(
                        value.Key,
                        value.Value))));

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

    private sealed class FixedBusinessClock(DateOnly today) : IBusinessClock
    {
        public DateTimeOffset UtcNow { get; } =
            new(
                today.ToDateTime(new TimeOnly(9, 0)),
                TimeSpan.Zero);

        public DateOnly Today { get; } = today;
    }
}
