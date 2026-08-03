namespace GazeteDagitim.Web.Services;

public interface IBusinessClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }
}

public sealed class SystemBusinessClock : IBusinessClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _businessTimeZone;

    public SystemBusinessClock()
        : this(TimeProvider.System, ResolveBusinessTimeZone())
    {
    }

    public SystemBusinessClock(TimeProvider timeProvider, TimeZoneInfo businessTimeZone)
    {
        _timeProvider = timeProvider;
        _businessTimeZone = businessTimeZone;
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateOnly Today
    {
        get
        {
            var local = TimeZoneInfo.ConvertTime(UtcNow, _businessTimeZone);
            return DateOnly.FromDateTime(local.DateTime);
        }
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next platform-specific identifier.
            }
            catch (InvalidTimeZoneException)
            {
                // Try the next platform-specific identifier.
            }
        }

        return TimeZoneInfo.Utc;
    }
}
