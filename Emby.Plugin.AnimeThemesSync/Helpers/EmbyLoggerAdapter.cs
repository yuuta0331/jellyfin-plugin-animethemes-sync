using System;
using Microsoft.Extensions.Logging;
using EmbyLogger = MediaBrowser.Model.Logging.ILogger;

namespace Emby.Plugin.AnimeThemesSync.Helpers;

/// <summary>
/// Adapter from Emby logger to Microsoft.Extensions.Logging abstraction.
/// </summary>
public sealed class EmbyLoggerAdapter : Microsoft.Extensions.Logging.ILogger
{
    private readonly EmbyLogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbyLoggerAdapter"/> class.
    /// </summary>
    /// <param name="logger">Emby logger.</param>
    public EmbyLoggerAdapter(EmbyLogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);

        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                _logger.Debug("{0}", message);
                break;
            case LogLevel.Information:
                _logger.Info("{0}", message);
                break;
            case LogLevel.Warning:
                if (exception != null)
                {
                    _logger.ErrorException("{0}", exception, message);
                }
                else
                {
                    _logger.Warn("{0}", message);
                }

                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                if (exception != null)
                {
                    _logger.ErrorException("{0}", exception, message);
                }
                else
                {
                    _logger.Error("{0}", message);
                }

                break;
            default:
                _logger.Info("{0}", message);
                break;
        }
    }

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
