using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

public class IoUtilities
{
    public static readonly List<string> ImageExtensions = [".png", ".bmp", ".jpg", ".jpeg", ".tiff", ".gif", ".tif", ".webp", ".ico"];
    public static readonly List<string> PdfExtensions = [".pdf"];
    public static readonly List<string> MarkdownExtensions = [".md", ".markdown"];
    public static readonly List<string> SpreadsheetExtensions = [".csv", ".tsv", ".tab"];

    public static bool IsImageFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        return IsImageFileExtension(Path.GetExtension(path));
    }

    public static bool IsImageFileExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return ImageExtensions.Contains(extension.ToLowerInvariant());
    }

    public static bool IsPdfFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        return IsPdfFileExtension(Path.GetExtension(path));
    }

    public static bool IsPdfFileExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return PdfExtensions.Contains(extension.ToLowerInvariant());
    }

    public static bool IsVisualDocumentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        return IsVisualDocumentFileExtension(Path.GetExtension(path));
    }

    public static bool IsVisualDocumentFileExtension(string extension)
    {
        return IsImageFileExtension(extension) || IsPdfFileExtension(extension);
    }

    public static bool IsMarkdownFileExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return MarkdownExtensions.Contains(extension.ToLowerInvariant());
    }

    public static bool IsSpreadsheetFileExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return SpreadsheetExtensions.Contains(extension.ToLowerInvariant());
    }

    public static EtwEditorMode GetEditorModeForPath(string? path)
    {
        string extension = Path.GetExtension(path ?? string.Empty);

        if (IsSpreadsheetFileExtension(extension))
            return EtwEditorMode.Spreadsheet;

        if (IsMarkdownFileExtension(extension))
            return EtwEditorMode.Markdown;

        return EtwEditorMode.Text;
    }

    public static OpenContentKind GetOpenContentKindForPath(string? path)
    {
        string extension = Path.GetExtension(path ?? string.Empty);

        if (IsPdfFileExtension(extension))
            return OpenContentKind.PdfDocument;

        if (IsImageFileExtension(extension))
            return OpenContentKind.Image;

        return OpenContentKind.TextFile;
    }

    public static async Task<(string TextContent, OpenContentKind SourceKindOfContent)> GetContentFromPath(string pathOfFileToOpen, bool isMultipleFiles = false, ILanguage? language = null)
    {
        StringBuilder stringBuilder = new();
        OpenContentKind openContentKind = GetOpenContentKindForPath(pathOfFileToOpen);

        if (isMultipleFiles)
            stringBuilder.AppendLine(pathOfFileToOpen);

        if (openContentKind is OpenContentKind.Image or OpenContentKind.PdfDocument)
        {
            try
            {
                stringBuilder.Append(await OcrUtilities.OcrAbsoluteFilePathAsync(pathOfFileToOpen, language));
            }
            catch (Exception)
            {
                await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Error",
                    Content = $"Failed to read {pathOfFileToOpen}",
                    CloseButtonText = "OK"
                }.ShowDialogAsync();
            }
        }
        else
        {
            // Continue with along trying to open a text file.
            openContentKind = OpenContentKind.TextFile;
            await TryToOpenTextFile(pathOfFileToOpen, isMultipleFiles, stringBuilder);
        }

        if (isMultipleFiles)
        {
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append(Environment.NewLine);
        }

        return (stringBuilder.ToString(), openContentKind);
    }

    public static async Task TryToOpenTextFile(string pathOfFileToOpen, bool isMultipleFiles, StringBuilder stringBuilder)
    {
        try
        {
            using StreamReader sr = File.OpenText(pathOfFileToOpen);

            string s = await sr.ReadToEndAsync();

            stringBuilder.Append(s);
        }
        catch (System.Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Failed to open file. {ex.Message}");
        }
    }

    public static string ListFilesFoldersInDirectory(string chosenFolderPath)
    {
        IEnumerable<string> files = Directory.EnumerateFiles(chosenFolderPath);
        IEnumerable<string> folders = Directory.EnumerateDirectories(chosenFolderPath);
        StringBuilder listOfNames = new();
        listOfNames.Append(chosenFolderPath).Append(Environment.NewLine).Append(Environment.NewLine);
        foreach (string folder in folders)
            listOfNames.Append($"{folder.AsSpan(1 + chosenFolderPath.Length, folder.Length - 1 - chosenFolderPath.Length)}{Environment.NewLine}");

        foreach (string file in files)
            listOfNames.Append($"{file.AsSpan(1 + chosenFolderPath.Length, file.Length - 1 - chosenFolderPath.Length)}{Environment.NewLine}");
        return listOfNames.ToString();
    }
}
