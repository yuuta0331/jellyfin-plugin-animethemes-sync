namespace AnimeThemesSync.Shared.Configuration;

/// <summary>
/// Defines the server-recognized suffix appended to browseable extras filenames.
/// </summary>
public enum ExtrasFileSuffix
{
    /// <summary>No suffix.</summary>
    None = 0,

    /// <summary>The generic <c>-other</c> suffix.</summary>
    Other = 1,

    /// <summary>The <c>-short</c> suffix.</summary>
    Short = 2,

    /// <summary>The <c>-scene</c> suffix.</summary>
    Scene = 3
}
