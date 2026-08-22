using System;
using System.Diagnostics;
using System.Reflection;
using Windows.ApplicationModel;

namespace Text_Grab.Utilities;

/// <summary>
/// Unlocks the Windows AI language model, which Microsoft ships as a Limited Access Feature.
///
/// An app must call <see cref="LimitedAccessFeatures.TryUnlockFeature"/> with a token issued for
/// its own publisher ID before <c>Microsoft.Windows.AI.Text.LanguageModel</c> will do anything;
/// without it every call fails with "Access is denied. Limited Access Feature is not available:
/// com.microsoft.windows.ai.languagemodel."
///
/// Tokens are requested from Microsoft at https://aka.ms/laffeatures and must not be committed to
/// source control, so the token and publisher ID are read at runtime from (in order):
///   1. AssemblyMetadata baked in at build time — set the MSBuild properties
///      <c>LafToken</c> and <c>LafPublisherId</c> (see Text-Grab.csproj).
///   2. The LAF_TOKEN and LAF_PUBLISHER_ID environment variables, for local development.
///
/// This mirrors how microsoft/ai-dev-gallery handles the same feature.
/// </summary>
internal static class LimitedAccessFeatureUtilities
{
    internal const string LanguageModelFeatureId = "com.microsoft.windows.ai.languagemodel";

    private const string TokenKey = "LAF_TOKEN";
    private const string PublisherIdKey = "LAF_PUBLISHER_ID";

    /// <summary>The unlock is process-wide and only needs to happen once.</summary>
    private static readonly Lazy<(bool Unlocked, string? Reason)> _languageModelUnlock = new(UnlockLanguageModel);

    /// <summary>
    /// Attempts to unlock the language model feature, returning why it is unavailable when it is.
    /// The result is cached for the life of the process.
    /// </summary>
    internal static (bool Unlocked, string? Reason) TryUnlockLanguageModel() => _languageModelUnlock.Value;

    private static (bool Unlocked, string? Reason) UnlockLanguageModel()
    {
        string publisherId = GetSetting(PublisherIdKey);

        // The publisher ID is the hash half of the package family name. Falling back to it means a
        // build with only a token configured still forms the correct usage string.
        if (string.IsNullOrWhiteSpace(publisherId))
            publisherId = GetPublisherHash();

        string token = GetSetting(TokenKey);
        string usage = $"{publisherId} has registered their use of {LanguageModelFeatureId} with Microsoft and agrees to the terms of use.";

        try
        {
            LimitedAccessFeatureRequestResult result =
                LimitedAccessFeatures.TryUnlockFeature(LanguageModelFeatureId, token, usage);

            if (result.Status is LimitedAccessFeatureStatus.Available or LimitedAccessFeatureStatus.AvailableWithoutToken)
            {
                Debug.WriteLine($"Windows AI language model unlocked: {result.Status}");
                return (true, null);
            }

            Debug.WriteLine($"Windows AI language model not unlocked: {result.Status}");
            return (false, DescribeFailure(result.Status, token, publisherId));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TryUnlockFeature failed: {ex.Message}");
            return (false, $"Windows could not unlock the AI language model feature: {ex.Message}");
        }
    }

    private static string DescribeFailure(LimitedAccessFeatureStatus status, string token, string publisherId)
    {
        string statusText = status switch
        {
            LimitedAccessFeatureStatus.Unavailable => "Windows reports the feature as unavailable",
            LimitedAccessFeatureStatus.Unknown => "Windows does not recognize this app as registered for the feature",
            _ => $"Windows returned {status}",
        };

        string tokenText = string.IsNullOrWhiteSpace(token)
            ? "This build of Text-Grab has no unlock token configured."
            : "The configured unlock token was rejected.";

        return $"""
            Windows AI's language model is a Limited Access Feature and must be unlocked before it can be used.

            {tokenText} {statusText}.

            A token has to be requested from Microsoft at https://aka.ms/laffeatures for publisher ID '{publisherId}', then supplied at build time via the LafToken and LafPublisherId MSBuild properties (or the LAF_TOKEN and LAF_PUBLISHER_ID environment variables).
            """;
    }

    /// <summary>
    /// Reads a value from build-time assembly metadata, falling back to an environment variable.
    /// </summary>
    private static string GetSetting(string key)
    {
        foreach (AssemblyMetadataAttribute attribute in typeof(LimitedAccessFeatureUtilities).Assembly
                     .GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(attribute.Key, key, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(attribute.Value))
                return attribute.Value;
        }

        return Environment.GetEnvironmentVariable(key) ?? string.Empty;
    }

    /// <summary>
    /// The publisher hash from the package family name ("Name_hash" -> "hash"), which is the
    /// publisher ID a Limited Access Feature token is issued against.
    /// </summary>
    internal static string GetPublisherHash()
    {
        try
        {
            string familyName = Package.Current.Id.FamilyName;
            if (string.IsNullOrWhiteSpace(familyName))
                return string.Empty;

            string[] parts = familyName.Split('_');
            return parts.Length >= 2 ? parts[1] : string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read the package family name: {ex.Message}");
            return string.Empty;
        }
    }
}
