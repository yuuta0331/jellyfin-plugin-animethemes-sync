using AnimeThemesSync.Shared.Models;
using AnimeThemesSync.Shared.Services;
using Xunit;

namespace Jellyfin.Plugin.AnimeThemesSync.Tests;

public class ThemeScoringTests
{
    [Fact]
    public void Rate_Spoiler_AddsPenalty()
    {
        var entry = new AnimeThemesEntry { Spoiler = true };
        var video = new AnimeThemesVideo { Tags = "NC" };

        var breakdown = ThemeScoringService.GetScoreBreakdown(entry, video);

        Assert.Contains("Spoiler:+50", breakdown);
    }

    [Fact]
    public void Rate_OverlapOver_AddsPenalty()
    {
        var entry = new AnimeThemesEntry();
        var video = new AnimeThemesVideo { Overlap = "Over", Tags = "NC" };

        var breakdown = ThemeScoringService.GetScoreBreakdown(entry, video);

        Assert.Contains("Overlap(Over):+20", breakdown);
    }

    [Fact]
    public void Rate_OverlapTransition_AddsPenalty()
    {
        var entry = new AnimeThemesEntry();
        var video = new AnimeThemesVideo { Overlap = "Transition", Tags = "NC" };

        var breakdown = ThemeScoringService.GetScoreBreakdown(entry, video);

        Assert.Contains("Overlap(Trans):+15", breakdown);
    }

    [Fact]
    public void Rate_SourceLd_AddsPenalty()
    {
        var entry = new AnimeThemesEntry();
        var video = new AnimeThemesVideo { Source = "LD", Tags = "NC" };

        var breakdown = ThemeScoringService.GetScoreBreakdown(entry, video);

        Assert.Contains("Source(LD):+10", breakdown);
    }

    [Fact]
    public void Rate_SourceWeb_AddsPenalty()
    {
        var entry = new AnimeThemesEntry();
        var video = new AnimeThemesVideo { Source = "WEB", Tags = "NC" };

        var breakdown = ThemeScoringService.GetScoreBreakdown(entry, video);

        Assert.Contains("Source(WEB):+5", breakdown);
    }

    [Fact]
    public void Rate_BroadcastWithCredits_AddsPenalty()
    {
        var entry = new AnimeThemesEntry();
        var video = new AnimeThemesVideo { Tags = string.Empty };

        var breakdown = ThemeScoringService.GetScoreBreakdown(entry, video);

        Assert.Contains("Credits:+10", breakdown);
    }

    [Fact]
    public void Rate_Creditless_HasNoCreditPenalty()
    {
        var entry = new AnimeThemesEntry();
        var video = new AnimeThemesVideo { Tags = "NC" };

        var breakdown = ThemeScoringService.GetScoreBreakdown(entry, video);

        Assert.DoesNotContain("Credits:+10", breakdown);
    }
}

