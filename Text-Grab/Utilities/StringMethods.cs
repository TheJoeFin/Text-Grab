using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Text_Grab.Utilities;

public static class StringMethods
{
    public static readonly List<Char> specialCharList = new()
    { '\\', ' ', '.', ',', '$', '^', '{', '[', '(', '|', ')', '*', '+', '?', '=' };

    public static readonly List<Char> ReservedChars = new()
    { ' ', '"', '*', '/', ':', '<', '>', '?', '\\', '|', '+', ',', '.', ';', '=', '[', ']', '!', '@' };

    public static readonly Dictionary<char, char> greekCyrillicLatinMap = new()
    {
        // Similar Looking Greek characters
        {'Γ', 'r'}, {'Δ', 'A'}, {'Θ', 'O'}, {'Λ', 'A'}, {'Ξ', 'E'},
        {'Π', 'n'}, {'Σ', 'E'}, {'Φ', 'O'}, {'Χ', 'X'}, {'Ψ', 'W'},
        {'Ω', 'O'}, {'α', 'a'}, {'β', 'B'}, {'γ', 'y'}, {'δ', 's'},
        {'ε', 'E'}, {'ζ', 'C'}, {'η', 'n'}, {'θ', 'O'}, {'ι', 'l'},
        {'κ', 'k'}, {'λ', 'A'}, {'μ', 'u'}, {'ν', 'v'}, {'ξ', 'E'},
        {'π', 'n'}, {'ρ', 'p'}, {'ς', 's'}, {'σ', 'o'}, {'τ', 't'},
        {'υ', 'v'}, {'φ', 'O'}, {'χ', 'X'}, {'ψ', 'U'}, {'ω', 'w'},
        {'ö', 'o'}, {'é', 'e'}, {'Å', 'A'}, {'Ö', 'O'}, {'ē', 'e'},
        {'ō', 'o'}, {'Ἀ', 'A'}, {'ό', 'o'},

        // Similar looking Cyrillic characters
        {'Б', 'B'}, {'Г', 'r'}, {'Д', 'A'}, {'Ё', 'E'}, {'Ж', 'K'},
        {'З', '3'}, {'И', 'N'}, {'Й', 'N'}, {'К', 'K'}, {'Л', 'n'},
        {'П', 'n'}, {'Ф', 'O'}, {'Ц', 'U'}, {'Ч', 'u'}, {'Ш', 'W'},
        {'Щ', 'W'}, {'Ъ', 'b'}, {'Ы', 'b'}, {'Ь', 'b'}, {'Э', '3'},
        {'Ю', 'O'}, {'Я', 'R'}, {'б', '6'}, {'в', 'B'}, {'г', 'r'},
        {'д', 'A'}, {'ё', 'e'}, {'ж', 'x'}, {'з', '3'}, {'и', 'N'},
        {'й', 'N'}, {'к', 'k'}, {'л', 'n'}, {'м', 'M'}, {'н', 'H'},
        {'п', 'n'}, {'т', 'T'}, {'ф', 'o'}, {'ц', 'u'}, {'ч', 'u'},
        {'ш', 'w'}, {'щ', 'w'}, {'ъ', 'b'}, {'ы', 'b'}, {'ь', 'b'},
        {'э', '3'}, {'ю', 'o'}, {'я', 'R'},

        // Other Chars
        {'ø', 'e'},
    };

    public static Dictionary<char, char> NumbersToLetters = new()
    {
        {'0', 'o'}, {'4', 'h'}, {'9', 'g'}, {'1', 'l'}
    };

    public static Dictionary<char, char> LettersToNumbers = new()
    {
        {'o', '0'}, {'O', '0'}, {'Q', '0'}, {'c', '0'}, {'C', '0'},
        {'i', '1'}, {'I', '1'}, {'l', '1'}, {'g', '9'}
    };

    public static string ReplaceWithDictionary(this string str, Dictionary<char, char> dict)
    {
        var sb = new StringBuilder();

        foreach (char c in str)
        {
            sb.Append(dict.ContainsKey(c) ? dict[c] : c);
        }

        return sb.ToString();
    }

