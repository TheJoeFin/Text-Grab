using System;
using System.Collections.Generic;
using System.Text;

namespace Text_Grab
{
    public static class StringExtensions
    {
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
