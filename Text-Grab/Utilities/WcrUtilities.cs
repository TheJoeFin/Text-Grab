using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.Vision;
using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

using Text_Grab.Extensions;


namespace Text_Grab.Utilities;

public static class WcrUtilities
{
    public static async Task<string> GetTextWithWcr(string imagePath)
    {
        AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
        if (readyState is not AIFeatureReadyState.Ready and not AIFeatureReadyState.EnsureNeeded)
        {
            return "Local model not ready please try again soon.";
        }
        if (readyState == AIFeatureReadyState.EnsureNeeded)
        {
            AIFeatureReadyResult op = await TextRecognizer.EnsureReadyAsync();
        }

        using TextRecognizer textRecognizer = await TextRecognizer.CreateAsync();

        SoftwareBitmap bitmap = await imagePath.FilePathToSoftwareBitmapAsync();
        ImageBuffer imageBuffer = ImageBuffer.CreateBufferAttachedToBitmap(bitmap);

        // System.UnauthorizedAccessException: 'Access is denied.
        RecognizedText? result = textRecognizer?
            .RecognizeTextFromImage(imageBuffer, new TextRecognizerOptions());

        StringBuilder stringBuilder = new();

        foreach (RecognizedLine? line in result.Lines)
        {
            stringBuilder.AppendLine(line.Text);
        }

        return stringBuilder.ToString();
    }
}
