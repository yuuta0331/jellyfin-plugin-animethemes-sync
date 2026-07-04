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
    public void LandscapeCanvas_TwoLandscapesUseBothSources()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, 2, CollectionImageSourceKind.Landscape);
        Assert.Equal(2, result.Cells.Count);
        Assert.Equal(new[] { 0, 1 }, result.Cells.Select(cell => cell.SourceIndex));
    }

    [Fact]
    public void LandscapeCanvas_FourPostersUseAllSources()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 4, CollectionImageSourceKind.Poster);
        Assert.Equal(4, result.Cells.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, result.Cells.Select(cell => cell.SourceIndex));
    }

    [Fact]
    public void LandscapeCanvas_FivePostersRemainWithinCropTolerance()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 5, CollectionImageSourceKind.Poster);
        Assert.Equal(5, result.Cells.Count);
        Assert.True(result.HasBackground);
        Assert.All(result.Cells, cell => AssertCellAspectWithinTolerance(cell, PosterAspect, CollectionImageLayoutEngine.PosterCellAspectTolerance));
    }

    [Fact]
    public void LandscapeCanvas_TenPostersUseFiveByTwo()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 10, CollectionImageSourceKind.Poster);
        Assert.Equal(10, result.Cells.Count);
        Assert.False(result.HasBackground);
    }

    [Fact]
    public void LandscapeCanvas_PosterTilesAreCappedAtTen()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, 14, CollectionImageSourceKind.Poster);
        Assert.Equal(10, result.Cells.Count);
    }

    [Fact]
    public void LandscapeCanvas_FiveLandscapesUseAllSources()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 5, CollectionImageSourceKind.Landscape);
        Assert.Equal(5, result.Cells.Count);
        Assert.Equal(Enumerable.Range(0, 5), result.Cells.Select(cell => cell.SourceIndex));
    }

    [Fact]
    public void LandscapeCanvas_ThreeLandscapesUseAllSources()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1280, 720, 3, CollectionImageSourceKind.Landscape);
        Assert.Equal(3, result.Cells.Count);
        Assert.Equal(Enumerable.Range(0, 3), result.Cells.Select(cell => cell.SourceIndex));
    }

    [Fact]
    public void LandscapeCanvas_TenLandscapesUseAllSources()
    {
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, 10, CollectionImageSourceKind.Landscape);
        Assert.Equal(10, result.Cells.Count);
    }

    [Fact]
    public void LandscapeCanvas_MixedActualDimensionsUseEverySourceInOrder()
    {
        var sources = new[]
        {
            new CollectionImageLayoutSource(CollectionImageSourceKind.Landscape, 1920, 1080),
            new CollectionImageLayoutSource(CollectionImageSourceKind.Landscape, 1600, 900),
            new CollectionImageLayoutSource(CollectionImageSourceKind.Poster, 1000, 1500),
            new CollectionImageLayoutSource(CollectionImageSourceKind.Poster, 800, 1200),
        };

        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, sources);

        Assert.Equal(sources.Length, result.Cells.Count);
        Assert.Equal(Enumerable.Range(0, sources.Length), result.Cells.Select(cell => cell.SourceIndex));
        AssertCellsValid(result.Cells, 1920, 1080);
        foreach (var cell in result.Cells)
        {
            var source = sources[cell.SourceIndex];
            var expectedAspect = (double)source.PixelWidth / source.PixelHeight;
            var tolerance = source.Kind == CollectionImageSourceKind.Poster
                ? CollectionImageLayoutEngine.PosterCellAspectTolerance
                : CollectionImageLayoutEngine.LandscapeCellAspectTolerance;
            AssertCellAspectWithinTolerance(cell, expectedAspect, tolerance);
        }
    }

    [Fact]
    public void LandscapeCanvas_SingleNonStandardSourcePreservesDecodedAspect()
    {
        var source = new CollectionImageLayoutSource(CollectionImageSourceKind.Landscape, 4, 3);
        var result = CollectionImageLayoutEngine.ComputeLandscapeCanvasLayout(1920, 1080, new[] { source });

        var cell = Assert.Single(result.Cells);
        Assert.Equal(1440, cell.Width);
        Assert.Equal(1080, cell.Height);
        Assert.Equal(240, cell.X);
        Assert.True(result.HasBackground);
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
            var expectedCount = kind == CollectionImageSourceKind.Poster
                ? Math.Min(count, CollectionImageLayoutEngine.PosterTilesMaxOnLandscapeCanvas)
                : count;
            Assert.Equal(expectedCount, result.Cells.Count);

            foreach (var cell in result.Cells)
            {
                AssertCellAspectWithinTolerance(cell, target, tolerance, $"count={count}");
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
            var expectedCount = kind == CollectionImageSourceKind.Poster
                ? Math.Min(count, CollectionImageLayoutEngine.PosterTilesMaxOnLandscapeCanvas)
                : count;
            Assert.Equal(expectedCount, first.Cells.Count);
            Assert.Equal(Enumerable.Range(0, expectedCount), first.Cells.Select(c => c.SourceIndex));
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

    [Fact]
    public void LayoutVersion_IsV4()
    {
        Assert.Equal("v4", CollectionImageLayoutEngine.LayoutVersion);
    }

    [Theory]
    [InlineData(SeasonCollectionLandscapeArtType.Thumb, "thumb.jpg")]
    [InlineData(SeasonCollectionLandscapeArtType.Backdrop, "backdrop.jpg")]
    public void ResolveLandscape_HonorsTypeAndFallsBack(SeasonCollectionLandscapeArtType type, string expectedPath)
    {
        var member = new CollectionMemberArtwork(
            Poster(0),
            new CollectionImageSource("thumb.jpg", 1, 1, CollectionImageSourceKind.Landscape),
            new CollectionImageSource("backdrop.jpg", 1, 1, CollectionImageSourceKind.Landscape));
        Assert.Equal(expectedPath, CollectionImageLayoutEngine.ResolveLandscape(member, type)!.Value.Path);

        var thumbOnly = new CollectionMemberArtwork(null, new CollectionImageSource("thumb.jpg", 1, 1, CollectionImageSourceKind.Landscape), null);
        Assert.Equal("thumb.jpg", CollectionImageLayoutEngine.ResolveLandscape(thumbOnly, SeasonCollectionLandscapeArtType.Backdrop)!.Value.Path);

        var backdropOnly = new CollectionMemberArtwork(null, null, new CollectionImageSource("backdrop.jpg", 1, 1, CollectionImageSourceKind.Landscape));
        Assert.Equal("backdrop.jpg", CollectionImageLayoutEngine.ResolveLandscape(backdropOnly, SeasonCollectionLandscapeArtType.Thumb)!.Value.Path);
    }

    [Fact]
    public void SelectPosterCanvasSources_ThreeMembersUseTwoPostersPlusThirdMemberLandscape()
    {
        var members = new[]
        {
            Member(0, thumb: 100),
            Member(1, thumb: 101),
            Member(2, thumb: 102),
        };
        var sources = CollectionImageLayoutEngine.SelectPosterCanvasSources(members, SeasonCollectionPosterFillMode.ArtworkFill, SeasonCollectionLandscapeArtType.Thumb);
        Assert.Equal(3, sources.Count);
        Assert.Equal(CollectionImageSourceKind.Poster, sources[0].Kind);
        Assert.Equal(CollectionImageSourceKind.Poster, sources[1].Kind);
        Assert.Equal(CollectionImageSourceKind.Landscape, sources[2].Kind);
        Assert.Equal("thumb-102", sources[2].Path);
        Assert.Equal("poster-0", sources[0].Path);
        Assert.Equal("poster-1", sources[1].Path);
        Assert.DoesNotContain("poster-2", sources.Select(s => s.Path));
    }

    [Fact]
    public void SelectPosterCanvasSources_TwoMembersReuseALandscapeStrip()
    {
        var members = new[] { Member(0, thumb: 100), Member(1, thumb: 101) };
        var sources = CollectionImageLayoutEngine.SelectPosterCanvasSources(members, SeasonCollectionPosterFillMode.ArtworkFill, SeasonCollectionLandscapeArtType.Thumb);
        Assert.Equal(3, sources.Count);
        Assert.Equal(CollectionImageSourceKind.Landscape, sources[2].Kind);
    }

    [Fact]
    public void SelectPosterCanvasSources_ThreeMembersWithoutLandscapeFallBackToPosters()
    {
        var members = new[] { Member(0), Member(1), Member(2) };
        var sources = CollectionImageLayoutEngine.SelectPosterCanvasSources(members, SeasonCollectionPosterFillMode.ArtworkFill, SeasonCollectionLandscapeArtType.Thumb);
        Assert.Equal(3, sources.Count);
        Assert.All(sources, s => Assert.Equal(CollectionImageSourceKind.Poster, s.Kind));
    }

    [Fact]
    public void SelectPosterCanvasSources_FourMembersUseFourPostersNoLandscape()
    {
        var members = new[] { Member(0, thumb: 1), Member(1, thumb: 2), Member(2, thumb: 3), Member(3, thumb: 4) };
        var sources = CollectionImageLayoutEngine.SelectPosterCanvasSources(members, SeasonCollectionPosterFillMode.ArtworkFill, SeasonCollectionLandscapeArtType.Thumb);
        Assert.Equal(4, sources.Count);
        Assert.All(sources, s => Assert.Equal(CollectionImageSourceKind.Poster, s.Kind));
    }

    [Fact]
    public void SelectPosterCanvasSources_EmptySpaceModeNeverAddsLandscape()
    {
        var members = new[] { Member(0, thumb: 1), Member(1, thumb: 2), Member(2, thumb: 3) };
        var sources = CollectionImageLayoutEngine.SelectPosterCanvasSources(members, SeasonCollectionPosterFillMode.EmptySpace, SeasonCollectionLandscapeArtType.Thumb);
        Assert.Equal(3, sources.Count);
        Assert.All(sources, s => Assert.Equal(CollectionImageSourceKind.Poster, s.Kind));
    }

    [Fact]
    public void SelectLandscapeCanvasSources_TypeSelectsThumbOrBackdrop()
    {
        var members = new[]
        {
            new CollectionMemberArtwork(
                Poster(0),
                new CollectionImageSource("thumb-0", 1, 1, CollectionImageSourceKind.Landscape),
                new CollectionImageSource("backdrop-0", 1, 1, CollectionImageSourceKind.Landscape)),
        };

        var thumb = CollectionImageLayoutEngine.SelectLandscapeCanvasSources(members, SeasonCollectionLandscapeSourceMode.LandscapeFirst, SeasonCollectionLandscapeArtType.Thumb);
        Assert.Equal("thumb-0", Assert.Single(thumb).Path);

        var backdrop = CollectionImageLayoutEngine.SelectLandscapeCanvasSources(members, SeasonCollectionLandscapeSourceMode.LandscapeFirst, SeasonCollectionLandscapeArtType.Backdrop);
        Assert.Equal("backdrop-0", Assert.Single(backdrop).Path);
    }

    [Fact]
    public void SelectLandscapeCanvasSources_LandscapeFirstAddsPosterFallbacksAfterPreferredSources()
    {
        var members = new[]
        {
            Member(0, thumb: 100),
            Member(1),
            Member(2, thumb: 102),
            new CollectionMemberArtwork(null, new CollectionImageSource("thumb-103", 1, 1, CollectionImageSourceKind.Landscape), null),
        };

        var sources = CollectionImageLayoutEngine.SelectLandscapeCanvasSources(
            members,
            SeasonCollectionLandscapeSourceMode.LandscapeFirst,
            SeasonCollectionLandscapeArtType.Thumb);

        Assert.Equal(new[] { "thumb-100", "thumb-102", "thumb-103", "poster-1" }, sources.Select(source => source.Path));
    }

    [Fact]
    public void SelectLandscapeCanvasSources_PosterFirstAddsLandscapeFallbacksAfterPreferredSources()
    {
        var members = new[]
        {
            Member(0, thumb: 100),
            new CollectionMemberArtwork(null, new CollectionImageSource("thumb-101", 1, 1, CollectionImageSourceKind.Landscape), null),
            Member(2, thumb: 102),
            new CollectionMemberArtwork(null, null, new CollectionImageSource("backdrop-103", 1, 1, CollectionImageSourceKind.Landscape)),
        };

        var sources = CollectionImageLayoutEngine.SelectLandscapeCanvasSources(
            members,
            SeasonCollectionLandscapeSourceMode.PosterFirst,
            SeasonCollectionLandscapeArtType.Thumb);

        Assert.Equal(new[] { "poster-0", "poster-2", "thumb-101", "backdrop-103" }, sources.Select(source => source.Path));
    }

    [Fact]
    public void SelectLandscapeCanvasSources_OnlyModesDoNotFallback()
    {
        var members = new[]
        {
            Member(0, thumb: 100),
            Member(1),
            new CollectionMemberArtwork(null, new CollectionImageSource("thumb-102", 1, 1, CollectionImageSourceKind.Landscape), null),
        };

        var landscapes = CollectionImageLayoutEngine.SelectLandscapeCanvasSources(
            members,
            SeasonCollectionLandscapeSourceMode.LandscapeOnly,
            SeasonCollectionLandscapeArtType.Thumb);
        var posters = CollectionImageLayoutEngine.SelectLandscapeCanvasSources(
            members,
            SeasonCollectionLandscapeSourceMode.PosterOnly,
            SeasonCollectionLandscapeArtType.Thumb);

        Assert.Equal(new[] { "thumb-100", "thumb-102" }, landscapes.Select(source => source.Path));
        Assert.Equal(new[] { "poster-0", "poster-1" }, posters.Select(source => source.Path));
    }

    [Fact]
    public void SelectLandscapeCanvasSources_PosterOnlyIgnoresLandscape()
    {
        var members = new[]
        {
            new CollectionMemberArtwork(Poster(0), new CollectionImageSource("thumb-0", 1, 1, CollectionImageSourceKind.Landscape), null),
        };
        var sources = CollectionImageLayoutEngine.SelectLandscapeCanvasSources(members, SeasonCollectionLandscapeSourceMode.PosterOnly, SeasonCollectionLandscapeArtType.Thumb);
        Assert.Equal(CollectionImageSourceKind.Poster, Assert.Single(sources).Kind);
    }

    private static CollectionImageSource Poster(int id) => new($"poster-{id}", 1, 1, CollectionImageSourceKind.Poster);

    private static CollectionMemberArtwork Member(int id, int? thumb = null, int? backdrop = null)
    {
        return new CollectionMemberArtwork(
            Poster(id),
            thumb.HasValue ? new CollectionImageSource($"thumb-{thumb.Value}", 1, 1, CollectionImageSourceKind.Landscape) : null,
            backdrop.HasValue ? new CollectionImageSource($"backdrop-{backdrop.Value}", 1, 1, CollectionImageSourceKind.Landscape) : null);
    }

    private static void AssertCellAspectWithinTolerance(
        CollectionImageLayoutCell cell,
        double expectedAspect,
        double tolerance,
        string? context = null)
    {
        var aspect = (double)cell.Width / cell.Height;
        var deviation = System.Math.Abs(System.Math.Log(aspect / expectedAspect));
        Assert.True(
            deviation <= tolerance + 0.02,
            $"{context} cell={cell} aspect={aspect:F3} deviation={deviation:F3} exceeds tolerance {tolerance}.");
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
