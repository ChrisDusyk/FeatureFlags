using System;

namespace FeatureFlags.Client;

/// <summary>
/// A FeatureFlags installation could not be read.
///
/// <para>
/// This does not reach an <c>IsEnabledAsync</c> caller: reads fall back to the last good snapshot,
/// or to the default. It surfaces from <see cref="IFeatureFlagClient.RefreshAsync"/>, which is an
/// explicit request and so reports what happened, and at startup when
/// <see cref="FeatureFlagsOptions.ThrowOnStartupFailure"/> is set.
/// </para>
/// </summary>
public sealed class FeatureFlagsException : Exception
{
    /// <summary>Creates the exception with a message describing what could not be read.</summary>
    public FeatureFlagsException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure underneath it.</summary>
    public FeatureFlagsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
