using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Text_Grab.Utilities;

internal sealed class AutomationProfile
{
    internal const string ProfileEnvironmentVariable = "TEXT_GRAB_AUTOMATION_PROFILE";
    internal const string SystemIntegrationEnvironmentVariable = "TEXT_GRAB_AUTOMATION_SYSTEM_INTEGRATION";
    internal const string DisposableRegistrationEnvironmentVariable = "TEXT_GRAB_AUTOMATION_DISPOSABLE_REGISTRATION";
    internal const string DisposableVmEnvironmentVariable = "TEXT_GRAB_DISPOSABLE_VM";
    private const string ProfileArgument = "--automation-profile";
    private const string SystemIntegrationArgument = "--automation-system-integration";
    private const string DisposableRegistrationArgument = "--automation-disposable-registration";
    private const string SeedFileName = "seed.json";

    private static readonly Lazy<AutomationProfile?> CurrentProfile = new(
        () => TryCreate(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable));

    private static AutomationProfile? _currentOverride;
    private static bool _hasCurrentOverride;

    private readonly IReadOnlyDictionary<string, JsonElement> _seedValues;

    private AutomationProfile(
        string rootPath,
        bool allowsSystemIntegration,
        bool allowsPersistentRegistration,
        IReadOnlyDictionary<string, JsonElement> seedValues)
    {
        RootPath = rootPath;
        AllowsSystemIntegration = allowsSystemIntegration;
        AllowsPersistentRegistration = allowsPersistentRegistration;
        _seedValues = seedValues;
    }

    internal static AutomationProfile? Current => _hasCurrentOverride ? _currentOverride : CurrentProfile.Value;

    // Test seam: the ambient profile is otherwise derived once from the process command
    // line / environment via a Lazy, which unit tests cannot control. The returned scope
    // restores the previous state on dispose.
    internal static IDisposable OverrideCurrentForTests(AutomationProfile? profile) =>
        new CurrentOverrideScope(profile);

    private sealed class CurrentOverrideScope : IDisposable
    {
        private readonly AutomationProfile? _previousProfile;
        private readonly bool _hadOverride;

        internal CurrentOverrideScope(AutomationProfile? profile)
        {
            _previousProfile = _currentOverride;
            _hadOverride = _hasCurrentOverride;
            _currentOverride = profile;
            _hasCurrentOverride = true;
        }

        public void Dispose()
        {
            _currentOverride = _previousProfile;
            _hasCurrentOverride = _hadOverride;
        }
    }

    internal string RootPath { get; }
    internal bool AllowsSystemIntegration { get; }
    // Unpackaged protocol/file associations mutate HKCU and are intentionally
    // excluded from ordinary real-input automation.
    internal bool AllowsPersistentRegistration { get; }
    internal string SettingsDirectory => Path.Combine(RootPath, "settings");
    internal string ClassicSettingsFilePath => Path.Combine(SettingsDirectory, "classic-settings.json");
    internal string ManagedSettingsDirectory => Path.Combine(RootPath, "settings-data");
    internal string TemplatesFilePath => Path.Combine(RootPath, "GrabTemplates.json");
    internal string TemplateImagesDirectory => Path.Combine(RootPath, "template-images");
    internal string HistoryDirectory => Path.Combine(RootPath, "history");
    internal string DataDirectory => Path.Combine(RootPath, "data");
    internal string OutputDirectory => Path.Combine(RootPath, "output");
    internal string LookupFilePath => Path.Combine(RootPath, "lookup", "QuickSimpleLookup.csv");
    internal string TemporaryDirectory => Path.Combine(RootPath, "temp");
    internal string DiagnosticsDirectory => Path.Combine(RootPath, "diagnostics");
    internal string DiagnosticsLogPath => Path.Combine(DiagnosticsDirectory, "events.jsonl");
    internal string FailureSentinelPath => Path.Combine(DiagnosticsDirectory, "failure.json");

    internal static AutomationProfile? TryCreate(
        IEnumerable<string> arguments,
        Func<string, string?> environmentVariable)
    {
        string? profilePath = environmentVariable(ProfileEnvironmentVariable);
        bool allowsSystemIntegration = IsEnabled(environmentVariable(SystemIntegrationEnvironmentVariable));
        bool requestsPersistentRegistration = IsEnabled(environmentVariable(DisposableRegistrationEnvironmentVariable));
        string[] args = arguments.ToArray();

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument.StartsWith($"{ProfileArgument}=", StringComparison.OrdinalIgnoreCase))
            {
                profilePath = argument[(ProfileArgument.Length + 1)..];
                continue;
            }

