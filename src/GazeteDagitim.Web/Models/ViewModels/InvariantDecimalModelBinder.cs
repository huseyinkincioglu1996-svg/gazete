using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GazeteDagitim.Web.Models.ViewModels;

/// <summary>
/// Parses decimal values emitted by HTML number inputs without treating their
/// invariant decimal point as a Turkish thousands separator.
/// </summary>
public sealed class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(
            bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(
            bindingContext.ModelName,
            valueProviderResult);

        var rawValue = valueProviderResult.FirstValue?.Trim();
        if (string.IsNullOrEmpty(rawValue))
        {
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                "Tutar alanı zorunludur.");
            return Task.CompletedTask;
        }

        var normalizedValue = NormalizeDecimalSeparators(rawValue);
        if (decimal.TryParse(
                normalizedValue,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsedValue))
        {
            bindingContext.Result = ModelBindingResult.Success(parsedValue);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            "Geçerli bir tutar girin.");
        return Task.CompletedTask;
    }

    private static string NormalizeDecimalSeparators(string value)
    {
        var commaIndex = value.LastIndexOf(',');
        var dotIndex = value.LastIndexOf('.');

        if (commaIndex >= 0 && dotIndex >= 0)
        {
            return commaIndex > dotIndex
                ? value.Replace(".", string.Empty, StringComparison.Ordinal)
                    .Replace(',', '.')
                : value.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        return commaIndex >= 0 ? value.Replace(',', '.') : value;
    }
}
