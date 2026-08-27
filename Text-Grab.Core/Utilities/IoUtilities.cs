using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
