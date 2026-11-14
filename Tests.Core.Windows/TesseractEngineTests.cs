using System.IO;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

public class TesseractEngineTests
{
    [Fact]
    public void EnsureDecided_SavedPathThatExists_IsExternal()
    {
        FakeTextGrabSettings settings = new() { TesseractPath = @"C:\tess\tesseract.exe" };

        TesseractEngineMode mode = TesseractEngineModeResolver.EnsureDecided(settings, _ => true);

        Assert.Equal(TesseractEngineMode.External, mode);
        Assert.Equal("External", settings.TesseractEngineMode);
    }

    [Fact]
    public void EnsureDecided_EmptyPath_IsBundled()
    {
        FakeTextGrabSettings settings = new();

        Assert.Equal(TesseractEngineMode.Bundled, TesseractEngineModeResolver.EnsureDecided(settings, _ => true));
        Assert.Equal("Bundled", settings.TesseractEngineMode);
    }

    [Fact]
    public void EnsureDecided_SavedPathWhoseFileIsGone_IsBundled()
    {
        FakeTextGrabSettings settings = new() { TesseractPath = @"C:\gone\tesseract.exe" };

        Assert.Equal(TesseractEngineMode.Bundled, TesseractEngineModeResolver.EnsureDecided(settings, _ => false));
    }

    [Fact]
    public void EnsureDecided_IsNotReevaluatedOnSecondLaunch()
    {
        FakeTextGrabSettings settings = new();
        TesseractEngineModeResolver.EnsureDecided(settings, _ => false);

        // The user later installs Tesseract and a path gets saved: still Bundled.
        settings.TesseractPath = @"C:\tess\tesseract.exe";

        Assert.Equal(TesseractEngineMode.Bundled, TesseractEngineModeResolver.EnsureDecided(settings, _ => true));
    }

    [Fact]
    public void EnsureDecided_NeverRunsTheInstallProbe()
    {
        // Tesseract installed in a known folder but no saved path: the probe would write TesseractPath.
        FakeTextGrabSettings settings = new();

        TesseractEngineModeResolver.EnsureDecided(settings);

        Assert.Equal(string.Empty, settings.TesseractPath);
        Assert.Equal("Bundled", settings.TesseractEngineMode);
    }

    [Fact]
    public void CanSwitchToExternal_RequiresExistingSavedPath()
    {
        Assert.False(TesseractEngineModeResolver.CanSwitchToExternal(new FakeTextGrabSettings(), _ => true));
        Assert.False(TesseractEngineModeResolver.CanSwitchToExternal(new FakeTextGrabSettings { TesseractPath = "x" }, _ => false));
        Assert.True(TesseractEngineModeResolver.CanSwitchToExternal(new FakeTextGrabSettings { TesseractPath = "x" }, _ => true));
    }

    [Fact]
    public async Task BundledEngine_IsReadyAndReadsFontSample()
    {
        // FakeTextGrabSettings has no saved path, so the resolver picks Bundled.
        Assert.Equal(TesseractEngineMode.Bundled, TesseractHelper.Mode);

        BundledTesseractStatus status = await TesseractHelper.GetBundledStatusAsync();
        Assert.True(status.State == BundledTesseractState.Ready, status.ErrorDetails);
        Assert.StartsWith("5.", status.Version);

        string text = await TesseractHelper.GetTextFromImagePathAsync(
            Path.Combine(AppContext.BaseDirectory, "Images", "font_sample.png"), "eng");

        Assert.Contains("Times-Roman", text);
        Assert.Contains("Helvetica", text);
        Assert.Contains("Courier", text);
    }

    [Fact]
    public void UseTesseract_DefaultsToOff() => Assert.False(new FakeTextGrabSettings().UseTesseract);

    [Fact]
    public void ResolveWritableFolder_CoversPackagedUnpackagedAndAutomation()
    {
        Assert.Equal(@"C:\pkg\tessdata", TessdataLocator.ResolveWritableFolder(null, true, @"C:\pkg", @"C:\exe"));
        Assert.Equal(@"C:\exe\tessdata", TessdataLocator.ResolveWritableFolder(null, false, null, @"C:\exe"));
        Assert.Equal(@"C:\profile\tessdata", TessdataLocator.ResolveWritableFolder(@"C:\profile", true, @"C:\pkg", @"C:\exe"));
    }

    [Fact]
    public void Merge_DownloadedModelOverridesBundledOfSameTag()
    {
        TessdataLanguageFile bundledEng = new("eng", @"b\eng.traineddata", TessdataSource.Bundled, 4);
        TessdataLanguageFile bundledDeu = new("deu", @"b\deu.traineddata", TessdataSource.Bundled, 4);
        TessdataLanguageFile downloadedEng = new("eng", @"w\eng.traineddata", TessdataSource.Downloaded, 15);

        IReadOnlyList<TessdataLanguageFile> merged = TessdataLocator.Merge([bundledEng, bundledDeu], [downloadedEng]);

        Assert.Equal(["deu", "eng"], merged.Select(f => f.Tag));
        Assert.Equal(TessdataSource.Downloaded, merged.Single(f => f.Tag == "eng").Source);
    }

    [Fact]
    public void Enumerate_SkipsOsdAndNonModelFiles()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"TextGrab_Tessdata_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "eng.traineddata"), [1]);
            File.WriteAllBytes(Path.Combine(folder, "osd.traineddata"), [1]);
            File.WriteAllText(Path.Combine(folder, "readme.txt"), "x");

            List<TessdataLanguageFile> files = [.. TessdataLocator.Enumerate(folder, TessdataSource.Downloaded)];

            Assert.Equal("eng", Assert.Single(files).Tag);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ResolveFolderForSpec_UnknownLanguage_ReturnsNull()
    {
        TessdataLanguageFile eng = new("eng", @"b\eng.traineddata", TessdataSource.Bundled, 4);

        Assert.Null(TessdataLocator.ResolveFolderForSpec("deu", [eng]));
        Assert.Null(TessdataLocator.ResolveFolderForSpec("eng+deu", [eng]));
    }

    [Fact]
    public void ResolveFolderForSpec_AllBundled_UsesBundledFolder()
    {
        TessdataLanguageFile eng = new("eng", @"C:\app\tessdata-bundled\eng.traineddata", TessdataSource.Bundled, 4);

        Assert.Equal(@"C:\app\tessdata-bundled", TessdataLocator.ResolveFolderForSpec("eng", [eng]));
    }
}
