using FeatureFlags.Domain.Segments;

namespace FeatureFlags.Server.Features.Segments.ListSegments;

/// <summary>
/// What the list screen needs, and not the definition itself. A segment's conditions are the detail
/// screen's business; a list of twenty of them would ship twenty condition sets to render counts.
/// </summary>
public sealed record ListSegmentSummary(
    Guid Id,
    string Key,
    string Name,
    string Description,
    int ConditionCount,
    int IncludedKeyCount,
    int ExcludedKeyCount,
    bool MatchesNobody,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ListSegmentSummary From(SegmentView segment) => new(
        segment.Id,
        segment.Key.Value,
        segment.Name,
        segment.Description,
        segment.Definition.Conditions.Count,
        segment.Definition.IncludedKeys.Count,
        segment.Definition.ExcludedKeys.Count,
        // Worth saying out loud in the list rather than leaving somebody to infer it from three
        // zeroes: a segment nobody can be in silently turns off every flag that targets it.
        segment.Definition.IsEmpty,
        segment.CreatedAt,
        segment.UpdatedAt);
}

public sealed record ListSegmentsResponse(IReadOnlyList<ListSegmentSummary> Segments);
