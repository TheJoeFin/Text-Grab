using System;
using System.Collections.Generic;
using System.Text;
using Text_Grab.Interfaces;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

public static class PostOcrUtilities
{
    public static string GetTextFromWordBorderInfo(IEnumerable<WordBorderInfo> wordBorderInfos, ILanguage language)
    {
        if (language.LanguageTag.StartsWith("ja"))
        {
            return GetTextFromJaWordBorders(wordBorderInfos);
        }

        StringBuilder sb = new();
        foreach (WordBorderInfo wordBorderInfo in wordBorderInfos)
        {
            sb.Append(wordBorderInfo.Word);
            sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    private static string GetTextFromJaWordBorders(IEnumerable<WordBorderInfo> wordBorderInfos)
    {
        throw new NotImplementedException();
    }
}
