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

public sealed class WeeklyPaymentScheduleDomainTests
{
    private static readonly DateOnly PlanStart = new(2026, 7, 31);

    [Theory]
    [InlineData(2026, 7, 31)]
    [InlineData(2026, 8, 7)]
    [InlineData(2026, 8, 14)]
    public async Task WeeklySchedule_IsDueOnStartAndEverySevenDaysAcrossMonthBoundary(
        int year,
        int month,
        int day)
    {
        await using var context = CreateContext();
        var subscriber = CreateWeeklySubscriber(
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
        Assert.Equal(120.75m, row.Amount);
    }

    [Fact]
    public async Task WeeklySchedule_InterimDeliveryDayIsNotPaymentDue()
    {
        await using var context = CreateContext();
        var subscriber = CreateWeeklySubscriber(
            PlanStart,
            newspaperDay: NewspaperDay.Monday);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);
        var interimMonday = new DateOnly(2026, 8, 3);

        var row = Assert.Single(
            (await service.GetDailyAsync(interimMonday)).Records);

        Assert.True(row.HasDelivery);
        Assert.True(row.IsScheduled);
        Assert.False(row.IsPaymentDue);
        Assert.False(row.ShowPaymentControls);
    }

    [Fact]
    public async Task WeeklySchedule_ListsAndPersistsPaymentWithoutNewspaperDelivery()
    {
        await using var context = CreateContext();
        var subscriber = CreateWeeklySubscriber(
            PlanStart,
            newspaperDay: NewspaperDay.Monday);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);
        var secondDueDate = PlanStart.AddDays(7);

        var row = Assert.Single(
            (await service.GetDailyAsync(secondDueDate)).Records);

        Assert.False(row.HasDelivery);
        Assert.False(row.IsScheduled);
        Assert.False(row.IsDelivered);
        Assert.True(row.IsPaymentDue);
        Assert.True(row.ShowPaymentControls);
        Assert.Equal(120.75m, row.Amount);

        var collected = await service.SaveDailyRowAsync(
            secondDueDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: true));

        Assert.Equal(120.75m, Assert.Single(collected.Records).Amount);
        var persisted = await context.SubscriberDailyDeliveries
            .AsNoTracking()
            .SingleAsync();
        Assert.True(persisted.IsCollected);
        Assert.Equal(120.75m, persisted.Amount);
        Assert.Equal(7, persisted.CollectionDayCount);
        Assert.Equal("Haftalık Tahsilat", persisted.CollectionPeriodName);
    }

    private static Subscriber CreateWeeklySubscriber(
        DateOnly createdOn,
        NewspaperDay newspaperDay)
    {
        var subscriber = new Subscriber
        {
            Name = "Haftalık Ödeme Abonesi",
            MonthlyFee = 517.50m,
            IsActive = true,
            CreatedAt = new DateTimeOffset(
                createdOn.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero),
            PaymentPeriodStartedOn = createdOn,
            PaymentPeriod = new PaymentPeriod
            {
                Name = "Haftalık Tahsilat",
                Frequency = PaymentPeriodFrequency.Weekly,
                DayCount = 7,
                CollectionDayOfMonth = 1,
                CollectionTime = new TimeOnly(8, 45),
                CollectionAmount = 120.75m,
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
                $"gazete-weekly-payment-tests-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }
}

public sealed class WeeklyPaymentScheduleEndpointTests
{
    [Fact]
    public async Task CreateWeeklyPeriod_NormalizesScheduleAndShowsWeeklyIndexText()
    {
        await using var factory = new GazeteWebFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        var antiforgeryToken = await GetAntiforgeryTokenAsync(
            client,
            "/settings/create");

        using var response = await client.PostAsync(
            "/settings/create",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", antiforgeryToken),
                new("Name", "HTTP Haftalık Tahsilat"),
                new("ScheduleType", "weekly"),
                new("CollectionDayOfMonth", ""),
                new("CollectionTime", "08:45"),
                new("DayCount", "31"),
                new("CollectionAmount", "120.75"),
                new("Description", "Başlangıçtan itibaren yedi günde bir tahsilat"),
                new("IsActive", "true")
            ]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var period = await dbContext.PaymentPeriods
                .AsNoTracking()
                .SingleAsync();
            Assert.Equal("HTTP Haftalık Tahsilat", period.Name);
            Assert.Equal(PaymentPeriodFrequency.Weekly, period.Frequency);
            Assert.Equal(7, period.DayCount);
            Assert.Equal(1, period.CollectionDayOfMonth);
            Assert.Equal(new TimeOnly(8, 45), period.CollectionTime);
            Assert.Equal(120.75m, period.CollectionAmount);
            Assert.True(period.IsActive);
        }

        var indexHtml = WebUtility.HtmlDecode(
            await client.GetStringAsync("/settings"));
        Assert.Contains(
            "HTTP Haftalık Tahsilat",
            indexHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Başlangıçtan itibaren her 7 gün",
            indexHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Her ödeme 7 gün",
            indexHtml,
            StringComparison.Ordinal);
        Assert.Contains("08:45", indexHtml, StringComparison.Ordinal);
        Assert.Contains("120,75", indexHtml, StringComparison.Ordinal);
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
}
