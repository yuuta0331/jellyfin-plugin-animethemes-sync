using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimeThemesSync.Shared.Interfaces;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Pure helpers for the plugin-managed theme media folders (backdrops,
/// theme-music, extras). Extracted from the host ThemeDownloader classes so
/// both hosts share one implementation.
/// </summary>
public static class LocalMediaPathHelper
{
    /// <summary>
    /// Maps a managed directory to its cleanup file kind.
    /// </summary>
    public static string CleanupFileKind(string directory)
    {
        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(name, "theme-music", StringComparison.OrdinalIgnoreCase) ? "Audio" :
            string.Equals(name, "extras", StringComparison.OrdinalIgnoreCase) ? "Extra" : "Video";
    }

    /// <summary>
    /// Registers the managed theme directories under one output root.
    /// </summary>
    public static void AddCleanupDirectories(string? root, Dictionary<string, HashSet<string>> directories)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        Add(Path.Combine(root, "theme-music"));
        Add(Path.Combine(root, "backdrops"));
        Add(Path.Combine(root, "extras"));

        void Add(string directory)
        {
            if (!directories.ContainsKey(directory))
            {
                directories[directory] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Gets whether a path lies inside one of the cleanup roots. Deletion safety
    /// depends on this check.
    /// </summary>
    public static bool IsWithinCleanupRoots(string path, IEnumerable<string> roots)
    {
        return roots.Any(root => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets whether the file extension is a supported theme media type.
    /// </summary>
    public static bool IsSupportedThemeFile(string path)
    {
        return ThemeFilePlanner.IsSupportedMediaExtension(Path.GetExtension(path));
    }

    /// <summary>
    /// Throws when a requested media path escapes the library item or is not a
    /// plugin-managed theme file. Streaming safety depends on this check.
    /// </summary>
    public static void ValidateLocalMediaPath(string itemPath, string mediaPath)
    {
        var root = Path.GetFullPath(itemPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(mediaPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested local media path is outside the library item.");
        }

        if (!ThemeFilePlanner.IsSupportedMediaExtension(Path.GetExtension(fullPath)))
        {
            throw new InvalidOperationException("The requested local media type is not supported.");
        }

        var relative = fullPath[root.Length..];
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault();
        if (!string.Equals(firstSegment, "backdrops", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(firstSegment, "theme-music", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(firstSegment, "extras", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested local media path is not managed by AnimeThemes Sync.");
        }
    }

    /// <summary>
    /// Counts supported theme files and their total size in one managed directory.
    /// </summary>
    public static void AccumulateLocalThemeDirectory(string directory, ref int count, ref long bytes, IThemeMediaFileSystem fileSystem)
    {
        if (!fileSystem.DirectoryExists(directory))
        {
            return;
        }

        foreach (var file in fileSystem.GetFilePaths(directory))
        {
            if (!IsSupportedThemeFile(file))
            {
                continue;
            }

            count++;
            bytes += new FileInfo(file).Length;
        }
    }

    /// <summary>
    /// Deletes plugin-owned theme files in one managed directory and accumulates
    /// what was removed. Files not owned by the plugin are left alone.
    /// </summary>
    public static void DeleteLocalThemeDirectory(
        string directory,
        List<AnimeThemesTheme> themes,
        ref int filesDeleted,
        ref long bytesDeleted,
        IThemeMediaFileSystem fileSystem)
    {
        if (!fileSystem.DirectoryExists(directory))
        {
            return;
        }

        foreach (var file in fileSystem.GetFilePaths(directory))
        {
            if (!IsSupportedThemeFile(file) || !ThemeFilePlanner.IsPluginOwnedFile(file, themes))
            {
                continue;
            }

            var length = new FileInfo(file).Length;
            fileSystem.DeleteFile(file);
            filesDeleted++;
            bytesDeleted += length;
        }
    }

    /// <summary>
    /// Removes a temp download file, ignoring delete errors by design (the file
    /// is retried or orphaned harmlessly).
    /// </summary>
    public static void CleanupTempFile(string tempPath)
    {
        if (File.Exists(tempPath))
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Ignore delete errors
            }
        }
    }
}
