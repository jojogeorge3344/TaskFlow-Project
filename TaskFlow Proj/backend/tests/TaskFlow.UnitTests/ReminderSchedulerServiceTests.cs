using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Reminders;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

public class ReminderSchedulerServiceTests
{
    private sealed class CountingReminders(bool fail) : IReminderService
    {
        public int Calls { get; private set; }

        public Task<int> GenerateDueAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return fail ? throw new InvalidOperationException("reminders broke") : Task.FromResult(0);
        }
    }

    private static ReminderSchedulerService Scheduler(IReminderService reminders)
    {
        var services = new ServiceCollection()
            .AddSingleton(reminders)
            .BuildServiceProvider();

        return new ReminderSchedulerService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ReminderEngineOptions()),
            NullLogger<ReminderSchedulerService>.Instance);
    }

    [Fact]
    public async Task Failing_pass_is_logged_and_the_next_pass_still_runs()
    {
        var reminders = new CountingReminders(fail: true);
        var scheduler = Scheduler(reminders);

        await scheduler.RunPassAsync(CancellationToken.None);
        await scheduler.RunPassAsync(CancellationToken.None);

        Assert.Equal(2, reminders.Calls);
    }
}
