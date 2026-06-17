using System;

namespace AnimeThemesSync.Shared.Interfaces;

/// <summary>
/// Lightweight logger abstraction for host-specific wrappers.
/// </summary>
public interface IAnimeThemesLogger
{
    void LogInformation(string messageTemplate, params object[] args);

    void LogWarning(string messageTemplate, params object[] args);

    void LogError(Exception exception, string messageTemplate, params object[] args);

    void LogDebug(string messageTemplate, params object[] args);
}
