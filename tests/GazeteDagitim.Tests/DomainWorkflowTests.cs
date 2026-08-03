using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Tests;

public sealed class DomainWorkflowTests
{
    [Fact]
    public void SundayMondayPlan_SkipsSunday_AndCoversTwoDaysOnMonday()
    {
        using var context = CreateContext();
        var service = new SubscriberDeliveryService(context);
        var subscriber = new Subscriber
        {
            Name = "Test Abonesi",
            NewspaperDays =
            [
                new SubscriberPublicationDay { Day = NewspaperDay.SundayMonday }
            ]
        };

        var sunday = new DateOnly(2026, 7, 26);
        var monday = sunday.AddDays(1);

        Assert.Null(service.PlanDailyDelivery(subscriber, sunday));
        var plan = Assert.IsType<SubscriberDeliveryPlan>(
            service.PlanDailyDelivery(subscriber, monday));
        Assert.True(plan.IsScheduled);
        Assert.Equal(2, plan.NewspaperCount);
        Assert.Equal([sunday, monday], plan.CoveredDates);
    }

    [Fact]
    public void EmptyNewspaperDays_ProducesLegacyUnscheduledRow()
    {
        using var context = CreateContext();
        var service = new SubscriberDeliveryService(context);
        var date = new DateOnly(2026, 7, 28);

        var plan = Assert.IsType<SubscriberDeliveryPlan>(
            service.PlanDailyDelivery(
                new Subscriber { Name = "Eski Kayıt" },
                date));

        Assert.False(plan.IsScheduled);
        Assert.Single(plan.CoveredDates);
        Assert.Equal(1, plan.NewspaperCount);
    }

    [Fact]
    public async Task SavingCollectedDelivery_SnapshotsDistributor()
    {
        await using var context = CreateContext();
        var distributor = new Distributor
        {
            Name = "Merkez Dağıtıcı",
            Address = "İstanbul",
            Phone = "555",
            Zone = DistributorZone.Region1
        };
        var subscriber = new Subscriber
        {
            Name = "Ayşe Yılmaz",
            Address = "Kadıköy",
            MonthlyFee = 250m,
            Distributor = distributor,
            NewspaperDays =
            [
                new SubscriberPublicationDay { Day = NewspaperDay.Tuesday }
            ]
        };
        context.Add(subscriber);
        await context.SaveChangesAsync();

        var service = new SubscriberDeliveryService(context);
        var date = new DateOnly(2026, 7, 28);
        await service.SaveDailyAsync(
            date,
            [
                new SubscriberDeliveryUpdate(
                    subscriber.Id,
                    IsDelivered: true,
                    IsCollected: true,
                    Amount: 250m,
                    SubscriberPaymentMethod.Cash)
            ]);

        var record = await context.SubscriberDailyDeliveries.SingleAsync();
        Assert.Equal(distributor.Id, record.DistributorId);
        Assert.Equal("Merkez Dağıtıcı", record.DistributorName);
        Assert.True(record.IsCollected);
        Assert.Equal(250m, record.Amount);
        var distributorDelivery = await context.Deliveries.SingleAsync();
        Assert.Equal(1, distributorDelivery.NewspaperCount);
        Assert.Equal(5m, distributorDelivery.Amount);
        Assert.Equal(DeliveryStatus.Completed, distributorDelivery.Status);
    }

