using System.Collections.Generic;

namespace AnimeThemesSync.Shared.Interfaces;

/// <summary>
/// The minimal file system surface shared services need for plugin-managed
/// theme media. Each host adapts its own IFileSystem to this interface so the
/// browser row and local media logic can live in shared code.
/// </summary>
public interface IThemeMediaFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> GetFilePaths(string path);

    void DeleteFile(string path);
}
