using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Tests;

public sealed class TenDayPaymentScheduleTests
{
    [Theory]
    [InlineData(2026, 2, 28, 80)]
    [InlineData(2028, 2, 29, 90)]
    [InlineData(2026, 4, 30, 100)]
    [InlineData(2026, 7, 31, 110)]
    public async Task DailyRows_UseTenTwentiethAndMonthEndWithProratedAmount(
        int year,
        int month,
        int monthEndDay,
        int expectedMonthEndAmount)
    {
        await using var context = CreateContext();
        var subscriber = CreateTenDaySubscriber(new DateOnly(year, month, 1));
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        var firstDue = Assert.Single(
            (await service.GetDailyAsync(new DateOnly(year, month, 10))).Records);
        var secondDue = Assert.Single(
            (await service.GetDailyAsync(new DateOnly(year, month, 20))).Records);
        var monthEndDue = Assert.Single(
            (await service.GetDailyAsync(
                new DateOnly(year, month, monthEndDay))).Records);
        var nonDue = Assert.Single(
            (await service.GetDailyAsync(
                new DateOnly(year, month, monthEndDay - 1))).Records);

        Assert.True(firstDue.IsPaymentDue);
        Assert.True(firstDue.ShowPaymentControls);
        Assert.Equal(100m, firstDue.Amount);
        Assert.True(secondDue.IsPaymentDue);
        Assert.True(secondDue.ShowPaymentControls);
        Assert.Equal(100m, secondDue.Amount);
        Assert.True(monthEndDue.IsPaymentDue);
        Assert.True(monthEndDue.ShowPaymentControls);
        Assert.Equal((decimal)expectedMonthEndAmount, monthEndDue.Amount);
        Assert.False(nonDue.IsPaymentDue);
        Assert.False(nonDue.ShowPaymentControls);
    }

    [Theory]
    [InlineData(2026, 2, 28, 8, 80)]
    [InlineData(2028, 2, 29, 9, 90)]
    [InlineData(2026, 4, 30, 10, 100)]
    [InlineData(2026, 7, 31, 11, 110)]
    public async Task MonthEndCollection_DefaultsToProratedAmountAndCoveredDays(
        int year,
        int month,
        int monthEndDay,
        int expectedCoveredDayCount,
        int expectedAmount)
    {
        await using var context = CreateContext();
        var subscriber = CreateTenDaySubscriber(new DateOnly(year, month, 1));
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var dueDate = new DateOnly(year, month, monthEndDay);
        var service = new SubscriberDeliveryService(context);

        var result = await service.SaveDailyRowAsync(
            dueDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: true));

