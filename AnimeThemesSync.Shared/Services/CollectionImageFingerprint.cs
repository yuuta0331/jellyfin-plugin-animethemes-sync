using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// One source image considered for a generated collection image.
/// </summary>
public readonly record struct CollectionImageSource(string Path, long FileSize, long LastWriteUtcTicks, CollectionImageSourceKind Kind);

/// <summary>
/// Computes the change-detection fingerprint for generated collection artwork.
/// The fingerprint changes when membership, member artwork files, canvas, overlay/canvas settings,
/// or the layout algorithm version change.
/// </summary>
public static class CollectionImageFingerprint
{
    public static string Compute(
        string imageType,
        int canvasWidth,
        int canvasHeight,
        string overlaySettings,
        string canvasSettings,
        IReadOnlyList<CollectionImageSource> sources)
    {
        var builder = new StringBuilder();
        builder.Append(CollectionImageLayoutEngine.LayoutVersion).Append('|')
            .Append(imageType).Append('|')
            .Append(canvasWidth.ToString(CultureInfo.InvariantCulture)).Append('x')
            .Append(canvasHeight.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(overlaySettings).Append('|')
            .Append(canvasSettings);
        foreach (var source in sources)
        {
            builder.Append('|')
                .Append(source.Path).Append(':')
                .Append(source.FileSize.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(source.LastWriteUtcTicks.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(source.Kind == CollectionImageSourceKind.Landscape ? 'L' : 'P');
        }

        // SHA256.HashData (CA1850) is unavailable on netstandard2.1, which this shared file must support.
#pragma warning disable CA1850
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
#pragma warning restore CA1850
        var result = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            result.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return result.ToString();
    }

    /// <summary>
    /// Builds a cheap change-detection identity (size + last write ticks) for a
    /// written image file, or null when the file is missing or unreadable.
    /// </summary>
    public static string? GetImageFileIdentity(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var file = new System.IO.FileInfo(path);
            return file.Exists
                ? FormattableString.Invariant($"{file.Length}:{file.LastWriteTimeUtc.Ticks}")
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
