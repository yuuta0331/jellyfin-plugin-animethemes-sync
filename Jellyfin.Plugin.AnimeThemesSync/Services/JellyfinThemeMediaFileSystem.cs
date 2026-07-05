using System.Collections.Generic;
using AnimeThemesSync.Shared.Interfaces;
using MediaBrowser.Model.IO;

namespace Jellyfin.Plugin.AnimeThemesSync.Services;

/// <summary>
/// Adapts Jellyfin's <see cref="IFileSystem"/> to the shared theme media surface.
/// </summary>
public sealed class JellyfinThemeMediaFileSystem : IThemeMediaFileSystem
{
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinThemeMediaFileSystem"/> class.
    /// </summary>
    public JellyfinThemeMediaFileSystem(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public bool FileExists(string path) => _fileSystem.FileExists(path);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => _fileSystem.DirectoryExists(path);

    /// <inheritdoc />
    public IEnumerable<string> GetFilePaths(string path) => _fileSystem.GetFilePaths(path);

    /// <inheritdoc />
    public void DeleteFile(string path) => _fileSystem.DeleteFile(path);
}