        var row = Assert.Single(result.Records);
        Assert.True(row.IsCollected);
        Assert.Equal((decimal)expectedAmount, row.Amount);
        var persisted = await context.SubscriberDailyDeliveries
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal((decimal)expectedAmount, persisted.Amount);
        Assert.Equal(expectedCoveredDayCount, persisted.CollectionDayCount);
    }

    [Theory]
    [InlineData(2026, 2, 28, 280)]
    [InlineData(2028, 2, 29, 290)]
    [InlineData(2026, 4, 30, 300)]
    [InlineData(2026, 7, 31, 310)]
    public async Task PaymentDetails_MonthTotalMatchesCalendarDayCount(
        int year,
        int month,
        int monthEndDay,
        int expectedTotal)
    {
        await using var context = CreateContext();
        var subscriber = CreateTenDaySubscriber(new DateOnly(year, month, 1));
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberPaymentDetailsService(
            context,
            new FixedBusinessClock(new DateOnly(year, month, monthEndDay)));

        var details = await service.GetAsync(subscriber.Id);

        Assert.Equal((decimal)expectedTotal, details.ExpectedTotal);
        Assert.Equal((decimal)expectedTotal, details.OutstandingBalance);
        Assert.Equal(0m, details.CollectedTotal);
    }

    [Fact]
    public async Task PaymentDetails_AllocatesCollectionFifoAcrossVariableDues()
    {
        await using var context = CreateContext();
        var monthEnd = new DateOnly(2026, 7, 31);
        var subscriber = CreateTenDaySubscriber(new DateOnly(2026, 7, 1));
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var deliveryService = new SubscriberDeliveryService(context);
        await deliveryService.SaveDailyRowAsync(
            monthEnd,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: true,
                Amount: 250m));
        var detailsService = new SubscriberPaymentDetailsService(
            context,
            new FixedBusinessClock(monthEnd));

        var details = await detailsService.GetAsync(subscriber.Id);

        Assert.Equal(310m, details.ExpectedTotal);
        Assert.Equal(250m, details.CollectedTotal);
        Assert.Equal(60m, details.OutstandingBalance);
        var nextDue = Assert.IsType<SubscriberPaymentDueRow>(details.NextDue);
        Assert.Equal(monthEnd, nextDue.OriginalDueDate);
        Assert.Equal(110m, nextDue.Amount);
        Assert.Equal(11, nextDue.CoveredDayCount);
        Assert.Equal(60m, nextDue.Balance);
    }

    [Fact]
    public async Task LeapMonthEndDeferral_PreservesOriginalAmountAndCanBeCancelled()
    {
        await using var context = CreateContext();
        var originalDueDate = new DateOnly(2028, 2, 29);
        var deferredUntil = new DateOnly(2028, 3, 5);
        var subscriber = CreateTenDaySubscriber(new DateOnly(2028, 2, 1));
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();

        var deliveryService = new SubscriberDeliveryService(context);
        await deliveryService.SaveDailyRowAsync(
            new DateOnly(2028, 2, 20),
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: true,
                Amount: 200m));
        var detailsService = new SubscriberPaymentDetailsService(
            context,
            new FixedBusinessClock(new DateOnly(2028, 2, 20)));

        var deferred = await detailsService.DeferAsync(
            subscriber.Id,
            originalDueDate,
            deferredUntil,
            "Ay sonu vadesi ertelendi");

        var deferredDue = Assert.IsType<SubscriberPaymentDueRow>(deferred.NextDue);
        Assert.Equal(originalDueDate, deferredDue.OriginalDueDate);
        Assert.Equal(deferredUntil, deferredDue.EffectiveDueDate);
        Assert.Equal(90m, deferredDue.Amount);
        Assert.Equal(9, deferredDue.CoveredDayCount);
        Assert.Equal(90m, deferredDue.Balance);

        var originalDateRow = Assert.Single(
            (await deliveryService.GetDailyAsync(originalDueDate)).Records);
        var deferredDateRow = Assert.Single(
            (await deliveryService.GetDailyAsync(deferredUntil)).Records);
        Assert.False(originalDateRow.ShowPaymentControls);
        Assert.True(deferredDateRow.ShowPaymentControls);
        Assert.Equal(90m, deferredDateRow.Amount);

        var activeDeferral = Assert.IsType<SubscriberPaymentDeferralRow>(
            deferred.ActiveDeferral);
        var cancelled = await detailsService.CancelDeferralAsync(
            subscriber.Id,
            activeDeferral.Id);

        var restoredDue = Assert.IsType<SubscriberPaymentDueRow>(cancelled.NextDue);
        Assert.Equal(originalDueDate, restoredDue.OriginalDueDate);
        Assert.Equal(originalDueDate, restoredDue.EffectiveDueDate);
        Assert.Equal(90m, restoredDue.Amount);
        Assert.Equal(9, restoredDue.CoveredDayCount);
        Assert.Null(cancelled.ActiveDeferral);

        var restoredOriginalRow = Assert.Single(
            (await deliveryService.GetDailyAsync(originalDueDate)).Records);
        var removedDeferredRow = Assert.Single(
            (await deliveryService.GetDailyAsync(deferredUntil)).Records);
        Assert.True(restoredOriginalRow.ShowPaymentControls);
        Assert.Equal(90m, restoredOriginalRow.Amount);
        Assert.False(removedDeferredRow.ShowPaymentControls);
    }

    [Fact]
    public async Task PaymentDueWithoutNewspaperDay_IsListedAsPaymentOnly()
    {
        await using var context = CreateContext();
        var dueDate = new DateOnly(2026, 7, 10);
        var subscriber = CreateTenDaySubscriber(
            new DateOnly(2026, 7, 1),
            NewspaperDay.Monday);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        var row = Assert.Single((await service.GetDailyAsync(dueDate)).Records);

        Assert.False(row.HasDelivery);
        Assert.False(row.IsScheduled);
        Assert.False(row.IsDelivered);
        Assert.True(row.IsPaymentDue);
        Assert.True(row.ShowPaymentControls);
        Assert.Equal(100m, row.Amount);
    }

    private static Subscriber CreateTenDaySubscriber(
        DateOnly createdOn,
        NewspaperDay? newspaperDay = null)
    {
        var subscriber = new Subscriber
        {
            Name = $"Ten Day Subscriber {Guid.NewGuid():N}",
            MonthlyFee = 300m,
            IsActive = true,
            CreatedAt = new DateTimeOffset(
                createdOn.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero),
            PaymentPeriod = new PaymentPeriod
            {
                Name = "Ten Day Plan",
                DayCount = 10,
                CollectionDayOfMonth = 10,
                CollectionTime = new TimeOnly(10, 0),
                CollectionAmount = 100m,
                IsActive = true
            }
        };
        if (newspaperDay.HasValue)
        {
            subscriber.NewspaperDays.Add(new SubscriberPublicationDay
            {
                Day = newspaperDay.Value
            });
        }

        return subscriber;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"gazete-ten-day-tests-{Guid.NewGuid():N}")
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
