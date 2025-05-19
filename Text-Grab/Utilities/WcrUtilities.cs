using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.Management.Deployment;
using System;
using System.Text;
using System.Threading.Tasks;
using Text_Grab.Extensions;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

using Text_Grab.Extensions;


namespace Text_Grab.Utilities;

public static class WcrUtilities
{
    public static async Task<string> GetTextWithWcr(string imagePath)
    {
        if (!AppUtilities.IsPackaged())
            return "ERROR: This method requires a packaged app environment.";

        AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
        if (readyState is AIFeatureReadyState.NotSupportedOnCurrentSystem)
        {
            return "ERROR: Windows AI not supported";
        }
        if (readyState == AIFeatureReadyState.NotReady)
        {
            AIFeatureReadyResult op = await TextRecognizer.EnsureReadyAsync();
        }

        using TextRecognizer textRecognizer = await TextRecognizer.CreateAsync();

        SoftwareBitmap bitmap = await imagePath.FilePathToSoftwareBitmapAsync();
        ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);

        // System.UnauthorizedAccessException: 'Access is denied.
        
        RecognizedText? result = textRecognizer?
            .RecognizeTextFromImage(imageBuffer);


        if (result == null)
            return "ERROR: No text recognized";

        StringBuilder stringBuilder = new();

        foreach (RecognizedLine? line in result.Lines)
        {
            stringBuilder.AppendLine(line.Text);
        }

        return stringBuilder.ToString();
    }
}
