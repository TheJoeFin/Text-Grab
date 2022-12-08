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
    [InlineData("Hello World!", @"[A-z]{5}\s[A-z]{5}!")]
    [InlineData("123-555-6789", @"\d{3}-\d{3}-\d{4}")]
    [InlineData("(123)-555-6789", @"(\()\d{3}(\))-\d{3}-\d{4}")]
    [InlineData("Abc123456-99", @"[A-z]{3}\d{6}-\d{2}")]
    public void ExtractSimplePatternFromEachString(string inputString, string expectedString)
    {
        string actualString = inputString.ExtractSimplePattern();
        Assert.Equal(expectedString, actualString);
    }
}