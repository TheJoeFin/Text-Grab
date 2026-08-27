using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

public class FileOpenUtilities
{
    public static EtwEditorMode GetEditorModeForPath(string? path)
    {
        string extension = Path.GetExtension(path ?? string.Empty);

        if (IoUtilities.IsSpreadsheetFileExtension(extension))
            return EtwEditorMode.Spreadsheet;

        if (IoUtilities.IsMarkdownFileExtension(extension))
            return EtwEditorMode.Markdown;

        return EtwEditorMode.Text;
    }

    public static async Task<(string TextContent, OpenContentKind SourceKindOfContent)> GetContentFromPath(string pathOfFileToOpen, bool isMultipleFiles = false, ILanguage? language = null)
    {
        StringBuilder stringBuilder = new();
        OpenContentKind openContentKind = IoUtilities.GetOpenContentKindForPath(pathOfFileToOpen);

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
}