            if (string.Equals(argument, ProfileArgument, StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length)
            {
                profilePath = args[++index];
                continue;
            }

            if (string.Equals(argument, SystemIntegrationArgument, StringComparison.OrdinalIgnoreCase))
                allowsSystemIntegration = true;

            if (string.Equals(argument, DisposableRegistrationArgument, StringComparison.OrdinalIgnoreCase))
                requestsPersistentRegistration = true;
        }

        if (string.IsNullOrWhiteSpace(profilePath))
            return null;

        string rootPath;
        try
        {
            rootPath = Path.GetFullPath(profilePath);
        }
        catch (Exception)
        {
            return null;
        }

        bool allowsPersistentRegistration = allowsSystemIntegration
            && requestsPersistentRegistration
            && IsEnabled(environmentVariable(DisposableVmEnvironmentVariable));

        return new AutomationProfile(
            rootPath,
            allowsSystemIntegration,
            allowsPersistentRegistration,
            ReadSeedValues(rootPath));
    }

    internal static bool IsAutomationArgument(string argument) =>
        string.Equals(argument, ProfileArgument, StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith($"{ProfileArgument}=", StringComparison.OrdinalIgnoreCase)
        || string.Equals(argument, SystemIntegrationArgument, StringComparison.OrdinalIgnoreCase)
        || string.Equals(argument, DisposableRegistrationArgument, StringComparison.OrdinalIgnoreCase);

    internal static string GetTemporaryDirectory()
    {
        AutomationProfile? profile = Current;
        if (profile is null)
            return Path.GetTempPath();

        Directory.CreateDirectory(profile.TemporaryDirectory);
        return profile.TemporaryDirectory;
    }

    internal static string GetTemporaryFilePath(string extension = ".tmp")
    {
        string normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        return Path.Combine(GetTemporaryDirectory(), $"{Guid.NewGuid():N}{normalizedExtension}");
    }

    // Widened from Properties.Settings (the app's concrete, internal ApplicationSettingsBase
    // subclass) so this can move to Core, which cannot reference the app assembly. Every
    // property below is written through the SettingsBase indexer instead of a generated typed
    // property. This is behavior-preserving, not just type-erasure: each generated property in
    // Settings.Designer.cs (e.g. `FirstRun`) is a thin wrapper whose setter is exactly
    // `this["FirstRun"] = value;` - the indexer assignment below is the same call the typed
    // property would have made.
    internal void ApplySeed(ApplicationSettingsBase settings)
    {
        settings["FirstRun"] = false;
        settings["RunInTheBackground"] = false;
        settings["StartupOnLogin"] = false;
        settings["GlobalHotkeysEnabled"] = false;
        settings["ShowToast"] = false;
        settings["DefaultLaunch"] = TextGrabMode.EditText.ToString();
        settings["LastUsedLang"] = "en-US";
        settings["UseTesseract"] = false;
        settings["UiAutomationEnabled"] = false;
        settings["WindowsAiDescriptionEnabled"] = false;
        settings["EnableFileBackedManagedSettings"] = true;
        settings["LookupFileLocation"] = LookupFilePath;

        foreach ((string propertyName, JsonElement value) in _seedValues)
        {
            SettingsProperty? property = settings.Properties[propertyName];
            if (property is null || !TryConvert(value, property.PropertyType, out object? convertedValue))
                continue;

            settings[propertyName] = convertedValue;
        }
    }

    private static bool IsEnabled(string? value) =>
        bool.TryParse(value, out bool enabled) && enabled
        || string.Equals(value, "1", StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, JsonElement> ReadSeedValues(string rootPath)
    {
        string seedPath = Path.Combine(rootPath, SeedFileName);
        if (!File.Exists(seedPath))
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(seedPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            JsonElement settingsElement = document.RootElement.TryGetProperty("settings", out JsonElement nestedSettings)
                && nestedSettings.ValueKind == JsonValueKind.Object
                ? nestedSettings
                : document.RootElement;

            return settingsElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        }
        catch (Exception)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }
    }

    private static bool TryConvert(JsonElement value, Type targetType, out object? convertedValue)
    {
        try
        {
            convertedValue = JsonSerializer.Deserialize(value.GetRawText(), targetType);
            return convertedValue is not null;
        }
        catch (JsonException)
        {
            convertedValue = null;
            return false;
        }
    }
}
