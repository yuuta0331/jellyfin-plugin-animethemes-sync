using System;
using System.Collections.Generic;

namespace AnimeThemesSync.Shared.Models;

public sealed record ManagerIssueRecord(
    string Id,
    string Fingerprint,
    string Category,
    string Severity,
    string State,
    string Title,
    string Message,
    string? TargetName,
    string? ItemId,
    string? SeriesItemId,
    string? SeasonItemId,
    string? RowId,
    string? TaskName,
    string? Operation,
    string? Url,
    string? DestinationPath,
    int? HttpStatusCode,
    string? Stage,
    string? SuggestedAction,
    int OccurrenceCount,
    string FirstSeenUtc,
    string LastSeenUtc,
    string? NextRetryUtc,
    string? ResolvedAtUtc);

public sealed record ManagerIssuePage(
    IReadOnlyList<ManagerIssueRecord> Items,
    int TotalRecordCount,
    int StartIndex,
    int Limit,
    ManagerIssueSummary Summary);

public sealed record ManagerIssueSummary(
    int Open,
    int Deferred,
    int Ignored,
    int Resolved,
    int Errors,
    int Warnings,
    int Downloads,
    int Mappings,
    int SeasonAutomation,
    int Tasks,
    int Imports,
    int Maintenance);

public sealed class ManagerIssueUpsert
{
    public string Category { get; set; } = ManagerIssueCategories.Task;

    public string Severity { get; set; } = ManagerIssueSeverities.Error;

    public string State { get; set; } = ManagerIssueStates.Open;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? TargetName { get; set; }

    public string? ItemId { get; set; }

    public string? SeriesItemId { get; set; }

    public string? SeasonItemId { get; set; }

    public string? RowId { get; set; }

    public string? TaskName { get; set; }

    public string? Operation { get; set; }

    public string? Url { get; set; }

    public string? DestinationPath { get; set; }

    public int? HttpStatusCode { get; set; }

    public string? Stage { get; set; }

    public string? SuggestedAction { get; set; }

    public string? FingerprintSeed { get; set; }

    public DateTimeOffset? NextRetryUtc { get; set; }
}

public static class ManagerIssueCategories
{
    public const string Download = "Download";
    public const string Mapping = "Mapping";
    public const string SeasonAutomation = "SeasonAutomation";
    public const string Task = "Task";
    public const string Import = "Import";
    public const string Maintenance = "Maintenance";
}

public static class ManagerIssueSeverities
{
    public const string Info = "Info";
    public const string Warning = "Warning";
    public const string Error = "Error";
    public const string Critical = "Critical";
}

public static class ManagerIssueStates
{
    public const string Open = "Open";
    public const string Deferred = "Deferred";
    public const string Ignored = "Ignored";
    public const string Resolved = "Resolved";
}
