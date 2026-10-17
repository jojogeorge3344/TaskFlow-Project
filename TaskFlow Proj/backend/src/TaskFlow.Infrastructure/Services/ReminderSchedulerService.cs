using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Reminders;

namespace TaskFlow.Infrastructure.Services;

/// <summary>The <c>Reminders</c> configuration section.</summary>
public class ReminderEngineOptions
{
    public const string SectionName = "Reminders";

    /// <summary>Turns the background scheduler on or off (tests and rollbacks switch it off).</summary>
    public bool SchedulerEnabled { get; set; } = true;

    /// <summary>Seconds between reminder passes.</summary>
    public int TickSeconds { get; set; } = 60;
}

/// <summary>Runs a reminder pass at startup and then on every tick (TDD §11.1). It keeps no state of its
/// own, so after a restart it simply resumes; duplicate prevention is the database's job.</summary>
public class ReminderSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReminderEngineOptions _options;
    private readonly ILogger<ReminderSchedulerService> _logger;

    public ReminderSchedulerService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReminderEngineOptions> options,
        ILogger<ReminderSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.SchedulerEnabled)
        {
            _logger.LogInformation("Reminder scheduler is disabled by configuration.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.TickSeconds));
        _logger.LogInformation("Reminder scheduler started; checking every {Interval}.", interval);

        await RunPassAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunPassAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async System.Threading.Tasks.Task RunPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
            await reminders.GenerateDueAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // One failed pass must never stop the scheduler; the next tick tries again.
            _logger.LogError(exception, "Reminder pass failed.");
        }
    }
}
