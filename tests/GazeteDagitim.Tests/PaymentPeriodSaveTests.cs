using System.Net;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class PaymentPeriodSaveTests
{
    [Fact]
    public async Task PeriodRows_AreCollapsibleAndClosedByDefault()
    {
        await using var factory = new GazeteWebFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.PaymentPeriods.Add(new PaymentPeriod
            {
                Name = "Test Periyodu",
                DayCount = 30,
                Frequency = PaymentPeriodFrequency.Monthly,
                CollectionDayOfMonth = 15,
                CollectionTime = new TimeOnly(9, 0),
                CollectionAmount = 300,
                IsActive = true
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/settings");
        var html = await response.Content.ReadAsStringAsync();
        var details = Regex.Match(
            html,
            "<details\\b[^>]*class=\"[^\"]*payment-period-row[^\"]*\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(details.Success);
        Assert.DoesNotContain(" open", details.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<summary", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ThreeDistinctPeriods_CanBeSavedConsecutively()
    {
        await using var factory = new GazeteWebFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var request in CreatePeriodRequests())
        {
            using var response = await PostPeriodAsync(client, request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/settings", response.Headers.Location?.OriginalString);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var records = await dbContext.PaymentPeriods
            .AsNoTracking()
            .OrderBy(period => period.Id)
            .ToListAsync();

        Assert.Equal(3, records.Count);
        Assert.Equal(7, records.Single(period => period.Name == "Test Haftalık").DayCount);
    }

    [Fact]
    public async Task DuplicateName_ReturnsValidationMessage_InsteadOfServerError()
    {
        await using var factory = new GazeteWebFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = CreatePeriodRequests()[0];

        using var firstResponse = await PostPeriodAsync(client, request);
        using var duplicateResponse = await PostPeriodAsync(client, request);
        var html = WebUtility.HtmlDecode(
            await duplicateResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Contains(
            "Bu adla bir ödeme periyodu zaten var.",
            html,
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<IReadOnlyList<(string Name, string Value)>>
        CreatePeriodRequests() =>
        [
            [
                ("Name", "Test Aylık"),
                ("ScheduleType", "monthly"),
                ("CollectionDayOfMonth", "15"),
                ("CollectionTime", "09:00"),
                ("DayCount", "30"),
                ("CollectionAmount", "300.00"),
                ("Description", "Birinci kayıt"),
                ("IsActive", "true")
            ],
            [
                ("Name", "Test Haftalık"),
                ("ScheduleType", "weekly"),
                ("CollectionDayOfMonth", ""),
                ("CollectionTime", "10:00"),
                ("DayCount", "31"),
                ("CollectionAmount", "70.00"),
                ("Description", "İkinci kayıt"),
                ("IsActive", "true")
            ],
            [
                ("Name", "Test Günlük"),
                ("ScheduleType", "daily"),
                ("CollectionDayOfMonth", ""),
                ("CollectionTime", "11:00"),
                ("DayCount", "30"),
                ("CollectionAmount", "10.00"),
                ("Description", "Üçüncü kayıt"),
                ("IsActive", "true")
            ]
        ];

    private static async Task<HttpResponseMessage> PostPeriodAsync(
        HttpClient client,
        IReadOnlyList<(string Name, string Value)> request)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        var fields = request
            .Prepend((Name: "__RequestVerificationToken", Value: token))
            .Select(field =>
                new KeyValuePair<string, string>(field.Name, field.Value));

        return await client.PostAsync(
            "/settings/create",
            new FormUrlEncodedContent(fields));
    }

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
