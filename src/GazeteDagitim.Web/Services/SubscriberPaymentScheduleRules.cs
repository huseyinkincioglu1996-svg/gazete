using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;

namespace GazeteDagitim.Web.Services;

internal sealed record SubscriberScheduledPayment(
    DateOnly OriginalDueDate,
    int CoveredDayCount,
    decimal Amount);

internal sealed record SubscriberEffectivePayment(
    DateOnly OriginalDueDate,
    DateOnly EffectiveDueDate,
    int CoveredDayCount,
    decimal Amount,
    bool IsDeferred);

internal sealed record SubscriberDailyPaymentDue(
    int CoveredDayCount,
    decimal Amount);

internal static class SubscriberPaymentScheduleRules
{
    private const int DailyPeriod = 1;
    private const int TenDayPeriod = 10;
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    public static bool HasCompletePlan(PaymentPeriod? period) =>
        period?.CollectionDayOfMonth is >= 1 and <= 31 &&
        period.CollectionTime is not null &&
        period.CollectionAmount is > 0 &&
        period.DayCount is >= 1 and <= 365 &&
        Enum.IsDefined(period.Frequency) &&
        (period.Frequency != PaymentPeriodFrequency.Daily ||
         period.DayCount == DailyPeriod);

    public static bool IsTenDayPlan(PaymentPeriod? period) =>
        HasCompletePlan(period) && period!.DayCount == TenDayPeriod;

    public static bool IsDailyPlan(PaymentPeriod? period) =>
        HasCompletePlan(period) &&
        period!.Frequency == PaymentPeriodFrequency.Daily;

    public static DateOnly GetPlanStartDate(
        Subscriber subscriber,
        DateOnly fallbackDate) =>
        subscriber.PaymentPeriodStartedOn ??
        (subscriber.CreatedAt == default
            ? fallbackDate
            : ToBusinessDate(subscriber.CreatedAt));

    public static DateOnly GetScheduledDueDate(
        int year,
        int month,
        int collectionDayOfMonth) =>
        new(
            year,
            month,
            Math.Min(
                collectionDayOfMonth,
                DateTime.DaysInMonth(year, month)));

    public static IReadOnlyList<SubscriberScheduledPayment> GetScheduledPayments(
        Subscriber subscriber,
        DateOnly endDate,
        DateOnly fallbackStartDate)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var period = subscriber.PaymentPeriod;
        if (!HasCompletePlan(period))
        {
            return [];
        }

        var startedOn = GetPlanStartDate(subscriber, fallbackStartDate);
        var scheduleEnd = GetScheduleEndDate(subscriber, endDate);
        if (scheduleEnd < startedOn)
        {
            return [];
        }

        var payments = new List<SubscriberScheduledPayment>();
        if (IsDailyPlan(period))
        {
            var paymentDate = startedOn;
            while (paymentDate <= scheduleEnd)
            {
                payments.Add(new SubscriberScheduledPayment(
                    paymentDate,
                    DailyPeriod,
                    DomainRules.RoundCurrency(period!.CollectionAmount!.Value)));
                if (paymentDate == scheduleEnd)
                {
                    break;
                }

                paymentDate = paymentDate.AddDays(1);
            }

            return payments;
        }

        var month = new DateOnly(startedOn.Year, startedOn.Month, 1);
        var endMonth = new DateOnly(scheduleEnd.Year, scheduleEnd.Month, 1);

        while (month <= endMonth)
        {
            if (period!.DayCount == TenDayPeriod)
            {
                AddTenDayPaymentsForMonth(
                    payments,
                    period,
                    month,
                    startedOn,
                    scheduleEnd);
            }
            else
            {
                var dueDate = GetScheduledDueDate(
                    month.Year,
                    month.Month,
                    period.CollectionDayOfMonth!.Value);
                if (dueDate >= startedOn && dueDate <= scheduleEnd)
                {
                    payments.Add(new SubscriberScheduledPayment(
                        dueDate,
                        period.DayCount,
                        DomainRules.RoundCurrency(period.CollectionAmount!.Value)));
                }
            }

            month = month.AddMonths(1);
        }

