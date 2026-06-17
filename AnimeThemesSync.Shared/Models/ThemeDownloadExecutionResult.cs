namespace AnimeThemesSync.Shared.Models;

public sealed record ThemeDownloadExecutionResult(
    int ItemsProcessed,
    int DownloadsPlanned,
    int DownloadsCompleted,
    int ExtrasPlanned,
    int ExtrasCompleted,
    int ExtraFailures);

public sealed record ThemeDownloadJobStartResult(
    string JobId,
    string Status,
    double Progress,
    string Message);

public sealed record ThemeDownloadJobStatus(
    string JobId,
    string Status,
    double Progress,
    string Message,
    ThemeDownloadExecutionResult? Result,
    string? Error);
