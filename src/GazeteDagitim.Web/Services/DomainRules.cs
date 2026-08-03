using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Services;

internal static class DomainRules
{
    public static BusinessDay ToBusinessDay(DateOnly date) =>
        date.DayOfWeek switch
        {
            DayOfWeek.Monday => BusinessDay.Monday,
            DayOfWeek.Tuesday => BusinessDay.Tuesday,
            DayOfWeek.Wednesday => BusinessDay.Wednesday,
            DayOfWeek.Thursday => BusinessDay.Thursday,
            DayOfWeek.Friday => BusinessDay.Friday,
            DayOfWeek.Saturday => BusinessDay.Saturday,
            DayOfWeek.Sunday => BusinessDay.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(date))
        };

    public static NewspaperDay ToNewspaperDay(BusinessDay day) => (NewspaperDay)(int)day;

    public static decimal RoundCurrency(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static (DateOnly Start, DateOnly EndExclusive) MonthRange(int year, int month)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            throw new DomainValidationException("Geçerli bir yıl ve ay belirtilmelidir.");
        }

        var start = new DateOnly(year, month, 1);
        return (start, start.AddMonths(1));
    }

    public static void EnsureValidRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainValidationException(
                "Bitiş tarihi başlangıç tarihinden önce olamaz.");
        }
    }
}
