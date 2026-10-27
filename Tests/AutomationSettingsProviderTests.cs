using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Text_Grab;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Tests;

// Shares the "Settings isolation" collection so it never runs in parallel with other
// tests that touch Settings.Default: OverrideCurrentForTests flips process-global
// AutomationProfile state, which would otherwise redirect a concurrent Save into this
// test's temporary profile directory.
[Collection("Settings isolation")]
public class AutomationSettingsProviderTests
{
    [Fact]
    public void Save_UnderProfile_WritesClassicSettingsIntoProfileDirectory()
    {
        using TempProfile temp = TempProfile.Create();
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(temp.Profile);

        Settings settings = new();
        settings.DefaultLaunch = "GrabFrame";
        settings.ShowToast = false;
        settings.Save();

        Assert.True(File.Exists(temp.Profile.ClassicSettingsFilePath));

        // The classic store must land inside the profile, never the real user.config.
        Dictionary<string, string> persisted = ReadClassicSettings(temp.Profile.ClassicSettingsFilePath);
        Assert.Equal("GrabFrame", persisted[nameof(Settings.DefaultLaunch)]);
        Assert.Equal("False", persisted[nameof(Settings.ShowToast)]);

        Settings reloaded = new();
        Assert.Equal("GrabFrame", reloaded.DefaultLaunch);
        Assert.False(reloaded.ShowToast);
    }

    [Fact]
    public void Reads_AreScopedToTheActiveProfile()
    {
        using TempProfile first = TempProfile.Create();
        using TempProfile second = TempProfile.Create();

        using (AutomationProfile.OverrideCurrentForTests(first.Profile))
        {
            Settings settings = new();
            settings.DefaultLaunch = "GrabFrame";
            settings.Save();
        }

        // A different profile must not see the first profile's saved value.
        using (AutomationProfile.OverrideCurrentForTests(second.Profile))
        {
            Settings settings = new();
            Assert.NotEqual("GrabFrame", settings.DefaultLaunch);
            Assert.False(File.Exists(second.Profile.ClassicSettingsFilePath));
        }
    }

    [Fact]
    public void SettingsService_UnderProfile_SeedsClassicSettingsFileOnce()
    {
        using TempProfile temp = TempProfile.Create();
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(temp.Profile);

        Assert.False(File.Exists(temp.Profile.ClassicSettingsFilePath));

        Settings first = new();
        using (new SettingsService(first, localSettings: null))
        {
            // The seed is applied and persisted into the isolated profile file.
            Assert.True(File.Exists(temp.Profile.ClassicSettingsFilePath));
            Assert.False(first.FirstRun);
            Assert.Equal(TextGrabMode.EditText.ToString(), first.DefaultLaunch);

            // Mutate through the live service so every backing store (classic file and
            // sidecar) stays consistent before the next run reads them back.
            first.DefaultLaunch = "GrabFrame";
            first.Save();
        }

        // A second run finds the profile file already present and must not reseed:
        // the value changed after seeding survives instead of being reset to the seed.
        Settings second = new();
        using (new SettingsService(second, localSettings: null))
            Assert.Equal("GrabFrame", second.DefaultLaunch);
    }

