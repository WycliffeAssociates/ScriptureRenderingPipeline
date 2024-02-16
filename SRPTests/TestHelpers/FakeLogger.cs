using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SRPTests.TestHelpers;

/*
 * This is a fake logger that can be used in unit tests.
 * It literally does nothing and is only here to satisfy the compiler.
 */
public class FakeLogger: ILogger
{
    public List<string> ErrorMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
    public List<string> InfoMessages { get; set; } = new();
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        switch (logLevel)
        {
            case LogLevel.Error:
                ErrorMessages.Add(formatter(state, exception));
                break;
            case LogLevel.Warning:
                WarningMessages.Add(formatter(state, exception));
                break;
            case LogLevel.Information:
                InfoMessages.Add(formatter(state, exception));
                break;
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }
}