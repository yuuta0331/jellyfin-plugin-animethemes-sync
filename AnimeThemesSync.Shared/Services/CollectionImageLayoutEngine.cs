using System;
using System.Collections.Generic;
using AnimeThemesSync.Shared.Configuration;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Orientation of a source image used in generated collection artwork.
/// </summary>
public enum CollectionImageSourceKind
{
    /// <summary>A portrait poster (~2:3).</summary>
    Poster = 0,

    /// <summary>A landscape backdrop or thumb (~16:9).</summary>
    Landscape = 1,
}

/// <summary>
/// One placement of a source image on the composite canvas.
/// </summary>
public readonly record struct CollectionImageLayoutCell(int SourceIndex, int X, int Y, int Width, int Height);

/// <summary>
/// Decoded source metadata used to compute a collection image layout.
/// </summary>
public readonly record struct CollectionImageLayoutSource(CollectionImageSourceKind Kind, int PixelWidth, int PixelHeight);

/// <summary>
/// The available artwork for one collection member.
/// </summary>
public readonly record struct CollectionMemberArtwork(
    CollectionImageSource? Poster,
    CollectionImageSource? Thumb,
    CollectionImageSource? Backdrop);

/// <summary>
/// Computed layout. Cells may reference fewer sources than were offered (count reduction);
/// <see cref="HasBackground"/> is true when the canvas color shows anywhere.
/// </summary>
public sealed class CollectionImageLayoutResult
{
    public CollectionImageLayoutResult(IReadOnlyList<CollectionImageLayoutCell> cells, bool hasBackground)
    {
        Cells = cells;
        HasBackground = hasBackground;
    }

    public IReadOnlyList<CollectionImageLayoutCell> Cells { get; }

    public bool HasBackground { get; }
}

/// <summary>
/// Pure layout math for generated collection artwork. Cells stay close to each source's natural
/// aspect ratio; when a count cannot be placed without ugly crops it is reduced instead.
/// Renderers center-crop each source into its cell.
/// </summary>
public static class CollectionImageLayoutEngine
{
    /// <summary>
    /// Bump to force regeneration of previously generated images when the layout rules change.
    /// </summary>
    public const string LayoutVersion = "v4";

    public const int PosterMaxImages = 4;
    public const int ThumbMaxImages = 24;
    public const int BackdropMaxImages = 40;
    public const int PosterTilesMaxOnLandscapeCanvas = 10;

    /// <summary>Maximum |ln(cellAspect / (2:3))| accepted for poster cells.</summary>
    public const double PosterCellAspectTolerance = 0.30;

    /// <summary>Maximum |ln(cellAspect / (16:9))| accepted for landscape cells.</summary>
    public const double LandscapeCellAspectTolerance = 0.45;

    private const double PosterAspect = 2.0 / 3.0;
    private const double LandscapeAspect = 16.0 / 9.0;
    private const double ScoreEpsilon = 1e-9;

