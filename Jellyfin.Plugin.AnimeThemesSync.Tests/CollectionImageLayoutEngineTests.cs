using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Services;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public sealed class CollectionImageLayoutEngineTests
{
    private const double PosterAspect = 2.0 / 3.0;
    private const double LandscapeAspect = 16.0 / 9.0;

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void PosterCanvas_ReturnsEmptyForNoImages(int count)
    {
        var result = CollectionImageLayoutEngine.ComputePosterCanvasLayout(1000, 1500, count, false, SeasonCollectionPosterFillMode.ArtworkFill);
        Assert.Empty(result.Cells);
        Assert.True(result.HasBackground);
    }

    [Fact]
    public void PosterCanvas_SingleImageFillsCanvas()
    {
        var result = CollectionImageLayoutEngine.ComputePosterCanvasLayout(1000, 1500, 1, true, SeasonCollectionPosterFillMode.ArtworkFill);
        var cell = Assert.Single(result.Cells);
        Assert.Equal(new CollectionImageLayoutCell(0, 0, 0, 1000, 1500), cell);
        Assert.False(result.HasBackground);
    }

    [Fact]
    public void PosterCanvas_TwoPostersWithArtworkFillUsesBottomFillCell()
    {
        var result = CollectionImageLayoutEngine.ComputePosterCanvasLayout(1000, 1500, 2, true, SeasonCollectionPosterFillMode.ArtworkFill);
        Assert.Equal(3, result.Cells.Count);
        Assert.Equal(new CollectionImageLayoutCell(0, 0, 0, 500, 750), result.Cells[0]);
        Assert.Equal(new CollectionImageLayoutCell(1, 500, 0, 500, 750), result.Cells[1]);
        Assert.Equal(new CollectionImageLayoutCell(2, 0, 750, 1000, 750), result.Cells[2]);
        Assert.False(result.HasBackground);
    }

    [Theory]
    [InlineData(true, SeasonCollectionPosterFillMode.EmptySpace)]
    [InlineData(false, SeasonCollectionPosterFillMode.ArtworkFill)]
    public void PosterCanvas_TwoPostersWithoutFillLeavesBackground(bool hasArtwork, SeasonCollectionPosterFillMode mode)
    {
        var result = CollectionImageLayoutEngine.ComputePosterCanvasLayout(1000, 1500, 2, hasArtwork, mode);
        Assert.Equal(2, result.Cells.Count);
        Assert.True(result.HasBackground);
        Assert.All(result.Cells, cell => Assert.Equal(0, cell.Y));
    }

    [Fact]
    public void PosterCanvas_ThreePostersWithArtworkFillUsesFourthCell()
    {
        var result = CollectionImageLayoutEngine.ComputePosterCanvasLayout(1000, 1500, 3, true, SeasonCollectionPosterFillMode.ArtworkFill);
        Assert.Equal(4, result.Cells.Count);
        Assert.Equal(new CollectionImageLayoutCell(3, 500, 750, 500, 750), result.Cells[3]);
        Assert.False(result.HasBackground);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void PosterCanvas_FourOrMorePostersUseTwoByTwoGrid(int count)
    {
        var result = CollectionImageLayoutEngine.ComputePosterCanvasLayout(1000, 1500, count, true, SeasonCollectionPosterFillMode.ArtworkFill);
        Assert.Equal(4, result.Cells.Count);
        Assert.False(result.HasBackground);
        Assert.All(result.Cells, cell => Assert.Equal(500, cell.Width));
        Assert.All(result.Cells, cell => Assert.Equal(750, cell.Height));
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void LandscapeCanvas_SinglePosterIsPillarboxed(int width, int height)
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(width, height, 1, CollectionImageSourceKind.Poster);
        var cell = Assert.Single(result.Cells);
        Assert.True(result.HasBackground);
        Assert.Equal(height, cell.Height);
        Assert.Equal((int)System.Math.Round(height * PosterAspect), cell.Width);
        Assert.Equal((width - cell.Width) / 2, cell.X);
    }

    [Fact]
    public void LandscapeCanvas_SingleLandscapeFillsCanvas()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, 1, CollectionImageSourceKind.Landscape);
        var cell = Assert.Single(result.Cells);
        Assert.Equal(new CollectionImageLayoutCell(0, 0, 0, 1920, 1080), cell);
        Assert.False(result.HasBackground);
    }

    [Fact]
    public void LandscapeCanvas_TwoLandscapesReduceToOne()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, 2, CollectionImageSourceKind.Landscape);
        var cell = Assert.Single(result.Cells);
        Assert.Equal(new CollectionImageLayoutCell(0, 0, 0, 1920, 1080), cell);
    }

    [Fact]
    public void LandscapeCanvas_FourPostersReduceToThree()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 4, CollectionImageSourceKind.Poster);
        Assert.Equal(3, result.Cells.Count);
        Assert.All(result.Cells, cell => Assert.Equal(720, cell.Height));
    }

    [Fact]
    public void LandscapeCanvas_FivePostersUseFourByTwoWithCenteredLastRow()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 5, CollectionImageSourceKind.Poster);
        Assert.Equal(5, result.Cells.Count);
        Assert.True(result.HasBackground);
        var lastRow = result.Cells.Where(c => c.Y > 0).ToList();
        var single = Assert.Single(lastRow);
        Assert.Equal(320, single.Width);
        Assert.Equal(480, single.X);
    }

    [Fact]
    public void LandscapeCanvas_TenPostersUseFiveByTwo()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 10, CollectionImageSourceKind.Poster);
        Assert.Equal(10, result.Cells.Count);
        Assert.False(result.HasBackground);
        Assert.Equal(5, result.Cells.Count(c => c.Y == 0));
    }

    [Fact]
    public void LandscapeCanvas_PosterTilesAreCappedAtTen()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, 14, CollectionImageSourceKind.Poster);
        Assert.Equal(10, result.Cells.Count);
    }

    [Fact]
    public void LandscapeCanvas_FiveLandscapesUseThreeByTwoJustified()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 5, CollectionImageSourceKind.Landscape);
        Assert.Equal(5, result.Cells.Count);
        Assert.False(result.HasBackground);
        var lastRow = result.Cells.Where(c => c.Y > 0).OrderBy(c => c.X).ToList();
        Assert.Equal(2, lastRow.Count);
        Assert.Equal(0, lastRow[0].X);
        Assert.Equal(1280, lastRow[1].X + lastRow[1].Width);
    }

    [Fact]
    public void LandscapeCanvas_ThreeLandscapesCenterTheLastCell()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 3, CollectionImageSourceKind.Landscape);
        Assert.Equal(3, result.Cells.Count);
        Assert.True(result.HasBackground);
        var last = result.Cells[2];
        Assert.Equal(640, last.Width);
        Assert.Equal(320, last.X);
    }

    [Fact]
    public void LandscapeCanvas_TenLandscapesPreferFourColumnsOverThree()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, 10, CollectionImageSourceKind.Landscape);
        Assert.Equal(10, result.Cells.Count);
        Assert.Equal(4, result.Cells.Count(c => c.Y == 0));
    }

    [Theory]
    [InlineData(1280, 720, CollectionImageSourceKind.Poster)]
    [InlineData(1920, 1080, CollectionImageSourceKind.Poster)]
    [InlineData(1280, 720, CollectionImageSourceKind.Landscape)]
    [InlineData(1920, 1080, CollectionImageSourceKind.Landscape)]
    public void LandscapeCanvas_AllCellsStayWithinAspectTolerance(int width, int height, CollectionImageSourceKind kind)
    {
        var target = kind == CollectionImageSourceKind.Poster ? PosterAspect : LandscapeAspect;
        var tolerance = kind == CollectionImageSourceKind.Poster
            ? CollectionImageLayoutEngine.PosterCellAspectTolerance
            : CollectionImageLayoutEngine.LandscapeCellAspectTolerance;
        for (var count = 2; count <= 24; count++)
        {
            var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(width, height, count, kind);
            if (result.Cells.Count <= 1)
            {
                continue;
            }

            foreach (var cell in result.Cells)
            {
                var aspect = (double)cell.Width / cell.Height;
                var deviation = System.Math.Abs(System.Math.Log(aspect / target));
                Assert.True(
                    deviation <= tolerance + 0.02,
                    $"count={count} cell={cell} aspect={aspect:F3} deviation={deviation:F3} exceeds tolerance {tolerance}.");
            }
        }
    }

    [Theory]
    [InlineData(CollectionImageSourceKind.Poster)]
    [InlineData(CollectionImageSourceKind.Landscape)]
    public void LandscapeCanvas_CellsAreValidAndDeterministic(CollectionImageSourceKind kind)
    {
        for (var count = 1; count <= 24; count++)
        {
            var first = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, count, kind);
            var second = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, count, kind);
            Assert.Equal(first.Cells, second.Cells);
            Assert.Equal(first.HasBackground, second.HasBackground);
            AssertCellsValid(first.Cells, 1920, 1080);
            Assert.Equal(Enumerable.Range(0, first.Cells.Count), first.Cells.Select(c => c.SourceIndex).OrderBy(i => i));
            if (!first.HasBackground)
            {
                var area = first.Cells.Sum(c => (long)c.Width * c.Height);
                Assert.Equal(1920L * 1080L, area);
            }
        }
    }

    [Fact]
    public void Fingerprint_ChangesWithSourceOrderIdentityKindAndSettings()
    {
        var a = new CollectionImageSource("a.jpg", 10, 100, CollectionImageSourceKind.Poster);
        var b = new CollectionImageSource("b.jpg", 20, 200, CollectionImageSourceKind.Poster);

        var baseline = CollectionImageFingerprint.Compute("Primary", 1000, 1500, "on:35:#000000", "0:0:#000000:100", new[] { a, b });
        Assert.Equal(baseline, CollectionImageFingerprint.Compute("Primary", 1000, 1500, "on:35:#000000", "0:0:#000000:100", new[] { a, b }));
        Assert.NotEqual(baseline, CollectionImageFingerprint.Compute("Primary", 1000, 1500, "on:35:#000000", "0:0:#000000:100", new[] { b, a }));
        Assert.NotEqual(baseline, CollectionImageFingerprint.Compute("Primary", 1000, 1500, "on:35:#000000", "0:0:#000000:100", new[] { a with { LastWriteUtcTicks = 101 }, b }));
        Assert.NotEqual(baseline, CollectionImageFingerprint.Compute("Primary", 1000, 1500, "on:35:#000000", "0:0:#000000:100", new[] { a with { Kind = CollectionImageSourceKind.Landscape }, b }));
        Assert.NotEqual(baseline, CollectionImageFingerprint.Compute("Primary", 1000, 1500, "on:35:#000000", "1:0:#000000:100", new[] { a, b }));
        Assert.NotEqual(baseline, CollectionImageFingerprint.Compute("Primary", 1000, 1500, "on:35:#000000", "0:0:#FFFFFF:80", new[] { a, b }));
        Assert.NotEqual(baseline, CollectionImageFingerprint.Compute("Primary", 1000, 1500, "off", "0:0:#000000:100", new[] { a, b }));
        Assert.NotEqual(baseline, CollectionImageFingerprint.Compute("Thumb", 1000, 1500, "on:35:#000000", "0:0:#000000:100", new[] { a, b }));
    }

    private static void AssertCellsValid(IReadOnlyList<CollectionImageLayoutCell> cells, int width, int height)
    {
        foreach (var cell in cells)
        {
            Assert.True(cell.Width > 0 && cell.Height > 0, $"Cell {cell} has non-positive size.");
            Assert.InRange(cell.X, 0, width);
            Assert.InRange(cell.Y, 0, height);
            Assert.True(cell.X + cell.Width <= width, $"Cell {cell} exceeds canvas width.");
            Assert.True(cell.Y + cell.Height <= height, $"Cell {cell} exceeds canvas height.");
        }

        for (var i = 0; i < cells.Count; i++)
        {
            for (var j = i + 1; j < cells.Count; j++)
            {
                var a = cells[i];
                var b = cells[j];
                var overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                Assert.False(overlap, $"Cells {a} and {b} overlap.");
            }
        }
    }
}
