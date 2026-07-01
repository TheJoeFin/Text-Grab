using System;
using System.Collections.Generic;
using System.Text;
using Text_Grab;
using Text_Grab.Utilities;

namespace Tests;

public class StringMethodTests
{
    private sealed class PredictableRandom(params int[] values) : Random
    {
        private readonly Queue<int> values = new(values);

        public override int Next(int maxValue)
        {
            Assert.NotEmpty(values);

            int nextValue = values.Dequeue();
            Assert.InRange(nextValue, 0, maxValue - 1);
            return nextValue;
        }
    }

    [Fact]
    public void MakeMultiLineStringSingleLine()
    {
        string bodyOfText = @"

This has
multiple
lines


";

        string lineOfText = "This has multiple lines";
        Assert.Equal(lineOfText, bodyOfText.MakeStringSingleLine());
    }

    [Fact]
    public void MakeStringSingleLine_NewlineOnly_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, Environment.NewLine.MakeStringSingleLine());
    }

    [Fact]
    public void JoinLines_WithJoiningTextAndAffixes_AsExpected()
    {
        string input = $"alpha{Environment.NewLine}beta{Environment.NewLine}gamma";

        string actual = input.JoinLines(", ", trimLineBeforeJoining: false, "[", "]");

        Assert.Equal("[alpha, beta, gamma]", actual);
    }

    [Fact]
    public void JoinLines_TrimEachLineBeforeJoining_AsExpected()
    {
        string input = " alpha \r\n\tbeta\t\r\ngamma  ";

        string actual = input.JoinLines(" | ", trimLineBeforeJoining: true);

        Assert.Equal("alpha | beta | gamma", actual);
    }

    [Fact]
    public void JoinLines_TrailingLineBreak_DoesNotAddExtraJoiningText()
    {
        const string input = "alpha\nbeta\n";

        string actual = input.JoinLines(", ", trimLineBeforeJoining: false);

        Assert.Equal("alpha, beta", actual);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("is", "This is test string data")]
    [InlineData("and", "Hello and How do you do?")]
    [InlineData("a", "What a wonderful world!")]
    [InlineData("me", "Take me out to the ballgame")]
    public void ReturnWordAtCursorPositionSix(string expectedWord, string fullLine)
    {
        (int start, int length) = fullLine.CursorWordBoundaries(6);
        string singleWordAtSix = fullLine.Substring(start, length);
        Assert.Equal(expectedWord, singleWordAtSix);
    }

    [Theory]
    [InlineData("there", "hello there", 11)]
    [InlineData("world", "hello world", 10)]
    [InlineData("Alpha", "Alpha", 5)]
    [InlineData("hello", " hello", 0)]
    public void CursorWordBoundaries_ClampsEndOfTextToNearestWord(string expectedWord, string input, int cursorPosition)
    {
        (int start, int length) = input.CursorWordBoundaries(cursorPosition);

        Assert.Equal(expectedWord, input.Substring(start, length));
    }

    [Fact]
    public void CursorWordBoundaries_AllWhitespace_ReturnsEmptyRange()
    {
        const string input = "   ";

        (int start, int length) = input.CursorWordBoundaries(1);

        Assert.Equal(string.Empty, input.Substring(start, length));
    }

    private static string multiLineInput = @"Hello this is lots 
