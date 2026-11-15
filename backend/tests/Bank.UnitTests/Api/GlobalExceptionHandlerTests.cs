using Bank.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bank.UnitTests.Api;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task AnErrorAfterTheClientLeft_Is499_WithoutABody()
    {
        var problems = new RecordingProblemDetails();
        var handler = new GlobalExceptionHandler(problems, NullLogger<GlobalExceptionHandler>.Instance);
        using var gone = new CancellationTokenSource();
        await gone.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = gone.Token };

        // What SQL Server raises when the command is cancelled under it: not an OperationCanceledException.
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("batch aborted"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
        Assert.False(problems.Written);
    }

    [Fact]
    public async Task AnErrorWhileTheClientWaits_IsStillA500()
    {
        var problems = new RecordingProblemDetails();
        var handler = new GlobalExceptionHandler(problems, NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.True(problems.Written);
    }

    private sealed class RecordingProblemDetails : IProblemDetailsService
    {
        public bool Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = true;
            return ValueTask.FromResult(true);
        }
    }
}
