namespace GazeteDagitim.Web.Models.ViewModels;

public sealed record ManagementOption(string Value, string Label);

public static class ManagementOptions
{
    public static IReadOnlyList<ManagementOption> BusinessDays { get; } =
    [
        new("Monday", "Pazartesi"),
        new("Tuesday", "Salı"),
        new("Wednesday", "Çarşamba"),
        new("Thursday", "Perşembe"),
        new("Friday", "Cuma"),
        new("Saturday", "Cumartesi"),
        new("Sunday", "Pazar")
    ];

    public static IReadOnlyList<ManagementOption> NewspaperDays { get; } =
    [
        .. BusinessDays,
        new("SundayMonday", "Pazar Pazartesi")
    ];

    public static IReadOnlyList<ManagementOption> PaymentTypes { get; } =
    [
        new("Daily", "Günlük"),
        new("Weekly", "Haftalık"),
        new("Monthly", "Aylık")
    ];

    public static string DayLabel(string value) =>
        NewspaperDays.FirstOrDefault(option =>
            string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))?.Label
        ?? value;

    public static string PaymentTypeLabel(string value) =>
        PaymentTypes.FirstOrDefault(option =>
            string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))?.Label
        ?? value;
}