    [Fact]
    public void ExternalLinkWarnings_DefaultToEnabled()
    {
        using TempProfile temp = TempProfile.Create();
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(temp.Profile);
        Settings settings = new();

        Assert.True(settings.WarnBeforeOpeningEmailLinks);
        Assert.True(settings.WarnBeforeOpeningTelephoneLinks);
        Assert.Equal("True", settings.Properties[nameof(Settings.WarnBeforeOpeningEmailLinks)].DefaultValue);
        Assert.Equal("True", settings.Properties[nameof(Settings.WarnBeforeOpeningTelephoneLinks)].DefaultValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExternalLinkWarning_OptOutPersistsForOnlyTheApprovedScheme(bool isEmail)
    {
        using TempProfile temp = TempProfile.Create();
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(temp.Profile);
        Settings settings = new();
        Uri email = new("mailto:user@example.invalid");
        Uri phone = new("tel:+15551234567");
        Uri target = isEmail ? email : phone;

        Assert.True(await ExternalLinkLauncher.ConfirmAsync(target, settings, _ => Task.FromResult((true, true))));

        Settings reloaded = new();
        Assert.Equal(!isEmail, reloaded.WarnBeforeOpeningEmailLinks);
        Assert.Equal(isEmail, reloaded.WarnBeforeOpeningTelephoneLinks);
        Dictionary<string, string> persisted = ReadClassicSettings(temp.Profile.ClassicSettingsFilePath);
        Assert.Equal((!isEmail).ToString(), persisted[nameof(Settings.WarnBeforeOpeningEmailLinks)]);
        Assert.Equal(isEmail.ToString(), persisted[nameof(Settings.WarnBeforeOpeningTelephoneLinks)]);
        Assert.True(await ExternalLinkLauncher.ConfirmAsync(
            target,
            reloaded,
            _ => throw new InvalidOperationException("The opted-out scheme must not prompt again.")));

        int prompts = 0;
        Assert.False(await ExternalLinkLauncher.ConfirmAsync(
            isEmail ? phone : email,
            reloaded,
            _ =>
            {
                prompts++;
                return Task.FromResult((false, false));
            }));
        Assert.Equal(1, prompts);
    }

    [Theory]
    [InlineData("mailto:user@example.invalid", false, false)]
    [InlineData("mailto:user@example.invalid", false, true)]
    [InlineData("mailto:user@example.invalid", true, false)]
    [InlineData("tel:+15551234567", false, false)]
    [InlineData("tel:+15551234567", false, true)]
    [InlineData("tel:+15551234567", true, false)]
    public async Task ExternalLinkWarning_WithoutApprovedOptOutDoesNotSavePreferences(
        string target, bool approved, bool dontWarnAgain)
    {
        using TempProfile temp = TempProfile.Create();
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(temp.Profile);
        Settings settings = new();

        bool result = await ExternalLinkLauncher.ConfirmAsync(
            new Uri(target),
            settings,
            _ => Task.FromResult((approved, dontWarnAgain)));

        Assert.Equal(approved, result);
        Assert.True(settings.WarnBeforeOpeningEmailLinks);
        Assert.True(settings.WarnBeforeOpeningTelephoneLinks);
        Assert.False(File.Exists(temp.Profile.ClassicSettingsFilePath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExternalLinkWarning_ReenablingPreferenceRestoresConfirmation(bool isEmail)
    {
        using TempProfile temp = TempProfile.Create();
        using IDisposable scope = AutomationProfile.OverrideCurrentForTests(temp.Profile);
        Settings settings = new()
        {
            WarnBeforeOpeningEmailLinks = false,
            WarnBeforeOpeningTelephoneLinks = false
        };
        if (isEmail)
            settings.WarnBeforeOpeningEmailLinks = true;
        else
            settings.WarnBeforeOpeningTelephoneLinks = true;
        settings.Save();

        Settings reloaded = new();
        int prompts = 0;
        bool approved = await ExternalLinkLauncher.ConfirmAsync(
            new Uri(isEmail ? "mailto:user@example.invalid" : "tel:+15551234567"),
            reloaded,
            _ =>
            {
                prompts++;
                return Task.FromResult((false, false));
            });

        Assert.False(approved);
        Assert.Equal(1, prompts);
        Assert.Equal(isEmail, reloaded.WarnBeforeOpeningEmailLinks);
        Assert.Equal(!isEmail, reloaded.WarnBeforeOpeningTelephoneLinks);
    }

    private static Dictionary<string, string> ReadClassicSettings(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
        ?? throw new InvalidOperationException("Classic settings file was empty.");

    private sealed class TempProfile : IDisposable
    {
        private TempProfile(string rootPath, AutomationProfile profile)
        {
            RootPath = rootPath;
            Profile = profile;
        }

        internal string RootPath { get; }
        internal AutomationProfile Profile { get; }

        internal static TempProfile Create()
        {
            string root = Path.Combine(Path.GetTempPath(), $"tg-ui-tests-{Guid.NewGuid():N}");
            AutomationProfile profile = AutomationProfile.TryCreate(
                ["Text-Grab.exe"],
                name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
                ?? throw new InvalidOperationException("Failed to create automation profile for test.");

            return new TempProfile(root, profile);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                    Directory.Delete(RootPath, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup; a leaked temp directory should not fail the test.
            }
        }
    }
}
