namespace AnimeThemesSync.Shared.Configuration;

/// <summary>
/// Controls where tags resolved from season mappings are applied.
/// </summary>
public enum SeasonTagTarget
{
    /// <summary>Aggregate every resolved season tag on the parent series.</summary>
    Series = 0,

    /// <summary>Apply each resolved season tag to that season only.</summary>
    Season = 1,

    /// <summary>Apply tags to both the parent series and each season.</summary>
    Both = 2,
}
