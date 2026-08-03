using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class DeliveryAutosaveDomainTests
{
    private static readonly DateOnly TestDate = new(2026, 7, 28);

    [Theory]
    [InlineData(15, true)]
    [InlineData(14, false)]
    public async Task DailyRows_ShowPaymentControlsOnlyOnPaymentDate(
        int selectedDay,
        bool expected)
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriberWithPaymentSchedule(
            collectionDayOfMonth: 15);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        var result = await service.GetDailyAsync(
            new DateOnly(2026, 7, selectedDay));

        var row = Assert.Single(result.Records);
        Assert.Equal(expected, row.ShowPaymentControls);
    }

    [Fact]
    public async Task DailyRows_ClampPaymentDateToLastDayOfShortMonth()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriberWithPaymentSchedule(
            collectionDayOfMonth: 31);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        var beforeMonthEnd = Assert.Single(
            (await service.GetDailyAsync(new DateOnly(2026, 2, 27))).Records);
        var monthEnd = Assert.Single(
            (await service.GetDailyAsync(new DateOnly(2026, 2, 28))).Records);

        Assert.False(beforeMonthEnd.ShowPaymentControls);
        Assert.True(monthEnd.ShowPaymentControls);
    }

    [Fact]
    public async Task DailyRows_UseActiveDeferralAsEffectivePaymentDate()
    {
        await using var context = CreateContext();
        var subscriber = CreateSubscriberWithPaymentSchedule(
            collectionDayOfMonth: 15);
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var originalDueDate = new DateOnly(2026, 8, 15);
        var deferredUntil = new DateOnly(2026, 8, 25);
        var deferral = new SubscriberPaymentDeferral
        {
            SubscriberId = subscriber.Id,
            OriginalDueDate = originalDueDate,
            PreviousDueDate = originalDueDate,
            DeferredUntil = deferredUntil,
            Reason = "Test ertelemesi"
        };
        context.SubscriberPaymentDeferrals.Add(deferral);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        var originalDateRow = Assert.Single(
            (await service.GetDailyAsync(originalDueDate)).Records);
        var deferredDateRow = Assert.Single(
            (await service.GetDailyAsync(deferredUntil)).Records);

        Assert.False(originalDateRow.ShowPaymentControls);
        Assert.True(deferredDateRow.ShowPaymentControls);

        deferral.CancelledAt = new DateTimeOffset(
            2026,
            8,
            16,
            9,
            0,
            0,
            TimeSpan.Zero);
        await context.SaveChangesAsync();

        var restoredOriginalDateRow = Assert.Single(
            (await service.GetDailyAsync(originalDueDate)).Records);

        Assert.True(restoredOriginalDateRow.ShowPaymentControls);
    }

    [Fact]
    public async Task NonDueExistingCollection_RemainsManageableUntilUncollected()
    {
        await using var context = CreateContext();
        var nonDueDate = new DateOnly(2026, 7, 14);
        var subscriber = CreateSubscriberWithPaymentSchedule(
            collectionDayOfMonth: 15);
        subscriber.DailyDeliveries.Add(new SubscriberDailyDelivery
        {
            Date = nonDueDate,
            NewspaperCount = 1,
            IsCollected = true,
            Amount = 240m,
            PaymentMethod = SubscriberPaymentMethod.Card,
            CoveredDates =
            [
                new SubscriberDailyDeliveryCoveredDate
                {
                    CoveredDate = nonDueDate
                }
            ]
        });
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        var existingRow = Assert.Single(
            (await service.GetDailyAsync(nonDueDate)).Records);

        Assert.True(existingRow.IsCollected);
        Assert.True(existingRow.ShowPaymentControls);

        await service.SaveDailyRowAsync(
            nonDueDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: false));
        context.ChangeTracker.Clear();

        var reloadedRow = Assert.Single(
            (await service.GetDailyAsync(nonDueDate)).Records);

        Assert.False(reloadedRow.IsCollected);
        Assert.False(reloadedRow.ShowPaymentControls);
    }

    [Fact]
    public async Task RowPatch_UpdatesIndependentStates_AndAllowsUnchecking()
    {
        await using var context = CreateContext();
        var subscriber = new Subscriber
        {
            Name = "Bağımsız İşlem Abonesi",
            MonthlyFee = 175m
        };
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        var deliveryResult = await service.SaveDailyRowAsync(
            TestDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsDelivered: true));
        var deliveredRow = Assert.Single(deliveryResult.Records);
        Assert.True(deliveredRow.IsDelivered);
        Assert.False(deliveredRow.IsCollected);
        Assert.Equal(175m, deliveredRow.Amount);

        var collectionResult = await service.SaveDailyRowAsync(
            TestDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: true,
                Amount: 225.50m,
                PaymentMethod: SubscriberPaymentMethod.Card));
        var collectedRow = Assert.Single(collectionResult.Records);
        Assert.True(collectedRow.IsDelivered);
        Assert.True(collectedRow.IsCollected);
        Assert.Equal(225.50m, collectedRow.Amount);
        Assert.Equal(SubscriberPaymentMethod.Card, collectedRow.PaymentMethod);

        var revertedDeliveryResult = await service.SaveDailyRowAsync(
            TestDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsDelivered: false));
        var deliveryRevertedRow = Assert.Single(revertedDeliveryResult.Records);
        Assert.False(deliveryRevertedRow.IsDelivered);
        Assert.True(deliveryRevertedRow.IsCollected);

        var revertedCollectionResult = await service.SaveDailyRowAsync(
            TestDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: false));
        var fullyRevertedRow = Assert.Single(revertedCollectionResult.Records);
        Assert.False(fullyRevertedRow.IsDelivered);
        Assert.False(fullyRevertedRow.IsCollected);
        Assert.Equal(225.50m, fullyRevertedRow.Amount);
        Assert.Single(await context.SubscriberDailyDeliveries.ToListAsync());
    }

    [Fact]
    public async Task RepeatingSameDesiredState_IsIdempotent()
    {
        await using var context = CreateContext();
        var subscriber = new Subscriber
        {
            Name = "Çift Tıklama Abonesi",
            MonthlyFee = 90m
        };
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);
        var patch = new SubscriberDeliveryPatch(
            subscriber.Id,
            IsDelivered: true,
            IsCollected: true,
            Amount: 90m,
            PaymentMethod: SubscriberPaymentMethod.Cash);

        await service.SaveDailyRowAsync(TestDate, patch);
        var result = await service.SaveDailyRowAsync(TestDate, patch);

        var row = Assert.Single(result.Records);
        Assert.True(row.IsDelivered);
        Assert.True(row.IsCollected);
        Assert.Equal(90m, row.Amount);
        Assert.Single(await context.SubscriberDailyDeliveries.ToListAsync());
        Assert.Single(await context.SubscriberDailyDeliveryCoveredDates.ToListAsync());
    }

    [Fact]
    public async Task ClosedCashHandover_BlocksRowPatchWithoutMutation()
    {
        await using var context = CreateContext();
        var subscriber = new Subscriber
        {
            Name = "Kilitli Otomatik Kayıt",
            MonthlyFee = 100m
        };
        context.Subscribers.Add(subscriber);
        context.CashHandovers.Add(new CashHandover
        {
            Date = TestDate,
            Status = CashHandoverStatus.Delivered,
            DeliveredAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        var service = new SubscriberDeliveryService(context);

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.SaveDailyRowAsync(
                TestDate,
                new SubscriberDeliveryPatch(
                    subscriber.Id,
                    IsDelivered: true)));

        Assert.Empty(await context.SubscriberDailyDeliveries.ToListAsync());
    }

    [Fact]
    public async Task CollectionTransition_CapturesPlanAndTime_ThenClearsOnRevert()
    {
        await using var context = CreateContext();
        var period = new PaymentPeriod
        {
            Name = "Otuz Günlük Plan",
            DayCount = 30,
            CollectionDayOfMonth = 15,
            CollectionTime = new TimeOnly(10, 30),
            CollectionAmount = 320.50m
        };
        var subscriber = new Subscriber
        {
            Name = "Planlı Tahsilat Abonesi",
            MonthlyFee = 500m,
            PaymentPeriod = period
        };
        context.Subscribers.Add(subscriber);
        await context.SaveChangesAsync();
        var clock = new FixedBusinessClock(
            TestDate,
            new DateTimeOffset(2026, 7, 28, 11, 45, 0, TimeSpan.Zero));
        var service = new SubscriberDeliveryService(context, clock);

        var collected = await service.SaveDailyRowAsync(
            TestDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: true));

        Assert.Equal(320.50m, Assert.Single(collected.Records).Amount);
        var persisted = await context.SubscriberDailyDeliveries.SingleAsync();
        Assert.Equal(clock.UtcNow, persisted.CollectedAt);
        Assert.Equal("Otuz Günlük Plan", persisted.CollectionPeriodName);
        Assert.Equal(30, persisted.CollectionDayCount);

        await service.SaveDailyRowAsync(
            TestDate,
            new SubscriberDeliveryPatch(
                subscriber.Id,
                IsCollected: false));

        persisted = await context.SubscriberDailyDeliveries.SingleAsync();
        Assert.Null(persisted.CollectedAt);
        Assert.Empty(persisted.CollectionPeriodName);
        Assert.Null(persisted.CollectionDayCount);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"gazete-autosave-tests-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }

    private static Subscriber CreateSubscriberWithPaymentSchedule(
        int collectionDayOfMonth) =>
        new()
        {
            Name = "Ödeme Takvimli Abone",
            MonthlyFee = 240m,
            IsActive = true,
            CreatedAt = new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero),
            PaymentPeriod = new PaymentPeriod
            {
                Name = "Aylık Test Planı",
                DayCount = 30,
                CollectionDayOfMonth = collectionDayOfMonth,
                CollectionTime = new TimeOnly(10, 30),
                CollectionAmount = 240m,
                IsActive = true
            }
        };

    private sealed class FixedBusinessClock(
        DateOnly today,
        DateTimeOffset utcNow) : IBusinessClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
        public DateOnly Today { get; } = today;
    }
}

