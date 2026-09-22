using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

// Install Tesseract for Windows from UB-Mannheim
// https://github.com/UB-Mannheim/tesseract/wiki

// Docs about command line usage
// https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html 

// This was developed using Tesseract v5 in 2022

public static class TesseractHelper
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

    public static async Task<OcrOutput> GetOcrOutputFromBitmap(Bitmap bmp, TessLang language)
    {
        bmp.Save(TesseractHelper.TempImagePath(), ImageFormat.Png);

        OcrOutput ocrOutput = new()
        {
            Engine = OcrEngineKind.Tesseract,
            Kind = OcrOutputKind.Paragraph,
            Language = language,
            SourceBitmap = bmp,
            RawOutput = await TesseractHelper.GetTextFromImagePathAsync(TempImagePath(), language.RawTag)
        };
        ocrOutput.CleanOutput();

        return ocrOutput;
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

    public static string TempImagePath()
    {
        if (AutomationProfile.Current is not null)
            return Path.Combine(AutomationProfile.GetTemporaryDirectory(), "tempImage.png");

        string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
        if (exePath is null)
        {
            string rawPath = @"%LOCALAPPDATA%\Text_Grab";
            exePath = Environment.ExpandEnvironmentVariables(rawPath);
        }

        return $"{exePath}\\tempImage.png";
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

    public static async Task<List<ILanguage>> TesseractLanguages()
    {
        List<string> languageStrings = await TesseractLanguagesAsStrings();
        List<ILanguage> tesseractLanguages = new();

        foreach (string language in languageStrings)
            tesseractLanguages.Add(new TessLang(language));

        return tesseractLanguages;
    }
}
