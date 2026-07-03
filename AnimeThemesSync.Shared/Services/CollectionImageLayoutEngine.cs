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
    public const string LayoutVersion = "v2";

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
    /// Poster sets are capped at <see cref="PosterTilesMaxOnLandscapeCanvas"/>; when no grid keeps
    /// cells within the aspect tolerance the count is reduced; a single poster is pillarboxed.
    /// </summary>
    public static CollectionImageLayoutResult ComputeLandscapeCanvasLayout(
        int canvasWidth,
        int canvasHeight,
        int imageCount,
        CollectionImageSourceKind kind)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0 || imageCount <= 0)
        {
            return new CollectionImageLayoutResult(Array.Empty<CollectionImageLayoutCell>(), true);
        }

        var target = kind == CollectionImageSourceKind.Poster ? PosterAspect : LandscapeAspect;
        var tolerance = kind == CollectionImageSourceKind.Poster ? PosterCellAspectTolerance : LandscapeCellAspectTolerance;
        var count = kind == CollectionImageSourceKind.Poster
            ? Math.Min(imageCount, PosterTilesMaxOnLandscapeCanvas)
            : imageCount;

        for (var n = count; n >= 2; n--)
        {
            var grid = SelectGrid(canvasWidth, canvasHeight, n, target, tolerance);
            if (grid != null)
            {
                return BuildGridCells(canvasWidth, canvasHeight, n, grid.Value.Columns, grid.Value.Rows, target, tolerance);
            }
        }

        if (kind == CollectionImageSourceKind.Landscape)
        {
            return new CollectionImageLayoutResult(new[] { new CollectionImageLayoutCell(0, 0, 0, canvasWidth, canvasHeight) }, false);
        }

        var pillarWidth = Math.Min(canvasWidth, (int)Math.Round(canvasHeight * PosterAspect));
        var pillarX = (canvasWidth - pillarWidth) / 2;
        return new CollectionImageLayoutResult(
            new[] { new CollectionImageLayoutCell(0, pillarX, 0, pillarWidth, canvasHeight) },
            pillarWidth < canvasWidth);
    }

    /// <summary>
    /// Selects one source per member for the landscape canvases according to the configured mode,
    /// then coerces the result to a single orientation (majority wins; minority members switch to
    /// their alternate artwork when available, otherwise they are dropped).
    /// </summary>
    public static List<CollectionImageSource> SelectLandscapeCanvasSources(
        IReadOnlyList<(CollectionImageSource? Poster, CollectionImageSource? Landscape)> members,
        SeasonCollectionLandscapeSourceMode mode)
    {
        var picks = new List<CollectionImageSource>(members.Count);
        foreach (var member in members)
        {
            var pick = mode switch
            {
                SeasonCollectionLandscapeSourceMode.LandscapeOnly => member.Landscape,
                SeasonCollectionLandscapeSourceMode.PosterOnly => member.Poster,
                SeasonCollectionLandscapeSourceMode.PosterFirst => member.Poster ?? member.Landscape,
                _ => member.Landscape ?? member.Poster,
            };
            if (pick != null)
            {
                picks.Add(pick.Value);
            }
        }

        var landscapeCount = 0;
        foreach (var pick in picks)
        {
            if (pick.Kind == CollectionImageSourceKind.Landscape)
            {
                landscapeCount++;
            }
        }

        var posterCount = picks.Count - landscapeCount;
        if (landscapeCount == 0 || posterCount == 0)
        {
            return picks;
        }

        var majority = landscapeCount >= posterCount ? CollectionImageSourceKind.Landscape : CollectionImageSourceKind.Poster;
        var coerced = new List<CollectionImageSource>(members.Count);
        foreach (var member in members)
        {
            var source = majority == CollectionImageSourceKind.Landscape ? member.Landscape : member.Poster;
            if (source != null)
            {
                coerced.Add(source.Value);
            }
        }

        return coerced;
    }

    private static (int Columns, int Rows)? SelectGrid(int canvasWidth, int canvasHeight, int imageCount, double targetAspect, double tolerance)
    {
        (int Columns, int Rows)? best = null;
        var bestScore = double.MaxValue;
        var bestEmpty = int.MaxValue;
        var bestSquareness = int.MaxValue;
        for (var columns = 1; columns <= imageCount; columns++)
        {
            var rows = (imageCount + columns - 1) / columns;
            var cellAspect = ((double)canvasWidth / columns) / ((double)canvasHeight / rows);
            var score = Math.Abs(Math.Log(cellAspect / targetAspect));
            if (score > tolerance + ScoreEpsilon)
            {
                continue;
            }

            var empty = (columns * rows) - imageCount;
            var squareness = Math.Abs(rows - columns);
            var better = score < bestScore - ScoreEpsilon
                || (Math.Abs(score - bestScore) <= ScoreEpsilon
                    && (empty < bestEmpty
                        || (empty == bestEmpty
                            && (squareness < bestSquareness
                                || (squareness == bestSquareness && best != null && columns > best.Value.Columns)))));
            if (better)
            {
                best = (columns, rows);
                bestScore = score;
                bestEmpty = empty;
                bestSquareness = squareness;
            }
        }

        return best;
    }

    private static CollectionImageLayoutResult BuildGridCells(int canvasWidth, int canvasHeight, int imageCount, int columns, int rows, double targetAspect, double tolerance)
    {
        var cells = new List<CollectionImageLayoutCell>(imageCount);
        var hasBackground = false;
        var sourceIndex = 0;
        for (var row = 0; row < rows; row++)
        {
            var y0 = (int)((long)row * canvasHeight / rows);
            var y1 = (int)((long)(row + 1) * canvasHeight / rows);
            var rowHeight = y1 - y0;
            var itemsInRow = row < rows - 1 ? columns : imageCount - (columns * (rows - 1));
            var justifiedAspect = ((double)canvasWidth / itemsInRow) / rowHeight;
            if (itemsInRow == columns || Math.Abs(Math.Log(justifiedAspect / targetAspect)) <= tolerance + ScoreEpsilon)
            {
                for (var column = 0; column < itemsInRow; column++)
                {
                    var x0 = (int)((long)column * canvasWidth / itemsInRow);
                    var x1 = (int)((long)(column + 1) * canvasWidth / itemsInRow);
                    cells.Add(new CollectionImageLayoutCell(sourceIndex++, x0, y0, x1 - x0, rowHeight));
                }
            }
            else
            {
                // Partial last row centered at the full-grid cell size; the sides show the canvas color.
                var cellWidth = canvasWidth / columns;
                var offset = (canvasWidth - (cellWidth * itemsInRow)) / 2;
                for (var column = 0; column < itemsInRow; column++)
                {
                    cells.Add(new CollectionImageLayoutCell(sourceIndex++, offset + (column * cellWidth), y0, cellWidth, rowHeight));
                }

                hasBackground = true;
            }
        }

        return new CollectionImageLayoutResult(cells, hasBackground);
    }
}
