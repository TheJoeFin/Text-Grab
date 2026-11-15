using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

// Install Tesseract for Windows from UB-Mannheim
// https://github.com/UB-Mannheim/tesseract/wiki
//
// Docs about command line usage
// https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html
//
// LEGACY: this is the original tesseract.exe + CliWrap path, kept for users who had already set it
// up before Tesseract shipped inside Text Grab. It is frozen: bug fixes only, no new features.

internal static class ExternalTesseractBackend
{
    private const string rawPath = @"%LOCALAPPDATA%\Tesseract-OCR\tesseract.exe";
    private const string rawProgramsPath = @"%LOCALAPPDATA%\Programs\Tesseract-OCR\tesseract.exe";
    private const string basicPath = @"C:\Program Files\Tesseract-OCR\tesseract.exe";

    public static bool CanLocateTesseractExe()
    {
        string tesseractPath = string.Empty;
        try
        {
            tesseractPath = GetTesseractPath();
        }
        catch (Exception)
        {
            tesseractPath = string.Empty;
#if DEBUG
            throw;
#endif
        }
        return !string.IsNullOrEmpty(tesseractPath);
    }

    /// <summary>The probe writes what it finds into TesseractPath; never call it in Bundled mode.</summary>
    private static string GetTesseractPath()
    {
        ITextGrabSettings defaultSettings = SettingsAccess.Current;

        if (!string.IsNullOrWhiteSpace(defaultSettings.TesseractPath)
            && File.Exists(defaultSettings.TesseractPath))
            return defaultSettings.TesseractPath;

        string tesExePath = Environment.ExpandEnvironmentVariables(rawPath);
        string programsPath = Environment.ExpandEnvironmentVariables(rawProgramsPath);

        if (File.Exists(tesExePath))
        {
            defaultSettings.TesseractPath = tesExePath;
            defaultSettings.Save();
            return tesExePath;
        }

        if (File.Exists(programsPath))
        {
            defaultSettings.TesseractPath = programsPath;
            defaultSettings.Save();
            return programsPath;
        }

        if (File.Exists(basicPath))
        {
            defaultSettings.TesseractPath = basicPath;
            defaultSettings.Save();
            return basicPath;
        }

        return string.Empty;
    }

    public static async Task<string> GetTextFromImagePathAsync(string imagePath, string tessTag)
    {
        string tesseractPath = GetTesseractPath();

        if (string.IsNullOrWhiteSpace(tesseractPath))
            return "Cannot find tesseract.exe";

        // probably not needed, but if the Windows languages get passed it, it should still work
        string languageString = tessTag;

        BufferedCommandResult result = await Cli.Wrap(tesseractPath)
            .WithValidation(CommandResultValidation.None)
            .WithArguments(args => args
                .Add(imagePath)
                .Add("-")
                .Add("-l")
                .Add(languageString)
            )
            .ExecuteBufferedAsync(Encoding.UTF8);

        return result.StandardOutput;
    }

    public static async Task<string> GetTextFromImagePath(string pathToFile, bool outputHocr)
    {
        string tesExePath = GetTesseractPath();

        if (string.IsNullOrEmpty(tesExePath))
            return "Cannot find tesseract.exe";

        string argumentsString = $"\"{pathToFile}\" - -l eng";

        if (outputHocr)
            argumentsString += " hocr";

        ProcessStartInfo psi = new()
        {
            FileName = tesExePath,
            Arguments = argumentsString,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        Process? process = Process.Start(psi);

        if (process is null)
            return string.Empty;

        StreamReader sr = process.StandardOutput;
        StreamReader errorReader = process.StandardError;

        process.WaitForExit(1000);

        if (process.HasExited)
        {
            string returningResult = await sr.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(returningResult))
                return returningResult;

            returningResult = await errorReader.ReadToEndAsync();

            return returningResult;
        }
        else
            return string.Empty;
    }

    public static async Task<List<string>> TesseractLanguagesAsStrings()
    {
        List<string> languageStrings = new();

        string tesseractPath = GetTesseractPath();

        if (string.IsNullOrWhiteSpace(tesseractPath))
        {
            languageStrings.Add("eng");
            return languageStrings;
        }

        BufferedCommandResult result = await Cli.Wrap(tesseractPath)
            .WithValidation(CommandResultValidation.None)
            .WithArguments(args => args
                .Add("--list-langs")
            ).ExecuteBufferedAsync();

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            languageStrings.Add("eng");
            return languageStrings;
        }

        string[] tempList = result.StandardOutput.Split(Environment.NewLine);

        foreach (string item in tempList)
            if (item.Length < 30 && !string.IsNullOrWhiteSpace(item) && item != "osd")
                languageStrings.Add(item);

        return languageStrings;
    }
}
