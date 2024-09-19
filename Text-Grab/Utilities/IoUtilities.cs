using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

public class IoUtilities
{
    public static readonly List<string> ImageExtensions = new() { ".png", ".bmp", ".jpg", ".jpeg", ".tiff", ".gif" };


    public static async Task<(string TextContent, OpenContentKind SourceKindOfContent)> GetContentFromPath(string pathOfFileToOpen, bool isMultipleFiles = false)
    {
        StringBuilder stringBuilder = new();
        OpenContentKind openContentKind = OpenContentKind.Image;

        if (isMultipleFiles)
        {
            stringBuilder.AppendLine(pathOfFileToOpen);
        }

        if (ImageExtensions.Contains(Path.GetExtension(pathOfFileToOpen).ToLower()))
        {
            try
            {
                stringBuilder.Append(await OcrUtilities.OcrAbsoluteFilePathAsync(pathOfFileToOpen));
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show($"Failed to read {pathOfFileToOpen}");
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