of text which has several lines
and some spaces at the ends of line 
to throw off any easy check";

    [Theory]
    [InlineData("Hello", "", " this ...")]
    [InlineData("lots", "Hello this is ", " ...")]
    [InlineData("of", "...", " text ...")]
    [InlineData("several", "...h has ", " lines...")]
    public void ReturnPreviewsFromWord(string firstWord, string expectedLeftPreview, string expectedRightPreview)
    {
        int length = firstWord.Length;
        int previewLength = 6;

        int cursorPosition = multiLineInput.IndexOf(firstWord);

        string PreviewLeft = StringMethods.GetCharactersToLeftOfNewLine(ref multiLineInput, cursorPosition, previewLength);
        string PreviewRight = StringMethods.GetCharactersToRightOfNewLine(ref multiLineInput, cursorPosition + length, previewLength);

        Assert.Equal(expectedLeftPreview, PreviewLeft);
        Assert.Equal(expectedRightPreview, PreviewRight);
    }

    [Theory]
    [InlineData(15, "lots")]
    [InlineData(20, "of")]
    [InlineData(51, "lines")]
    [InlineData(114, "check")]
    [InlineData(0, "Hello")]
    [InlineData(1000, "check")]
    [InlineData(-10, "Hello")]
    [InlineData(-1, "Hello")]
    [InlineData(10, "this")]
    public void ReturnWordAtCursorWithNewLines(int cursorPosition, string expectedWord)
    {
        // Given
        string actualWord = multiLineInput.GetWordAtCursorPosition(cursorPosition);

        // Then
        Assert.Equal(expectedWord, actualWord);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Hello, world! 0123456789", "Hello, world! olz3hSb7Bg")]
    [InlineData("Foo 4r b4r", "Foo hr bhr")]
    [InlineData("B4zz5 9zzl3", "BhzzS gzzl3")]
    [InlineData("abcdefghijklmnop", "abcdefghijklmnop")]
    public void TryFixToLetters_ReplacesDigitsWithLetters_AsExpected(string input, string expected)
    {
        // Act
        string result = input.TryFixToLetters();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("he11o there", "hello there")]
    [InlineData("my number is l23456789o", "my number is 1234567890")]
    public void TryFixNumOrLetters(string input, string expected)
    {
        string result = input.TryFixEveryWordLetterNumberErrors();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Hello, world! 0123456789", "4e110, w0r1d! 0123456789")]
    [InlineData("Foo 4r b4r", "F00 4r 64r")]
    [InlineData("B4zzS 9zzl3", "84225 92213")]
    [InlineData("abcdefghijklmnopqrs", "a60def941jk1mn0pqr5")]
    public void TryFixToLetters_ReplacesLettersWithDigits_AsExpected(string input, string expected)
    {
        // Act
        string result = input.TryFixToNumbers();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RemoveDuplicateLines_AsExpected()
    {
        // Given
        string inputString = @"This is a line
This is a line
This is a line
This is a line
Another Line
Another Line
This is a line";

        string expectedString = @"This is a line
Another Line";

        // When
        string actualString = inputString.RemoveDuplicateLines();

        // Then
        Assert.Equal(expectedString, actualString);
    }

    [Fact]
    public void ShuffleLines_UsesProvidedRandom()
    {
        string inputString = @"one
two
three
four";

        string actualString = inputString.ShuffleLines(new PredictableRandom(1, 1, 0));

        Assert.Equal(
            @"three
one
four
two",
            actualString);
    }

    [Fact]
    public void ShuffleLines_PreservesTrailingNewline()
    {
        string inputString = $"alpha{Environment.NewLine}beta{Environment.NewLine}";

        string actualString = inputString.ShuffleLines(new PredictableRandom(0));

        Assert.Equal($"beta{Environment.NewLine}alpha{Environment.NewLine}", actualString);
    }

    // { ' ', '"', '*', '/', ':', '<', '>', '?', '\\', '|', '+', ',', '.', ';', '=', '[', ']', '!', '@' }; 
    [Theory]
    [InlineData("", "")]
    [InlineData("A<>B<>C", "A-B-C")]
    [InlineData("abc+123/def:*", "abc-123-def-")]
    [InlineData("@TheJoeFin", "-TheJoeFin")]
    [InlineData("Hello World!", "Hello-World-")]
    [InlineData("Nothing", "Nothing")]
    [InlineData("   ", "-")]
    [InlineData("-----", "-")]
    public void ReplaceReservedCharacters(string inputString, string expectedString)
    {
        // When
        string actualString = inputString.ReplaceReservedCharacters();

        // Then
        Assert.Equal(expectedString, actualString);
    }

    [Theory]
    [InlineData("", @"", 3)]
    [InlineData("Hello World!", @"[A-Za-z]{5}\s[A-Za-z]{5}!", 3)]
    [InlineData("123-555-6789", @"\d{3}-\d{3}-\d{4}", 3)]
    [InlineData("(123)-555-6789", @"(\()\d{3}(\))-\d{3}-\d{4}", 3)]
    [InlineData("Abc123456-99", @"[A-Za-z]{3}\d{6}-\d{2}", 3)]
    [InlineData("ab12ab12ab12ab12ab12", @"([A-Za-z]{2}\d{2}){5}", 3)]
    // Precision level 0 tests (least precise - non-whitespace)
    [InlineData("Abc123", @"\S+", 0)]
    [InlineData("Hello World", @"\S+", 0)]
    // Precision level 1 tests (word characters)
    [InlineData("Abc123", @"\w+", 1)]
    [InlineData("Test456", @"\w+", 1)]
    // Precision level 2 tests (word characters with count)
    [InlineData("Abc123", @"\w{3}\w{3}", 2)]
    [InlineData("Hello", @"\w{5}", 2)]
    // Precision level 4 tests (individual character class per position with case variants)
    [InlineData("Abc", @"(?i)Abc", 4)]
    [InlineData("123", @"(?i)123", 4)]
    [InlineData("Test", @"(?i)Test", 4)]
    // Precision level 5 tests (exact escaped string - most precise)
    [InlineData("Abc123", @"Abc123", 5)]
    [InlineData("Test", @"Test", 5)]
    [InlineData("Hello World!", @"Hello\ World!", 5)]
    public void ExtractSimplePatternFromEachString(string inputString, string expectedString, int precisionLevel)
    {
        string actualString = inputString.ExtractSimplePattern(precisionLevel);
        Assert.Equal(expectedString, actualString);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("test@example.com", true)]
    [InlineData("test@example.co", true)]
    [InlineData("test@example.", false)]
    [InlineData("joe@TextGrab.net", true)]
    [InlineData("joe@Text Grab.net", false)]
    public void TestIsValidEmailAddress(string inputString, bool expectedIsValid)
    {
        Assert.Equal(expectedIsValid, inputString.IsValidEmailAddress());
    }

    [Fact]
    public void TestGetLineStartAndLength()
    {
        string inputString = @"Don't Forget to do
the method just the way
The quick brown fox
jumped over the lazy
brown dog";

        (int start, int length) = inputString.GetStartAndLengthOfLineAtPosition(20);
        string actualString = inputString.Substring(start, length);

        string expectedString = "the method just the way\r\n";

        Assert.Equal(expectedString, actualString);
    }

    [Fact]
    public void TestUnstackGroups()
    {
        string inputString = @"1
2
3
4
5
a
b
c
d
e
jan
feb
mar
apr
may";

        string acualString = inputString.UnstackGroups(5);

        string expectedString = @"1	a	jan
2	b	feb
3	c	mar
4	d	apr
5	e	may";

        Assert.Equal(expectedString, acualString);
    }

    [Fact]
    public void TestUnstackString()
    {
        string inputString = @"1
a
jan
2
b
feb
3
c
mar
4
d
apr
5
e
may";

        string acualString = inputString.UnstackStrings(3);

        string expectedString = @"1	a	jan
2	b	feb
3	c	mar
4	d	apr
5	e	may";

        Assert.Equal(expectedString, acualString);
    }

    [Theory]
    [InlineData("The quick brown fox", "fox", "The quick brown ")]
    [InlineData("jumped over over the lazy", "over", "jumped   the lazy")]
    [InlineData("brown dogs and what not", "o", "brwn dgs and what nt")]
    public void TestRemoveThisString(string inputString, string remove, string expected)
    {
        Assert.Equal(expected, inputString.RemoveAllInstancesOf(remove));
    }

    [Theory]
    [InlineData("The quick brown fox", "fox brown quick The\r\n")]
    [InlineData("jumped over the lazy", "lazy the over jumped\r\n")]
    [InlineData("brown dogs and what not", "not what and dogs brown\r\n")]
    [InlineData(@"brown dogs
and what not", @"dogs brown
not what and
")]
    public void TestReverseString(string inputString, string expected)
    {
        StringBuilder sb = new(inputString);
        sb.ReverseWordsForRightToLeft();
        Assert.Equal(expected, sb.ToString());
    }

    [Theory]
    [InlineData(@"hello there
general kenobi", @"lo there
eral kenobi
", 3, SpotInLine.Beginning)]
    [InlineData(@"hello there
general kenobi", @"hello th
general ken
", 3, SpotInLine.End)]
    [InlineData(@"hello there
general kenobi
you are a bold one!", @"hello th
general ken
you are a bold o
", 3, SpotInLine.End)]
    [InlineData(@"hello there
general kenobi
22
you are a bold one!", @"hello th
general ken

you are a bold o
", 3, SpotInLine.End)]
    public void TestRemoveFromEachLines(string inputString, string expected, int numberOfChars, SpotInLine spotInLine)
    {
        Assert.Equal(expected, inputString.RemoveFromEachLine(numberOfChars, spotInLine));
    }

    [Theory]
    [InlineData(@"hello there
general kenobi", @"Yep hello there
Yep general kenobi", "Yep ", SpotInLine.Beginning)]
    [InlineData(@"hello there
general kenobi", @"hello there Great
general kenobi Great", " Great", SpotInLine.End)]
    [InlineData(@"hello there
general kenobi
you are a bold one!", @"hello there Awesome
general kenobi Awesome
you are a bold one! Awesome", " Awesome", SpotInLine.End)]
    public void TestAddToEachLines(string inputString, string expected, string stringToAdd, SpotInLine spotInLine)
    {
        Assert.Equal(expected, inputString.AddCharsToEachLine(stringToAdd, spotInLine));
    }

    [Theory]
    [InlineData("AWESOME", CurrentCase.Upper)]
    [InlineData("awesome", CurrentCase.Lower)]
    [InlineData("Awesome", CurrentCase.Camel)]
    [InlineData("", CurrentCase.Unknown)]
    [InlineData("   ", CurrentCase.Unknown)]
    [InlineData("the case", CurrentCase.Lower)]
    [InlineData("THE CASE", CurrentCase.Upper)]
    [InlineData("The Case", CurrentCase.Camel)]
    public void TestDetermineToggleCase(string inputString, CurrentCase expectedCase)
    {
        Assert.Equal(expectedCase, StringMethods.DetermineToggleCase(inputString));
    }

    [Theory]
    [InlineData('A', true)]
    [InlineData('a', true)]
    [InlineData('b', true)]
    [InlineData('c', true)]
    [InlineData('C', true)]
    [InlineData('d', true)]
    [InlineData('z', true)]
    [InlineData('Z', true)]
    [InlineData('1', true)]
    [InlineData('4', true)]
    [InlineData('-', true)]
    [InlineData('*', true)]
    [InlineData('+', true)]
    [InlineData('%', true)]
    [InlineData('3', true)]
    [InlineData('|', true)]
    [InlineData('\r', true)]
    [InlineData('\n', true)]
    [InlineData('\t', true)]
    [InlineData('À', false)]
    [InlineData('Ü', false)]
    [InlineData('Ö', false)]
    [InlineData('Ç', false)]
    public void TestIsBasicLatin(char inputChar, bool isLatin)
    {
        Assert.Equal(isLatin, inputChar.IsBasicLatin());
    }

    [Theory]
    [InlineData("string to test", "string to test")]
    [InlineData("ABCDEФGHIJKLMNOПQЯSTUVWXYZ", "ABCDEOGHIJKLMNOnQRSTUVWXYZ")]
    [InlineData("HЭllΘ There! @$2890", "H3llO There! @$2890")]
    [InlineData("", "")]
    public void TestReplaceGreekAndCyrillic(string inputString, string expectedString)
    {
        Assert.Equal(expectedString, inputString.ReplaceGreekOrCyrillicWithLatin());
    }

    [Theory]
    [InlineData(@"hello there
general kenobi", @"hello ther
general ke", 10, SpotInLine.Beginning)]
    [InlineData(@"hello there
general kenobi", @"hello there
neral kenobi", 12, SpotInLine.End)]
    [InlineData(@"hello there
general kenobi", @"hello there
general kenobi", 100, SpotInLine.Beginning)]
    [InlineData(@"hello there
general kenobi", @"hello there
general kenobi", 100, SpotInLine.End)]
    [InlineData(@"hello there
general kenobi
you are a bold one!", @"hello
gener
you a", 5, SpotInLine.Beginning)]
    [InlineData(@"hello there
general kenobi
you are a bold one!", @"", 0, SpotInLine.Beginning)]
    [InlineData(@"hello there
general kenobi
you are a bold one!", @"", 0, SpotInLine.End)]
    public void TestLimitEachLine(string inputString, string expected, int charLimit, SpotInLine spotInLine)
    {
        Assert.Equal(expected, inputString.LimitCharactersPerLine(charLimit, spotInLine));
    }

    [Theory]
    [InlineData("g7a56312-d8e8-4ca5-87fa-18e3S266d3le", "97a56312-d8e8-4ca5-87fa-18e35266d31e")]
    [InlineData("g7a56312-d8e 8-4ca5-87fa-18e3S2 66d3le", "97a56312-d8e8-4ca5-87fa-18e35266d31e")]
    [InlineData("g7a56312-\r\nd8e8\r\n-4ca5-87fa-18e3S266d3le", "97a56312-d8e8-4ca5-87fa-18e35266d31e")]
    public void TestGuidCorrections(string input, string expected)
    {
        Assert.Equal(expected, input.CorrectCommonGuidErrors());
    }
}