    public static string ReplaceGreekOrCyrillicWithLatin(this string str)
    {
        return str.ReplaceWithDictionary(greekCyrillicLatinMap);
    }

    public static IEnumerable<int> AllIndexesOf(this string str, string searchstring)
    {
        int minIndex = str.IndexOf(searchstring);
        while (minIndex != -1)
        {
            yield return minIndex;
            minIndex = str.IndexOf(searchstring, minIndex + searchstring.Length);
        }
    }

    public static IEnumerable<int> FindAllIndicesOfString(this string sourceString, string stringToFind)
    {
        int stepsToSearch = 1 + sourceString.Length - stringToFind.Length;

        for (int i = 0; i < stepsToSearch; i++)
        {
            if (sourceString.Substring(i, stringToFind.Length) == stringToFind)
                yield return i;
        }
    }

    public static (int, int) CursorWordBoundaries(this string input, int cursorPosition)
    {
        if (string.IsNullOrEmpty(input))
            return (0, 0);

        if (cursorPosition < 0)
            cursorPosition = 0;

        // Check if the cursor is at a space
        if (char.IsWhiteSpace(input[cursorPosition]))
            cursorPosition = findNearestLetterIndex(input, cursorPosition);

        // Find the start and end of the word by moving the cursor
        // backwards and forwards until we find a non-letter character.
        int start = cursorPosition;
        int end = cursorPosition;

        while (start > 0 && !char.IsWhiteSpace(input[start - 1]))
            start--;

        while (end < input.Length && !char.IsWhiteSpace(input[end]))
            end++;

        return (start, end - start);
    }


    public static string GetWordAtCursorPosition(this string input, int cursorPosition)
    {
        if (input.Length == 0)
            return string.Empty;

        cursorPosition = Math.Clamp(cursorPosition, 0, input.Length - 1);

        (int start, int length) = input.CursorWordBoundaries(cursorPosition);

        // Return the substring of the input that represents the word.
        return input.Substring(start, length);
    }

    private static int findNearestLetterIndex(string input, int cursorPosition)
    {
        Math.Clamp(cursorPosition, 0, input.Length - 1);

        int lastCharIndex = input.Length - 1;

        int nearestToTheRight = cursorPosition;
        int nearestToTheLeft = cursorPosition;

        while (nearestToTheLeft >= 0 && char.IsWhiteSpace(input[nearestToTheLeft]))
            nearestToTheLeft--;

        while (nearestToTheRight <= lastCharIndex && char.IsWhiteSpace(input[nearestToTheRight]))
            nearestToTheRight++;

        // could not find
        if (nearestToTheLeft < 0
            && nearestToTheRight > lastCharIndex)
            return cursorPosition;

        int leftDistance = cursorPosition - nearestToTheLeft;
        int rightDistance = nearestToTheRight - cursorPosition;

        if (rightDistance < leftDistance)
            return nearestToTheRight;

        return nearestToTheLeft;
    }

    public static (int, int) GetStartAndLengthOfLineAtPosition(this string text, int position)
    {
        if (!text.EndsWith(Environment.NewLine))
            text += Environment.NewLine;

        IEnumerable<int> allNewLines = text.AllIndexesOf(Environment.NewLine);
        int lastLine = allNewLines.LastOrDefault();
        bool foundEnd = false;

        int startSelectionIndex = 0;
        int stopSelectionIndex = 0;

        foreach (int newLineIndex in allNewLines)
        {
            if (position > newLineIndex)
                startSelectionIndex = newLineIndex + Environment.NewLine.Length;

            if (!foundEnd
                && newLineIndex >= position)
            {
                stopSelectionIndex = newLineIndex;
                foundEnd = true;
            }
        }

        if (position > lastLine)
            stopSelectionIndex = text.Length;

        int selectionLength = stopSelectionIndex - startSelectionIndex + Environment.NewLine.Length;
        if (selectionLength < 0)
            selectionLength = 0;

        return (startSelectionIndex, selectionLength);
    }

