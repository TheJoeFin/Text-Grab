using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using Text_Grab.Extensions;
using Text_Grab.Models;
using Windows.Graphics.Imaging;

namespace Text_Grab.Utilities;

public static class WindowsAiUtilities
{
    public static bool CanDeviceUseWinAI()
    {
        // Check if the app is packaged and if the AI feature is supported
        if (!AppUtilities.IsPackaged())
            return false;

        try
        {
            AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
            if (readyState == AIFeatureReadyState.NotSupportedOnCurrentSystem)
                return false;
            else
                return true;
        }
        catch (Exception)
        {
#if DEBUG
            throw;
#endif
            return false;
        }
    }

    public static async Task<string> GetTextWithWinAI(string imagePath)
    {
        if (!CanDeviceUseWinAI())
            return "ERROR: Cannot use Windows AI on this device.";

        AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            AIFeatureReadyResult op = await TextRecognizer.EnsureReadyAsync();
        }

        using TextRecognizer textRecognizer = await TextRecognizer.CreateAsync();

        SoftwareBitmap bitmap = await imagePath.FilePathToSoftwareBitmapAsync();
        ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);

        RecognizedText? result = textRecognizer?
            .RecognizeTextFromImage(imageBuffer);

        if (result is null)
            return "ERROR: No text recognized";

        StringBuilder stringBuilder = new();

        foreach (RecognizedLine? line in result.Lines)
            stringBuilder.AppendLine(line.Text);

        return stringBuilder.ToString();
    }

    public static async Task<WinAiOcrLinesWords?> GetOcrResultAsync(Bitmap bmp)
    {
        string tempFilePath = System.IO.Path.GetTempFileName();
        bmp.Save(tempFilePath, System.Drawing.Imaging.ImageFormat.Png);
        SoftwareBitmap softwareBitmap = await tempFilePath.FilePathToSoftwareBitmapAsync();

        // for some reason "await bmp.CreateSoftwareBitmap()" does not work, so we use the file path method instead
        RecognizedText? recognizedText = await GetOcrResultAsync(softwareBitmap);

        if (recognizedText is null)
            return null;

        return new WinAiOcrLinesWords(recognizedText);
    }

    public static async Task<RecognizedText?> GetOcrResultAsync(SoftwareBitmap softwareBitmap)
    {
        if (!CanDeviceUseWinAI())
            return null;

        AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            AIFeatureReadyResult op = await TextRecognizer.EnsureReadyAsync();
        }

        using TextRecognizer textRecognizer = await TextRecognizer.CreateAsync();
        ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(softwareBitmap);

        RecognizedText? result = textRecognizer?
            .RecognizeTextFromImage(imageBuffer);

        return result;
    }
}
