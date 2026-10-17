using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Email;
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

    private sealed class CountingDispatcher(bool fail) : IEmailDispatcher
    {
        public int Calls { get; private set; }

        public Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return fail ? throw new InvalidOperationException("smtp broke") : Task.FromResult(0);
        }
    }

    private static ReminderSchedulerService Scheduler(IReminderService reminders, IEmailDispatcher dispatcher)
    {
        var services = new ServiceCollection()
            .AddSingleton(reminders)
            .AddSingleton(dispatcher)
            .BuildServiceProvider();

        return new ReminderSchedulerService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ReminderEngineOptions()),
            NullLogger<ReminderSchedulerService>.Instance);
    }

    [Fact]
    public async Task Failing_dispatcher_does_not_stop_reminder_passes()
    {
        var reminders = new CountingReminders(fail: false);
        var dispatcher = new CountingDispatcher(fail: true);
        var scheduler = Scheduler(reminders, dispatcher);

        await scheduler.RunPassAsync(CancellationToken.None);
        await scheduler.RunPassAsync(CancellationToken.None);

        Assert.Equal(2, reminders.Calls);
        Assert.Equal(2, dispatcher.Calls);
    }

    [Fact]
    public async Task Failing_reminder_pass_still_dispatches_emails()
    {
        var reminders = new CountingReminders(fail: true);
        var dispatcher = new CountingDispatcher(fail: false);

        await Scheduler(reminders, dispatcher).RunPassAsync(CancellationToken.None);

        Assert.Equal(1, reminders.Calls);
        Assert.Equal(1, dispatcher.Calls);
    }
}