    public static string TryFixToLetters(this string fixToLetters)
    {
        return fixToLetters.ReplaceWithDictionary(NumbersToLetters);
    }

    public static string TryFixToNumbers(this string fixToNumbers)
    {

        return fixToNumbers.ReplaceWithDictionary(LettersToNumbers);
    }

    public static string TryFixNumberLetterErrors(this string stringToFix)
    {
        if (stringToFix.Length < 5)
            return stringToFix;

        int totalNumbers = 0;
        int totalLetters = 0;

        foreach (char charFromString in stringToFix)
        {
            if (char.IsNumber(charFromString))
                totalNumbers++;

            if (char.IsLetter(charFromString))
                totalLetters++;
        }

        float fractionNumber = totalNumbers / (float)stringToFix.Length;
        float letterNumber = totalLetters / (float)stringToFix.Length;

        if (fractionNumber > 0.6)
        {
            stringToFix = stringToFix.TryFixToNumbers();
        }
        else if (letterNumber > 0.6)
        {
            stringToFix = stringToFix.TryFixToLetters();
        }

        return stringToFix;
    }

    public static string TryFixEveryWordLetterNumberErrors(this string stringToFix)
    {
        string[] listOfWords = stringToFix.Split(' ');
        List<string> fixedWords = new();

        foreach (string word in listOfWords)
        {
            string newWord = word.TryFixNumberLetterErrors();
            fixedWords.Add(newWord);
        }
        string joinedString = string.Join(' ', fixedWords.ToArray());
        joinedString = joinedString.Replace("\t ", "\t");
        joinedString = joinedString.Replace("\r ", "\r");
        joinedString = joinedString.Replace("\n ", "\n");
        return joinedString.Trim();
    }

    public static string MakeStringSingleLine(this string textToEdit)
    {
        if (!textToEdit.Contains('\n')
            && !textToEdit.Contains('\r'))
            return textToEdit;

        StringBuilder workingString = new(textToEdit);

        workingString.Replace("\r\n", " ");
        workingString.Replace(Environment.NewLine, " ");
        workingString.Replace('\n', ' ');
        workingString.Replace('\r', ' ');

        Regex regex = new("[ ]{2,}");
        string temp = regex.Replace(workingString.ToString(), " ");
        workingString.Clear();
        workingString.Append(temp);
        if (workingString[0] == ' ')
            workingString.Remove(0, 1);

        if (workingString[workingString.Length - 1] == ' ')
            workingString.Remove(workingString.Length - 1, 1);

        return workingString.ToString();
    }

    public static string ToCamel(this string stringToCamel)
    {
        string toReturn = string.Empty;
        bool isSpaceOrNewLine = true;

        foreach (char characterToCheck in stringToCamel)
        {
            if (isSpaceOrNewLine
                && char.IsLetter(characterToCheck))
            {
                isSpaceOrNewLine = false;
                toReturn += char.ToUpper(characterToCheck);
            }
            else
            {
                toReturn += characterToCheck;

                if (char.IsWhiteSpace(characterToCheck)
                    || char.IsPunctuation(characterToCheck)
                    || characterToCheck == '\n'
                    || characterToCheck == '\r')
                {
                    isSpaceOrNewLine = true;
                }
            }
        }
        return toReturn;
    }

    public static CurrentCase DetermineToggleCase(string textToModify)
    {
        if (string.IsNullOrWhiteSpace(textToModify))
            return CurrentCase.Unknown;

        bool isAllLower = true;
        bool isAllUpper = true;

        foreach (char letter in textToModify)
        {
            if (!char.IsLetter(letter))
                continue;

            if (char.IsLower(letter))
                isAllUpper = false;

            if (char.IsUpper(letter))
                isAllLower = false;
        }

        if (!isAllLower && isAllUpper)
            return CurrentCase.Upper;
        else if (!isAllUpper && isAllLower)
            return CurrentCase.Lower;

        return CurrentCase.Camel;
    }

    public enum CharType { Letter, Number, Space, Special, Other };

    public class CharRun
    {
        public CharType TypeOfChar { get; set; }
        public Char Character { get; set; }
        public int numberOfRun { get; set; }
    }

