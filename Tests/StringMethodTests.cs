using System.Text;
using Text_Grab;
using Text_Grab.Utilities;

namespace Tests;

public class StringMethodTests
{
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

    private static string multiLineInput = @"Hello this is lots 
of text which has several lines
and some spaces at the ends of line 
to throw off any easy check";

    [Theory]
    [InlineData(15, "lots")]
    [InlineData(20, "of")]
    [InlineData(51, "lines")]
    [InlineData(114, "check")]
    [InlineData(0, "Hello")]
    [InlineData(1000, "check")]
    [InlineData(-10, "Hello")]
    [InlineData(-1, "Hello")]
    public void ReturnWordAtCursorWithNewLines(int cursorPosition, string expectedWord)
    {
        // Given
        string actualWord = multiLineInput.GetWordAtCursorPosition(cursorPosition);

        // Then
        Assert.Equal(expectedWord, actualWord);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Hello, world! 0123456789", "Hello, world! ol23h5678g")]
    [InlineData("Foo 4r b4r", "Foo hr bhr")]
    [InlineData("B4zz 9zzl3", "Bhzz gzzl3")]
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
    [InlineData("Hello, world! 0123456789", "He110, w0r1d! 0123456789")]
    [InlineData("Foo 4r b4r", "F00 4r b4r")]
    [InlineData("B4zz 9zzl3", "B4zz 9zz13")]
    [InlineData("abcdefghijklmnop", "ab0def9h1jk1mn0p")]
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
    [InlineData("", @"")]
    [InlineData("Hello World!", @"[A-z]{5}\s[A-z]{5}!")]
    [InlineData("123-555-6789", @"\d{3}-\d{3}-\d{4}")]
    [InlineData("(123)-555-6789", @"(\()\d{3}(\))-\d{3}-\d{4}")]
    [InlineData("Abc123456-99", @"[A-z]{3}\d{6}-\d{2}")]
    [InlineData("ab12ab12ab12ab12ab12", @"([A-z]{2}\d{2}){5}")]
    public void ExtractSimplePatternFromEachString(string inputString, string expectedString)
    {
        string actualString = inputString.ExtractSimplePattern();
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
general ke", 10)]
    [InlineData(@"hello there
general kenobi", @"hello there
general kenobi", 100)]
    [InlineData(@"hello there
general kenobi
you are a bold one!", @"hello
gener
you a", 5)]
    [InlineData(@"hello there
general kenobi
you are a bold one!", @"", 0)]
    public void TestLimitEachLine(string inputString, string expected, int charLimit)
    {
        Assert.Equal(expected, inputString.LimitCharactersPerLine(charLimit));
    }
}