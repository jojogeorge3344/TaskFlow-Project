using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Rules;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class TaskStatusTransitionsTests
{
    public static TheoryData<TaskStatus, TaskStatus, bool> TransitionExpectations => new()
    {
        // ToDo: -> self, InProgress, Blocked, Cancelled (not Completed)
        { TaskStatus.ToDo, TaskStatus.ToDo, true },
        { TaskStatus.ToDo, TaskStatus.InProgress, true },
        { TaskStatus.ToDo, TaskStatus.Blocked, true },
        { TaskStatus.ToDo, TaskStatus.Cancelled, true },
        { TaskStatus.ToDo, TaskStatus.Completed, false },

        // InProgress: -> self, Blocked, Completed, Cancelled (not ToDo)
        { TaskStatus.InProgress, TaskStatus.InProgress, true },
        { TaskStatus.InProgress, TaskStatus.Blocked, true },
        { TaskStatus.InProgress, TaskStatus.Completed, true },
        { TaskStatus.InProgress, TaskStatus.Cancelled, true },
        { TaskStatus.InProgress, TaskStatus.ToDo, false },

        // Blocked: -> self, InProgress, Cancelled (not ToDo, not Completed)
        { TaskStatus.Blocked, TaskStatus.Blocked, true },
        { TaskStatus.Blocked, TaskStatus.InProgress, true },
        { TaskStatus.Blocked, TaskStatus.Cancelled, true },
        { TaskStatus.Blocked, TaskStatus.ToDo, false },
        { TaskStatus.Blocked, TaskStatus.Completed, false },

        // Completed: terminal - only self
        { TaskStatus.Completed, TaskStatus.Completed, true },
        { TaskStatus.Completed, TaskStatus.ToDo, false },
        { TaskStatus.Completed, TaskStatus.InProgress, false },
        { TaskStatus.Completed, TaskStatus.Blocked, false },
        { TaskStatus.Completed, TaskStatus.Cancelled, false },

        // Cancelled: terminal - only self
        { TaskStatus.Cancelled, TaskStatus.Cancelled, true },
        { TaskStatus.Cancelled, TaskStatus.ToDo, false },
        { TaskStatus.Cancelled, TaskStatus.InProgress, false },
        { TaskStatus.Cancelled, TaskStatus.Blocked, false },
        { TaskStatus.Cancelled, TaskStatus.Completed, false }
    };

    [Theory]
    [MemberData(nameof(TransitionExpectations))]
    public void Can_MatchesTddTransitionTable(TaskStatus from, TaskStatus to, bool expected)
    {
        Assert.Equal(expected, TaskStatusTransitions.Can(from, to));
    }
}

public class TaskStageCalculatorTests
{
    private static readonly DateTime Today = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Calculate_WhenTaskIsCompleted_ReturnsCompletedRegardlessOfDeadline()
    {
        var pastDeadline = Today.AddDays(-5);

        Assert.Equal(TaskStage.Completed, TaskStageCalculator.Calculate(TaskStatus.Completed, pastDeadline, Today));
        Assert.Equal(TaskStage.Completed, TaskStageCalculator.Calculate(TaskStatus.Completed, Today.AddDays(5), Today));
    }

    [Fact]
    public void Calculate_WhenTaskIsCancelled_ReturnsCompletedTerminalStage()
    {
        Assert.Equal(TaskStage.Completed, TaskStageCalculator.Calculate(TaskStatus.Cancelled, Today.AddDays(2), Today));
    }

    [Fact]
    public void Calculate_WhenDeadlineIsPast_ReturnsOverdue()
    {
        Assert.Equal(TaskStage.Overdue, TaskStageCalculator.Calculate(TaskStatus.InProgress, Today.AddDays(-1), Today));
        Assert.Equal(TaskStage.Overdue, TaskStageCalculator.Calculate(TaskStatus.ToDo, Today.AddDays(-1), Today));
    }

    [Fact]
    public void Calculate_WhenDeadlineIsToday_ReturnsDueToday()
    {
        Assert.Equal(TaskStage.DueToday, TaskStageCalculator.Calculate(TaskStatus.InProgress, Today, Today));
    }

    [Fact]
    public void Calculate_WhenDeadlineIsLater_ReturnsUpcoming()
    {
        Assert.Equal(TaskStage.Upcoming, TaskStageCalculator.Calculate(TaskStatus.ToDo, Today.AddDays(3), Today));
    }

    [Fact]
    public void Calculate_OverdueWinsOverFutureOnly_NotForTerminal()
    {
        // Terminal state wins even when overdue.
        Assert.Equal(TaskStage.Completed, TaskStageCalculator.Calculate(TaskStatus.Completed, Today.AddDays(-3), Today));
    }
}