    public static string ReplaceReservedCharacters(this string stringToClean)
    {
        StringBuilder sb = new();
        sb.Append(stringToClean);

        foreach (Char reservedChar in ReservedChars)
            sb.Replace(reservedChar, '-');

        return Regex.Replace(sb.ToString(), @"-+", "-");
    }

    public static string EscapeSpecialRegexChars(this string stringToEscape, bool matchExactly)
    {
        StringBuilder sb = new();
        sb.Append(stringToEscape);

        foreach (char specialChar in specialCharList)
        {
            if (specialChar is '*' && !matchExactly)
                sb.Replace(specialChar.ToString(), $".{specialChar}");
            else
                sb.Replace(specialChar.ToString(), $"\\{specialChar}");
        }

        return sb.ToString();
    }

    public static string ExtractSimplePattern(this string stringToExtract)
    {
        List<CharRun> charRunList = new();

        foreach (char c in stringToExtract)
        {
            CharType thisCharType = CharType.Other;
            if (Char.IsWhiteSpace(c))
                thisCharType = CharType.Space;
            else if (specialCharList.Contains(c))
                thisCharType = CharType.Special;
            else if (Char.IsLetter(c))
                thisCharType = CharType.Letter;
            else if (Char.IsNumber(c))
                thisCharType = CharType.Number;

            if (thisCharType == charRunList.LastOrDefault()?.TypeOfChar)
            {
                if (thisCharType == CharType.Other)
                {
                    if (c == charRunList.Last().Character)
                        charRunList.Last().numberOfRun++;
                }
                else
                {
                    charRunList.Last().numberOfRun++;
                }
            }
            else
            {
                CharRun newRun = new()
                {
                    Character = c,
                    numberOfRun = 1,
                    TypeOfChar = thisCharType
                };
                charRunList.Add(newRun);
            }
        }

        StringBuilder sb = new();
        // sb.Append("(");

        foreach (CharRun ct in charRunList)
        {
            // append previous stuff to the string       
            switch (ct.TypeOfChar)
            {
                case CharType.Letter:
                    // sb.Append("\\w");
                    sb.Append("[A-z]");
                    break;
                case CharType.Number:
                    sb.Append("\\d");
                    break;
                case CharType.Space:
                    sb.Append("\\s");
                    break;
                case CharType.Special:
                    sb.Append($"(\\{ct.Character})");
                    break;
                default:
                    sb.Append(ct.Character);
                    break;
            }

            if (ct.numberOfRun > 1)
            {
                sb.Append('{').Append(ct.numberOfRun).Append('}');
            }
        }

        return sb.ToString().ShortenRegexPattern();
    }

    private static string ShortenRegexPattern(this string pattern)
    {
        // Go through the pattern look for larger repeating sections
        string originalPattern = pattern;

        StringBuilder sb = new();

        List<string> possibleShortenedPatterns = new();
        possibleShortenedPatterns.Add(originalPattern);

        // only look for patterns which are 4 - length / 3 long.
        int maxRepSegCheckLen = originalPattern.Length / 3;

        for (int i = 4; i < maxRepSegCheckLen; i++)
        {
            List<string> chunkLists = Split(originalPattern, i).ToList();
            //int chunkID = 0;
            //while (chunkID * (i+ 1) < originalPattern.Length)
            //{
            //    string chunk = originalPattern.Substring(chunkID * i, i);
            //    chunkLists.Add(chunk);
            //    chunkID++;
            //}
            //if (chunkID * i < originalPattern.Length)
            //    chunkLists.Add(originalPattern.Substring(chunkID * i, originalPattern.Length - (chunkID * i)));
            if (originalPattern.Length % i != 0)
                chunkLists.Add(originalPattern[^(originalPattern.Length % i)..]);

            for (int j = 0; j < chunkLists.Count; j++)
            {
                int matchingRun = 1;
                while ((j + matchingRun) < chunkLists.Count
                    && chunkLists[j] == chunkLists[j + matchingRun])
                {
                    matchingRun++;
                }
                if (matchingRun > 0)
                {
                    sb.Append('(').Append(chunkLists[j]).Append("){").Append(matchingRun).Append('}');
                    j += matchingRun - 1;
                }
                else
                    sb.Append(chunkLists[j]);

            }

            possibleShortenedPatterns.Add(sb.ToString());
            sb.Clear();
        }

        possibleShortenedPatterns = possibleShortenedPatterns.OrderBy(p => p.Length).ToList();

        return possibleShortenedPatterns.First();
    }

