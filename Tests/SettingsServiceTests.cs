using System.IO;
using System.Text.Json;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;

namespace Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempFolder;

    public SettingsServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), $"TextGrab_SettingsService_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, true);
    }

    [Fact]
    public void LoadStoredRegexes_MigratesAndCachesRegexSetting()
    {
        Settings settings = new();
        settings.RegexList = JsonSerializer.Serialize(new[]
        {
            new StoredRegex
            {
                Id = "regex-1",
                Name = "Invoice Number",
                Pattern = @"INV-\d+",
                Description = "test pattern"
            }
        });

        SettingsService service = new(
            settings,
            localSettings: null,
            managedJsonSettingsFolderPath: _tempFolder,
            saveClassicSettingsChanges: false);

        Assert.Equal(string.Empty, settings.RegexList);

        StoredRegex[] firstRead = service.LoadStoredRegexes();
        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");

        Assert.True(File.Exists(regexFilePath));

        File.WriteAllText(
            regexFilePath,
            JsonSerializer.Serialize(new[]
            {
                new StoredRegex
                {
                    Id = "regex-2",
                    Name = "Changed",
                    Pattern = "changed"
                }
            }));

        StoredRegex[] secondRead = service.LoadStoredRegexes();

        StoredRegex initialPattern = Assert.Single(firstRead);
        StoredRegex cachedPattern = Assert.Single(secondRead);
        Assert.Equal("regex-1", initialPattern.Id);
        Assert.Equal("regex-1", cachedPattern.Id);
    }

    [Fact]
    public void SavePostGrabCheckStates_WritesFileAndLeavesClassicSettingEmpty()
    {
        Settings settings = new();
        SettingsService service = new(
            settings,
            localSettings: null,
            managedJsonSettingsFolderPath: _tempFolder,
            saveClassicSettingsChanges: false);

        service.SavePostGrabCheckStates(new Dictionary<string, bool>
        {
            ["Fix GUIDs"] = true
        });

        Assert.Equal(string.Empty, settings.PostGrabCheckStates);
        Assert.True(File.Exists(Path.Combine(_tempFolder, "PostGrabCheckStates.json")));
        Assert.True(service.LoadPostGrabCheckStates()["Fix GUIDs"]);
        Assert.Contains(
            "Fix GUIDs",
            service.GetManagedJsonSettingValueForExport(nameof(Settings.PostGrabCheckStates)));
    }
}
