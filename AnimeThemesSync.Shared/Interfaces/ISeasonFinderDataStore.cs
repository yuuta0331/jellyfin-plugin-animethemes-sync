using System.Collections.Generic;
using AnimeThemesSync.Shared.Configuration;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Interfaces;

/// <summary>
/// Host-specific persistent storage for Season Finder data.
/// </summary>
public interface ISeasonFinderDataStore
{
    string DatabasePath { get; }

    void EnsureInitialized();

    void MigrateLegacyMappings(IEnumerable<SeasonThemeMapping>? mappings);

    List<SeasonThemeMapping> GetSeasonThemeMappings();

    void ReplaceSeasonThemeMappings(IEnumerable<SeasonThemeMapping> mappings, string source);

    void ApplySeasonThemeMappingChanges(IReadOnlyList<SeasonThemeMappingChange> changes);

    void ReplaceRows(IEnumerable<SeasonFinderRowRecord> records);

    void UpsertRow(SeasonFinderRowRecord record);

    SeasonFinderItemsPage QueryRows(string? libraryId, int? startIndex, int? limit, string? searchTerm, string? status, int? seasonNumber, string? sortBy, string? sortOrder);

    SeasonAutomationState GetSeasonAutomationState(string seriesItemId);

    void SaveSeasonAutomationState(SeasonAutomationState state);

    SeasonMetadataSnapshot? GetSeasonMetadataSnapshot(string seriesItemId);

    void SaveSeasonMetadataSnapshot(SeasonMetadataSnapshot snapshot);

    ApiFetchCacheEntry? GetApiFetchCache(string cacheKey);

    void UpsertApiFetchCache(ApiFetchCacheEntry entry, int ttlDays = 30);

    IReadOnlyList<ManagedSeasonCollectionAssetState> GetCollectionAssetStates();

    void UpsertCollectionAssetState(ManagedSeasonCollectionAssetState state);

    void DeleteCollectionAssetState(string collectionKey);

    IReadOnlyList<SeasonSummary> GetSeasonSummaries(string seriesItemId);

    IReadOnlyList<SeasonThemeMappingRow> GetAllRows();

    bool IsCacheReady();

    SeasonFinderStorageStatus GetStorageStatus();

    CacheMaintenanceStatus GetCacheMaintenanceStatus(int seasonMetadataTtlDays, int providerResponseTtlDays);

    void SetRebuildError(string? error);

    void ClearCache();

    void ClearProviderCache();

    bool TryGetSearch(string query, int? year, out string json, int ttlDays = 30);

    void SetSearch(string query, int? year, string json, int ttlDays = 30);
}
