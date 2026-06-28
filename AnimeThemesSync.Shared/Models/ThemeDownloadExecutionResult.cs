using System;

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
    string Message,
    string JobType,
    Guid? ItemId,
    string? RowId,
    string DisplayTitle);

public sealed record ThemeDownloadJobStatus(
    string JobId,
    string Status,
    double Progress,
    string Message,
    ThemeDownloadExecutionResult? Result,
    string? Error,
    string JobType,
    Guid? ItemId,
    string? RowId,
    string DisplayTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int? QueuePosition,
    bool CanCancel);

public sealed record ThemeDownloadJobDescriptor(
    string JobType,
    Guid? ItemId,
    string? RowId,
    string DisplayTitle);
