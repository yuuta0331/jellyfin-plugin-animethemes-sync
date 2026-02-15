namespace Jellyfin.Plugin.AnimeThemesSync.Services;

/// <summary>
/// Represents a scored candidate: a specific entry+video combination ready for selection.
/// </summary>
/// <param name="Theme">The parent theme (OP1, ED1, etc).</param>
/// <param name="Entry">The specific entry/version within the theme.</param>
/// <param name="Video">The best video selected for this entry.</param>
/// <param name="Score">The penalty score (lower = better).</param>
internal record ScoredCandidate(
    AnimeThemesTheme Theme,
    AnimeThemesEntry Entry,
    AnimeThemesVideo Video,
    double Score);
