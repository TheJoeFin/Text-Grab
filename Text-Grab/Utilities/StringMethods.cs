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
        fixToLetters = fixToLetters.Replace('0', 'o');
        fixToLetters = fixToLetters.Replace('4', 'h');
        fixToLetters = fixToLetters.Replace('9', 'g');
        fixToLetters = fixToLetters.Replace('1', 'l');

        return fixToLetters;
    }

    public static string TryFixToNumbers(this string fixToNumbers)
    {

        fixToNumbers = fixToNumbers.Replace('o', '0');
        fixToNumbers = fixToNumbers.Replace('O', '0');
        fixToNumbers = fixToNumbers.Replace('Q', '0');
        fixToNumbers = fixToNumbers.Replace('c', '0');
        fixToNumbers = fixToNumbers.Replace('C', '0');
        fixToNumbers = fixToNumbers.Replace('i', '1');
        fixToNumbers = fixToNumbers.Replace('I', '1');
        fixToNumbers = fixToNumbers.Replace('l', '1');
        fixToNumbers = fixToNumbers.Replace('g', '9');

        return fixToNumbers;
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
        StringBuilder endingNewLines = new();

        for (int i = textToEdit.Length - 1; i >= 0; i--)
        {
            if (textToEdit[i] == '\n'
                || textToEdit[i] == '\r')
                endingNewLines.Insert(0, textToEdit[i]);
            else
                break;
        }

        StringBuilder workingString = new();
        workingString.Append(textToEdit);

        workingString.Replace("\r\n", " ");
        workingString.Replace(Environment.NewLine, " ");
        workingString.Replace('\n', ' ');
        workingString.Replace('\r', ' ');

        Regex regex = new("[ ]{2,}");
        string temp = regex.Replace(workingString.ToString(), " ").Trim();
        workingString.Clear();
        workingString.Append(temp);

        workingString.Append(endingNewLines);

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
        bool isAllLower = true;
        bool isAllUpper = true;

        foreach (char letter in textToModify)
        {
            if (char.IsLower(letter))
            {
                isAllUpper = false;
            }
            if (char.IsUpper(letter))
            {
                isAllLower = false;
            }
        }

        if (!isAllLower
            && isAllUpper)
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
        {
            sb.Replace(reservedChar, '-');
        }

        return sb.ToString();
    }

    public static string EscapeSpecialRegexChars(this string stringToEscape)
    {
        StringBuilder sb = new();
        sb.Append(stringToEscape);

        foreach (char specialChar in specialCharList)
        {
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
        // sb.Append(")");
        return sb.ToString();
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
        Regex regex = new(stringToRemove);
        return regex.Replace(stringToBeEdited, "");
    }

    public static void ReverseWordsForRightToLeft(StringBuilder text)
    {
        string[] textListLines = text.ToString().Split(new char[] { '\n', '\r' });
        Regex regexSpaceJoiningWord = new(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}");

        _ = text.Clear();
        foreach (string textLine in textListLines)
        {
            bool firstWord = true;
            bool isPrevWordSpaceJoining = false;
            List<string> wordArray = textLine.Split().ToList();
            wordArray.Reverse();

            foreach (string wordText in wordArray)
            {
                bool isThisWordSpaceJoining = regexSpaceJoiningWord.IsMatch(wordText);

                if (firstWord || (!isThisWordSpaceJoining && !isPrevWordSpaceJoining))
                    _ = text.Append(wordText);
                else
                    _ = text.Append(' ').Append(wordText);

                firstWord = false;
                isPrevWordSpaceJoining = isThisWordSpaceJoining;
            }

            if (textLine.Length > 0)
                _ = text.Append(Environment.NewLine);
        }
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
}
