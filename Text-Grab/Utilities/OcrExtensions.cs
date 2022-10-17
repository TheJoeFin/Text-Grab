using System;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Media.Ocr;

namespace Text_Grab.Utilities;

public static class OcrExtensions
{

    public static void GetTextFromOcrLine(this OcrLine ocrLine, bool isSpaceJoiningOCRLang, StringBuilder text, Regex regexSpaceJoiningWord)
    {
        if (isSpaceJoiningOCRLang == true)
            text.AppendLine(ocrLine.Text);
        else
        {
            bool isFirstWord = true;
            bool isPrevWordSpaceJoining = false;

            foreach (OcrWord ocrWord in ocrLine.Words)
            {
                bool isThisWordSpaceJoining = regexSpaceJoiningWord.IsMatch(ocrWord.Text);

                if (isFirstWord || (!isThisWordSpaceJoining && !isPrevWordSpaceJoining))
                    _ = text.Append(ocrWord.Text);
                else
                    _ = text.Append(' ').Append(ocrWord.Text);

                isFirstWord = false;
                isPrevWordSpaceJoining = isThisWordSpaceJoining;
            }

            text.Append(Environment.NewLine);
        }
    }
}
