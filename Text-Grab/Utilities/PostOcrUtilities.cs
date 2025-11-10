using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Text_Grab.Interfaces;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

public static class PostOcrUtilities
{
    public static string GetTextFromOcrResult(IOcrLinesWords ocrResult, ILanguage language)
    {
        // Convert OCR result to WordBorderInfo objects for each individual word
        List<WordBorderInfo> wordBorderInfos = [];

        foreach (IOcrLine ocrLine in ocrResult.Lines)
        {
            foreach (IOcrWord ocrWord in ocrLine.Words)
            {
                WordBorderInfo wordInfo = new()
                {
                    BorderRect = new System.Windows.Rect(
                        ocrWord.BoundingBox.X,
                        ocrWord.BoundingBox.Y,
                        ocrWord.BoundingBox.Width,
                        ocrWord.BoundingBox.Height),
                    Word = ocrWord.Text,
                    ResultRowID = 0,
                    ResultColumnID = 0
                };

                wordBorderInfos.Add(wordInfo);
            }
        }

        // Use the existing word border processing logic
        return GetTextFromWordBorderInfo(wordBorderInfos, language);
    }

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
        // Sort words by Y position (top to bottom), then by X position (left to right)
        List<WordBorderInfo> sortedWords = [.. wordBorderInfos.OrderBy(w => w.BorderRect.Top).ThenBy(w => w.BorderRect.Left)];

        if (sortedWords.Count == 0)
            return string.Empty;

        List<string> lines = [];
        List<WordBorderInfo> currentLine = [];
        double lineYThreshold = 5.0; // Pixels - words within this Y distance are on the same line

        foreach (WordBorderInfo word in sortedWords)
        {
            if (currentLine.Count == 0)
            {
                // Start a new line
                currentLine.Add(word);
            }
            else
            {
                // Check if this word is on the same line as the current line
                double currentLineY = currentLine.Average(w => w.BorderRect.Top);
                double wordY = word.BorderRect.Top;

                if (Math.Abs(wordY - currentLineY) <= lineYThreshold)
                {
                    // Same line - add to current line
                    currentLine.Add(word);
                }
                else
                {
                    // Different line - process current line and start a new one
                    string processedLine = ProcessLineToString(currentLine);
                    lines.Add(processedLine);
                    currentLine.Clear();
                    currentLine.Add(word);
                }
            }
        }

        // Process the last line
        if (currentLine.Count > 0)
        {
            string processedLine = ProcessLineToString(currentLine);
            lines.Add(processedLine);
        }

        // Post-process: merge single-character lines with previous line
        List<string> mergedLines = [];
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            string cleanLine = line.Trim();

            // If this is a single short character and not the first line, append to previous
            if (i > 0 && cleanLine.Length <= 2 && !cleanLine.Contains('\u3000'))
            {
                // Append to previous line with a space and restore newline
                mergedLines[^1] = mergedLines[^1].TrimEnd() + " " + cleanLine + Environment.NewLine;
            }
            else
            {
                mergedLines.Add(line);
            }
        }

        return string.Join("", mergedLines).TrimEnd();
    }

    private static string ProcessLineToString(List<WordBorderInfo> lineWords)
    {
        if (lineWords.Count == 0)
            return string.Empty;

        // Sort words by X position (left to right)
        lineWords.Sort((a, b) => a.BorderRect.Left.CompareTo(b.BorderRect.Left));

        // Determine if this is a furigana line (multiple short words) or main text
        // Furigana typically consists of multiple short words that should be joined with spaces
        bool isFurigana = lineWords.Count > 1 && lineWords.All(w => w.Word.Length <= 4);

        StringBuilder result = new();

        if (isFurigana)
        {
            // Join with ideographic space (U+3000)
            for (int i = 0; i < lineWords.Count; i++)
            {
                string cleanWord = lineWords[i].Word.Replace(" ", "");
                result.Append(cleanWord);
                if (i < lineWords.Count - 1)
                {
                    result.Append('\u3000'); // Ideographic space
                }
            }
        }
        else
        {
            // Main text - concatenate with spaces removed
            foreach (WordBorderInfo word in lineWords)
            {
                string cleanWord = word.Word.Replace(" ", "");
                result.Append(cleanWord);
            }
        }

        result.AppendLine();
        return result.ToString();
    }
}
