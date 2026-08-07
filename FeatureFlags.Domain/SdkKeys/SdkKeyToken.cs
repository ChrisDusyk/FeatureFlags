using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.SdkKeys;

/// <summary>
/// The credential a program holds, and the only place its format is decided.
///
/// <code>ffs_dev_Ab3xK9mQr7T_8fJ2…43 characters…kLp</code>
///
/// <para>
/// Four segments. The <c>ffs_</c> prefix is what lets the server tell an SDK key from a JWT before
/// it does any work with either. The environment segment is there for whoever is reading a
/// configuration file — <b>it is not authoritative and is never trusted</b>; the environment comes
/// from the stored row. The selector is a public lookup handle. The secret is the credential.
/// </para>
///
/// <para>
/// Splitting the selector from the secret is what lets the secret be hashed at rest and still be
/// found in one indexed query. Looking a key up by a hash of the whole token would work too, but
/// only until the hash needed changing.
/// </para>
/// </summary>
public sealed partial class SdkKeyToken
{
    /// <summary>Distinguishes an SDK key from a JWT on the same Authorization header.</summary>
    public const string Prefix = "ffs";

    private const int SelectorBytes = 8;
    private const int SecretBytes = 32;

    /// <summary>Unpadded base64url of <see cref="SelectorBytes"/> bytes.</summary>
    public const int SelectorLength = 11;

    /// <summary>Unpadded base64url of <see cref="SecretBytes"/> bytes.</summary>
    public const int SecretLength = 43;

    private SdkKeyToken(string value, string selector, byte[] secretHash)
    {
        Value = value;
        Selector = selector;
        SecretHash = secretHash;
    }

    /// <summary>
    /// The whole token, as the holder must present it. This exists exactly once, on the way out of
    /// the endpoint that issued it — nothing stores it.
    /// </summary>
    public string Value { get; }

    /// <summary>The public handle the key is looked up by. Stored in plaintext, indexed, unique.</summary>
    public string Selector { get; }

    /// <summary>What is stored in place of the secret.</summary>
    public byte[] SecretHash { get; }

    /// <summary>
    /// Mints a new token. The secret is 256 bits from <see cref="RandomNumberGenerator"/>, which is
    /// why a plain SHA-256 is the right hash for it: there is no password here to guess, no
    /// dictionary to run, and nothing for a slow KDF to protect against. Reach for Argon2 when a
    /// human chose the secret.
    /// </summary>
    public static SdkKeyToken Issue(EnvironmentKey environment)
    {
        var selector = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SelectorBytes));
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));

        return new SdkKeyToken(
            $"{Prefix}_{environment.Value}_{selector}_{secret}",
            selector,
            HashSecret(secret));
    }

    /// <summary>
    /// Whether a presented credential is shaped like an SDK key at all. Cheap and total: a JWT is
    /// dot-separated base64url and can never begin with <c>ffs_</c>, so the server can route a
    /// credential to the right authentication scheme without parsing it or touching the database.
    /// </summary>
    public static bool LooksLikeSdkKey(string? value) =>
        value is not null && value.StartsWith($"{Prefix}_", StringComparison.Ordinal);

    /// <summary>
    /// Splits a presented token into the parts needed to verify it. Every malformed token fails the
    /// same way on purpose — a caller learning <em>which</em> part it got wrong learns something
    /// about a credential it does not hold.
    /// </summary>
    public static Result<SdkKeyCredential> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !TokenPattern().IsMatch(value))
            return Result.Failure<SdkKeyCredential>(SdkKeyErrors.TokenMalformed);

        var segments = value.Split('_');

        return Result.Success(new SdkKeyCredential(
            segments[2],
            HashSecret(segments[3])));
    }

    /// <summary>
    /// Hashes the secret's own text rather than the bytes behind it. Two different strings can
    /// decode to the same bytes under a lax base64 reader; the text of a token we minted cannot be
    /// ambiguous about itself.
    /// </summary>
    private static byte[] HashSecret(string secret) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    /// <summary>
    /// The environment segment is matched loosely — anything slug-shaped — because it is decoration.
    /// Pinning it to <see cref="EnvironmentKey.All"/> would make retiring an environment silently
    /// invalidate keys that the database still says are fine.
    /// <para>
    /// The two lengths are <see cref="SelectorLength"/> and <see cref="SecretLength"/>, spelled out
    /// because an attribute argument has to be a literal — <c>SdkKeyTokenTests</c> holds them to
    /// each other.
    /// </para>
    /// </summary>
    [GeneratedRegex(@"^ffs_[a-z0-9-]+_[A-Za-z0-9_-]{11}_[A-Za-z0-9_-]{43}$")]
    private static partial Regex TokenPattern();
}

/// <summary>
/// A presented token, taken apart. Carries the hash rather than the secret so that nothing past
/// <see cref="SdkKeyToken.Parse"/> holds the credential itself.
/// </summary>
public sealed record SdkKeyCredential(string Selector, byte[] SecretHash);
