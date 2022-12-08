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

        string lineOfText = "This has multiple lines\r\n";
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

    [Fact]
    public void ReturnWordAtCursorWithNewLines()
    {
        // Given
        string multiLineInput = @"Hello this is lots 
of text which has several lines
and some spaces at the ends of line 
to throw off any easy check";

        // When
        string lotsString = multiLineInput.GetWordAtCursorPosition(15);
        string ofString = multiLineInput.GetWordAtCursorPosition(20);
        string linesString = multiLineInput.GetWordAtCursorPosition(51);
        string checkString = multiLineInput.GetWordAtCursorPosition(114);
        string HelloString = multiLineInput.GetWordAtCursorPosition(0);

        // Try some bad inputs, make sure they correct
        string checkStringBeyondLength = multiLineInput.GetWordAtCursorPosition(1000);
        string HelloStringNegativeInput = multiLineInput.GetWordAtCursorPosition(-10);

        // Then
        Assert.Equal("lots", lotsString);
        Assert.Equal("of", ofString);
        Assert.Equal("lines", linesString);
        Assert.Equal("check", checkString);
        Assert.Equal("Hello", HelloString);

        Assert.Equal("Hello", HelloStringNegativeInput);
        Assert.Equal("check", checkStringBeyondLength);
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
}