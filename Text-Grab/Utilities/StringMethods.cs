using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            fixToNumbers = fixToNumbers.Replace('l', '1');
            fixToNumbers = fixToNumbers.Replace('Q', '0');

            return fixToNumbers;
        }

        public static string TryFixNumberLetterErrors(this string stringToFix)
        {
            int totalNumbers = 0;

            foreach (char charFromString in stringToFix)
            {
                if (char.IsNumber(charFromString))
                    totalNumbers++;
            }

            float fractionNumber = totalNumbers / (float)stringToFix.Length;

            if (fractionNumber > 0.6)
                stringToFix = stringToFix.TryFixToNumbers();
            else
                stringToFix = stringToFix.TryFixToLetters();

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
    }
}
