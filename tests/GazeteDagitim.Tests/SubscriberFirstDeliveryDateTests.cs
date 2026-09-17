using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Tests;

public sealed class SubscriberFirstDeliveryDateTests
{
    private static readonly DateOnly FirstDeliveryDate = new(2026, 7, 1);
    private static readonly DateOnly Today = new(2026, 9, 17);

    [Fact]
    public async Task DateBeforeFirstDelivery_IsNotListed_AndCannotBeRecorded()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriber();
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var previousScheduledDay = FirstDeliveryDate.AddDays(-7);

        var daily = await service.GetDailyAsync(previousScheduledDay);

        Assert.Empty(daily.Records);
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.SaveDailyRowAsync(
                previousScheduledDay,
                new SubscriberDeliveryPatch(
                    subscriber.Id,
                    IsDelivered: true)));
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.SaveDailyRowAsync(
                previousScheduledDay,
                new SubscriberDeliveryPatch(
                    subscriber.Id,
                    IsCollected: true,
                    Amount: 25m,
                    PaymentMethod: SubscriberPaymentMethod.Cash)));
        Assert.Empty(await context.SubscriberDailyDeliveries.ToListAsync());
    }

    [Fact]
    public async Task HistoricalStartAndLaterScheduledDate_AllowDeliveryAndCollection()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriber();
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var laterHistoricalDate = FirstDeliveryDate.AddDays(7);

        var startRow = Assert.Single(
            (await service.GetDailyAsync(FirstDeliveryDate)).Records);
        Assert.True(startRow.HasDelivery);
        Assert.True(startRow.IsScheduled);
        Assert.True(startRow.IsPaymentDue);
        Assert.True(startRow.ShowPaymentControls);

        var savedStart = Assert.Single(
            (await service.SaveDailyRowAsync(
                FirstDeliveryDate,
                new SubscriberDeliveryPatch(
                    subscriber.Id,
                    IsDelivered: true,
                    IsCollected: true,
                    Amount: 25m,
                    PaymentMethod: SubscriberPaymentMethod.Cash))).Records);
        Assert.True(savedStart.IsDelivered);
        Assert.True(savedStart.IsCollected);
        Assert.Equal(25m, savedStart.Amount);

        var savedLater = Assert.Single(
            (await service.SaveDailyRowAsync(
                laterHistoricalDate,
                new SubscriberDeliveryPatch(
                    subscriber.Id,
                    IsDelivered: true,
                    IsCollected: true,
                    Amount: 25m,
                    PaymentMethod: SubscriberPaymentMethod.Card))).Records);
        Assert.True(savedLater.IsDelivered);
        Assert.True(savedLater.IsCollected);
        Assert.Equal(SubscriberPaymentMethod.Card, savedLater.PaymentMethod);

        var persisted = await context.SubscriberDailyDeliveries
            .AsNoTracking()
            .Include(value => value.CoveredDates)
            .OrderBy(value => value.Date)
            .ToListAsync();
        Assert.Collection(
            persisted,
            first =>
            {
                Assert.Equal(FirstDeliveryDate, first.Date);
                Assert.True(first.IsDelivered);
                Assert.True(first.IsCollected);
                Assert.Equal(
                    FirstDeliveryDate,
                    Assert.Single(first.CoveredDates).CoveredDate);
            },
            second =>
            {
                Assert.Equal(laterHistoricalDate, second.Date);
                Assert.True(second.IsDelivered);
                Assert.True(second.IsCollected);
                Assert.Equal(
                    laterHistoricalDate,
                    Assert.Single(second.CoveredDates).CoveredDate);
            });
    }

    private static Subscriber CreateSubscriber()
    {
        var subscriber = new Subscriber
        {
            Name = "Geçmiş Başlangıçlı Abone",
            MonthlyFee = 25m,
            IsActive = true,
            FirstDeliveryDate = FirstDeliveryDate,
            PaymentPeriodStartedOn = FirstDeliveryDate,
            PaymentPeriod = new PaymentPeriod
            {
                Name = "Günlük Tahsilat",
                Frequency = PaymentPeriodFrequency.Daily,
                DayCount = 1,
                CollectionDayOfMonth = 1,
                CollectionTime = new TimeOnly(9, 0),
                CollectionAmount = 25m,
                IsActive = true
            }
        };
        subscriber.NewspaperDays.Add(new SubscriberPublicationDay
        {
            Day = NewspaperDay.Wednesday
        });
        return subscriber;
    }

    private static SubscriberDeliveryService CreateService(AppDbContext context) =>
        new(
            context,
            new FixedBusinessClock(
                Today,
                new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.FromHours(3))));

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"subscriber-first-delivery-tests-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FixedBusinessClock(
        DateOnly today,
        DateTimeOffset utcNow) : IBusinessClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
        public DateOnly Today { get; } = today;
    }
}
