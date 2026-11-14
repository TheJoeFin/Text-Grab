using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace Text_Grab.Utilities;

public enum BundledTesseractState
{
    Ready,
    LibraryFailedToLoad,
    NoLanguages,
}

public sealed record BundledTesseractStatus(BundledTesseractState State, string Version, string? ErrorDetails);

/// <summary>
/// In-process Tesseract (Tesseract.CrossPlatform). One engine is cached per folder + language spec.
/// A TesseractEngine is not thread-safe, so every use goes through one gate.
/// </summary>
internal static class BundledTesseractBackend
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static TesseractEngine? _engine;
    private static string? _engineKey;
    private static bool _nativeSearchPathConfigured;
    private static bool _exitHooked;

    /// <summary>
    /// The wrapper probes next to the assembly / app base dir. In an unpackaged single-file build the
    /// natives are embedded and extracted to a temp folder, which the wrapper does not look in.
    /// Harmless in the packaged and non-single-file cases (the extraction dir is simply not found).
    /// </summary>
    internal static void EnsureNativeLibrariesResolvable()
    {
        if (_nativeSearchPathConfigured)
            return;

        _nativeSearchPathConfigured = true;

        string dirs = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string ?? string.Empty;
        foreach (string dir in dirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(Path.Combine(dir, "tesseract.dll")))
            {
                TesseractEnviornment.CustomSearchPath = dir; // sic: the property is misspelled in the package
                break;
            }
        }
    }

    public static Task<string> RecognizeAsync(Bitmap bitmap, string languageSpec)
    {
        using MemoryStream stream = new();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        byte[] pngBytes = stream.ToArray();

        return RunAsync(languageSpec, engine =>
        {
            using Pix pix = Pix.LoadFromMemory(pngBytes);
            using Page page = engine.Process(pix);
            return page.GetText();
        });
    }

    public static Task<string> RecognizeFileAsync(string imagePath, string languageSpec) =>
        RunAsync(languageSpec, engine =>
        {
            using Pix pix = Pix.LoadFromFile(imagePath);
            using Page page = engine.Process(pix);
            return page.GetText();
        });

    private static async Task<string> RunAsync(string languageSpec, Func<TesseractEngine, string> work)
    {
        string spec = string.IsNullOrWhiteSpace(languageSpec) ? "eng" : languageSpec;
        IReadOnlyList<TessdataLanguageFile> installed = TessdataLocator.GetInstalledLanguages();
        string? folder = TessdataLocator.ResolveFolderForSpec(spec, installed);
        if (folder is null)
            return $"Missing Tesseract language data for '{spec}'. Download it in Settings > Tesseract.";

        await Gate.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    return work(GetOrCreateEngine(folder, spec));
                }
                catch (Exception ex) when (ex is TesseractException or DllNotFoundException or BadImageFormatException or InvalidOperationException)
                {
                    DisposeEngine();
                    return $"Tesseract failed: {ex.Message}";
                }
            });
        }
        finally
        {
            Gate.Release();
        }
    }

    private static TesseractEngine GetOrCreateEngine(string folder, string spec)
    {
        string key = $"{folder}|{spec}";
        if (_engine is not null && _engineKey == key)
            return _engine;

        DisposeEngine();
        EnsureNativeLibrariesResolvable();

        if (!_exitHooked)
        {
            _exitHooked = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeEngine();
        }

        _engine = new TesseractEngine(folder, spec, EngineMode.Default);
        _engineKey = key;
        return _engine;
    }

    private static void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
        _engineKey = null;
    }

    /// <summary>Drop the cached engine, e.g. after a model was downloaded or removed.</summary>
    public static async Task InvalidateAsync()
    {
        await Gate.WaitAsync();
        try { DisposeEngine(); }
        finally { Gate.Release(); }
    }

    /// <summary>Creates an engine for the first available language to report whether the natives load.</summary>
    public static async Task<BundledTesseractStatus> GetStatusAsync()
    {
        TessdataLanguageFile? first = TessdataLocator.GetInstalledLanguages().FirstOrDefault();
        if (first is null)
            return new BundledTesseractStatus(BundledTesseractState.NoLanguages, string.Empty, null);

        string? folder = TessdataLocator.ResolveFolderForSpec(first.Tag, [first]);
        if (folder is null)
            return new BundledTesseractStatus(BundledTesseractState.NoLanguages, string.Empty, null);

        await Gate.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    TesseractEngine engine = GetOrCreateEngine(folder, first.Tag);
                    return new BundledTesseractStatus(BundledTesseractState.Ready, engine.Version, null);
                }
                catch (Exception ex)
                {
                    DisposeEngine();
                    return new BundledTesseractStatus(BundledTesseractState.LibraryFailedToLoad, string.Empty, ex.ToString());
                }
            });
        }
        finally
        {
            Gate.Release();
        }
    }
}
