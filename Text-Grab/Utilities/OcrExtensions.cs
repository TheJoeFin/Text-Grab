using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Text_Grab.Properties;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Text_Grab.Utilities;

public static class OcrExtensions
{

    public static void GetTextFromOcrLine(this OcrLine ocrLine, bool isSpaceJoiningOCRLang, StringBuilder text)
    {
        // (when OCR language is zh or ja)
        // matches words in a space-joining language, which contains:
        // - one letter that is not in "other letters" (CJK characters are "other letters")
        // - one number digit
        // - any words longer than one character
        // Chinese and Japanese characters are single-character words
        // when a word is one punctuation/symbol, join it without spaces

        if (isSpaceJoiningOCRLang)
            text.AppendLine(ocrLine.Text);
        else
        {
            bool isFirstWord = true;
            bool isPrevWordSpaceJoining = false;

            Regex regexSpaceJoiningWord = new(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}");

            foreach (OcrWord ocrWord in ocrLine.Words)
            {
                string wordString;

                if (Settings.Default.CorrectErrors)
                    wordString = ocrWord.Text.TryFixEveryWordLetterNumberErrors();
                else
                    wordString = ocrWord.Text;

                bool isThisWordSpaceJoining = regexSpaceJoiningWord.IsMatch(wordString);

                if (isFirstWord || (!isThisWordSpaceJoining && !isPrevWordSpaceJoining))
                    _ = text.Append(wordString);
                else
                    _ = text.Append(' ').Append(wordString);

                isFirstWord = false;
                isPrevWordSpaceJoining = isThisWordSpaceJoining;
            }
        }
    }

    public static async Task<(OcrResult, double)> GetOcrResultFromRegion(Rectangle region, Language language)
    {
        using Bitmap bmp = new(region.Width, region.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(region.Left, region.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        double scale = await ImageMethods.GetIdealScaleFactor(bmp, language);
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bmp, scale);

        OcrResult ocrResult = await GetOcrResultFromBitmap(scaledBitmap, language);

        return (ocrResult, scale);
    }

    public async static Task<OcrResult> GetOcrResultFromBitmap(Bitmap scaledBitmap, Language selectedLanguage)
    {
        await using MemoryStream memory = new();

        scaledBitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
        using SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

        await memory.FlushAsync();

        OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
        return await ocrEngine.RecognizeAsync(softwareBmp);
    }

    public static Language GetOCRLanguage()
    {
        // use currently selected Language
        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;
        Language selectedLanguage = new(inputLang);

        if (!string.IsNullOrEmpty(Settings.Default.LastUsedLang))
            selectedLanguage = new(Settings.Default.LastUsedLang);

        List<Language> possibleOCRLangs = OcrEngine.AvailableRecognizerLanguages.ToList();

        if (possibleOCRLangs.Count == 0)
        {
            System.Windows.MessageBox.Show("No possible OCR languages are installed.", "Text Grab");
            throw new Exception("No possible OCR languages are installed");
        }

        // If the selected input language or last used language is not a possible OCR Language
        // then we need to find a similar language to use
        if (possibleOCRLangs.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
        {
            List<Language> similarLanguages = possibleOCRLangs.Where(
                la => la.AbbreviatedName == selectedLanguage.AbbreviatedName).ToList();

            if (similarLanguages is not null && similarLanguages.Count > 0)
                selectedLanguage = similarLanguages.First();
            else
                selectedLanguage = possibleOCRLangs.First();
        }

        return selectedLanguage;
    }
}
