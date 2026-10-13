using FluentValidation.TestHelper;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Tasks.Validators;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using ReminderFrequency = TaskFlow.Domain.Enums.ReminderFrequency;

namespace TaskFlow.UnitTests;

public class CreateTaskRequestValidatorTests
{
    [Fact]
    public void CreateTaskRequest_EmptyTitle_IsInvalid()
    {
        var validator = new CreateTaskRequestValidator();

        var result = validator.TestValidate(ValidCreate() with { Title = string.Empty });

        result.ShouldHaveValidationErrorFor(task => task.Title);
    }

    [Fact]
    public void CreateTaskRequest_MissingAssignee_IsInvalid()
    {
        var validator = new CreateTaskRequestValidator();

        var result = validator.TestValidate(ValidCreate() with { AssignedToUserId = 0 });

        result.ShouldHaveValidationErrorFor(task => task.AssignedToUserId);
    }

    [Fact]
    public void CreateTaskRequest_DeadlineNotAfterStart_IsInvalid()
    {
        var validator = new CreateTaskRequestValidator();

        var result = validator.TestValidate(ValidCreate() with
        {
            StartDate = new DateTime(2026, 10, 15),
            Deadline = new DateTime(2026, 10, 15)
        });

        result.ShouldHaveValidationErrorFor(task => task.Deadline);
    }

    [Fact]
    public void CreateTaskRequest_ZeroEstimatedHours_IsInvalid()
    {
        var validator = new CreateTaskRequestValidator();

        var result = validator.TestValidate(ValidCreate() with { EstimatedHours = 0 });

        result.ShouldHaveValidationErrorFor(task => task.EstimatedHours);
    }

    [Fact]
    public void CreateTaskRequest_InvalidPriority_IsInvalid()
    {
        var validator = new CreateTaskRequestValidator();

        var result = validator.TestValidate(ValidCreate() with { Priority = (TaskPriority)999 });

        result.ShouldHaveValidationErrorFor(task => task.Priority);
    }

    [Fact]
    public void CreateTaskRequest_TooManyRecipients_IsInvalid()
    {
        var validator = new CreateTaskRequestValidator();

        var result = validator.TestValidate(ValidCreate() with
        {
            AdditionalRecipientUserIds = Enumerable.Range(1, 51).ToList()
        });

        result.ShouldHaveValidationErrorFor(task => task.AdditionalRecipientUserIds);
    }

    [Fact]
    public void CreateTaskRequest_Valid_IsValid()
    {
        var validator = new CreateTaskRequestValidator();

        var result = validator.TestValidate(ValidCreate() with { AdditionalRecipientUserIds = new[] { 3, 7 } });

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateTaskRequest ValidCreate() => new(
        "Prepare report",
        "October figures",
        3,
        TaskPriority.Medium,
        new DateTime(2026, 10, 13),
        new DateTime(2026, 10, 15),
        6m,
        AdditionalRecipientUserIds: null,
        FirstReminderDays: 2,
        SecondReminderDays: 1,
        OverdueReminderFrequency: ReminderFrequency.Daily);
}
