using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

public static class TesseractHelper
{
    public static async Task<string> GetTextFromImage(string pathToFile)
    {
        string rawPath = @"%LOCALAPPDATA%\Tesseract-OCR\tesseract.exe";
        string tesExePath = Environment.ExpandEnvironmentVariables(rawPath);

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
}
