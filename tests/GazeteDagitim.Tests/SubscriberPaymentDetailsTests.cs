using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class SubscriberPaymentDetailsDomainTests
{
    [Fact]
    public async Task PaymentDay_UsesLastDayOfShortMonth()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriberWithPlan(
            createdOn: new DateOnly(2026, 2, 1),
            collectionDayOfMonth: 31,
            collectionAmount: 250m);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberPaymentDetailsService(
            context,
            new FixedBusinessClock(new DateOnly(2026, 2, 15)));

        var details = await service.GetAsync(subscriber.Id);

        Assert.NotNull(details.Plan);
        Assert.NotNull(details.NextDue);
        Assert.Equal(new DateOnly(2026, 2, 28), details.NextDue.OriginalDueDate);
        Assert.Equal(new DateOnly(2026, 2, 28), details.NextDue.EffectiveDueDate);
        Assert.Contains(
            details.Movements,
            movement =>
                movement.Type == SubscriberPaymentMovementType.Due &&
                movement.Date == new DateOnly(2026, 2, 28));
    }

    [Fact]
    public async Task Collection_ReducesOutstandingBalanceAndCreatesHistory()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriberWithPlan(
            createdOn: new DateOnly(2026, 1, 1),
            collectionDayOfMonth: 15,
            collectionAmount: 100m);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var clock = new FixedBusinessClock(new DateOnly(2026, 3, 20));
        var service = new SubscriberPaymentDetailsService(context, clock);

        var beforeCollection = await service.GetAsync(subscriber.Id);

        context.SubscriberDailyDeliveries.Add(new SubscriberDailyDelivery
        {
            SubscriberId = subscriber.Id,
            Date = new DateOnly(2026, 3, 18),
            IsCollected = true,
            CollectedAt = clock.UtcNow,
            Amount = 75m,
            PaymentMethod = SubscriberPaymentMethod.Cash,
            CollectionPeriodName = subscriber.PaymentPeriod!.Name,
            CollectionDayCount = subscriber.PaymentPeriod.DayCount
        });
        await context.SaveChangesAsync();

        var afterCollection = await service.GetAsync(subscriber.Id);

        Assert.Equal(300m, beforeCollection.ExpectedTotal);
        Assert.Equal(300m, beforeCollection.OutstandingBalance);
        Assert.Equal(300m, afterCollection.ExpectedTotal);
        Assert.Equal(75m, afterCollection.CollectedTotal);
        Assert.Equal(225m, afterCollection.OutstandingBalance);
        var collection = Assert.Single(afterCollection.Collections);
        Assert.Equal(75m, collection.Amount);
        Assert.Contains(
            afterCollection.Movements,
            movement =>
                movement.Type == SubscriberPaymentMovementType.Collection &&
                movement.ReducesBalance &&
                movement.Amount == 75m);
    }

    [Fact]
    public async Task DeferralAndCancellation_AreReflectedInDueDateAndHistory()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriberWithPlan(
            createdOn: new DateOnly(2026, 8, 1),
            collectionDayOfMonth: 15,
            collectionAmount: 180m);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var clock = new FixedBusinessClock(new DateOnly(2026, 8, 15));
        var service = new SubscriberPaymentDetailsService(context, clock);
        var originalDueDate = new DateOnly(2026, 8, 15);
        var deferredUntil = new DateOnly(2026, 8, 25);

        var deferred = await service.DeferAsync(
            subscriber.Id,
            originalDueDate,
            deferredUntil,
            "Abonenin talebi");

        Assert.NotNull(deferred.ActiveDeferral);
        Assert.NotNull(deferred.NextDue);
        Assert.Equal(originalDueDate, deferred.NextDue.OriginalDueDate);
        Assert.Equal(deferredUntil, deferred.NextDue.EffectiveDueDate);
        Assert.Equal(180m, deferred.ExpectedTotal);
        Assert.Equal(180m, deferred.OutstandingBalance);
        Assert.Equal(0m, deferred.OverdueBalance);
        Assert.Equal("Abonenin talebi", deferred.ActiveDeferral.Reason);
        Assert.Null(deferred.ActiveDeferral.CancelledAt);

        var cancelled = await service.CancelDeferralAsync(
            subscriber.Id,
            deferred.ActiveDeferral.Id);

        Assert.Null(cancelled.ActiveDeferral);
        Assert.NotNull(cancelled.NextDue);
        Assert.Equal(originalDueDate, cancelled.NextDue.OriginalDueDate);
        Assert.Equal(originalDueDate, cancelled.NextDue.EffectiveDueDate);
        var history = Assert.Single(cancelled.Deferrals);
        Assert.Equal(originalDueDate, history.OriginalDueDate);
        Assert.Equal(deferredUntil, history.DeferredUntil);
        Assert.NotNull(history.CancelledAt);
        Assert.Contains(
            cancelled.Movements,
            movement =>
                movement.Type ==
                SubscriberPaymentMovementType.DeferralCancellation);
    }

    [Fact]
    public async Task SubscriberWithoutPlan_ReturnsEmptyScheduleWithoutFailure()
    {
        await using var context = CreateContext();
        var subscriber = new Subscriber
        {
            Name = "Plansız Abone",
            MonthlyFee = 125m,
            IsActive = true,
            CreatedAt = ToUtcTimestamp(new DateOnly(2026, 7, 1))
        };
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberPaymentDetailsService(
            context,
            new FixedBusinessClock(new DateOnly(2026, 7, 31)));

        var details = await service.GetAsync(subscriber.Id);

        Assert.Equal(subscriber.Id, details.SubscriberId);
        Assert.Null(details.Plan);
        Assert.Null(details.NextDue);
        Assert.Null(details.ActiveDeferral);
        Assert.Equal(0m, details.ExpectedTotal);
        Assert.Equal(0m, details.OutstandingBalance);
        Assert.Empty(details.Collections);
        Assert.Empty(details.Deferrals);
        Assert.Empty(details.Movements);
    }

    [Fact]
    public async Task InactiveSubscriber_DoesNotAccrueAfterDeactivation()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriberWithPlan(
            createdOn: new DateOnly(2026, 1, 1),
            collectionDayOfMonth: 15,
            collectionAmount: 100m);
        subscriber.IsActive = false;
        subscriber.DeactivatedAt = ToUtcTimestamp(new DateOnly(2026, 2, 20));
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberPaymentDetailsService(
            context,
            new FixedBusinessClock(new DateOnly(2026, 4, 30)));

        var details = await service.GetAsync(subscriber.Id);

        Assert.Equal(200m, details.ExpectedTotal);
        Assert.Equal(200m, details.OutstandingBalance);
        Assert.DoesNotContain(
            details.Movements,
            movement => movement.Date > new DateOnly(2026, 2, 20));
    }

    [Fact]
    public async Task SubscriberWithoutPlan_DoesNotLabelCollectionAsAdvance()
    {
        await using var context = CreateContext();
        var subscriber = new Subscriber
        {
            Name = "Plansız Tahsilat Abonesi",
            MonthlyFee = 125m,
            IsActive = true,
            CreatedAt = ToUtcTimestamp(new DateOnly(2026, 7, 1))
        };
        subscriber.DailyDeliveries.Add(new SubscriberDailyDelivery
        {
            Date = new DateOnly(2026, 7, 15),
            IsCollected = true,
            CollectedAt = ToUtcTimestamp(new DateOnly(2026, 7, 15)),
            Amount = 125m,
            PaymentMethod = SubscriberPaymentMethod.Cash
        });
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberPaymentDetailsService(
            context,
            new FixedBusinessClock(new DateOnly(2026, 7, 31)));

        var details = await service.GetAsync(subscriber.Id);

        Assert.Equal(125m, details.CollectedTotal);
        Assert.Equal(0m, details.AdvanceBalance);
        Assert.Single(details.Collections);
    }

    private static Subscriber CreateSubscriberWithPlan(
        DateOnly createdOn,
        int collectionDayOfMonth,
        decimal collectionAmount) =>
        new()
        {
            Name = "Ödeme Detay Abonesi",
            MonthlyFee = collectionAmount,
            IsActive = true,
            CreatedAt = ToUtcTimestamp(createdOn),
            PaymentPeriod = new PaymentPeriod
            {
                Name = "Test Ödeme Planı",
                DayCount = 30,
                CollectionDayOfMonth = collectionDayOfMonth,
                CollectionTime = new TimeOnly(10, 30),
                CollectionAmount = collectionAmount,
                IsActive = true
            }
        };

    private static DateTimeOffset ToUtcTimestamp(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"gazete-subscriber-payment-tests-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FixedBusinessClock(DateOnly today) : IBusinessClock
    {
        public DateTimeOffset UtcNow { get; } =
            new(today.ToDateTime(new TimeOnly(9, 30)), TimeSpan.Zero);

        public DateOnly Today { get; } = today;
    }
}