    static IEnumerable<string> Split(string str, int chunkSize)
    {
        return Enumerable.Range(0, str.Length / chunkSize)
            .Select(i => str.Substring(i * chunkSize, chunkSize));
    }

    public static string UnstackStrings(this string stringToUnstack, int numberOfColumns)
    {
        StringBuilder sbUnstacked = new();

        stringToUnstack = Regex.Replace(stringToUnstack, @"(\r\n|\n|\r)", Environment.NewLine);

        string[] splitString = stringToUnstack.Split(new string[] { Environment.NewLine }, StringSplitOptions.TrimEntries);

        int columnIterator = 0;

        foreach (string line in splitString)
        {
            if (columnIterator == numberOfColumns)
            {
                sbUnstacked.Append(Environment.NewLine).Append(line);
                columnIterator = 0;
            }
            else
            {
                if (columnIterator != 0)
                    sbUnstacked.Append('\t');
                sbUnstacked.Append(line);
            }
            columnIterator++;
        }

        return sbUnstacked.ToString();
    }

    public static string UnstackGroups(this string stringGroupedToUnstack, int numberOfRows)
    {
        StringBuilder sbUnstacked = new();

        stringGroupedToUnstack = Regex.Replace(stringGroupedToUnstack, @"(\r\n|\n|\r)", Environment.NewLine);

        string[] splitInputString = stringGroupedToUnstack.Split(new string[] { Environment.NewLine }, StringSplitOptions.TrimEntries);

        int numberOfColumns = splitInputString.Length / numberOfRows;

        for (int j = 0; j < numberOfRows; j++)
        {
            if (j != 0)
                sbUnstacked.Append(Environment.NewLine);

            for (int i = 0; i < numberOfColumns; i++)
            {
                int lineNumberToAppend = j + (i * numberOfRows);

                if (lineNumberToAppend - 1 > splitInputString.Length)
                    break;

                if (i != 0)
                    sbUnstacked.Append('\t');

                sbUnstacked.Append(splitInputString[lineNumberToAppend]);
            }
        }

        return sbUnstacked.ToString();
    }

    public static string RemoveDuplicateLines(this string stringToDeduplicate)
    {
        string[] splitString = stringToDeduplicate.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.TrimEntries);
        List<string> uniqueLines = new();

        foreach (string originalLine in splitString)
            if (!uniqueLines.Contains(originalLine))
                uniqueLines.Add(originalLine);

