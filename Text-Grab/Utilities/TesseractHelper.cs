using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

// Install Tesseract for Windows from UB-Mannheim
// https://github.com/UB-Mannheim/tesseract/wiki

// Docs about commandline usage
// https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html 

// This was developed using Tesseract v5 in 2022

public static class TesseractHelper
{
    public static async Task<string> GetTextFromImage(string pathToFile, bool outputHocr = false)
    {
        string rawPath = @"%LOCALAPPDATA%\Programs\Tesseract-OCR\tesseract.exe";
        string tesExePath = Environment.ExpandEnvironmentVariables(rawPath);

        if (!File.Exists(tesExePath))
            tesExePath = "C:\\Program Files\\Tesseract-OCR\\tesseract.exe";

        if (!File.Exists(tesExePath))
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

        process.WaitForExit();

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
}

public class TessOcrLine
{
    public string Text { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public static class HocrReader
{
    public static List<TessOcrLine> ReadLines(string hocrText)
    {
        // Create a list to hold the OcrLine objects
        var lines = new List<TessOcrLine>();

        // Split the hOCR text into lines
        var hocrLines = hocrText.Split(new string[] { "<span class='ocr_line'", "</span>" }, StringSplitOptions.RemoveEmptyEntries);

        // Iterate through the lines
        foreach (var hocrLineText in hocrLines)
        {
            // Extract the line information
            var line = ReadLine(hocrLineText);

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
        var textMatch = Regex.Match(hocrLineText, "<span class='ocr_line'[^>]*>(.*?)</span>");
        line.Text = textMatch.Groups[1].Value;

        // Extract the bounding box coordinates from the hOCR text
        var bboxMatch = Regex.Match(hocrLineText, "bbox (\\d+) (\\d+) (\\d+) (\\d+)");
        line.X = int.Parse(bboxMatch.Groups[1].Value);
        line.Y = int.Parse(bboxMatch.Groups[2].Value);
        line.Width = int.Parse(bboxMatch.Groups[3].Value);
        line.Height = int.Parse(bboxMatch.Groups[4].Value);

        return line;
    }
}