using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

// Install Tesseract for Windows from UB-Mannheim
// https://github.com/UB-Mannheim/tesseract/wiki

public static class TesseractHelper
{
    public static async Task<string> GetTextFromImage(string pathToFile)
    {
        string rawPath = @"%LOCALAPPDATA%\Tesseract-OCR\tesseract.exe";
        string tesExePath = Environment.ExpandEnvironmentVariables(rawPath);

        if (!File.Exists(tesExePath))
            tesExePath = "C:\\Program Files\\Tesseract-OCR\\tesseract.exe";

        if (!File.Exists(tesExePath))
            return "Cannot find tesseract.exe";

        ProcessStartInfo psi = new()
        {
            FileName = tesExePath,
            Arguments = $"\"{pathToFile}\" - -l eng",
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
