using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;

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

    private readonly static Settings DefaultSettings = AppUtilities.TextGrabSettings;


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
        if (!string.IsNullOrWhiteSpace(DefaultSettings.TesseractPath)
            && File.Exists(DefaultSettings.TesseractPath))
            return DefaultSettings.TesseractPath;

        string tesExePath = Environment.ExpandEnvironmentVariables(rawPath);
        string programsPath = Environment.ExpandEnvironmentVariables(rawProgramsPath);

        if (File.Exists(tesExePath))
        {
            DefaultSettings.TesseractPath = tesExePath;
            DefaultSettings.Save();
            return tesExePath;
        }

        if (File.Exists(programsPath))
        {
            DefaultSettings.TesseractPath = programsPath;
            DefaultSettings.Save();
            return programsPath;
        }

        if (File.Exists(basicPath))
        {
            DefaultSettings.TesseractPath = basicPath;
            DefaultSettings.Save();
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

    public static async Task<OcrOutput> GetOcrOutputFromBitmap(Bitmap bmp, Windows.Globalization.Language language, string tessTag = "")
    {
        bmp.Save(TesseractHelper.TempImagePath(), ImageFormat.Png);
        if (string.IsNullOrWhiteSpace(tessTag))
            tessTag = language.LanguageTag;

        OcrOutput ocrOutput = new()
        {
            Engine = OcrEngineKind.Tesseract,
            Kind = OcrOutputKind.Paragraph,
            SourceBitmap = bmp,
            RawOutput = await TesseractHelper.GetTextFromImagePathAsync(TempImagePath(), tessTag)
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
        string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
        if (exePath is null)
        {
            string rawPath = @"%LOCALAPPDATA%\Text_Grab";
            exePath = Environment.ExpandEnvironmentVariables(rawPath);
        }

        return $"{exePath}\\tempImage.png";
    }

    public async static Task<List<string>> TesseractLanguagesAsStrings()
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

    public async static Task<List<ILanguage>> TesseractLanguages()
    {
        List<string> languageStrings = await TesseractLanguagesAsStrings();
        List<ILanguage> tesseractLanguages = new();

        foreach (string language in languageStrings)
            tesseractLanguages.Add(new TessLang(language));

        return tesseractLanguages;
    }
}

public class TesseractGitHubFileDownloader
{
    private readonly HttpClient _client;

    public TesseractGitHubFileDownloader()
    {
        _client = new HttpClient();
        // It's a good practice to set a user-agent when making requests
        _client.DefaultRequestHeaders.Add("User-Agent", "Text Grab settings language downloader");
    }

    public async Task DownloadFileAsync(string filenameToDownload, string localDestination)
    {
        // Construct the URL to the raw content of the file in the GitHub repository
        // https://github.com/tesseract-ocr/tessdata
        string fileUrl = $"https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/{filenameToDownload}";

        try
        {
            // Send a GET request to the specified URL
            HttpResponseMessage response = await _client.GetAsync(fileUrl);
            response.EnsureSuccessStatusCode();

            // Read the response content
            byte[] fileContents = await response.Content.ReadAsByteArrayAsync();

            // Write the content to a file on the local file system
            await File.WriteAllBytesAsync(localDestination, fileContents);
            Console.WriteLine("File downloaded successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public static readonly string[] tesseractTrainedDataFileNames = [
        "afr.traineddata",
        "amh.traineddata",
        "ara.traineddata",
        "asm.traineddata",
        "aze.traineddata",
        "aze_cyrl.traineddata",
        "bel.traineddata",
        "ben.traineddata",
        "bod.traineddata",
        "bos.traineddata",
        "bre.traineddata",
        "bul.traineddata",
        "cat.traineddata",
        "ceb.traineddata",
        "ces.traineddata",
        "chi_sim.traineddata",
        "chi_sim_vert.traineddata",
        "chi_tra.traineddata",
        "chi_tra_vert.traineddata",
        "chr.traineddata",
        "cos.traineddata",
        "cym.traineddata",
        "dan.traineddata",
        "dan_frak.traineddata",
        "deu.traineddata",
        "deu_frak.traineddata",
        "div.traineddata",
        "dzo.traineddata",
        "ell.traineddata",
        "eng.traineddata",
        "enm.traineddata",
        "epo.traineddata",
        "equ.traineddata",
        "est.traineddata",
        "eus.traineddata",
        "fao.traineddata",
        "fas.traineddata",
        "fil.traineddata",
        "fin.traineddata",
        "fra.traineddata",
        "frk.traineddata",
        "frm.traineddata",
        "fry.traineddata",
        "gla.traineddata",
        "gle.traineddata",
        "glg.traineddata",
        "grc.traineddata",
        "guj.traineddata",
        "hat.traineddata",
        "heb.traineddata",
        "hin.traineddata",
        "hrv.traineddata",
        "hun.traineddata",
        "hye.traineddata",
        "iku.traineddata",
        "ind.traineddata",
        "isl.traineddata",
        "ita.traineddata",
        "ita_old.traineddata",
        "jav.traineddata",
        "jpn.traineddata",
        "jpn_vert.traineddata",
        "kan.traineddata",
        "kat.traineddata",
        "kat_old.traineddata",
        "kaz.traineddata",
        "khm.traineddata",
        "kir.traineddata",
        "kmr.traineddata",
        "kor.traineddata",
        "kor_vert.traineddata",
        "lao.traineddata",
        "lat.traineddata",
        "lav.traineddata",
        "lit.traineddata",
        "ltz.traineddata",
        "mal.traineddata",
        "mar.traineddata",
        "mkd.traineddata",
        "mlt.traineddata",
        "mon.traineddata",
        "mri.traineddata",
        "msa.traineddata",
        "mya.traineddata",
        "nep.traineddata",
        "nld.traineddata",
        "nor.traineddata",
        "oci.traineddata",
        "ori.traineddata",
        "osd.traineddata",
        "pan.traineddata",
        "pol.traineddata",
        "por.traineddata",
        "pus.traineddata",
        "que.traineddata",
        "ron.traineddata",
        "rus.traineddata",
        "san.traineddata",
        "sin.traineddata",
        "slk.traineddata",
        "slk_frak.traineddata",
        "slv.traineddata",
        "snd.traineddata",
        "spa.traineddata",
        "spa_old.traineddata",
        "sqi.traineddata",
        "srp.traineddata",
        "srp_latn.traineddata",
        "sun.traineddata",
        "swa.traineddata",
        "swe.traineddata",
        "syr.traineddata",
        "tam.traineddata",
        "tat.traineddata",
        "tel.traineddata",
        "tgk.traineddata",
        "tgl.traineddata",
        "tha.traineddata",
        "tir.traineddata",
        "ton.traineddata",
        "tur.traineddata",
        "uig.traineddata",
        "ukr.traineddata",
        "urd.traineddata",
        "uzb.traineddata",
        "uzb_cyrl.traineddata",
        "vie.traineddata",
        "yid.traineddata",
        "yor.traineddata",
    ];
}

public class TessOcrLine
{
    public int Height { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Width { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public static class HocrReader
{
    private static readonly string[] separator = ["<span class='ocr_line'", "</span>"];

    public static List<TessOcrLine> ReadLines(string hocrText)
    {
        // Create a list to hold the OcrLine objects
        List<TessOcrLine> lines = new();

        // Split the hOCR text into lines
        string[] hocrLines = hocrText.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        // Iterate through the lines
        foreach (string hocrLineText in hocrLines)
        {
            // Extract the line information
            TessOcrLine line = ReadLine(hocrLineText);

            // Add the line to the list
            lines.Add(line);
        }

        return lines;
    }

    private static TessOcrLine ReadLine(string hocrLineText)
    {
        // Create a new OcrLine object
        TessOcrLine line = new();

        // Extract the text of the line from the hOCR text
        Match textMatch = Regex.Match(hocrLineText, "<span class='ocr_line'[^>]*>(.*?)</span>");
        line.Text = textMatch.Groups[1].Value;

        // Extract the bounding box coordinates from the hOCR text
        Match bboxMatch = Regex.Match(hocrLineText, "bbox (\\d+) (\\d+) (\\d+) (\\d+)");
        line.X = int.Parse(bboxMatch.Groups[1].Value);
        line.Y = int.Parse(bboxMatch.Groups[2].Value);
        line.Width = int.Parse(bboxMatch.Groups[3].Value);
        line.Height = int.Parse(bboxMatch.Groups[4].Value);

        return line;
    }
}