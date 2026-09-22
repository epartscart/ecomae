using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Opt-in twin of PHP <c>epc-currency-live-rates-cron.php</c>: calls the nightly FX tick
/// periodically; the tick itself only applies when the CP-configured window is due.
/// Disabled by default so the PHP cron stays authoritative until the host enables it.
/// </summary>
public sealed class CpCurrencyFxScheduleOptions
{
    public const string SectionName = "Cp:CurrencyFxSchedule";

    public bool Enabled { get; set; }

    public int IntervalMinutes { get; set; } = 5;
}

public sealed class CpCurrencyFxScheduleHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<CpCurrencyFxScheduleOptions> _options;
    private readonly ILogger<CpCurrencyFxScheduleHostedService> _logger;

    public CpCurrencyFxScheduleHostedService(
        IServiceScopeFactory scopes,
        IOptions<CpCurrencyFxScheduleOptions> options,
        ILogger<CpCurrencyFxScheduleHostedService> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("CP currency FX schedule worker disabled ({Section}:Enabled=false).", CpCurrencyFxScheduleOptions.SectionName);
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(options.IntervalMinutes, 1, 60));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var live = scope.ServiceProvider.GetRequiredService<ICpCurrencyLiveRatesService>();
                var tick = await live.TickAsync(force: false, stoppingToken).ConfigureAwait(false);
                if (tick.Ran)
                {
                    _logger.LogInformation("CP currency FX nightly apply ran: ok={Ok} updated={Updated} provider={Provider} error={Error}", tick.Ok, tick.Updated, tick.Provider, tick.Error);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CP currency FX schedule tick failed.");
            }
        }
    }
}