public sealed class SubscriberPaymentDetailsEndpointTests
{
    [Fact]
    public async Task DetailsPageAndSubscriberList_ExposePaymentDetailsNavigation()
    {
        await using var factory = new GazeteWebFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        var subscriberId = await SeedSubscriberAsync(factory);

        using var detailsResponse = await client.GetAsync(
            $"/subscribers/{subscriberId}/payments");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();

        Assert.Equal(
            "text/html",
            detailsResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("HTTP Detay Abonesi", detailsHtml);
        Assert.Contains("subscriber-payment-page", detailsHtml);
        Assert.Contains("subscriber-payment-timeline", detailsHtml);
        Assert.Contains(
            "name=\"DeferralInput.DeferredUntil\"",
            detailsHtml,
            StringComparison.Ordinal);
        Assert.Contains("data-val=\"true\"", detailsHtml, StringComparison.Ordinal);

        using var listResponse = await client.GetAsync("/subscribers");
        listResponse.EnsureSuccessStatusCode();
        var listHtml = await listResponse.Content.ReadAsStringAsync();

        Assert.Contains("HTTP Detay Abonesi", listHtml);
        Assert.Contains(
            $"/subscribers/{subscriberId}/payments",
            listHtml,
            StringComparison.Ordinal);
        Assert.Contains("data-row-link=", listHtml, StringComparison.Ordinal);

        var token = ExtractInputValue(
            detailsHtml,
            "__RequestVerificationToken");
        var originalDueDate = DateOnly.ParseExact(
            ExtractInputValue(
                detailsHtml,
                "DeferralInput.OriginalDueDate"),
            "yyyy-MM-dd");
        var deferredUntil = DateOnly.ParseExact(
            ExtractInputAttribute(
                detailsHtml,
                "DeferralInput.DeferredUntil",
                "min"),
            "yyyy-MM-dd");
        using var deferResponse = await client.PostAsync(
            $"/subscribers/{subscriberId}/payments/defer",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new(
                    "DeferralInput.OriginalDueDate",
                    originalDueDate.ToString("yyyy-MM-dd")),
                new(
                    "DeferralInput.DeferredUntil",
                    deferredUntil.ToString("yyyy-MM-dd")),
                new("DeferralInput.Reason", "HTTP erteleme testi")
            ]));