public sealed class DeliveryAutosaveEndpointTests
{
    private static readonly DateOnly TestDate = new(2026, 7, 28);

    [Fact]
    public async Task DeliveriesPage_ShowsListedSubscriberCountInsideDeliveredSummary()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        await SeedPaymentScheduleSubscribersAsync(factory);

        using var response = await client.GetAsync(
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var summary = Regex.Match(
            html,
            """
            <a\b[^>]*data-testid="delivered-summary"[^>]*>
            (?<content>.*?)
            </a>
            """,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.CultureInvariant);
        Assert.True(summary.Success, "Teslim edilen özet alanı bulunamadı.");
        var content = summary.Groups["content"].Value;
        Assert.Contains(
            "data-testid=\"listed-subscriber-total\"",
            content,
            StringComparison.Ordinal);
        var inlineTotal = Regex.Match(
            content,
            """
            <div\b[^>]*class="summary-total-with-listed"[^>]*>
            (?<content>.*?)
            </div>
            """,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.CultureInvariant);
        Assert.True(
            inlineTotal.Success,
            "Teslim edilen ve listelenen abone sayıları aynı satırda bulunamadı.");
        var inlineContent = inlineTotal.Groups["content"].Value;
        Assert.Contains(
            "id=\"delivered-total\">0</strong>",
            inlineContent,
            StringComparison.Ordinal);
        Assert.Matches(
            @"<small\b[^>]*data-testid=""listed-subscriber-total""[^>]*>\s*\(2\)\s*</small>",
            inlineContent);
        Assert.Contains(
            "aria-label=\"Listelenen abone sayısı: 2\"",
            content,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Listelenen abone:",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeliveriesPage_WithoutCompanySettings_ShowsDistributorAndCoverageByDefault()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var (subscriberId, _) =
            await SeedPaymentScheduleSubscribersAsync(factory);

        using var response = await client.GetAsync(
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        AssertDistributorAndCoverageVisibility(
            html,
            subscriberId,
            expectedVisible: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeliveriesPage_UsesCompanyDistributorAndCoverageVisibilitySetting(
        bool expectedVisible)
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var (subscriberId, _) =
            await SeedPaymentScheduleSubscribersAsync(factory);
        await SeedCompanySettingsAsync(factory, expectedVisible);

        using var response = await client.GetAsync(
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        AssertDistributorAndCoverageVisibility(
            html,
            subscriberId,
            expectedVisible);
    }

    [Fact]
    public async Task DeliveriesPage_RendersPaymentFieldsOnlyForDueSubscriber()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var (dueSubscriberId, nonDueSubscriberId) =
            await SeedPaymentScheduleSubscribersAsync(factory);

        using var response = await client.GetAsync(
            $"/deliveries?date={TestDate:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var dueRow = ExtractDeliveryRow(html, dueSubscriberId);
        var nonDueRow = ExtractDeliveryRow(html, nonDueSubscriberId);

        Assert.Contains(
            "data-collected-toggle",
            dueRow,
            StringComparison.Ordinal);
        Assert.Contains("data-amount", dueRow, StringComparison.Ordinal);
        Assert.Contains(
            "data-payment-field",
            dueRow,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "data-collected-toggle",
            nonDueRow,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "data-amount",
            nonDueRow,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "data-payment-field",
            nonDueRow,
            StringComparison.Ordinal);
        Assert.Equal(
            3,
            Regex.Matches(
                nonDueRow,
                @"\bpayment-not-due\b",
                RegexOptions.CultureInvariant).Count);
    }

    [Fact]
    public async Task DeliveriesPage_RendersAccessibleMarkOnlyDeliveryAndPaymentToggles()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var (dueSubscriberId, _) =
            await SeedPaymentScheduleSubscribersAsync(factory);

        using var response = await client.GetAsync(
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var row = ExtractDeliveryRow(html, dueSubscriberId);
        AssertMarkOnlyToggle(
            row,
            "data-delivered-toggle",
            expectedPressed: false,
            subscriberName: "Bugün Ödemeli HTTP Abonesi");
        AssertMarkOnlyToggle(
            row,
            "data-collected-toggle",
            expectedPressed: false,
            subscriberName: "Bugün Ödemeli HTTP Abonesi");
    }

    [Fact]
    public async Task DeliveriesPage_RendersSemanticCellsForCompactMobileGrid()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var (dueSubscriberId, _) =
            await SeedPaymentScheduleSubscribersAsync(factory);

        using var response = await client.GetAsync(
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var row = ExtractDeliveryRow(html, dueSubscriberId);
        foreach (var cssClass in new[]
                 {
                     "delivery-action-cell",
                     "collection-action-cell",
                     "amount-cell",
                     "method-cell",
                     "distributor-cell",
                     "coverage-cell",
                     "subscriber-cell"
                 })
        {
            AssertTableCellHasClass(row, cssClass);
        }

        Assert.Equal(
            3,
            Regex.Matches(
                row,
                @"\bdata-payment-cell\b",
                RegexOptions.CultureInvariant).Count);
        Assert.Contains("data-delivered-toggle", row, StringComparison.Ordinal);
        Assert.Contains("data-collected-toggle", row, StringComparison.Ordinal);
        Assert.Contains("data-payment-field", row, StringComparison.Ordinal);
        Assert.Contains("data-amount", row, StringComparison.Ordinal);
        Assert.Contains("data-row-status", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveRow_WithPartialPatches_DoesNotOverwriteOtherState()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var subscriberId = await SeedSubscriberAsync(
            factory,
            "Kısmi Kayıt Abonesi",
            140m);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        using var deliveryResponse = await client.PostAsync(
            "/deliveries/save-row",
            PartialAutosaveForm(
                token,
                subscriberId,
                ("Delivered", "true")));
        Assert.Equal(HttpStatusCode.OK, deliveryResponse.StatusCode);

        using var collectionResponse = await client.PostAsync(
            "/deliveries/save-row",
            PartialAutosaveForm(
                token,
                subscriberId,
                ("Collected", "true"),
                ("Amount", "140.00"),
                ("PaymentMethod", "Havale/EFT")));
        Assert.Equal(HttpStatusCode.OK, collectionResponse.StatusCode);
        var saved = Assert.IsType<DailyDeliveryRowAutosaveResponseModel>(
            await collectionResponse.Content
                .ReadFromJsonAsync<DailyDeliveryRowAutosaveResponseModel>());
        Assert.True(saved.Row!.Delivered);
        Assert.True(saved.Row.Collected);
        Assert.Equal("Havale/EFT", saved.Row.PaymentMethod);
    }

    [Fact]
    public async Task SaveRow_ReturnsPersistedStateAndSummary_ThenAllowsRevert()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var subscriberId = await SeedSubscriberAsync(
            factory,
            "Uç Nokta Abonesi",
            125.50m);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        using var saveResponse = await client.PostAsync(
            "/deliveries/save-row",
            AutosaveForm(
                token,
                subscriberId,
                delivered: true,
                collected: true,
                amount: "125.50",
                paymentMethod: "Kart"));

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var saved = Assert.IsType<DailyDeliveryRowAutosaveResponseModel>(
            await saveResponse.Content
                .ReadFromJsonAsync<DailyDeliveryRowAutosaveResponseModel>());
        Assert.True(saved.Success);
        Assert.NotNull(saved.Row);
        Assert.Equal(subscriberId, saved.Row.SubscriberId);
        Assert.True(saved.Row.Delivered);
        Assert.True(saved.Row.Collected);
        Assert.Equal(125.50m, saved.Row.Amount);
        Assert.Equal("Kart", saved.Row.PaymentMethod);
        Assert.NotNull(saved.Summary);
        Assert.Equal(1, saved.Summary.DeliveredCount);
        Assert.Equal(1, saved.Summary.CollectedCount);
        Assert.Equal(125.50m, saved.Summary.CollectedTotal);

        using var revertResponse = await client.PostAsync(
            "/deliveries/save-row",
            AutosaveForm(
                token,
                subscriberId,
                delivered: false,
                collected: false,
                amount: "125.50",
                paymentMethod: "Kart"));

        Assert.Equal(HttpStatusCode.OK, revertResponse.StatusCode);
        var reverted = Assert.IsType<DailyDeliveryRowAutosaveResponseModel>(
            await revertResponse.Content
                .ReadFromJsonAsync<DailyDeliveryRowAutosaveResponseModel>());
        Assert.False(reverted.Row!.Delivered);
        Assert.False(reverted.Row.Collected);
        Assert.Equal(0, reverted.Summary!.DeliveredCount);
        Assert.Equal(0, reverted.Summary.CollectedCount);
        Assert.Equal(0m, reverted.Summary.CollectedTotal);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbContext.SubscriberDailyDeliveries
            .AsNoTracking()
            .SingleAsync();
        Assert.False(persisted.IsDelivered);
        Assert.False(persisted.IsCollected);
        Assert.Equal(125.50m, persisted.Amount);
    }

    [Fact]
    public async Task SaveRow_WithClosedCashHandover_ReturnsConflictJson()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var subscriberId = await SeedSubscriberAsync(
            factory,
            "Kasa Kilidi Abonesi",
            80m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.CashHandovers.Add(new CashHandover
            {
                Date = TestDate,
                Status = CashHandoverStatus.Delivered,
                DeliveredAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/deliveries?date={TestDate:yyyy-MM-dd}");
        using var response = await client.PostAsync(
            "/deliveries/save-row",
            AutosaveForm(
                token,
                subscriberId,
                delivered: true,
                collected: false,
                amount: "80.00",
                paymentMethod: "Nakit"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = Assert.IsType<DailyDeliveryRowAutosaveResponseModel>(
            await response.Content
                .ReadFromJsonAsync<DailyDeliveryRowAutosaveResponseModel>());
        Assert.False(error.Success);
        Assert.Contains("kasa", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(error.Row);
        Assert.Null(error.Summary);
    }

    [Fact]
    public async Task SaveRow_WithCollectedZeroAmount_ReturnsBadRequestWithoutMutation()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var subscriberId = await SeedSubscriberAsync(
            factory,
            "Geçersiz Tutar Abonesi",
            0m);
        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/deliveries?date={TestDate:yyyy-MM-dd}");

        using var response = await client.PostAsync(
            "/deliveries/save-row",
            AutosaveForm(
                token,
                subscriberId,
                delivered: false,
                collected: true,
                amount: "0.00",
                paymentMethod: "Nakit"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = Assert.IsType<DailyDeliveryRowAutosaveResponseModel>(
            await response.Content
                .ReadFromJsonAsync<DailyDeliveryRowAutosaveResponseModel>());
        Assert.False(error.Success);
        Assert.Contains("sıfırdan büyük", error.Message, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.SubscriberDailyDeliveries.ToListAsync());
    }

    [Fact]
    public async Task SaveRow_WithoutAntiforgeryToken_IsRejected()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var subscriberId = await SeedSubscriberAsync(
            factory,
            "Antiforgery Abonesi",
            50m);

        using var response = await client.PostAsync(
            "/deliveries/save-row",
            new FormUrlEncodedContent(
            [
                new("Date", TestDate.ToString("yyyy-MM-dd")),
                new("SubscriberId", subscriberId.ToString()),
                new("Delivered", "true"),
                new("Collected", "false"),
                new("Amount", "50.00"),
                new("PaymentMethod", "Nakit")
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.SubscriberDailyDeliveries.ToListAsync());
    }

    private static HttpClient CreateClient(GazeteWebFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

    private static async Task<int> SeedSubscriberAsync(
        GazeteWebFactory factory,
        string name,
        decimal monthlyFee)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscriber = new Subscriber
        {
            Name = name,
            MonthlyFee = monthlyFee,
            IsActive = true
        };
        dbContext.Subscribers.Add(subscriber);
        await dbContext.SaveChangesAsync();
        return subscriber.Id;
    }

    private static async Task<(int DueSubscriberId, int NonDueSubscriberId)>
        SeedPaymentScheduleSubscribersAsync(GazeteWebFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createdAt = new DateTimeOffset(
            2026,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        var dueSubscriber = new Subscriber
        {
            Name = "Bugün Ödemeli HTTP Abonesi",
            MonthlyFee = 280m,
            IsActive = true,
            CreatedAt = createdAt,
            PaymentPeriod = new PaymentPeriod
            {
                Name = "Ayın 28'i",
                DayCount = 30,
                CollectionDayOfMonth = 28,
                CollectionTime = new TimeOnly(10, 0),
                CollectionAmount = 280m,
                IsActive = true
            }
        };
        var nonDueSubscriber = new Subscriber
        {
            Name = "Dün Ödemeli HTTP Abonesi",
            MonthlyFee = 270m,
            IsActive = true,
            CreatedAt = createdAt,
            PaymentPeriod = new PaymentPeriod
            {
                Name = "Ayın 27'si",
                DayCount = 30,
                CollectionDayOfMonth = 27,
                CollectionTime = new TimeOnly(10, 0),
                CollectionAmount = 270m,
                IsActive = true
            }
        };
        dbContext.Subscribers.AddRange(dueSubscriber, nonDueSubscriber);
        await dbContext.SaveChangesAsync();
        return (dueSubscriber.Id, nonDueSubscriber.Id);
    }

    private static async Task SeedCompanySettingsAsync(
        GazeteWebFactory factory,
        bool showDistributorAndCoverage)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.CompanySettings.Add(new CompanySettings
        {
            NewspaperUnitPrice = 10m,
            ShowDistributorAndCoverage = showDistributorAndCoverage
        });
        await dbContext.SaveChangesAsync();
    }

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
            $"Subscriber {subscriberId} delivery row was not found.");
        return row.Value;
    }

    private static void AssertDistributorAndCoverageVisibility(
        string html,
        int subscriberId,
        bool expectedVisible)
    {
        var row = ExtractDeliveryRow(html, subscriberId);
        var hasDistributorHeader = Regex.IsMatch(
            html,
            @"<th\b[^>]*>\s*Dağıtıcı\s*</th>",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);
        var hasCoverageHeader = Regex.IsMatch(
            html,
            @"<th\b[^>]*>\s*Kapsam\s*</th>",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);
        var hasDistributorCell = Regex.IsMatch(
            row,
            @"\bclass=""[^""]*\bdistributor-cell\b[^""]*""",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);
        var hasCoverageCell = Regex.IsMatch(
            row,
            @"\bclass=""[^""]*\bcoverage-cell\b[^""]*""",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);

        Assert.Equal(expectedVisible, hasDistributorHeader);
        Assert.Equal(expectedVisible, hasCoverageHeader);
        Assert.Equal(expectedVisible, hasDistributorCell);
        Assert.Equal(expectedVisible, hasCoverageCell);
        AssertTableCellHasClass(row, "delivery-action-cell");
        AssertTableCellHasClass(row, "collection-action-cell");
        AssertTableCellHasClass(row, "subscriber-cell");
    }

    private static void AssertMarkOnlyToggle(
        string rowHtml,
        string markerAttribute,
        bool expectedPressed,
        string subscriberName)
    {
        var button = Regex.Match(
            rowHtml,
            $"""
             <button\b
             (?=[^>]*\b{Regex.Escape(markerAttribute)}(?:\s|=|/?>))
             [^>]*>
             (?<content>.*?)
             </button>
             """,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.CultureInvariant);
        Assert.True(
            button.Success,
            $"Toggle with {markerAttribute} was not found.");

        var decodedButton = WebUtility.HtmlDecode(button.Value);
        Assert.Contains(
            $"aria-pressed=\"{expectedPressed.ToString().ToLowerInvariant()}\"",
            decodedButton,
            StringComparison.Ordinal);
        Assert.Contains(
            $"aria-label=\"{subscriberName}",
            decodedButton,
            StringComparison.Ordinal);
        Assert.True(
            Regex.IsMatch(
                decodedButton,
                """
                <span\b
                (?=[^>]*\bclass="[^"]*\btracking-toggle-icon\b[^"]*")
                (?=[^>]*\baria-hidden="true")
                [^>]*>
                """,
                RegexOptions.IgnoreCase |
                RegexOptions.IgnorePatternWhitespace |
                RegexOptions.CultureInvariant),
            $"Accessible status mark was not found for {markerAttribute}.");
        Assert.DoesNotContain(
            "data-toggle-label",
            decodedButton,
            StringComparison.Ordinal);

        var visibleContent = WebUtility.HtmlDecode(
            button.Groups["content"].Value);
        Assert.DoesNotContain(
            "Teslim et",
            visibleContent,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Teslim edildi",
            visibleContent,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Ödeme al",
            visibleContent,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Ödeme alındı",
            visibleContent,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertTableCellHasClass(
        string rowHtml,
        string cssClass)
    {
        var cell = Regex.Match(
            rowHtml,
            $"""
             <td\b
             (?=[^>]*\bclass="[^"]*\b{Regex.Escape(cssClass)}\b[^"]*")
             [^>]*>
             """,
            RegexOptions.IgnoreCase |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.CultureInvariant);
        Assert.True(
            cell.Success,
            $"Table cell class '{cssClass}' was not found.");
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

    private static FormUrlEncodedContent AutosaveForm(
        string antiforgeryToken,
        int subscriberId,
        bool delivered,
        bool collected,
        string amount,
        string paymentMethod) =>
        new(
        [
            new("__RequestVerificationToken", antiforgeryToken),
            new("Date", TestDate.ToString("yyyy-MM-dd")),
            new("SubscriberId", subscriberId.ToString()),
            new("Delivered", delivered.ToString()),
            new("Collected", collected.ToString()),
            new("Amount", amount),
            new("PaymentMethod", paymentMethod)
        ]);

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
                changes.Select(
                    change => new KeyValuePair<string, string>(
                        change.Key,
                        change.Value))));
}
