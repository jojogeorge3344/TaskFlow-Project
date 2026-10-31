using TaskFlow.Api.Dtos.Assistant;
using Microsoft.AspNetCore.Http;
using TaskFlow.Api.Common.Exceptions;

namespace TaskFlow.UnitTests.Common;

public class AppExceptionTests
{
    public static TheoryData<AppException, int, string?> Cases => new()
    {
        { new BusinessRuleException("rule"), StatusCodes.Status400BadRequest, null },
        { new RequestValidationException("field", "bad"), StatusCodes.Status400BadRequest, null },
        { new ForbiddenException("no"), StatusCodes.Status403Forbidden, null },
        { new NotFoundException("gone"), StatusCodes.Status404NotFound, null },
        { new ConflictException("clash"), StatusCodes.Status409Conflict, null },
        { new RateLimitedException("slow down"), StatusCodes.Status429TooManyRequests, "Too many requests" },
        { new AnalyticsFilterException("filter"), StatusCodes.Status400BadRequest, null },
        { new AnalyticsEntityNotFoundException("missing"), StatusCodes.Status404NotFound, null },
        { new AnalyticsForbiddenException("hidden"), StatusCodes.Status403Forbidden, null },
        { new MailValidationException("to", "unknown"), StatusCodes.Status400BadRequest, null },
        { new AssistantRequestException("cannot"), StatusCodes.Status400BadRequest, "TFA could not handle that" },
        { new AssistantRateLimitedException(TimeSpan.FromSeconds(5)), StatusCodes.Status429TooManyRequests, "Too many requests" },
        { new AssistantActionConflictException("done"), StatusCodes.Status409Conflict, "This action can no longer be done" },
        { new RoleException(RoleFailure.NotFound, "x"), StatusCodes.Status404NotFound, null },
        { new RoleException(RoleFailure.Invalid, "x"), StatusCodes.Status400BadRequest, null },
        { new RoleException(RoleFailure.ReadOnly, "x"), StatusCodes.Status403Forbidden, null },
        { new RoleException(RoleFailure.Duplicate, "x"), StatusCodes.Status409Conflict, null },
        { new RoleException(RoleFailure.InUse, "x"), StatusCodes.Status409Conflict, null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachFailureKindHasItsStatusAndTitle(AppException exception, int status, string? title)
    {
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(title, exception.Title);
    }

    [Fact]
    public void ValidationExceptionCarriesFieldErrors()
    {
        var exception = new MailValidationException("to", "Unknown recipient.");

        Assert.Equal(["Unknown recipient."], exception.Errors["to"]);
        Assert.Equal("Unknown recipient.", exception.Message);
    }

    [Fact]
    public void AssistantRateLimitCarriesRetryAfter()
    {
        AppException exception = new AssistantRateLimitedException(TimeSpan.FromSeconds(12));

        Assert.Equal(TimeSpan.FromSeconds(12), exception.RetryAfter);
    }
}
