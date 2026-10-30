using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using TaskFlow.Api.Config;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Common.Security;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Mail;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Infrastructure.Services;
using User = TaskFlow.Api.Models.Entities.User;

namespace TaskFlow.UnitTests;

/// <summary>An in-memory database with a few users and a <see cref="MailService"/> on a controllable clock.</summary>
internal sealed class MailTestHarness : IAsyncDisposable
{
    private MailTestHarness()
    {
        Tracking = TaskTrackingServiceHarness.Create();
        Asha = Tracking.AddUser("asha", SystemRoles.Administrator);
        Ravi = Tracking.AddUser("ravi", SystemRoles.Manager);
        Priya = Tracking.AddUser("priya");
        Kiran = Tracking.AddUser("kiran");
    }

    public TaskTrackingServiceHarness Tracking { get; }

    public FixedTimeProvider Clock { get; } = new(new DateTime(2026, 10, 17, 6, 0, 0));

    public User Asha { get; }
    public User Ravi { get; }
    public User Priya { get; }
    public User Kiran { get; }

    public static MailTestHarness Create() => new();

    /// <summary>A fresh service each call, like a new request scope.</summary>
    public MailService Mail =>
        new(
            Tracking.Db,
            Clock,
            new AppClock(new ApplicationSettingsRepository(Tracking.Db), Clock, Options.Create(new ApplicationOptions { Timezone = "UTC" }), NullLogger<AppClock>.Instance));

    public static SendMailRequest To(IEnumerable<User> to, string subject = "Weekly report", string body = "Please review.", IEnumerable<User>? cc = null) =>
        new(to.Select(user => user.Email).ToList(), cc?.Select(user => user.Email).ToList(), subject, body, null);

    /// <summary>Sends and moves the clock on, so mails get distinct times.</summary>
    public async Task<MailItemDetailDto> SendAsync(User from, SendMailRequest request)
    {
        var sent = await Mail.SendAsync(from.Id, request) ?? throw new InvalidOperationException("Send returned null.");
        Clock.Set(Clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        return sent;
    }

    public async Task<IReadOnlyCollection<MailListItemDto>> FolderAsync(User user, MailView view, string? search = null) =>
        (await Mail.ListAsync(user.Id, new MailListQuery(view, search))).Items;

    public ValueTask DisposeAsync() => Tracking.DisposeAsync();
}
