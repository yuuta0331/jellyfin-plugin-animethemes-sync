using System;
using System.Collections.Generic;

namespace AnimeThemesSync.Shared.Models;

public sealed record LocalMediaCleanupFile(
    string CandidateId,
    Guid ItemId,
    string ItemName,
    string ItemType,
    string? LibraryName,
    string Path,
    string FileName,
    string FileKind,
    string Status,
    string Source,
    long Size,
    DateTimeOffset LastWriteTimeUtc);

public sealed record LocalMediaCleanupTaskStatus(
    string TaskId,
    string ScanId,
    string TaskType,
    string Status,
    double Progress,
    string Message,
    LocalMediaCleanupDeleteResult? Result,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    bool CanCancel);

public sealed record LocalMediaCleanupScanPage(
    IReadOnlyList<LocalMediaCleanupFile> Items,
    int TotalRecordCount,
    long TotalBytes,
    int StartIndex,
    int Limit,
    string ScanId,
    DateTimeOffset ExpiresAt);

public sealed class LocalMediaCleanupDeleteRequest
{
    public List<string> CandidateIds { get; set; } = [];
}

public sealed record LocalMediaCleanupDeleteResult(
    int FilesDeleted,
    long BytesDeleted,
    int FilesSkipped,
    int FilesFailed);

public sealed record ThemeFileRegistryEntry(
    Guid LogicalItemId,
    string ThemeKey,
    string FileKind,
    string Path,
    string Source);
