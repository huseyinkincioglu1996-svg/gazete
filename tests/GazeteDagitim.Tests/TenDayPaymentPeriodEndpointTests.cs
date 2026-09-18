using System.Net;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class TenDayPaymentPeriodEndpointTests
{
    [Fact]
    public async Task CreateForm_OffersTenDaySchedule()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);

        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync("/settings/create"));

        Assert.Contains(
            "<option value=\"ten-day\">10 günlük</option>",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTenDayPeriod_NormalizesCalendarSchedule()
    {
        await using var factory = new GazeteWebFactory();
        using var client = CreateClient(factory);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync(
            "/settings/create",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", antiforgeryToken),
                new("Name", "HTTP 10 Günlük Tahsilat"),
                new("ScheduleType", "ten-day"),
                new("CollectionDayOfMonth", ""),
                new("CollectionTime", "08:45"),
                new("DayCount", "31"),
                new("CollectionAmount", "100.00"),
                new("Description", "Ayın 10., 20. ve son günü tahsilat"),
                new("IsActive", "true")
            ]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/settings", response.Headers.Location?.OriginalString);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var period = await dbContext.PaymentPeriods
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(PaymentPeriodFrequency.Monthly, period.Frequency);
        Assert.Equal(10, period.DayCount);
        Assert.Equal(10, period.CollectionDayOfMonth);
        Assert.Equal(new TimeOnly(8, 45), period.CollectionTime);
        Assert.Equal(100m, period.CollectionAmount);

        var indexHtml = WebUtility.HtmlDecode(
            await client.GetStringAsync("/settings"));
        Assert.Contains(
            "Ayın 10., 20. ve son günü",
            indexHtml,
            StringComparison.Ordinal);
        Assert.Contains("10 gün", indexHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditForm_SelectsTenDayForExistingMonthlyRecord()
    {
        await using var factory = new GazeteWebFactory();
        int periodId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var period = new PaymentPeriod
            {
                Name = "Mevcut 10 Günlük Tahsilat",
                Frequency = PaymentPeriodFrequency.Monthly,
                DayCount = 10,
                CollectionDayOfMonth = 10,
                CollectionTime = new TimeOnly(9, 0),
                CollectionAmount = 100m,
                IsActive = true
            };
            dbContext.PaymentPeriods.Add(period);
            await dbContext.SaveChangesAsync();
            periodId = period.Id;
        }

        using var client = CreateClient(factory);
        var html = WebUtility.HtmlDecode(
            await client.GetStringAsync($"/settings/{periodId}/edit"));
        var selectedOption = Regex.Match(
            html,
            "<option(?=[^>]*value=\"ten-day\")(?=[^>]*selected)[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.True(selectedOption.Success);
    }

    private static HttpClient CreateClient(GazeteWebFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/settings/create");
        var input = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(input.Success);
        var value = Regex.Match(
            input.Value,
            "value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(value.Success);
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }
}
