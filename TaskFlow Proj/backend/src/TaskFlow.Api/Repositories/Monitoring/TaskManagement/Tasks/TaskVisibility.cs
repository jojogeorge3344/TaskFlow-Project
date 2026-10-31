namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks;

/// <summary>Which live tasks a caller may see, as decided by the task service. With <see cref="ViewAll"/> every live task
/// is a candidate, otherwise only the caller's own (assigned, created or receiving notifications). When
/// <see cref="TeamUserIds"/> is given the candidates are further limited to the team's tasks (assigned to, or notifying,
/// a team member), plus, with <see cref="OrOwn"/>, the caller's own.</summary>
public record TaskVisibility(int UserId, bool ViewAll, IReadOnlyList<int>? TeamUserIds = null, bool OrOwn = false);
