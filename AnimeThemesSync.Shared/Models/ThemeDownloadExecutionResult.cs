namespace AnimeThemesSync.Shared.Models;

public sealed record ThemeDownloadExecutionResult(
    int ItemsProcessed,
    int DownloadsPlanned,
    int DownloadsCompleted,
    int ExtrasPlanned,
    int ExtrasCompleted,
    int ExtraFailures);