        return string.Join(Environment.NewLine, uniqueLines.ToArray());
    }

    public static string RemoveAllInstancesOf(this string stringToBeEdited, string stringToRemove)
    {
        Regex regex = new(stringToRemove.EscapeSpecialRegexChars(false));
        return regex.Replace(stringToBeEdited, "");
    }

    public static string RemoveFromEachLine(this string stringToEdit, int numberOfChars, SpotInLine spotInLine)
    {
        string[] splitString = stringToEdit.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);

        StringBuilder sb = new();
        foreach (string line in splitString)
        {
            int lineLength = line.Length;
            if (lineLength <= numberOfChars)
            {
                sb.AppendLine();
                continue;
            }

            switch (spotInLine)
            {
                case SpotInLine.Beginning:
                    sb.AppendLine(line.Substring(numberOfChars));
                    break;
                case SpotInLine.End:
                    sb.AppendLine(line.Substring(0, lineLength - numberOfChars));
                    break;
                default:
                    break;
            }
        }

        return sb.ToString();
    }

    public static string AddCharsToEachLine(this string stringToEdit, string stringToAdd, SpotInLine spotInLine)
    {
        string[] splitString = stringToEdit.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);

        if (splitString.Length > 1)
            if (splitString.LastOrDefault() == "")
                Array.Resize(ref splitString, splitString.Length - 1);

        StringBuilder sb = new();
        foreach (string line in splitString)
        {
            switch (spotInLine)
            {
                case SpotInLine.Beginning:
                    sb.AppendLine(stringToAdd + line);
                    break;
                case SpotInLine.End:
                    sb.AppendLine(line + stringToAdd);
                    break;
                default:
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    public static string LimitCharactersPerLine(this string stringToEdit, int characterLimit, SpotInLine spotInLine)
    {
        string[] splitString = stringToEdit.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);
        StringBuilder returnStringBuilder = new();
        foreach (string line in splitString)
        {
            int lineLimit = Math.Clamp(characterLimit, 0, line.Length);
            if (line.Length < lineLimit)
            {
                returnStringBuilder.AppendLine(line);
                continue;
            }

            if (spotInLine== SpotInLine.Beginning)
                returnStringBuilder.AppendLine(line.Substring(0, lineLimit));
            else
                returnStringBuilder.AppendLine(line.Substring(line.Length - (lineLimit), lineLimit));
        }

        return returnStringBuilder.ToString().Trim();
    }

    public static bool IsValidEmailAddress(this string input)
    {
        // Generated from ChatGPT
        // Use a regular expression to match the input against a pattern for a valid email address.
        Regex regex = new(@"^[\w!#$%&'*+\-/=?\^_`{|}~]+(\.[\w!#$%&'*+\-/=?\^_`{|}~]+)*" + "@" + @"((([\-\w]+\.)+[a-zA-Z]{2,4})|(([0-9]{1,3}\.){3}[0-9]{1,3}))$");
        return regex.IsMatch(input);
    }

    public static bool IsBasicLatin(this char c)
    {
        // Basic Latin characters are those with Unicode code points
        // in the range U+0000 to U+007F (inclusive)
        return c >= '\u0000' && c <= '\u007F';
    }

    public static string GetCharactersToLeftOfNewLine(ref string mainString, int index, int numberOfCharacters)
    {
        int newLineIndex = GetNewLineIndexToLeft(ref mainString, index);

        if (newLineIndex < 1)
            return mainString.Substring(0, index);

        newLineIndex++;

        if (newLineIndex > mainString.Length)
            return mainString.Substring(mainString.Length, 0);

        if (index - newLineIndex < 0)
            return mainString.Substring(newLineIndex, 0);

        if (index - newLineIndex < numberOfCharacters)
            return string.Concat("...", mainString.AsSpan(newLineIndex, index - newLineIndex));

        return string.Concat("...", mainString.AsSpan(index - numberOfCharacters, numberOfCharacters));
    }

    public static string GetCharactersToRightOfNewLine(ref string mainString, int index, int numberOfCharacters)
    {
        int newLineIndex = GetNewLineIndexToRight(ref mainString, index);
        if (newLineIndex < 1)
            return mainString.Substring(index);

        if (newLineIndex - index > numberOfCharacters)
            return string.Concat(mainString.AsSpan(index, numberOfCharacters), "...");

        if (newLineIndex == mainString.Length)
            return mainString.Substring(index);

        return string.Concat(mainString.AsSpan(index, newLineIndex - index), "...");
    }

    public static int GetNewLineIndexToLeft(ref string mainString, int index)
    {
        char newLineChar = Environment.NewLine.ToArray().Last();

        int newLineIndex = index;
        while (newLineIndex > 0 && newLineIndex < mainString.Length && mainString[newLineIndex] != newLineChar)
            newLineIndex--;

        return newLineIndex;
    }

    public static int GetNewLineIndexToRight(ref string mainString, int index)
    {
        char newLineChar = Environment.NewLine.ToArray().First();

        int newLineIndex = index;
        while (newLineIndex < mainString.Length && mainString[newLineIndex] != newLineChar)
            newLineIndex++;

        return newLineIndex;
    }

    public static bool EndsWithNewline(this string s)
    {
        return Regex.IsMatch(s, @"\n$");
    }
}
