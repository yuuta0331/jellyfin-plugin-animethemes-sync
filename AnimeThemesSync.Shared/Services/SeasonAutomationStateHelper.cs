using System;
using System.Linq;
using AnimeThemesSync.Shared.Models;

namespace AnimeThemesSync.Shared.Services;

/// <summary>
/// Pure transformations of <see cref="SeasonAutomationState"/>. Extracted from
/// the host ThemeDownloader classes so both hosts share one implementation.
/// </summary>
public static class SeasonAutomationStateHelper
{
    /// <summary>
    /// Carries the previous run's tags/collections into the current state when the
    /// matching feature is disabled, keeping rule references consistent and de-duplicated.
    /// </summary>
    public static void PreserveDisabledAutomationState(
        SeasonAutomationState current,
        SeasonAutomationState previous,
        bool preserveTags,
        bool preserveCollections)
    {
        if (preserveTags)
        {
            current.Tags.AddRange(previous.Tags);
        }

        if (preserveCollections)
        {
            current.Collections.AddRange(previous.Collections);
            current.CollectionMembers.AddRange(previous.CollectionMembers);
        }

        var referencedRuleKeys = current.Tags.Select(i => i.RuleKey)
            .Concat(current.CollectionMembers.Select(i => i.RuleKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        current.Rules.AddRange(previous.Rules.Where(i => referencedRuleKeys.Contains(i.RuleKey)));
        current.Rules = current.Rules.GroupBy(i => i.RuleKey, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
        current.Tags = current.Tags.GroupBy(i => i.RuleKey + "|" + i.TargetItemId + "|" + i.TagName, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
        current.Collections = current.Collections.GroupBy(i => i.CollectionKey, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
        current.CollectionMembers = current.CollectionMembers.GroupBy(i => i.CollectionKey + "|" + i.TargetItemId, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList();
    }

    /// <summary>
    /// Projects the SQLite automation state into the legacy JSON season metadata
    /// state kept for backward compatibility.
    /// </summary>
    public static SeasonMetadataState BuildLegacySeasonMetadataState(SeasonAutomationState automation, SeasonMetadataState? legacy)
    {
        if (automation.Rules.Count == 0)
        {
            return legacy ?? new SeasonMetadataState { SeriesItemId = automation.SeriesItemId };
        }

        var collections = automation.Collections.ToDictionary(i => i.CollectionKey, StringComparer.OrdinalIgnoreCase);
        return new SeasonMetadataState
        {
            SeriesItemId = automation.SeriesItemId,
            ManagedTags = automation.Tags.Where(i => i.AddedByPlugin).GroupBy(i => i.TargetItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(i => i.Key, i => i.Select(t => t.TagName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase),
            CollectionMemberships = automation.CollectionMembers.Select(member =>
            {
                collections.TryGetValue(member.CollectionKey, out var collection);
                return new SeasonCollectionMembershipState
                {
                    BroadcastSeasonKey = member.CollectionKey, CollectionId = collection?.CollectionItemId ?? string.Empty,
                    CollectionName = collection?.CollectionName ?? member.CollectionKey, ItemId = member.TargetItemId,
                    AddedByPlugin = member.AddedByPlugin,
                };
            }).ToList(),
            BroadcastSeasons = automation.Rules.Where(i => i.AnimeYear.HasValue && !string.IsNullOrWhiteSpace(i.AnimeSeason))
                .Select(i => new BroadcastSeasonValue(i.BroadcastSeasonKey ?? string.Empty, i.BroadcastSeasonLabel ?? string.Empty, i.AnimeYear!.Value, i.AnimeSeason!))
                .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase).Select(i => i.First()).ToList(),
        };
    }
}
