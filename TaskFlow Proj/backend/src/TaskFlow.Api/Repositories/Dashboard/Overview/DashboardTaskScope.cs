namespace TaskFlow.Api.Repositories.Dashboard.Overview;

/// <summary>The live tasks a dashboard card reads: every one, those assigned to or notifying a team member, or those
/// assigned to or notifying one person.</summary>
public record DashboardTaskScope(IReadOnlyList<int>? TeamUserIds = null, int? OwnUserId = null);
