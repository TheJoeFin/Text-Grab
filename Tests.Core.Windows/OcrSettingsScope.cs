using Text_Grab.Services;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// Tests that vary OCR settings swap the process-global <see cref="SettingsAccess"/> resolver, so
/// they all live in this collection, which never runs in parallel with any other test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class OcrSettingsCollectionDefinition
{
    public const string Name = "OcrSettings";
}

/// <summary>
/// Installs one <see cref="FakeTextGrabSettings"/> instance as the active settings for the life of
/// the scope, then restores the default resolver (fresh shipped defaults per read).
/// </summary>
internal sealed class OcrSettingsScope : IDisposable
{
    public OcrSettingsScope(Action<FakeTextGrabSettings> configure)
    {
        FakeTextGrabSettings settings = new();
        configure(settings);
        SettingsAccess.SetResolver(() => settings);
    }

    public void Dispose() => SettingsAccess.SetResolver(() => new FakeTextGrabSettings());
}
