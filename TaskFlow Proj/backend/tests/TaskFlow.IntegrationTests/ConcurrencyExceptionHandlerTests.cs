using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api;

namespace TaskFlow.IntegrationTests;

public class ConcurrencyExceptionHandlerTests
{
    private readonly ConcurrencyExceptionHandler _handler = new();

    [Fact]
    public async Task A_lost_save_race_becomes_a_409_conflict()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await _handler.TryHandleAsync(context, new DbUpdateConcurrencyException("race"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task Other_exceptions_are_left_to_the_default_handler()
    {
        var context = new DefaultHttpContext();

        Assert.False(await _handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None));
    }
}
