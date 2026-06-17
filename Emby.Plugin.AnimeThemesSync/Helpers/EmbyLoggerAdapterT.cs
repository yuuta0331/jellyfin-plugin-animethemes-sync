using Microsoft.Extensions.Logging;
using System;

namespace Emby.Plugin.AnimeThemesSync.Helpers;

/// <summary>
/// Typed logger adapter wrapper.
/// </summary>
/// <typeparam name="T">Category type.</typeparam>
public sealed class EmbyLoggerAdapter<T> : ILogger<T>
{
    private readonly EmbyLoggerAdapter _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbyLoggerAdapter{T}"/> class.
    /// </summary>
    /// <param name="inner">Untyped logger adapter.</param>
    public EmbyLoggerAdapter(EmbyLoggerAdapter inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return _inner.BeginScope(state);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return _inner.IsEnabled(logLevel);
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
    {
        _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