    /// <summary>
    /// Computes the poster (Primary, 2:3) canvas layout for up to four member posters.
    /// With two or three posters the leftover region is either a fill-artwork cell
    /// (SourceIndex == posterCount, expects a landscape source) or canvas background.
    /// </summary>
    public static CollectionImageLayoutResult ComputePosterCanvasLayout(
        int canvasWidth,
        int canvasHeight,
        int posterCount,
        bool hasFillArtwork,
        SeasonCollectionPosterFillMode fillMode)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0 || posterCount <= 0)
        {
            return new CollectionImageLayoutResult(Array.Empty<CollectionImageLayoutCell>(), true);
        }

        var w = canvasWidth;
        var h = canvasHeight;
        var leftWidth = w / 2;
        var rightWidth = w - leftWidth;
        var topHeight = h / 2;
        var bottomHeight = h - topHeight;
        var useFill = fillMode == SeasonCollectionPosterFillMode.ArtworkFill && hasFillArtwork;
        switch (Math.Min(posterCount, PosterMaxImages))
        {
            case 1:
                return new CollectionImageLayoutResult(new[] { new CollectionImageLayoutCell(0, 0, 0, w, h) }, false);
            case 2:
            {
                var cells = new List<CollectionImageLayoutCell>
                {
                    new(0, 0, 0, leftWidth, topHeight),
                    new(1, leftWidth, 0, rightWidth, topHeight),
                };
                if (useFill)
                {
                    cells.Add(new CollectionImageLayoutCell(2, 0, topHeight, w, bottomHeight));
                }

                return new CollectionImageLayoutResult(cells, !useFill);
            }

            case 3:
            {
                var cells = new List<CollectionImageLayoutCell>
                {
                    new(0, 0, 0, leftWidth, topHeight),
                    new(1, leftWidth, 0, rightWidth, topHeight),
                    new(2, 0, topHeight, leftWidth, bottomHeight),
                };
                if (useFill)
                {
                    cells.Add(new CollectionImageLayoutCell(3, leftWidth, topHeight, rightWidth, bottomHeight));
                }

                return new CollectionImageLayoutResult(cells, !useFill);
            }

            default:
                return new CollectionImageLayoutResult(
                    new[]
                    {
                        new CollectionImageLayoutCell(0, 0, 0, leftWidth, topHeight),
                        new CollectionImageLayoutCell(1, leftWidth, 0, rightWidth, topHeight),
                        new CollectionImageLayoutCell(2, 0, topHeight, leftWidth, bottomHeight),
                        new CollectionImageLayoutCell(3, leftWidth, topHeight, rightWidth, bottomHeight),
                    },
                    false);
        }
    }

    /// <summary>
    /// Computes the layout for a landscape (16:9) canvas from a homogeneous source set.
    /// This compatibility overload uses the canonical aspect ratio for the supplied source kind.
    /// </summary>
    public static CollectionImageLayoutResult ComputeLandscapeCanvasLayout(
        int canvasWidth,
        int canvasHeight,
        int imageCount,
        CollectionImageSourceKind kind)
    {
        var count = kind == CollectionImageSourceKind.Poster
            ? Math.Min(imageCount, PosterTilesMaxOnLandscapeCanvas)
            : imageCount;
        var width = kind == CollectionImageSourceKind.Poster ? 2 : 16;
        var height = kind == CollectionImageSourceKind.Poster ? 3 : 9;
        var sources = new List<CollectionImageLayoutSource>(Math.Max(0, count));
        for (var i = 0; i < count; i++)
        {
            sources.Add(new CollectionImageLayoutSource(kind, width, height));
        }

        return ComputeLandscapeCanvasLayout(canvasWidth, canvasHeight, sources);
    }

    /// <summary>
    /// Computes a justified-row layout for a landscape canvas from decoded source dimensions.
    /// Every source is used exactly once and remains in input order. Row partitions are balanced,
    /// then the layout with the greatest covered area inside the per-kind crop tolerances wins.
    /// </summary>
    public static CollectionImageLayoutResult ComputeLandscapeCanvasLayout(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<CollectionImageLayoutSource> sources)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0 || sources.Count == 0)
        {
            return new CollectionImageLayoutResult(Array.Empty<CollectionImageLayoutCell>(), true);
        }

        if (sources.Count == 1)
        {
            return BuildSingleSourceLayout(canvasWidth, canvasHeight, sources[0]);
        }

        var aspects = new double[sources.Count];
        var prefixAspects = new double[sources.Count + 1];
        var prefixPosters = new int[sources.Count + 1];
        for (var i = 0; i < sources.Count; i++)
        {
            aspects[i] = GetSourceAspect(sources[i]);
            prefixAspects[i + 1] = prefixAspects[i] + aspects[i];
            prefixPosters[i + 1] = prefixPosters[i] + (sources[i].Kind == CollectionImageSourceKind.Poster ? 1 : 0);
        }

        LayoutCandidate? best = null;
        for (var rowCount = 1; rowCount <= sources.Count; rowCount++)
        {
            var rows = FindBalancedRows(prefixAspects, prefixPosters, rowCount, sources.Count);
            var candidate = BuildJustifiedRows(canvasWidth, canvasHeight, sources, aspects, prefixAspects, prefixPosters, rows);
            if (candidate != null && IsBetterLayout(candidate, best))
            {
                best = candidate;
            }
        }

        return best == null
            ? new CollectionImageLayoutResult(Array.Empty<CollectionImageLayoutCell>(), true)
            : new CollectionImageLayoutResult(best.Cells, best.HasBackground);
    }

    /// <summary>
    /// Resolves a member's landscape source honoring the preferred type, falling back to the other.
    /// </summary>
    public static CollectionImageSource? ResolveLandscape(CollectionMemberArtwork member, SeasonCollectionLandscapeArtType type)
    {
        return type == SeasonCollectionLandscapeArtType.Backdrop
            ? member.Backdrop ?? member.Thumb
            : member.Thumb ?? member.Backdrop;
    }

    /// <summary>
    /// Builds the ordered source list for the poster (Primary) canvas. In ArtworkFill mode with two or
    /// three members the leftover space becomes a single landscape strip drawn from a member that is not
    /// already shown as a poster (when possible), instead of cramming a landscape into a portrait cell.
    /// </summary>
    public static List<CollectionImageSource> SelectPosterCanvasSources(
        IReadOnlyList<CollectionMemberArtwork> members,
        SeasonCollectionPosterFillMode fillMode,
        SeasonCollectionLandscapeArtType posterFillType)
    {
        var posters = new List<CollectionImageSource>(members.Count);
        foreach (var member in members)
        {
            if (member.Poster != null)
            {
                posters.Add(member.Poster.Value);
            }
        }

        var n = members.Count;
        if (fillMode != SeasonCollectionPosterFillMode.ArtworkFill || n >= 4 || n <= 1)
        {
            return Take(posters, PosterMaxImages);
        }

        // Two or three members: show two posters plus one landscape strip.
        if (posters.Count < 2)
        {
            return Take(posters, PosterMaxImages);
        }

        var fill = FindPosterFillLandscape(members, posterFillType);
        return fill != null
            ? new List<CollectionImageSource> { posters[0], posters[1], fill.Value }
            : Take(posters, PosterMaxImages);
    }

    private static CollectionImageSource? FindPosterFillLandscape(
        IReadOnlyList<CollectionMemberArtwork> members,
        SeasonCollectionLandscapeArtType posterFillType)
    {
        // Prefer a member not already shown as one of the first two posters, so no work is duplicated.
        for (var i = 2; i < members.Count; i++)
        {
            var landscape = ResolveLandscape(members[i], posterFillType);
            if (landscape != null)
            {
                return landscape;
            }
        }

        // Only two members exist: reuse one of their landscapes.
        for (var i = 0; i < members.Count && i < 2; i++)
        {
            var landscape = ResolveLandscape(members[i], posterFillType);
            if (landscape != null)
            {
                return landscape;
            }
        }

        return null;
    }

    /// <summary>
    /// Selects one source per member for the landscape canvases according to the configured mode and
    /// landscape type. Preferred sources are returned first in member order; members without a preferred
    /// source contribute their fallback afterward, also in member order.
    /// </summary>
    public static List<CollectionImageSource> SelectLandscapeCanvasSources(
        IReadOnlyList<CollectionMemberArtwork> members,
        SeasonCollectionLandscapeSourceMode mode,
        SeasonCollectionLandscapeArtType landscapeType)
    {
        var preferred = new List<CollectionImageSource>(members.Count);
        var fallback = new List<CollectionImageSource>(members.Count);
        foreach (var member in members)
        {
            var landscape = ResolveLandscape(member, landscapeType);
            if (mode == SeasonCollectionLandscapeSourceMode.LandscapeOnly)
            {
                AddIfPresent(preferred, landscape);
            }
            else if (mode == SeasonCollectionLandscapeSourceMode.PosterOnly)
            {
                AddIfPresent(preferred, member.Poster);
            }
            else if (mode == SeasonCollectionLandscapeSourceMode.PosterFirst)
            {
                if (member.Poster != null)
                {
                    preferred.Add(member.Poster.Value);
                }
                else
                {
                    AddIfPresent(fallback, landscape);
                }
            }
            else if (landscape != null)
            {
                preferred.Add(landscape.Value);
            }
            else
            {
                AddIfPresent(fallback, member.Poster);
            }
        }

        preferred.AddRange(fallback);
        return preferred;
    }

    private static List<CollectionImageSource> Take(List<CollectionImageSource> source, int count)
    {
        if (source.Count <= count)
        {
            return source;
        }

        return source.GetRange(0, count);
    }

    private static void AddIfPresent(List<CollectionImageSource> target, CollectionImageSource? source)
    {
        if (source != null)
        {
            target.Add(source.Value);
        }
    }

    private static CollectionImageLayoutResult BuildSingleSourceLayout(
        int canvasWidth,
        int canvasHeight,
        CollectionImageLayoutSource source)
    {
        var aspect = GetSourceAspect(source);
        var canvasAspect = (double)canvasWidth / canvasHeight;
        int width;
        int height;
        if (aspect >= canvasAspect)
        {
            width = canvasWidth;
            height = Math.Max(1, Math.Min(canvasHeight, (int)Math.Round(width / aspect)));
        }
        else
        {
            height = canvasHeight;
            width = Math.Max(1, Math.Min(canvasWidth, (int)Math.Round(height * aspect)));
        }

        var cell = new CollectionImageLayoutCell(0, (canvasWidth - width) / 2, (canvasHeight - height) / 2, width, height);
        return new CollectionImageLayoutResult(new[] { cell }, width != canvasWidth || height != canvasHeight);
    }

    private static double GetSourceAspect(CollectionImageLayoutSource source)
    {
        if (source.PixelWidth > 0 && source.PixelHeight > 0)
        {
            return (double)source.PixelWidth / source.PixelHeight;
        }

        return source.Kind == CollectionImageSourceKind.Poster ? PosterAspect : LandscapeAspect;
    }

    private static double GetTolerance(int posterCount) => posterCount > 0 ? PosterCellAspectTolerance : LandscapeCellAspectTolerance;

    private static List<(int Start, int End)> FindBalancedRows(
        double[] prefixAspects,
        int[] prefixPosters,
        int rowCount,
        int sourceCount)
    {
        var maxPressure = new double[rowCount + 1][];
        var balance = new double[rowCount + 1][];
        var previous = new int[rowCount + 1][];
        for (var row = 0; row <= rowCount; row++)
        {
            maxPressure[row] = new double[sourceCount + 1];
            balance[row] = new double[sourceCount + 1];
            previous[row] = new int[sourceCount + 1];
            for (var end = 0; end <= sourceCount; end++)
            {
                maxPressure[row][end] = double.PositiveInfinity;
                balance[row][end] = double.PositiveInfinity;
                previous[row][end] = -1;
            }
        }

        maxPressure[0][0] = 0;
        balance[0][0] = 0;
        for (var row = 1; row <= rowCount; row++)
        {
            for (var end = row; end <= sourceCount; end++)
            {
                for (var start = row - 1; start < end; start++)
                {
                    if (double.IsPositiveInfinity(maxPressure[row - 1][start]))
                    {
                        continue;
                    }

                    var aspect = prefixAspects[end] - prefixAspects[start];
                    var posterCount = prefixPosters[end] - prefixPosters[start];
                    var pressure = aspect * Math.Exp(-GetTolerance(posterCount));
                    var candidateMax = Math.Max(maxPressure[row - 1][start], pressure);
                    var candidateBalance = balance[row - 1][start] + (pressure * pressure);
                    if (candidateMax < maxPressure[row][end] - ScoreEpsilon
                        || (Math.Abs(candidateMax - maxPressure[row][end]) <= ScoreEpsilon
                            && candidateBalance < balance[row][end] - ScoreEpsilon))
                    {
                        maxPressure[row][end] = candidateMax;
                        balance[row][end] = candidateBalance;
                        previous[row][end] = start;
                    }
                }
            }
        }

        var result = new List<(int Start, int End)>(rowCount);
        var cursor = sourceCount;
        for (var row = rowCount; row > 0; row--)
        {
            var start = previous[row][cursor];
            result.Add((start, cursor));
            cursor = start;
        }

        result.Reverse();
        return result;
    }

    private static LayoutCandidate? BuildJustifiedRows(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<CollectionImageLayoutSource> sources,
        double[] aspects,
        double[] prefixAspects,
        int[] prefixPosters,
        List<(int Start, int End)> rows)
    {
        var maxPressure = 0.0;
        foreach (var row in rows)
        {
            var aspect = prefixAspects[row.End] - prefixAspects[row.Start];
            var posterCount = prefixPosters[row.End] - prefixPosters[row.Start];
            maxPressure = Math.Max(maxPressure, aspect * Math.Exp(-GetTolerance(posterCount)));
        }

        if (maxPressure <= 0)
        {
            return null;
        }

        var rowHeight = Math.Min(canvasHeight / rows.Count, (int)Math.Floor((canvasWidth + ScoreEpsilon) / maxPressure));
        if (rowHeight <= 0)
        {
            return null;
        }

        var cells = new List<CollectionImageLayoutCell>(sources.Count);
        var y = (canvasHeight - (rowHeight * rows.Count)) / 2;
        long coveredArea = 0;
        var maxNormalizedDeviation = 0.0;
        var totalNormalizedDeviation = 0.0;
        foreach (var row in rows)
        {
            var aspect = prefixAspects[row.End] - prefixAspects[row.Start];
            var posterCount = prefixPosters[row.End] - prefixPosters[row.Start];
            var tolerance = GetTolerance(posterCount);
            var desiredScale = (double)canvasWidth / (rowHeight * aspect);
            var minimumScale = Math.Exp(-tolerance);
            var maximumScale = Math.Exp(tolerance);
            var scale = Math.Max(minimumScale, Math.Min(maximumScale, desiredScale));
            var rowWidth = Math.Min(canvasWidth, Math.Max(row.End - row.Start, (int)Math.Round(rowHeight * aspect * scale)));
            var x = (canvasWidth - rowWidth) / 2;
            var cursor = x;
            var cumulativeAspect = 0.0;
            for (var sourceIndex = row.Start; sourceIndex < row.End; sourceIndex++)
            {
                cumulativeAspect += aspects[sourceIndex];
                var next = sourceIndex == row.End - 1
                    ? x + rowWidth
                    : x + (int)Math.Round(rowWidth * cumulativeAspect / aspect);
                var width = Math.Max(1, next - cursor);
                cells.Add(new CollectionImageLayoutCell(sourceIndex, cursor, y, width, rowHeight));
                var actualAspect = (double)width / rowHeight;
                var deviation = Math.Abs(Math.Log(actualAspect / aspects[sourceIndex]));
                var sourceTolerance = sources[sourceIndex].Kind == CollectionImageSourceKind.Poster
                    ? PosterCellAspectTolerance
                    : LandscapeCellAspectTolerance;
                var normalizedDeviation = deviation / sourceTolerance;
                maxNormalizedDeviation = Math.Max(maxNormalizedDeviation, normalizedDeviation);
                totalNormalizedDeviation += normalizedDeviation;
                cursor = next;
            }

            coveredArea += (long)rowWidth * rowHeight;
            y += rowHeight;
        }

        return new LayoutCandidate(
            cells,
            coveredArea < (long)canvasWidth * canvasHeight,
            coveredArea,
            maxNormalizedDeviation,
            totalNormalizedDeviation,
            rows.Count);
    }

    private static bool IsBetterLayout(LayoutCandidate candidate, LayoutCandidate? current)
    {
        if (current == null)
        {
            return true;
        }

        if (candidate.CoveredArea != current.CoveredArea)
        {
            return candidate.CoveredArea > current.CoveredArea;
        }

        if (Math.Abs(candidate.MaxNormalizedDeviation - current.MaxNormalizedDeviation) > ScoreEpsilon)
        {
            return candidate.MaxNormalizedDeviation < current.MaxNormalizedDeviation;
        }

        if (Math.Abs(candidate.TotalNormalizedDeviation - current.TotalNormalizedDeviation) > ScoreEpsilon)
        {
            return candidate.TotalNormalizedDeviation < current.TotalNormalizedDeviation;
        }

        return candidate.RowCount < current.RowCount;
    }

    private sealed class LayoutCandidate
    {
        public LayoutCandidate(
            List<CollectionImageLayoutCell> cells,
            bool hasBackground,
            long coveredArea,
            double maxNormalizedDeviation,
            double totalNormalizedDeviation,
            int rowCount)
        {
            Cells = cells;
            HasBackground = hasBackground;
            CoveredArea = coveredArea;
            MaxNormalizedDeviation = maxNormalizedDeviation;
            TotalNormalizedDeviation = totalNormalizedDeviation;
            RowCount = rowCount;
        }

        public List<CollectionImageLayoutCell> Cells { get; }

        public bool HasBackground { get; }

        public long CoveredArea { get; }

        public double MaxNormalizedDeviation { get; }

        public double TotalNormalizedDeviation { get; }

        public int RowCount { get; }
    }
}
