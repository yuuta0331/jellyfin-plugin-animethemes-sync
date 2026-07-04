using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace Emby.Plugin.AnimeThemesSync.ScheduledTasks;

/// <summary>
/// Refreshes season metadata and applies local season automation without downloading themes.
/// </summary>
public sealed class SeasonMetadataRefreshTask : IScheduledTask
{
    public string Name => "Refresh Anime Season Metadata";

    public string Key => "AnimeThemesSyncSeasonMetadataRefresh";

    public string Description => "Refreshes stale or missing anime season metadata, tags, collections, and Browser data without downloading themes.";

    public string Category => "Anime";

    public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        var downloader = ThemeDownloader.Current
            ?? throw new InvalidOperationException("AnimeThemes Sync is not initialized.");
        return downloader.ExecuteSeasonMetadataMaintenanceAsync(progress, cancellationToken);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromDays(7).Ticks,
            },
        ];
    }
}
