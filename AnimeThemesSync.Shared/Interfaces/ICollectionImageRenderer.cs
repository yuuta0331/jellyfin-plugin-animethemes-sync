using System;
using System.Collections.Generic;
using AnimeThemesSync.Shared.Services;

namespace AnimeThemesSync.Shared.Interfaces;

/// <summary>
/// One image offered to the collection artwork compositor.
/// </summary>
public readonly record struct CollectionImageRenderSource(string Path, CollectionImageSourceKind Kind);

/// <summary>
/// Encoded output of the compositor.
/// </summary>
#pragma warning disable CA1819 // The encoded payload is intentionally a byte array.
public readonly record struct CollectionImageRenderResult(byte[] Data, string MimeType);
#pragma warning restore CA1819

/// <summary>
/// Host-specific compositor for generated collection artwork.
/// </summary>
public interface ICollectionImageRenderer
{
    /// <summary>
    /// Composites the source images into one encoded image. Undecodable sources are dropped and the
    /// layout is recomputed via <paramref name="layoutForSources"/>, which receives the kinds and decoded
    /// dimensions of the surviving sources in their original order; returned cells index into that list.
    /// The canvas is cleared with <paramref name="canvasColor"/> at <paramref name="canvasOpacityPercent"/>;
    /// an opacity below 100 produces a PNG (alpha), otherwise a JPEG. Returns null when nothing could be rendered.
    /// </summary>
    CollectionImageRenderResult? Render(
        IReadOnlyList<CollectionImageRenderSource> sources,
        int canvasWidth,
        int canvasHeight,
        Func<IReadOnlyList<CollectionImageLayoutSource>, CollectionImageLayoutResult> layoutForSources,
        string? overlayColor,
        int overlayOpacityPercent,
        string canvasColor,
        int canvasOpacityPercent);
}
