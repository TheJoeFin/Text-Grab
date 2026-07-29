using System.IO;
using Text_Grab;
using Text_Grab.Utilities;

namespace Tests;

public class AutomationProfileTests
{
    [Fact]
    public void TryCreate_UsesEnvironmentProfileAndKeepsIntegrationDisabled()
    {
        const string profilePath = @"C:\UiRuns\environment-profile";

        AutomationProfile? profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? profilePath : null);

        Assert.NotNull(profile);
        Assert.Equal(profilePath, profile.RootPath);
        Assert.False(profile.AllowsSystemIntegration);
        Assert.Equal(Path.Combine(profilePath, "history"), profile.HistoryDirectory);
        Assert.Equal(Path.Combine(profilePath, "settings", "classic-settings.json"), profile.ClassicSettingsFilePath);
    }

    [Fact]
    public void TryCreate_CommandLineProfileAndIntegrationOverrideEnvironment()
    {
        const string environmentProfile = @"C:\UiRuns\environment-profile";
        const string commandLineProfile = @"C:\UiRuns\command-line-profile";

        AutomationProfile? profile = AutomationProfile.TryCreate(
        [
            "Text-Grab.exe",
            "--automation-profile",
            commandLineProfile,
            "--automation-system-integration"
        ],
        name => name == AutomationProfile.ProfileEnvironmentVariable ? environmentProfile : null);

        Assert.NotNull(profile);
        Assert.Equal(commandLineProfile, profile.RootPath);
        Assert.True(profile.AllowsSystemIntegration);
        Assert.False(profile.AllowsPersistentRegistration);
        Assert.Equal(Path.Combine(commandLineProfile, "temp"), profile.TemporaryDirectory);
    }

    [Fact]
    public void TryCreate_PersistentRegistrationRequiresSystemAndDisposableOptIn()
    {
        AutomationProfile? ordinarySystemProfile = AutomationProfile.TryCreate(
            ["Text-Grab.exe", "--automation-profile", @"C:\UiRuns\system", "--automation-system-integration"],
            _ => null);
        AutomationProfile? disposableProfile = AutomationProfile.TryCreate(
            [
                "Text-Grab.exe",
                "--automation-profile", @"C:\UiRuns\disposable",
                "--automation-system-integration",
                "--automation-disposable-registration"
            ],
            name => name == AutomationProfile.DisposableVmEnvironmentVariable ? "1" : null);
        AutomationProfile? incompleteProfile = AutomationProfile.TryCreate(
            ["Text-Grab.exe", "--automation-profile", @"C:\UiRuns\incomplete", "--automation-disposable-registration"],
            _ => null);
        AutomationProfile? nonDisposableProfile = AutomationProfile.TryCreate(
            [
                "Text-Grab.exe",
                "--automation-profile", @"C:\UiRuns\non-disposable",
                "--automation-system-integration",
                "--automation-disposable-registration"
            ],
            _ => null);

        Assert.NotNull(ordinarySystemProfile);
        Assert.NotNull(disposableProfile);
        Assert.NotNull(incompleteProfile);
        Assert.NotNull(nonDisposableProfile);
        Assert.False(ordinarySystemProfile.AllowsPersistentRegistration);
        Assert.True(disposableProfile.AllowsPersistentRegistration);
        Assert.False(incompleteProfile.AllowsPersistentRegistration);
        Assert.False(nonDisposableProfile.AllowsPersistentRegistration);
    }

    [Fact]
    public void TryCreate_ReturnsNullWithoutProfile()
    {
        AutomationProfile? profile = AutomationProfile.TryCreate(["Text-Grab.exe"], _ => null);

        Assert.Null(profile);
    }

    [Fact]
    public void ParseStartupArguments_IgnoresAutomationProfileArguments()
    {
        App.StartupArguments startupArguments = App.ParseStartupArguments(
        [
            "--automation-profile",
            @"C:\UiRuns\run-1",
            "--automation-system-integration",
            "--automation-disposable-registration",
            "Settings"
        ]);

        Assert.Equal("Settings", startupArguments.PrimaryArgument);
    }
}