        Assert.Equal(
            System.Net.HttpStatusCode.Redirect,
            deferResponse.StatusCode);
        Assert.Equal(
            $"/subscribers/{subscriberId}/payments",
            deferResponse.Headers.Location?.OriginalString);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();
        var deferral = await verificationContext.SubscriberPaymentDeferrals
            .SingleAsync();
        Assert.Equal(originalDueDate, deferral.OriginalDueDate);
        Assert.Equal(deferredUntil, deferral.DeferredUntil);
        Assert.Equal("HTTP erteleme testi", deferral.Reason);
    }

    private static string ExtractInputValue(string html, string inputName)
        => ExtractInputAttribute(html, inputName, "value");

    private static string ExtractInputAttribute(
        string html,
        string inputName,
        string attributeName)
    {
        var input = System.Text.RegularExpressions.Regex.Match(
            html,
            $"<input(?=[^>]*name=\"{System.Text.RegularExpressions.Regex.Escape(inputName)}\")[^>]*>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"{inputName} alanı HTML içinde bulunamadı.");

        var value = System.Text.RegularExpressions.Regex.Match(
            input.Value,
            $"{System.Text.RegularExpressions.Regex.Escape(attributeName)}=\"([^\"]*)\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.True(
            value.Success,
            $"{inputName} alanının {attributeName} niteliği bulunamadı.");
        return System.Net.WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private static async Task<int> SeedSubscriberAsync(
        GazeteWebFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscriber = new Subscriber
        {
            Name = "HTTP Detay Abonesi",
            Phone = "0555 000 00 00",
            MonthlyFee = 210m,
            IsActive = true,
            CreatedAt = ToUtcTimestamp(new DateOnly(2026, 7, 1)),
            PaymentPeriod = new PaymentPeriod
            {
                Name = "HTTP Test Planı",
                DayCount = 30,
                CollectionDayOfMonth = 20,
                CollectionTime = new TimeOnly(11, 0),
                CollectionAmount = 210m,
                IsActive = true
            }
        };
        dbContext.Subscribers.Add(subscriber);
        await dbContext.SaveChangesAsync();
        return subscriber.Id;
    }

    private static DateTimeOffset ToUtcTimestamp(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
