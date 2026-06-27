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

    SeasonFinderItemsPage QueryRows(string? libraryId, int? startIndex, int? limit, string? searchTerm, string? status, string? sortBy, string? sortOrder);

    IReadOnlyList<SeasonThemeMappingRow> GetAllRows();

    bool IsCacheReady();

    SeasonFinderStorageStatus GetStorageStatus();

    void SetRebuildError(string? error);

    void ClearCache();

    bool TryGetSearch(string query, int? year, out string json);

    void SetSearch(string query, int? year, string json);
}