    [Fact]
    public async Task ClosedCashHandover_BlocksDeliveryChanges()
    {
        await using var context = CreateContext();
        var subscriber = new Subscriber
        {
            Name = "Kilit Testi",
            MonthlyFee = 100m
        };
        var date = new DateOnly(2026, 7, 28);
        context.Add(subscriber);
        context.CashHandovers.Add(new CashHandover
        {
            Date = date,
            Status = CashHandoverStatus.Delivered,
            DeliveredAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new SubscriberDeliveryService(context);
        var error = await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.SaveDailyAsync(
                date,
                [
                    new SubscriberDeliveryUpdate(
                        subscriber.Id,
                        true,
                        false,
                        0m,
                        SubscriberPaymentMethod.Cash)
                ]));

        Assert.Contains("kasa", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CashHandover_DerivesOnlyCollectedCash_AndMonthlyDeliveredTotal()
    {
        await using var context = CreateContext();
        var date = new DateOnly(2026, 7, 28);
        var subscriber = new Subscriber { Name = "Nakit Abonesi", MonthlyFee = 120m };
        context.Add(subscriber);
        await context.SaveChangesAsync();
        context.SubscriberDailyDeliveries.AddRange(
            new SubscriberDailyDelivery
            {
                SubscriberId = subscriber.Id,
                Date = date,
                NewspaperCount = 1,
                IsCollected = true,
                Amount = 120m,
                PaymentMethod = SubscriberPaymentMethod.Cash,
                CoveredDates =
                [
                    new SubscriberDailyDeliveryCoveredDate { CoveredDate = date }
                ]
            },
            new SubscriberDailyDelivery
            {
                SubscriberId = subscriber.Id,
                Date = date.AddDays(-1),
                NewspaperCount = 1,
                IsCollected = true,
                Amount = 70m,
                PaymentMethod = SubscriberPaymentMethod.Card,
                CoveredDates =
                [
                    new SubscriberDailyDeliveryCoveredDate { CoveredDate = date.AddDays(-1) }
                ]
            });
        await context.SaveChangesAsync();

        var clock = new FixedBusinessClock(date);
        var service = new CashHandoverService(context, clock);
        var draft = await service.GetDailyAsync(date);
        Assert.Single(draft.AutomaticItems);
        Assert.Equal(120m, draft.AutomaticTotal);

        await service.SaveDailyAsync(
            date,
            new CashHandoverUpdate(
                [new CashHandoverItemInput("Ek Tahsilat", 30m, "Manuel")],
                CashHandoverStatus.Delivered));
        var monthly = await service.GetMonthlyAsync(date.Year, date.Month);
        Assert.Equal(150m, monthly.Total);
        Assert.Single(monthly.Records);
    }

    [Fact]
    public async Task PaymentTracking_SeparatesOutgoingPaymentsAndCollectedCash()
    {
        await using var context = CreateContext();
        var distributor = new Distributor
        {
            Name = "Bölge Dağıtıcı",
            Address = "Adres",
            Phone = "555",
            Zone = DistributorZone.Region1
        };
        var subscriber = new Subscriber
        {
            Name = "Ödeme Abonesi",
            MonthlyFee = 200m,
            Distributor = distributor
        };
        context.Add(subscriber);
        await context.SaveChangesAsync();
        var date = new DateOnly(2026, 7, 15);
        context.Payments.AddRange(
            new Payment
            {
                DistributorId = distributor.Id,
                Date = date,
                PeriodStart = date,
                PeriodEnd = date,
                PaymentType = PaymentType.Daily,
                Amount = 50m,
                Status = PaymentStatus.Paid,
                PaidAt = DateTimeOffset.UtcNow
            },
            new Payment
            {
                DistributorId = distributor.Id,
                Date = date.AddDays(1),
                PeriodStart = date.AddDays(1),
                PeriodEnd = date.AddDays(1),
                PaymentType = PaymentType.Daily,
                Amount = 75m,
                Status = PaymentStatus.Pending
            });
        context.SubscriberDailyDeliveries.Add(new SubscriberDailyDelivery
        {
            SubscriberId = subscriber.Id,
            DistributorId = distributor.Id,
            DistributorName = distributor.Name,
            Date = date,
            NewspaperCount = 1,
            IsCollected = true,
            Amount = 200m,
            PaymentMethod = SubscriberPaymentMethod.Cash,
            CoveredDates =
            [
                new SubscriberDailyDeliveryCoveredDate { CoveredDate = date }
            ]
        });
        await context.SaveChangesAsync();

        var result = await new PaymentTrackingService(context)
            .GetMonthlyAsync(2026, 7, distributor.Id);

        Assert.Equal(125m, result.Summary.DistributorPaymentTotal);
        Assert.Equal(50m, result.Summary.PaidTotal);
        Assert.Equal(75m, result.Summary.PendingTotal);
        Assert.Equal(200m, result.Summary.CashCollectionTotal);
        Assert.Equal(1, result.Summary.CashCollectionCount);
    }

    [Fact]
    public async Task SavingExistingDelivery_PreservesCoveredDateSnapshot_WithoutTrackingConflict()
    {
        await using var context = CreateContext();
        var monday = new DateOnly(2026, 7, 27);
        var subscriber = new Subscriber
        {
            Name = "Pazar Pazartesi Abonesi",
            NewspaperDays =
            [
                new SubscriberPublicationDay { Day = NewspaperDay.SundayMonday }
            ]
        };
        context.Add(subscriber);
        await context.SaveChangesAsync();

        var service = new SubscriberDeliveryService(context);
        var update = new SubscriberDeliveryUpdate(
            subscriber.Id,
            IsDelivered: true,
            IsCollected: false,
            Amount: 0m,
            SubscriberPaymentMethod.Cash);
        await service.SaveDailyAsync(monday, [update]);

        context.SubscriberPublicationDays.RemoveRange(subscriber.NewspaperDays);
        subscriber.NewspaperDays =
        [
            new SubscriberPublicationDay
            {
                SubscriberId = subscriber.Id,
                Day = NewspaperDay.Tuesday
            }
        ];
        await context.SaveChangesAsync();

        await service.SaveDailyAsync(monday, [update]);

        var delivery = await context.SubscriberDailyDeliveries
            .AsNoTracking()
            .Include(value => value.CoveredDates)
            .SingleAsync();
        Assert.Equal(2, delivery.NewspaperCount);
        Assert.Equal(
            [monday.AddDays(-1), monday],
            delivery.CoveredDates.Select(value => value.CoveredDate).Order().ToArray());
    }

    [Fact]
    public async Task DeliveredCashHandover_PersistsGrandTotal_AndCannotBeReopened()
    {
        await using var context = CreateContext();
        var date = new DateOnly(2026, 7, 28);
        var subscriber = new Subscriber { Name = "Nakit Abonesi" };
        context.Add(subscriber);
        await context.SaveChangesAsync();
        context.SubscriberDailyDeliveries.Add(new SubscriberDailyDelivery
        {
            SubscriberId = subscriber.Id,
            Date = date,
            NewspaperCount = 1,
            IsCollected = true,
            Amount = 120m,
            PaymentMethod = SubscriberPaymentMethod.Cash,
            CoveredDates =
            [
                new SubscriberDailyDeliveryCoveredDate { CoveredDate = date }
            ]
        });
        await context.SaveChangesAsync();

        var service = new CashHandoverService(context, new FixedBusinessClock(date));
        await service.SaveDailyAsync(
            date,
            new CashHandoverUpdate(
                [new CashHandoverItemInput("Manuel", 30m)],
                CashHandoverStatus.Delivered));

        var persisted = await context.CashHandovers.AsNoTracking().SingleAsync();
        Assert.Equal(150m, persisted.Total);
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.SaveDailyAsync(
                date,
                new CashHandoverUpdate(Status: CashHandoverStatus.Draft)));
        Assert.Equal(
            CashHandoverStatus.Delivered,
            (await context.CashHandovers.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task PeriodicPayment_ReconcilesPendingAmount_ButDoesNotRewritePaidAmount()
    {
        await using var context = CreateContext();
        var date = new DateOnly(2026, 7, 28);
        var distributor = new Distributor
        {
            Name = "Günlük Dağıtıcı",
            Address = "Adres",
            Phone = "555",
            Zone = DistributorZone.Region1,
            PaymentType = PaymentType.Daily,
            NewspaperPrice = 5m
        };
        var delivery = new Delivery
        {
            Distributor = distributor,
            Date = date,
            Day = BusinessDay.Tuesday,
            NewspaperCount = 2,
            Amount = 10m,
            Status = DeliveryStatus.Completed
        };
        context.Add(delivery);
        await context.SaveChangesAsync();

        var service = new PeriodicPaymentService(context);
        var created = await service.CreateScheduledPaymentsAsync(PaymentType.Daily, date);
        Assert.Equal(1, created.Created);

        delivery.NewspaperCount = 3;
        delivery.Amount = 15m;
        await context.SaveChangesAsync();
        var reconciled = await service.CreateScheduledPaymentsAsync(PaymentType.Daily, date);
        Assert.Equal(1, reconciled.Existing);
        var payment = await context.Payments.SingleAsync();
        Assert.Equal(15m, payment.Amount);

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        delivery.NewspaperCount = 4;
        delivery.Amount = 20m;
        await context.SaveChangesAsync();

        var paidDrift = await service.CreateScheduledPaymentsAsync(PaymentType.Daily, date);
        Assert.Equal(1, paidDrift.Failed);
        Assert.Equal(15m, (await context.Payments.AsNoTracking().SingleAsync()).Amount);
    }

    [Fact]
    public async Task PaymentTracking_DoesNotMovePastUnassignedCashToCurrentDistributor()
    {
        await using var context = CreateContext();
        var date = new DateOnly(2026, 7, 28);
        var subscriber = new Subscriber { Name = "Sonradan Atanan Abone" };
        var currentDistributor = new Distributor
        {
            Name = "Yeni Dağıtıcı",
            Address = "Adres",
            Phone = "555",
            Zone = DistributorZone.Region1
        };
        context.AddRange(subscriber, currentDistributor);
        await context.SaveChangesAsync();

        context.SubscriberDailyDeliveries.Add(new SubscriberDailyDelivery
        {
            SubscriberId = subscriber.Id,
            Date = date,
            NewspaperCount = 1,
            IsCollected = true,
            Amount = 80m,
            PaymentMethod = SubscriberPaymentMethod.Cash,
            CoveredDates =
            [
                new SubscriberDailyDeliveryCoveredDate { CoveredDate = date }
            ]
        });
        await context.SaveChangesAsync();

        subscriber.DistributorId = currentDistributor.Id;
        await context.SaveChangesAsync();

        var service = new PaymentTrackingService(context);
        var all = await service.GetMonthlyAsync(2026, 7);
        var filtered = await service.GetMonthlyAsync(
            2026,
            7,
            currentDistributor.Id);

        var cash = Assert.Single(all.CashCollections);
        Assert.Null(cash.DistributorId);
        Assert.Equal(string.Empty, cash.DistributorName);
        Assert.Empty(filtered.CashCollections);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"gazete-tests-{Guid.NewGuid():N}")
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
