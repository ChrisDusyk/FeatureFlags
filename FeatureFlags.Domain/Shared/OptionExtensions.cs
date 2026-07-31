namespace FeatureFlags.Domain.Shared;

public static class OptionExtensions
{
    public static Option<TValue> ToOption<TValue>(this TValue? value) where TValue : class =>
        value is null ? Option<TValue>.None : Option<TValue>.Some(value);

    public static Result<TValue> ToResult<TValue>(this Option<TValue> option, Error error) =>
        option.Match(Result.Success, () => Result.Failure<TValue>(error));
}
