using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Models.ViewModels.Subscribers;
using Microsoft.Extensions.DependencyInjection;

namespace GazeteDagitim.Tests;

public sealed class InputDateValidationTests
{
    [Fact]
    public void PostedDateProperties_RejectUnsupportedDates()
    {
        Type[] postedModelTypes =
        [
            typeof(DailyDeliveriesInputModel),
            typeof(DailyDeliveryRowAutosaveInputModel),
            typeof(DailyNewspaperCashSaleInputModel),
            typeof(CashHandoverInputModel),
            typeof(SubscriberFormViewModel),
            typeof(SubscriberPaymentDeferralInputModel)
        ];

        var dateProperties = postedModelTypes
            .SelectMany(type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => IsDate(property.PropertyType))
                .Select(property => (ModelType: type, Property: property)))
            .ToArray();

        Assert.NotEmpty(dateProperties);
        foreach (var (modelType, property) in dateProperties)
        {
            Assert.True(
                property.GetCustomAttribute<SupportedDateAttribute>() is not null,
                $"{modelType.Name}.{property.Name} must use " +
                $"{nameof(SupportedDateAttribute)}.");
        }
    }

    [Fact]
    public async Task SubscriberCreate_WithMinimumDate_IsRejectedWithoutSaving()
    {
        await using var factory = new GazeteWebFactory();
        using var client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        var token = await GetAntiforgeryTokenAsync(client, "/subscribers/create");

        using var response = await client.PostAsync(
            "/subscribers/create",
            Form(
                token,
                ("Name", "Geçersiz Tarihli Abone"),
                ("MonthlyFee", "100.00"),
                ("FirstDeliveryDate", "0001-01-01"),
                ("IsActive", "true")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("01.01.1900", html, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(dbContext.Subscribers);
    }

    [Theory]
    [InlineData("gazete.turnaexpress.com.tr", HttpStatusCode.OK)]
    [InlineData("localhost", HttpStatusCode.OK)]
    [InlineData("evil.example", HttpStatusCode.BadRequest)]
    public async Task HostFiltering_AllowsOnlyConfiguredApplicationHosts(
        string host,
        HttpStatusCode expectedStatus)
    {
        await using var factory = new GazeteWebFactory();
        using var client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/menu");
        request.Headers.Host = host;

        using var response = await client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private static bool IsDate(Type type) =>
        type == typeof(DateOnly) || type == typeof(DateOnly?);

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
