using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Tracks browseable extras filenames so display-name format changes can rename existing files.
/// </summary>
public static class ThemeExtrasManifestService
{
    public const string ManifestFileName = ".animethemes-sync-extras.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static ThemeExtraFileResult MigrateExtraFile(ThemeExtraPlan plan, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(plan.Key) || File.Exists(plan.TargetPath))
        {
            UpdateExtraFile(plan);
            return new ThemeExtraFileResult("current");
        }

        var previousPath = FindPreviousPath(plan);
        if (string.IsNullOrWhiteSpace(previousPath) ||
            !File.Exists(previousPath) ||
            string.Equals(previousPath, plan.TargetPath, StringComparison.OrdinalIgnoreCase))
        {
            return new ThemeExtraFileResult("not-found");
        }

        var targetDirectory = Path.GetDirectoryName(plan.TargetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        if (File.Exists(plan.TargetPath))
        {
            if (!overwrite)
            {
                return new ThemeExtraFileResult("target-exists");
            }

            File.Delete(plan.TargetPath);
        }

        File.Move(previousPath, plan.TargetPath);
        UpdateExtraFile(plan);
        return new ThemeExtraFileResult("renamed");
    }

    public static void UpdateExtraFile(ThemeExtraPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Key) || !File.Exists(plan.TargetPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(plan.TargetPath);
        var fileName = Path.GetFileName(plan.TargetPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var manifest = LoadManifest(directory);
        manifest.Files[plan.Key] = fileName;
        SaveManifest(directory, manifest);
    }

    private static string? FindPreviousPath(ThemeExtraPlan plan)
    {
        var directory = Path.GetDirectoryName(plan.TargetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            var manifest = LoadManifest(directory);
            if (manifest.Files.TryGetValue(plan.Key, out var fileName) && !string.IsNullOrWhiteSpace(fileName))
            {
                var manifestPath = Path.Combine(directory, fileName);
                if (File.Exists(manifestPath))
                {
                    return manifestPath;
                }
            }
        }

        foreach (var legacyPath in plan.LegacyTargetPaths)
        {
            if (!string.IsNullOrWhiteSpace(legacyPath) && File.Exists(legacyPath))
            {
                return legacyPath;
            }
        }

        return null;
    }

    private static ExtrasManifest LoadManifest(string directory)
    {
        var path = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(path))
        {
            return new ExtrasManifest();
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<ExtrasManifest>(File.ReadAllText(path), JsonOptions) ?? new ExtrasManifest();
            manifest.Files ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return manifest;
        }
        catch (JsonException)
        {
            return new ExtrasManifest();
        }
        catch (IOException)
        {
            return new ExtrasManifest();
        }
    }

    private static void SaveManifest(string directory, ExtrasManifest manifest)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ManifestFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private sealed class ExtrasManifest
    {
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
