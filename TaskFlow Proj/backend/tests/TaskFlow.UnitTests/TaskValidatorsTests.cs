using FluentValidation.TestHelper;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Tasks.Validators;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class TaskValidatorsTests
{
    [Fact]
    public void UpdateTaskRequest_EmptyTitle_IsInvalid()
    {
        var validator = new UpdateTaskRequestValidator();
        var request = ValidUpdate();

        var result = validator.TestValidate(request with { Title = string.Empty });

        result.ShouldHaveValidationErrorFor(task => task.Title);
    }

    [Fact]
    public void UpdateTaskRequest_DeadlineNotAfterStart_IsInvalid()
    {
        var validator = new UpdateTaskRequestValidator();

        var result = validator.TestValidate(ValidUpdate() with
        {
            StartDate = new DateTime(2026, 10, 15),
            Deadline = new DateTime(2026, 10, 15)
        });

        result.ShouldHaveValidationErrorFor(task => task.Deadline);
    }

    [Fact]
    public void UpdateTaskRequest_ZeroEstimatedHours_IsInvalid()
    {
        var validator = new UpdateTaskRequestValidator();

        var result = validator.TestValidate(ValidUpdate() with { EstimatedHours = 0 });

        result.ShouldHaveValidationErrorFor(task => task.EstimatedHours);
    }

    [Fact]
    public void UpdateTaskRequest_Valid_IsValid()
    {
        var validator = new UpdateTaskRequestValidator();

        var result = validator.TestValidate(ValidUpdate());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateTaskStatusRequest_OutOfRangeStatus_IsInvalid()
    {
        var validator = new UpdateTaskStatusRequestValidator();

        var result = validator.TestValidate(new UpdateTaskStatusRequest((TaskStatus)999));

        result.ShouldHaveValidationErrorFor(request => request.Status);
    }

    [Fact]
    public void UpdateTaskStatusRequest_ValidStatus_IsValid()
    {
        var validator = new UpdateTaskStatusRequestValidator();

        var result = validator.TestValidate(new UpdateTaskStatusRequest(TaskStatus.Completed));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ReassignTaskRequest_EmptyReason_IsInvalid()
    {
        var validator = new ReassignTaskRequestValidator();

        var result = validator.TestValidate(new ReassignTaskRequest(7, "  "));

        result.ShouldHaveValidationErrorFor(request => request.Reason);
    }

    [Fact]
    public void ReassignTaskRequest_ZeroNewAssignee_IsInvalid()
    {
        var validator = new ReassignTaskRequestValidator();

        var result = validator.TestValidate(new ReassignTaskRequest(0, "Handover"));

        result.ShouldHaveValidationErrorFor(request => request.NewAssigneeUserId);
    }

    [Fact]
    public void ReassignTaskRequest_Valid_IsValid()
    {
        var validator = new ReassignTaskRequestValidator();

        var result = validator.TestValidate(new ReassignTaskRequest(7, "Handover"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddCommentRequest_WhitespaceBody_IsInvalid()
    {
        var validator = new AddCommentRequestValidator();

        var result = validator.TestValidate(new AddCommentRequest(" \t "));

        result.ShouldHaveValidationErrorFor(request => request.Body);
    }

    [Fact]
    public void AddCommentRequest_Valid_IsValid()
    {
        var validator = new AddCommentRequestValidator();

        var result = validator.TestValidate(new AddCommentRequest("Looks good"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SetRecipientsRequest_TooManyRecipients_IsInvalid()
    {
        var validator = new SetRecipientsRequestValidator();

        var result = validator.TestValidate(new SetRecipientsRequest(Enumerable.Range(1, 51).ToList()));

        result.ShouldHaveValidationErrorFor(request => request.RecipientUserIds);
    }

    [Fact]
    public void SetRecipientsRequest_NonPositiveRecipient_IsInvalid()
    {
        var validator = new SetRecipientsRequestValidator();

        var result = validator.TestValidate(new SetRecipientsRequest(new[] { 0, 3 }));

        result.ShouldHaveValidationErrorFor(request => request.RecipientUserIds);
    }

    [Fact]
    public void SetRecipientsRequest_Valid_IsValid()
    {
        var validator = new SetRecipientsRequestValidator();

        var result = validator.TestValidate(new SetRecipientsRequest(new[] { 3, 7 }));

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static UpdateTaskRequest ValidUpdate() => new(
        "Prepare report",
        "October figures",
        TaskFlow.Api.Models.Enums.TaskPriority.Medium,
        new DateTime(2026, 10, 13),
        new DateTime(2026, 10, 15),
        6m);
}