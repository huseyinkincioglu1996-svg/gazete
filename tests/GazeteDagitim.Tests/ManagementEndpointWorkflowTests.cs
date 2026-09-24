using System.Net;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class ManagementEndpointWorkflowTests
{
    [Fact]
    public async Task Distributor_CreateEditDeactivate_CompletesThroughHttpForms()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);

        var token = await GetAntiforgeryTokenAsync(client, "/distributors/create");
        using var createResponse = await client.PostAsync(
            "/distributors/create",
            Form(
                token,
                ("Name", "Uçtan Uca Dağıtıcı"),
                ("Address", "Test adresi"),
                ("Phone", "05550000000"),
                ("Zone", "Region1"),
                ("PaymentType", "Weekly"),
                ("NewspaperPrice", "5.25"),
                ("DistributionDays", "Monday"),
                ("DistributionDays", "Friday"),
                ("WeeklyPaymentDays", "Friday")));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int distributorId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var distributor = await dbContext.Distributors
                .AsNoTracking()
                .Include(value => value.DistributionDays)
                .Include(value => value.WeeklyPaymentDays)
                .SingleAsync();
            distributorId = distributor.Id;
            Assert.Equal(5.25m, distributor.NewspaperPrice);
            Assert.Equal(PaymentType.Weekly, distributor.PaymentType);
            Assert.Equal(2, distributor.DistributionDays.Count);
            Assert.Single(distributor.WeeklyPaymentDays);
        }

        token = await GetAntiforgeryTokenAsync(
            client,
            $"/distributors/{distributorId}/edit");
        using var editResponse = await client.PostAsync(
            $"/distributors/{distributorId}/edit",
            Form(
                token,
                ("Id", distributorId.ToString()),
                ("Name", "Güncel Dağıtıcı"),
                ("Address", "Güncel adres"),
                ("Phone", "05551111111"),
                ("Zone", "Region2"),
                ("PaymentType", "Monthly"),
                ("NewspaperPrice", "7.75"),
                ("DistributionDays", "Tuesday"),
                ("MonthlyPaymentDays", "10"),
                ("MonthlyPaymentDays", "20")));

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        token = await GetAntiforgeryTokenAsync(client, "/distributors");
        using var deactivateResponse = await client.PostAsync(
            $"/distributors/{distributorId}/deactivate",
            Form(token, ("includeInactive", "true")));

        Assert.Equal(HttpStatusCode.Redirect, deactivateResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var distributor = await dbContext.Distributors
                .AsNoTracking()
                .Include(value => value.DistributionDays)
                .Include(value => value.WeeklyPaymentDays)
                .Include(value => value.MonthlyPaymentDays)
                .SingleAsync(value => value.Id == distributorId);
            Assert.Equal("Güncel Dağıtıcı", distributor.Name);
            Assert.Equal(7.75m, distributor.NewspaperPrice);
            Assert.Equal(DistributorZone.Region2, distributor.Zone);
            Assert.Equal(PaymentType.Monthly, distributor.PaymentType);
            Assert.False(distributor.IsActive);
            Assert.Single(distributor.DistributionDays);
            Assert.Empty(distributor.WeeklyPaymentDays);
            Assert.Equal([10, 20], distributor.MonthlyPaymentDays
                .OrderBy(value => value.DayOfMonth)
                .Select(value => value.DayOfMonth)
                .ToArray());
        }
    }

    [Fact]
    public async Task Subscriber_CreateEditToggle_PreservesBrowserDecimalValues()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var (periodId, distributorId) = await SeedSubscriberReferencesAsync(factory);

        var token = await GetAntiforgeryTokenAsync(client, "/subscribers/create");
        using var createResponse = await client.PostAsync(
            "/subscribers/create",
            Form(
                token,
                ("Name", "Uçtan Uca Abone"),
                ("Phone", "05552222222"),
                ("Address", "Abone adresi"),
                ("MonthlyFee", "125.50"),
                ("FirstDeliveryDate", "2026-07-01"),
                ("PaymentPeriodId", periodId.ToString()),
                ("DistributorId", distributorId.ToString()),
                ("Latitude", "41.0123"),
                ("Longitude", "28.9876"),
                ("NewspaperDays", "Monday"),
                ("NewspaperDays", "Wednesday"),
                ("IsActive", "true")));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int subscriberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscriber = await dbContext.Subscribers
                .AsNoTracking()
                .Include(value => value.NewspaperDays)
                .SingleAsync();
            subscriberId = subscriber.Id;
            Assert.Equal(125.50m, subscriber.MonthlyFee);
            Assert.Equal(41.0123m, subscriber.Latitude);
            Assert.Equal(28.9876m, subscriber.Longitude);
            Assert.True(subscriber.IsActive);
            Assert.Equal(2, subscriber.NewspaperDays.Count);
        }

        token = await GetAntiforgeryTokenAsync(
            client,
            $"/subscribers/{subscriberId}/edit");
        using var editResponse = await client.PostAsync(
            $"/subscribers/{subscriberId}/edit",
            Form(
                token,
                ("Id", subscriberId.ToString()),
                ("Name", "Güncel Abone"),
                ("Phone", "05553333333"),
                ("Address", "Güncel abone adresi"),
                ("MonthlyFee", "150.75"),
                ("FirstDeliveryDate", "2026-07-02"),
                ("PaymentPeriodId", periodId.ToString()),
                ("DistributorId", distributorId.ToString()),
                ("Latitude", "40.125"),
                ("Longitude", "29.625"),
                ("NewspaperDays", "Friday")));

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        token = await GetAntiforgeryTokenAsync(client, "/subscribers?status=all");
        using var toggleResponse = await client.PostAsync(
            $"/subscribers/{subscriberId}/toggle-status",
            Form(
                token,
                ("isActive", "true"),
                ("status", "all")));

        Assert.Equal(HttpStatusCode.Redirect, toggleResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscriber = await dbContext.Subscribers
                .AsNoTracking()
                .Include(value => value.NewspaperDays)
                .SingleAsync(value => value.Id == subscriberId);
            Assert.Equal("Güncel Abone", subscriber.Name);
            Assert.Equal(150.75m, subscriber.MonthlyFee);
            Assert.Equal(40.125m, subscriber.Latitude);
            Assert.Equal(29.625m, subscriber.Longitude);
            Assert.True(subscriber.IsActive);
            Assert.Null(subscriber.DeactivatedAt);
            Assert.Equal(NewspaperDay.Friday, subscriber.NewspaperDays.Single().Day);
        }
    }

    [Fact]
    public async Task PaymentPeriod_CreateEditToggle_CompletesThroughHttpForms()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);

        var token = await GetAntiforgeryTokenAsync(client, "/settings/create");
        using var createResponse = await client.PostAsync(
            "/settings/create",
            Form(
                token,
                ("Name", "Uçtan Uca Periyot"),
                ("ScheduleType", "weekly"),
                ("DayCount", "99"),
                ("CollectionDayOfMonth", "31"),
                ("CollectionTime", "11:30"),
                ("CollectionAmount", "87.65"),
                ("Description", "Haftalık test"),
                ("IsActive", "true")));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int periodId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var period = await dbContext.PaymentPeriods.AsNoTracking().SingleAsync();
            periodId = period.Id;
            Assert.Equal(PaymentPeriodFrequency.Weekly, period.Frequency);
            Assert.Equal(7, period.DayCount);
            Assert.Equal(87.65m, period.CollectionAmount);
        }

        token = await GetAntiforgeryTokenAsync(client, $"/settings/{periodId}/edit");
        using var editResponse = await client.PostAsync(
            $"/settings/{periodId}/edit",
            Form(
                token,
                ("Id", periodId.ToString()),
                ("Name", "Güncel Periyot"),
                ("ScheduleType", "ten-day"),
                ("DayCount", "30"),
                ("CollectionDayOfMonth", "1"),
                ("CollectionTime", "13:45"),
                ("CollectionAmount", "112.35"),
                ("Description", "On günlük test"),
                ("IsActive", "true")));

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        token = await GetAntiforgeryTokenAsync(client, "/settings");
        using var toggleResponse = await client.PostAsync(
            $"/settings/{periodId}/toggle-status",
            Form(
                token,
                ("isActive", "false"),
                ("status", "all")));

        Assert.Equal(HttpStatusCode.Redirect, toggleResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var period = await dbContext.PaymentPeriods
                .AsNoTracking()
                .SingleAsync(value => value.Id == periodId);
            Assert.Equal("Güncel Periyot", period.Name);
            Assert.Equal(PaymentPeriodFrequency.Monthly, period.Frequency);
            Assert.Equal(10, period.DayCount);
            Assert.Equal(10, period.CollectionDayOfMonth);
            Assert.Equal(new TimeOnly(13, 45), period.CollectionTime);
            Assert.Equal(112.35m, period.CollectionAmount);
            Assert.False(period.IsActive);
        }
    }

    [Fact]
    public async Task CompanySettingsSave_AndDistributorPaymentComplete_ThroughHttpForms()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var (distributorId, paymentId) = await SeedPaymentAsync(factory);

        var token = await GetAntiforgeryTokenAsync(
            client,
            "/menu/company/settings");
        using var settingsResponse = await client.PostAsync(
            "/menu/company/settings/save",
            Form(
                token,
                ("FeaturedDistributorId", distributorId.ToString()),
                ("NewspaperUnitPrice", "12.75"),
                ("ShowDistributorAndCoverage", "true")));

        Assert.Equal(HttpStatusCode.Redirect, settingsResponse.StatusCode);

        token = await GetAntiforgeryTokenAsync(
            client,
            $"/payments?month=2026-07&distributorId={distributorId}");
        using var paymentResponse = await client.PostAsync(
            "/payments/mark-paid",
            Form(
                token,
                ("id", paymentId.ToString()),
                ("month", "2026-07"),
                ("distributorId", distributorId.ToString())));

        Assert.Equal(HttpStatusCode.Redirect, paymentResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await dbContext.CompanySettings.AsNoTracking().SingleAsync();
        var payment = await dbContext.Payments.AsNoTracking().SingleAsync();
        Assert.Equal(distributorId, settings.FeaturedDistributorId);
        Assert.Equal(12.75m, settings.NewspaperUnitPrice);
        Assert.True(settings.ShowDistributorAndCoverage);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.NotNull(payment.PaidAt);
    }

    [Fact]
    public async Task SubscriberPaymentDeferral_Cancel_CompletesThroughHttpForm()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var (subscriberId, deferralId) = await SeedDeferralAsync(factory);

        var token = await GetAntiforgeryTokenAsync(
            client,
            $"/subscribers/{subscriberId}/payments");
        using var response = await client.PostAsync(
            $"/subscribers/{subscriberId}/payments/deferrals/{deferralId}/cancel",
            Form(token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deferral = await dbContext.SubscriberPaymentDeferrals
            .AsNoTracking()
            .SingleAsync(value => value.Id == deferralId);
        Assert.NotNull(deferral.CancelledAt);
    }

    private static HttpClient CreateClient(GazeteWebFactory factory) =>
        factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

    private static async Task<(int PaymentPeriodId, int DistributorId)>
        SeedSubscriberReferencesAsync(GazeteWebFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var period = new PaymentPeriod
        {
            Name = "Test Periyodu",
            DayCount = 30,
            CollectionDayOfMonth = 1,
            CollectionTime = new TimeOnly(9, 0),
            CollectionAmount = 125.50m,
            IsActive = true
        };
        var distributor = new Distributor
        {
            Name = "Test Dağıtıcısı",
            Address = "Test adresi",
            Phone = "05554444444",
            Zone = DistributorZone.Region1,
            NewspaperPrice = 5m,
            IsActive = true
        };
        dbContext.AddRange(period, distributor);
        await dbContext.SaveChangesAsync();
        return (period.Id, distributor.Id);
    }

    private static async Task<(int DistributorId, int PaymentId)> SeedPaymentAsync(
        GazeteWebFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var distributor = new Distributor
        {
            Name = "Ödeme Dağıtıcısı",
            Address = "Test adresi",
            Phone = "05555555555",
            Zone = DistributorZone.Region1,
            PaymentType = PaymentType.Monthly,
            NewspaperPrice = 5m,
            IsActive = true
        };
        var payment = new Payment
        {
            Distributor = distributor,
            Amount = 500m,
            Date = new DateOnly(2026, 7, 31),
            PeriodStart = new DateOnly(2026, 7, 1),
            PeriodEnd = new DateOnly(2026, 7, 31),
            PaymentType = PaymentType.Monthly,
            Status = PaymentStatus.Pending
        };
        dbContext.Add(payment);
        await dbContext.SaveChangesAsync();
        return (distributor.Id, payment.Id);
    }

    private static async Task<(int SubscriberId, int DeferralId)> SeedDeferralAsync(
        GazeteWebFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscriber = new Subscriber
        {
            Name = "Erteleme Abonesi",
            IsActive = true,
            FirstDeliveryDate = new DateOnly(2026, 7, 1)
        };
        var deferral = new SubscriberPaymentDeferral
        {
            Subscriber = subscriber,
            OriginalDueDate = new DateOnly(2026, 7, 10),
            PreviousDueDate = new DateOnly(2026, 7, 10),
            DeferredUntil = new DateOnly(2026, 7, 15),
            Reason = "Uçtan uca iptal testi"
        };
        dbContext.Add(deferral);
        await dbContext.SaveChangesAsync();
        return (subscriber.Id, deferral.Id);
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

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Key, string Value)[] values) =>
        new(
            new[]
            {
                new KeyValuePair<string, string>(
                    "__RequestVerificationToken",
                    antiforgeryToken)
            }.Concat(
                values.Select(
                    value => new KeyValuePair<string, string>(
                        value.Key,
                        value.Value))));
}
