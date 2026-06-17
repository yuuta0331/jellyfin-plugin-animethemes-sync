using MediaBrowser.Model.Logging;
using System;

namespace Emby.Plugin.AnimeThemesSync.Extensions;

/// <summary>
/// Compatibility extensions to keep message-template style logging calls.
/// </summary>
public static class EmbyLoggerExtensions
{
    public static void LogInformation(this ILogger logger, string message, params object?[] args)
    {
        logger.Info(message, args);
    }

    public static void LogDebug(this ILogger logger, string message, params object?[] args)
    {
        logger.Debug(message, args);
    }

    public static void LogWarning(this ILogger logger, string message, params object?[] args)
    {
        logger.Warn(message, args);
    }

    public static void LogWarning(this ILogger logger, Exception exception, string message, params object?[] args)
    {
        logger.Warn(message + " Exception: " + exception.Message, args);
    }

    public static void LogError(this ILogger logger, string message, params object?[] args)
    {
        logger.Error(message, args);
    }

    public static void LogError(this ILogger logger, Exception exception, string message, params object?[] args)
    {
        logger.ErrorException(message, exception, args);
    }
}
