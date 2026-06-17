namespace AnimeThemesSync.Shared.Interfaces;

/// <summary>
/// Abstracts plugin configuration read access for shared services.
/// </summary>
/// <typeparam name="TConfiguration">Configuration type.</typeparam>
public interface IConfigurationService<out TConfiguration>
    where TConfiguration : class
{
    /// <summary>
    /// Gets the current configuration snapshot.
    /// </summary>
    TConfiguration Current { get; }
}
