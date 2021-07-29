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
    }
}
