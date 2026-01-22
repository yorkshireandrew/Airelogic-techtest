using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace HealthTest.Test
{
    public class TestLogger : ILogger<LandingSubmitHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new List<(LogLevel, string)>();

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter != null ? formatter(state, exception) : state?.ToString() ?? string.Empty;
            Entries.Add((logLevel, message));
        }

        private class NullScope : IDisposable { public static NullScope Instance { get; } = new NullScope(); public void Dispose() { } }
    }
}
