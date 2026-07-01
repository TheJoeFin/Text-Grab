using System.IO;
using System.Text.Json;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;

namespace Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly string _regularSettingsFilePath;

    public SettingsServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), $"TextGrab_SettingsService_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempFolder);
        _regularSettingsFilePath = Path.Combine(_tempFolder, "Settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, true);
    }

    [Fact]
    public void LoadStoredRegexes_DefaultModePrefersLegacyAndKeepsLegacyPopulated()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = false,
            RegexList = SerializeRegexes("legacy-regex")
        };
        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        File.WriteAllText(regexFilePath, SerializeRegexes("sidecar-regex"));

        SettingsService service = CreateService(settings);

        StoredRegex loadedRegex = Assert.Single(service.LoadStoredRegexes());

        Assert.Equal("legacy-regex", loadedRegex.Id);
        Assert.Contains("legacy-regex", settings.RegexList);
        Assert.Contains("legacy-regex", File.ReadAllText(regexFilePath));
    }

    [Fact]
    public void LoadStoredRegexes_DefaultModeBackfillsLegacyFromSidecarWhenNeeded()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = false,
            RegexList = string.Empty
        };
        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        File.WriteAllText(regexFilePath, SerializeRegexes("recovered-regex"));

        SettingsService service = CreateService(settings);

        StoredRegex loadedRegex = Assert.Single(service.LoadStoredRegexes());

        Assert.Equal("recovered-regex", loadedRegex.Id);
        Assert.Contains("recovered-regex", settings.RegexList);
        Assert.Equal(File.ReadAllText(regexFilePath), settings.RegexList);
    }

    [Fact]
    public void LoadStoredRegexes_FileBackedModePrefersSidecarAndBackfillsLegacy()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = true,
            RegexList = SerializeRegexes("legacy-regex")
        };
        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        File.WriteAllText(regexFilePath, SerializeRegexes("sidecar-regex"));

        SettingsService service = CreateService(settings);

        StoredRegex loadedRegex = Assert.Single(service.LoadStoredRegexes());

        Assert.Equal("sidecar-regex", loadedRegex.Id);
        Assert.Contains("sidecar-regex", settings.RegexList);
        Assert.Contains("sidecar-regex", File.ReadAllText(regexFilePath));
    }

    [Fact]
    public void SavePostGrabCheckStates_FileBackedModeWritesBothStores()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = true
        };
        SettingsService service = CreateService(settings);

        service.SavePostGrabCheckStates(new Dictionary<string, bool>
        {
            ["Fix GUIDs"] = true
        });

        string filePath = Path.Combine(_tempFolder, "PostGrabCheckStates.json");
        Assert.Contains("Fix GUIDs", settings.PostGrabCheckStates);
        Assert.True(File.Exists(filePath));
        Assert.Contains("Fix GUIDs", File.ReadAllText(filePath));
        Assert.True(service.LoadPostGrabCheckStates()["Fix GUIDs"]);
    }

    [Fact]
    public void ClearingManagedSettingClearsLegacyAndSidecar()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = false
        };
        SettingsService service = CreateService(settings);

        service.SaveStoredRegexes(
        [
            new StoredRegex
            {
                Id = "clear-me",
                Name = "Clear Me",
                Pattern = ".*"
            }
        ]);

        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        Assert.NotEmpty(settings.RegexList);
        Assert.True(File.Exists(regexFilePath));

        settings.RegexList = string.Empty;

        Assert.Equal(string.Empty, settings.RegexList);
        Assert.False(File.Exists(regexFilePath));
        Assert.Empty(service.LoadStoredRegexes());
    }

    [Fact]
    public void Constructor_FileBackedModeReflectsSettingsValueSetBeforeConstruction()
    {
        // EnableFileBackedManagedSettings must be read AFTER any migration so the
        // persisted user preference is honoured for the current session.
        Settings settings = new()
        {
            FirstRun = false,
            EnableFileBackedManagedSettings = true
        };

        SettingsService service = CreateService(settings);

        Assert.True(service.IsFileBackedManagedSettingsEnabled);
    }

    [Fact]
    public void Constructor_FileBackedModeDefaultsToFalseWhenNotSet()
    {
        Settings settings = new()
        {
            FirstRun = false,
            EnableFileBackedManagedSettings = false
        };

        SettingsService service = CreateService(settings);

        Assert.False(service.IsFileBackedManagedSettingsEnabled);
    }

    [Fact]
    public void Constructor_UnpackagedUpgradePathDoesNotThrowWhenNoPreviousVersion()
    {
        // When saveClassicSettingsChanges is false (test mode) the Upgrade() code path is
        // skipped, so this simply verifies that the constructor completes successfully when
        // FirstRun is true and localSettings is null.
        Settings settings = new()
        {
            FirstRun = true,
            EnableFileBackedManagedSettings = false
        };

        SettingsService service = CreateService(settings);

        // Service initialises without throwing; FileBackedMode reflects the setting.
        Assert.False(service.IsFileBackedManagedSettingsEnabled);
    }

    [Fact]
    public void Constructor_RegularSettingsSidecarWithFileBackedFlagImportsPortableSettings()
    {
        Settings settings = new()
        {
            FirstRun = false,
            EnableFileBackedManagedSettings = false,
            ShowToast = true,
            DefaultLaunch = "Fullscreen"
        };

        File.WriteAllText(
            _regularSettingsFilePath,
            """
            {
              "EnableFileBackedManagedSettings": true,
              "ShowToast": false,
              "DefaultLaunch": "EditText"
            }
            """);

        SettingsService service = CreateService(settings);

        Assert.True(service.IsFileBackedManagedSettingsEnabled);
        Assert.True(settings.EnableFileBackedManagedSettings);
        Assert.False(settings.ShowToast);
        Assert.Equal("EditText", settings.DefaultLaunch);
    }

    [Fact]
    public void Constructor_FileBackedModeWithoutRegularSettingsSidecarCreatesOneFromClassicSettings()
    {
        Settings settings = new()
        {
            FirstRun = false,
            EnableFileBackedManagedSettings = true,
            ShowToast = false,
            DefaultLaunch = "QuickLookup",
            GrabTemplatesJSON = """[{ "id": "template-1" }]"""
        };

        SettingsService service = CreateService(settings);

        Assert.True(service.IsFileBackedManagedSettingsEnabled);
        Assert.True(File.Exists(_regularSettingsFilePath));

        string persistedJson = File.ReadAllText(_regularSettingsFilePath);
        Assert.Contains(@"""EnableFileBackedManagedSettings"": true", persistedJson);
        Assert.Contains(@"""ShowToast"": false", persistedJson);
        Assert.Contains(@"""DefaultLaunch"": ""QuickLookup""", persistedJson);
        Assert.DoesNotContain("GrabTemplatesJSON", persistedJson);
    }

    [Fact]
    public void Constructor_RegularSettingsSidecarOnlyOverridesKnownValuesAndBackfillsMissingOnes()
    {
        Settings settings = new()
        {
            FirstRun = false,
            EnableFileBackedManagedSettings = true,
            ShowToast = true,
            DefaultLaunch = "QuickLookup"
        };

        File.WriteAllText(
            _regularSettingsFilePath,
            """
            {
              "EnableFileBackedManagedSettings": true,
              "ShowToast": false
            }
            """);

        SettingsService service = CreateService(settings);

        Assert.True(service.IsFileBackedManagedSettingsEnabled);
        Assert.False(settings.ShowToast);
        Assert.Equal("QuickLookup", settings.DefaultLaunch);

        string persistedJson = File.ReadAllText(_regularSettingsFilePath);
        Assert.Contains(@"""ShowToast"": false", persistedJson);
        Assert.Contains(@"""DefaultLaunch"": ""QuickLookup""", persistedJson);
    }

    [Fact]
    public void RegularSettingChange_PersistsToRegularSettingsSidecarWhenFileBackedModeEnabled()
    {
        Settings settings = new()
        {
            FirstRun = false,
            EnableFileBackedManagedSettings = true,
            ShowToast = true
        };

        SettingsService service = CreateService(settings);

        settings.ShowToast = false;

        string persistedJson = File.ReadAllText(_regularSettingsFilePath);
        Assert.Contains(@"""ShowToast"": false", persistedJson);
    }

    [Fact]
    public void LoadStoredRegexes_SidecarSurvivesSimulatedPackageUpgrade()
    {
        // Simulate a package upgrade: sidecar file already exists (from the previous
        // version) but ClassicSettings.RegexList is empty (reset by the upgrade).
        // The service must load from the sidecar and backfill ClassicSettings.
        Settings settings = new()
        {
            FirstRun = false,
            EnableFileBackedManagedSettings = false,
            RegexList = string.Empty
        };
        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        File.WriteAllText(regexFilePath, SerializeRegexes("survived-upgrade"));

        SettingsService service = CreateService(settings);

        StoredRegex loaded = Assert.Single(service.LoadStoredRegexes());
        Assert.Equal("survived-upgrade", loaded.Id);
        // Verify backfill into ClassicSettings so the next migration has something to copy.
        Assert.Contains("survived-upgrade", settings.RegexList);
    }

    private SettingsService CreateService(Settings settings) =>
        new(
            settings,
            localSettings: null,
            managedJsonSettingsFolderPath: _tempFolder,
            regularSettingsSidecarFilePath: _regularSettingsFilePath,
            saveClassicSettingsChanges: false);

    private static string SerializeRegexes(string id) =>
        JsonSerializer.Serialize(new[]
        {
            new StoredRegex
            {
                Id = id,
                Name = $"{id} name",
                Pattern = @"INV-\d+",
                Description = "transition test pattern"
            }
        });
}
