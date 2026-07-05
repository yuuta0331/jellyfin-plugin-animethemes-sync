using System.Collections.Generic;
using AnimeThemesSync.Shared.Interfaces;
using MediaBrowser.Model.IO;

namespace Emby.Plugin.AnimeThemesSync.Helpers;

/// <summary>
/// Adapts Emby's <see cref="IFileSystem"/> to the shared theme media surface.
/// </summary>
public sealed class EmbyThemeMediaFileSystem : IThemeMediaFileSystem
{
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbyThemeMediaFileSystem"/> class.
    /// </summary>
    public EmbyThemeMediaFileSystem(IFileSystem fileSystem)
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