        return payments;
    }

    public static SubscriberScheduledPayment? GetScheduledPaymentOn(
        Subscriber subscriber,
        DateOnly originalDueDate,
        DateOnly fallbackStartDate)
    {
        var period = subscriber.PaymentPeriod;
        if (!IsDailyPlan(period))
        {
            return GetScheduledPayments(
                    subscriber,
                    originalDueDate,
                    fallbackStartDate)
                .SingleOrDefault(value =>
                    value.OriginalDueDate == originalDueDate);
        }

        var startedOn = GetPlanStartDate(subscriber, fallbackStartDate);
        var scheduleEnd = GetScheduleEndDate(subscriber, originalDueDate);
        if (originalDueDate < startedOn || originalDueDate > scheduleEnd)
        {
            return null;
        }

        return new SubscriberScheduledPayment(
            originalDueDate,
            DailyPeriod,
            DomainRules.RoundCurrency(period!.CollectionAmount!.Value));
    }

    public static IReadOnlyList<SubscriberEffectivePayment> GetEffectivePaymentsOn(
        Subscriber subscriber,
        DateOnly effectiveDate,
        DateOnly fallbackStartDate)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (!HasCompletePlan(subscriber.PaymentPeriod))
        {
            return [];
        }

        var activeDeferrals = subscriber.PaymentDeferrals
            .Where(value => value.CancelledAt is null)
            .ToArray();
        var originalDates = activeDeferrals
            .Where(value => value.DeferredUntil == effectiveDate)
            .Select(value => value.OriginalDueDate)
            .Append(effectiveDate)
            .Distinct()
            .Order()
            .ToArray();
        var payments = new List<SubscriberEffectivePayment>();

        foreach (var originalDate in originalDates)
        {
            var scheduled = GetScheduledPaymentOn(
                subscriber,
                originalDate,
                fallbackStartDate);
            if (scheduled is null)
            {
                continue;
            }

            var deferral = activeDeferrals.SingleOrDefault(
                value => value.OriginalDueDate == originalDate);
            var resolvedDate = deferral?.DeferredUntil ?? originalDate;
            if (resolvedDate != effectiveDate)
            {
                continue;
            }

            payments.Add(new SubscriberEffectivePayment(
                scheduled.OriginalDueDate,
                resolvedDate,
                scheduled.CoveredDayCount,
                scheduled.Amount,
                deferral is not null));
        }

        return payments;
    }

    public static SubscriberDailyPaymentDue? GetDailyPaymentDue(
        Subscriber subscriber,
        DateOnly date,
        DateOnly fallbackStartDate)
    {
        var payments = GetEffectivePaymentsOn(
            subscriber,
            date,
            fallbackStartDate);
        if (payments.Count == 0)
        {
            return null;
        }

        return new SubscriberDailyPaymentDue(
            payments.Sum(value => value.CoveredDayCount),
            DomainRules.RoundCurrency(payments.Sum(value => value.Amount)));
    }

    public static bool IsEffectiveDueDate(
        Subscriber subscriber,
        DateOnly date,
        DateOnly fallbackStartDate) =>
        GetDailyPaymentDue(subscriber, date, fallbackStartDate) is not null;

    private static void AddTenDayPaymentsForMonth(
        ICollection<SubscriberScheduledPayment> payments,
        PaymentPeriod period,
        DateOnly month,
        DateOnly startedOn,
        DateOnly scheduleEnd)
    {
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        AddTenDayPayment(
            payments,
            period,
            new DateOnly(month.Year, month.Month, 1),
            new DateOnly(month.Year, month.Month, 10),
            startedOn,
            scheduleEnd);
        AddTenDayPayment(
            payments,
            period,
            new DateOnly(month.Year, month.Month, 11),
            new DateOnly(month.Year, month.Month, 20),
            startedOn,
            scheduleEnd);
        AddTenDayPayment(
            payments,
            period,
            new DateOnly(month.Year, month.Month, 21),
            new DateOnly(month.Year, month.Month, daysInMonth),
            startedOn,
            scheduleEnd);
    }

    private static void AddTenDayPayment(
        ICollection<SubscriberScheduledPayment> payments,
        PaymentPeriod period,
        DateOnly segmentStart,
        DateOnly dueDate,
        DateOnly startedOn,
        DateOnly scheduleEnd)
    {
        if (dueDate < startedOn || dueDate > scheduleEnd)
        {
            return;
        }

        var effectiveStart = startedOn > segmentStart
            ? startedOn
            : segmentStart;
        var coveredDayCount = dueDate.DayNumber - effectiveStart.DayNumber + 1;
        if (coveredDayCount <= 0)
        {
            return;
        }

        var amount = DomainRules.RoundCurrency(
            period.CollectionAmount!.Value *
            coveredDayCount /
            TenDayPeriod);
        payments.Add(new SubscriberScheduledPayment(
            dueDate,
            coveredDayCount,
            amount));
    }

    private static DateOnly GetScheduleEndDate(
        Subscriber subscriber,
        DateOnly requestedEndDate)
    {
        var deactivatedOn = GetDeactivationDate(subscriber);
        return deactivatedOn.HasValue && deactivatedOn.Value < requestedEndDate
            ? deactivatedOn.Value
            : requestedEndDate;
    }

    private static DateOnly? GetDeactivationDate(Subscriber subscriber)
    {
        if (subscriber.IsActive)
        {
            return null;
        }

        var timestamp = subscriber.DeactivatedAt ??
                        (subscriber.UpdatedAt != default
                            ? subscriber.UpdatedAt
                            : subscriber.CreatedAt);
        return timestamp == default ? null : ToBusinessDate(timestamp);
    }

    private static DateOnly ToBusinessDate(DateTimeOffset timestamp)
    {
        var local = TimeZoneInfo.ConvertTime(timestamp, BusinessTimeZone);
        return DateOnly.FromDateTime(local.DateTime);
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
