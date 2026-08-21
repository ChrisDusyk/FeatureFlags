using FeatureFlags.Domain.Shared;
using FeatureFlags.Evaluation;

namespace FeatureFlags.Server.Features.Evaluation.EvaluateForContext;

/// <summary>
/// What a caller can get wrong about a context. Server-side rather than in the domain, because
/// these are limits on one HTTP route's request rather than facts about evaluation — the engine
/// itself is happy to answer for a context of any size.
/// </summary>
public static class EvaluationErrors
{
    public static Error ContextTooLarge => Error.Validation(
        "Evaluation.Context.TooLarge",
        "The context is larger than this endpoint accepts. It takes at most 64 attributes, " +
        "attribute names of 100 characters or fewer, and a key of 256 characters or fewer.");

    public static Error AttributeNotRepresentable(string attribute) => Error.Validation(
        "Evaluation.Context.AttributeNotRepresentable",
        $"The value for '{attribute}' is not one every client can compare. Text must be " +
        $"{AttributeValue.MaxTextLength} characters or fewer, and a number must be finite and no " +
        $"larger than {AttributeValue.MaxMagnitude:0}.");
}
