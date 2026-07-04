using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.AnimeThemesSync.ScheduledTasks;

/// <summary>
/// Refreshes season metadata and applies local season automation without downloading themes.
/// </summary>
public sealed class SeasonMetadataRefreshTask : IScheduledTask
{
    private readonly ThemeDownloader _themeDownloader;

    public SeasonMetadataRefreshTask(ThemeDownloader themeDownloader)
    {
        _themeDownloader = themeDownloader;
    }

    public string Name => "Refresh Anime Season Metadata";

    public string Key => "AnimeThemesSyncSeasonMetadataRefresh";

    public string Description => "Refreshes stale or missing anime season metadata, tags, collections, and Browser data without downloading themes.";

    public string Category => "Anime";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        return _themeDownloader.ExecuteSeasonMetadataMaintenanceAsync(progress, cancellationToken);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromDays(7).Ticks,
            },
        ];
    }
}
