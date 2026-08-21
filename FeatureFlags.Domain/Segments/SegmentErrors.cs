using FeatureFlags.Domain.Shared;
using FeatureFlags.Evaluation;

namespace FeatureFlags.Domain.Segments;

public static class SegmentErrors
{
    public static Error KeyRequired => Error.Validation(
        "Segment.Key.Required",
        "A segment key is required.");

    public static Error KeyTooLong => Error.Validation(
        "Segment.Key.TooLong",
        $"A segment key must be {SegmentKey.MaxLength} characters or fewer.");

    public static Error KeyInvalidFormat => Error.Validation(
        "Segment.Key.InvalidFormat",
        "A segment key must be a lowercase slug containing only letters, digits, and single hyphens between segments.");

    public static Error NameRequired => Error.Validation(
        "Segment.Name.Required",
        "A segment name is required.");

    public static Error NameTooLong => Error.Validation(
        "Segment.Name.TooLong",
        $"A segment name must be {Segment.MaxNameLength} characters or fewer.");

    public static Error DescriptionTooLong => Error.Validation(
        "Segment.Description.TooLong",
        $"A segment description must be {Segment.MaxDescriptionLength} characters or fewer.");

    public static Error AttributeRequired => Error.Validation(
        "Segment.Condition.AttributeRequired",
        "A condition must name the attribute it tests.");

    public static Error AttributeTooLong => Error.Validation(
        "Segment.Condition.AttributeTooLong",
        $"An attribute name must be {SegmentCondition.MaxAttributeLength} characters or fewer.");

    public static Error OperatorRequired => Error.Validation(
        "Segment.Condition.OperatorRequired",
        "A condition must say how it compares.");

    public static Error OperatorUnrecognized(string value) => Error.Validation(
        "Segment.Condition.OperatorUnrecognized",
        $"'{value}' is not a comparison this application recognizes. It understands: " +
        $"{string.Join(", ", ConditionOperator.All.Select(candidate => candidate.Value))}.");

    public static Error ValuesRequired => Error.Validation(
        "Segment.Condition.ValuesRequired",
        "A condition must have something to compare against.");

    public static Error TooManyValues => Error.Validation(
        "Segment.Condition.TooManyValues",
        $"A condition can compare against at most {SegmentCondition.MaxValues} values.");

    public static Error OperatorTakesOneValue(ConditionOperator @operator) => Error.Validation(
        "Segment.Condition.OperatorTakesOneValue",
        $"'{@operator}' compares against a single value. Use '{ConditionOperator.OneOf}' for several.");

    /// <summary>
    /// A value the three evaluation engines could not agree on: NaN, an infinity, a number past
    /// 2^53 where a double and a JavaScript number stop lining up, or a string over the cap.
    /// </summary>
    public static Error ValueNotRepresentable(string attribute) => Error.Validation(
        "Segment.Condition.ValueNotRepresentable",
        $"A value for '{attribute}' is not one every client can compare. Text must be " +
        $"{AttributeValue.MaxTextLength} characters or fewer, and a number must be finite and no " +
        $"larger than {AttributeValue.MaxMagnitude:0}.");

    public static Error ValueKindNotAccepted(ConditionOperator @operator, AttributeValueKind kind) => Error.Validation(
        "Segment.Condition.ValueKindNotAccepted",
        $"'{@operator}' cannot compare a {Describe(kind)}. Comparing values of different types never " +
        "matches, so this would have been saved and then quietly matched nobody.");

    public static Error TooManyConditions => Error.Validation(
        "Segment.Definition.TooManyConditions",
        $"A segment can have at most {SegmentDefinition.MaxConditions} conditions.");

    public static Error TooManyKeys => Error.Validation(
        "Segment.Definition.TooManyKeys",
        $"A segment can name at most {SegmentDefinition.MaxKeys} keys in each of its lists.");

    public static Error ContextKeyTooLong => Error.Validation(
        "Segment.Definition.ContextKeyTooLong",
        $"A key must be {SegmentDefinition.MaxKeyLength} characters or fewer.");

    public static Error DuplicateKey(SegmentKey key) => Error.Conflict(
        "Segment.DuplicateKey",
        $"A segment with the key '{key}' already exists.");

    /// <summary>
    /// The key belonged to a segment that has been retired. Its events are still there and are
    /// still readable, which is exactly why the key cannot be handed to something new.
    /// </summary>
    public static Error KeyRetired(SegmentKey key) => Error.Conflict(
        "Segment.KeyRetired",
        $"The key '{key}' belonged to a segment that was deleted, and is not reused. Choose another.");

    /// <summary>Another writer appended an event for this segment between this caller's read and
    /// its write — the store's own sequence-number check caught it, so the caller must reload.</summary>
    public static Error ConcurrencyConflict(SegmentKey key) => Error.Conflict(
        "Segment.ConcurrencyConflict",
        $"The segment '{key}' was changed by someone else. Reload and try again.");

    public static Error NotFound(SegmentKey key) => Error.NotFound(
        "Segment.NotFound",
        $"No segment with the key '{key}' exists.");

    public static Error Deleted(SegmentKey key) => Error.Conflict(
        "Segment.Deleted",
        $"The segment '{key}' has been deleted and can no longer be changed.");

    public static Error AlreadyDeleted(SegmentKey key) => Error.Conflict(
        "Segment.AlreadyDeleted",
        $"The segment '{key}' has already been deleted.");

    private static string Describe(AttributeValueKind kind) => kind switch
    {
        AttributeValueKind.Text => "text value",
        AttributeValueKind.Number => "number",
        _ => "true/false value",
    };
}
