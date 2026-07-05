using System;

namespace AnimeThemesSync.Shared.Models;

public sealed record DownloadFailureRecord(
    string Url,
    string DestinationPath,
    string Status,
    int AttemptCount,
    string LastError,
    int? LastStatusCode,
    DateTimeOffset? NextRetryUtc,
    DateTimeOffset UpdatedUtc);

public static class DownloadFailureStatuses
{
    public const string TransientFailed = "TransientFailed";
    public const string PermanentFailed = "PermanentFailed";
}
