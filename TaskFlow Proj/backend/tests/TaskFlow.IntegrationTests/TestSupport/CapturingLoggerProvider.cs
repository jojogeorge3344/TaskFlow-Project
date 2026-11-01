using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TaskFlow.IntegrationTests.TestSupport;

/// <summary>Collects every log line (and exception text) the API writes during a test, so a test can assert that
/// a secret never appears in the logs.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _owner;

        public CapturingLogger(CapturingLoggerProvider owner)
        {
            _owner = owner;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _owner.Lines.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                _owner.Lines.Enqueue(exception.ToString());
            }
        }
    }
}
