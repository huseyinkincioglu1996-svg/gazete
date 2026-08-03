using GazeteDagitim.Web.Models.Enums;
using GazeteDagitim.Web.Services;

namespace GazeteDagitim.Web.Infrastructure;

public sealed class ScheduledJobsHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledJobsHostedService> logger) : BackgroundService
{
    private const int CatchUpDayCount = 7;
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var deliveriesProcessed = new HashSet<DateOnly>();
        var paymentsProcessed = new HashSet<DateOnly>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, BusinessTimeZone);
            var today = DateOnly.FromDateTime(localNow.DateTime);

            try
            {
                foreach (var date in BuildCatchUpDates(today))
                {
                    if (!deliveriesProcessed.Contains(date) &&
                        await RunDeliveriesAsync(date, stoppingToken))
                    {
                        deliveriesProcessed.Add(date);
                    }

                    var paymentDateIsReady = date < today ||
                                             localNow.TimeOfDay >=
                                             new TimeSpan(23, 59, 0);
                    if (paymentDateIsReady &&
                        !paymentsProcessed.Contains(date) &&
                        await RunPaymentsAsync(date, stoppingToken))
                    {
                        paymentsProcessed.Add(date);
                    }
                }

                var oldestCatchUpDate = today.AddDays(-(CatchUpDayCount - 1));
                deliveriesProcessed.RemoveWhere(value => value < oldestCatchUpDate);
                paymentsProcessed.RemoveWhere(value => value < oldestCatchUpDate);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Zamanlanmış gazete dağıtım/ödeme işi çalıştırılamadı.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    internal async Task<bool> RunDeliveriesAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPeriodicPaymentService>();
        var result = await service.CreateScheduledDeliveriesAsync(date, cancellationToken);
        logger.LogInformation(
            "{Date} dağıtım planı: {Created} oluşturuldu, {Existing} zaten vardı.",
            date,
            result.Created,
            result.Existing);
        if (result.Failed > 0)
        {
            logger.LogWarning(
                "{Date} dağıtım planında {Failed} kayıt başarısız oldu; iş yeniden denenecek.",
                date,
                result.Failed);
        }

        return result.Failed == 0;
    }

    internal async Task<bool> RunPaymentsAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var completedWithoutFailure = true;
        foreach (var paymentType in Enum.GetValues<PaymentType>())
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IPeriodicPaymentService>();
            var result = await service.CreateScheduledPaymentsAsync(
                paymentType,
                date,
                cancellationToken);
            logger.LogInformation(
                "{Date} {PaymentType} ödeme planı: {Created} oluşturuldu, " +
                "{Existing} zaten vardı, {Skipped} atlandı.",
                date,
                paymentType,
                result.Created,
                result.Existing,
                result.Skipped);
            if (result.Failed > 0)
            {
                logger.LogWarning(
                    "{Date} {PaymentType} ödeme planında {Failed} kayıt başarısız oldu; " +
                    "iş yeniden denenecek.",
                    date,
                    paymentType,
                    result.Failed);
            }

            completedWithoutFailure &= result.Failed == 0;
        }

        return completedWithoutFailure;
    }

    internal static IReadOnlyList<DateOnly> BuildCatchUpDates(DateOnly today) =>
        Enumerable.Range(0, CatchUpDayCount)
            .Select(offset => today.AddDays(offset - (CatchUpDayCount - 1)))
            .ToArray();

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
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
