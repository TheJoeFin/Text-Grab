using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Text_Grab.Utilities;

public static class CharacterUtilities
{
    public static string GetUnicodeCategory(char c)
    {
        UnicodeCategory category = char.GetUnicodeCategory(c);
        return category switch
        {
            UnicodeCategory.UppercaseLetter => "Uppercase Letter",
            UnicodeCategory.LowercaseLetter => "Lowercase Letter",
            UnicodeCategory.TitlecaseLetter => "Titlecase Letter",
            UnicodeCategory.ModifierLetter => "Modifier Letter",
            UnicodeCategory.OtherLetter => "Other Letter",
            UnicodeCategory.NonSpacingMark => "Non-Spacing Mark",
            UnicodeCategory.SpacingCombiningMark => "Spacing Mark",
            UnicodeCategory.EnclosingMark => "Enclosing Mark",
            UnicodeCategory.DecimalDigitNumber => "Decimal Digit",
            UnicodeCategory.LetterNumber => "Letter Number",
            UnicodeCategory.OtherNumber => "Other Number",
            UnicodeCategory.SpaceSeparator => "Space Separator",
            UnicodeCategory.LineSeparator => "Line Separator",
            UnicodeCategory.ParagraphSeparator => "Paragraph Separator",
            UnicodeCategory.Control => "Control Character",
            UnicodeCategory.Format => "Format Character",
            UnicodeCategory.Surrogate => "Surrogate",
            UnicodeCategory.PrivateUse => "Private Use",
            UnicodeCategory.ConnectorPunctuation => "Connector Punctuation",
            UnicodeCategory.DashPunctuation => "Dash Punctuation",
            UnicodeCategory.OpenPunctuation => "Open Punctuation",
            UnicodeCategory.ClosePunctuation => "Close Punctuation",
            UnicodeCategory.InitialQuotePunctuation => "Initial Quote",
            UnicodeCategory.FinalQuotePunctuation => "Final Quote",
            UnicodeCategory.OtherPunctuation => "Other Punctuation",
            UnicodeCategory.MathSymbol => "Math Symbol",
            UnicodeCategory.CurrencySymbol => "Currency Symbol",
            UnicodeCategory.ModifierSymbol => "Modifier Symbol",
            UnicodeCategory.OtherSymbol => "Other Symbol",
            UnicodeCategory.OtherNotAssigned => "Not Assigned",
            _ => "Unknown"
        };
    }

    public static bool IsCommonHtmlEntity(char c)
    {
        return c switch
        {
            '<' or '>' or '&' or '"' or '\'' or ' ' => true,
            _ => false
        };
    }

    public static string GetHtmlEntity(char c, int codePoint)
    {
        return c switch
        {
            '<' => "&lt; or &#60;",
            '>' => "&gt; or &#62;",
            '&' => "&amp; or &#38;",
            '"' => "&quot; or &#34;",
            '\'' => "&apos; or &#39;",
            ' ' when codePoint == 160 => "&nbsp; or &#160;",
            _ => $"&#{codePoint};"
        };
    }

    public static string GetCharacterDetailsText(char c)
    {
        int codePoint = char.ConvertToUtf32(c.ToString(), 0);
        string unicodeHex = $"U+{codePoint:X4}";
        string category = GetUnicodeCategory(c);

        StringBuilder details = new();
        details.AppendLine($"Character: '{c}'");
        details.AppendLine($"Unicode: {unicodeHex} (decimal: {codePoint})");
        details.AppendLine($"Category: {category}");

        // UTF-8 encoding
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(c.ToString());
        string utf8Hex = string.Join(" ", utf8Bytes.Select(b => $"0x{b:X2}"));
        details.AppendLine($"UTF-8: {utf8Hex}");

        // HTML entity if applicable
        if (codePoint < 128 || IsCommonHtmlEntity(c))
        {
            string htmlEntity = GetHtmlEntity(c, codePoint);
            if (!string.IsNullOrEmpty(htmlEntity))
            {
                details.AppendLine($"HTML: {htmlEntity}");
            }
        }

        return details.ToString().TrimEnd();
    }
}
