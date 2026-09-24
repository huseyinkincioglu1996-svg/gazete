using System.Reflection;
using GazeteDagitim.Web.Models.ViewModels;
using GazeteDagitim.Web.Models.ViewModels.Distributors;
using GazeteDagitim.Web.Models.ViewModels.PaymentPeriods;
using GazeteDagitim.Web.Models.ViewModels.Subscribers;
using Microsoft.AspNetCore.Mvc;

namespace GazeteDagitim.Tests;

public sealed class InvariantDecimalBindingTests
{
    [Fact]
    public void PostedDecimalProperties_UseInvariantDecimalBinder()
    {
        Type[] postedModelTypes =
        [
            typeof(DistributorFormViewModel),
            typeof(SubscriberFormViewModel),
            typeof(PaymentPeriodFormViewModel),
            typeof(DailyDeliveryRowInputModel),
            typeof(DailyDeliveryRowAutosaveInputModel),
            typeof(CashHandoverManualItemInputModel),
            typeof(CompanySettingsInputModel)
        ];

        var decimalProperties = postedModelTypes
            .SelectMany(type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => IsDecimal(property.PropertyType))
                .Select(property => (ModelType: type, Property: property)))
            .ToArray();

        Assert.NotEmpty(decimalProperties);
        foreach (var (modelType, property) in decimalProperties)
        {
            var attribute = property.GetCustomAttribute<ModelBinderAttribute>();
            Assert.True(
                attribute?.BinderType == typeof(InvariantDecimalModelBinder),
                $"{modelType.Name}.{property.Name} must use " +
                $"{nameof(InvariantDecimalModelBinder)}.");
        }
    }

    private static bool IsDecimal(Type type) =>
        type == typeof(decimal) || type == typeof(decimal?);
}
