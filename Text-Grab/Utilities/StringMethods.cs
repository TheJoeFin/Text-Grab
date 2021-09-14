using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Text_Grab.Utilities
{
    public static class StringMethods
    {
        public static List<Char> specialCharList = new List<Char>()
                { '\\', ' ', '.', ',', '$', '^', '{', '[', '(', '|', ')', '*', '+', '?', '=' };

        public static List<Char> ReservedChars = new List<Char>()
        { ' ', '"', '*', '/', ':', '<', '>', '?', '\\', '|', '+', ',', '.', ';', '=', '[', ']', '!', '@' };

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
            fixToNumbers = fixToNumbers.Replace('g', '9');
            fixToNumbers = fixToNumbers.Replace('i', '1');
            fixToNumbers = fixToNumbers.Replace('I', '1');
            fixToNumbers = fixToNumbers.Replace('l', '1');
            fixToNumbers = fixToNumbers.Replace('Q', '0');

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
                Debug.WriteLine($"Tried to fix {stringToFix} to numbers");
            }
            else if (letterNumber > 0.6)
            {
                stringToFix = stringToFix.TryFixToLetters();
                Debug.WriteLine($"Tried to fix {stringToFix} to letters");
            }

            return stringToFix;
        }

        public static string TryFixEveryWordLetterNumberErrors(this string stringToFix)
        {
            List<string> listOfWords = stringToFix.Split(' ').ToList();
            List<string> fixedWords = new List<string>();

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
            StringBuilder endingNewLines = new StringBuilder();

            for (int i = textToEdit.Length - 1; i >= 0; i--)
            {
                if (textToEdit[i] == '\n'
                    || textToEdit[i] == '\r')
                    endingNewLines.Insert(0, textToEdit[i]);
                else
                    break;
            }

            StringBuilder workingString = new StringBuilder();
            workingString.Append(textToEdit);

            workingString.Replace("\r\n", " ");
            workingString.Replace(Environment.NewLine, " ");
            workingString.Replace('\n', ' ');
            workingString.Replace('\r', ' ');

            Regex regex = new Regex("[ ]{2,}");
            string temp = regex.Replace(workingString.ToString(), " ").Trim();
            workingString.Clear();
            workingString.Append(temp);

            workingString.Append(endingNewLines.ToString());

            return workingString.ToString();
        }

        public static string ToCamel(this string stringToCamel)
        {
            string toReturn = string.Empty;
            bool isSpaceOrNewLine = true;

            foreach (char characterToCheck in stringToCamel)
            {
                if (isSpaceOrNewLine == true
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

        public enum CharType { Letter, Number, Space, Special, Other };

        public class CharRun
        {
            public CharType TypeOfChar { get; set; }
            public Char Character { get; set; }
            public int numberOfRun { get; set; }
        }

        public static string ReplaceReservedCharacters(this string stringToClean)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(stringToClean);

            foreach (Char reservedChar in ReservedChars)
            {
                sb.Replace(reservedChar, '-');
            }

            return sb.ToString();
        }

        public static string EscapeSpecialRegexChars(this string stringToEscape)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(stringToEscape);

            foreach (char specialChar in specialCharList)
            {
                sb.Replace(specialChar.ToString(), $"\\{specialChar}");
            }

            return sb.ToString();
        }

        public static string ExtractSimplePattern(this string stringToExtract)
        {
            List<CharRun> charRunList = new List<CharRun>();

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
                    CharRun newRun = new CharRun()
                    {
                        Character = c,
                        numberOfRun = 1,
                        TypeOfChar = thisCharType
                    };
                    charRunList.Add(newRun);
                }
            }

            StringBuilder sb = new StringBuilder();
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
    }
}
