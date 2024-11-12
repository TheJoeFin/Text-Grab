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

    private string actualGuids = """
        97a56312-d8e8-4ca5-87fa-18e35266d31e
        bdc5a5f2-d6ff-403d-a632-f9006387e149
        aeef14aa-9aff-4f0d-8ca5-e5df1b399c20
        c702f24c-e51b-4ebd-bb45-56df08266e80
        a87f9201-a046-425d-b92b-b488667a5d92
        bc656414-4f2a-4219-b763-810632a535e2
        11c5ecc4-0c0a-4606-a54f-16df976637d1
        5cec5cc9-782d-47aa-bff3-c84e13a81604
        8501db7b-ee04-4fb2-8516-f5e2f0bc71bf
        8da03c16-6d3f-4750-831b-c3866af85551
        03d82c33-489c-41b2-8222-cc489d00b1bf
        edf3b5ee-658e-41ea-8f7a-494a07beb322
        4418874a-30c7-4a16-aba5-0f2a0c49b4f9
        d4144186-4fad-40a0-bda9-7e3a2ea58a48
        486d81d0-0d56-466b-856c-0bc37e897b7b
        935155d5-1a96-4901-8b7d-23854fceb32d
        ff826fac-d166-441e-8040-05218989e805
        0a4ed755-f236-4e10-8b0b-592a527bb560
        9be83ad8-5e2d-4e37-a9f5-9b728cd9b934
        926ef504-264d-4762-b781-8813156eaa86
        """;


    [Theory]
    [InlineData("g7a56312-d8e8-4ca5-87fa-18e3S266d3le", "97a56312-d8e8-4ca5-87fa-18e35266d31e")]
    [InlineData("g7a56312-d8e 8-4ca5-87fa-18e3S2 66d3le", "97a56312-d8e8-4ca5-87fa-18e35266d31e")]
    [InlineData("g7a56312-\r\nd8e8\r\n-4ca5-87fa-18e3S266d3le", "97a56312-d8e8-4ca5-87fa-18e35266d31e")]
    public void TestGuidCorrections(string input, string expected)
    {
        Assert.Equal(expected, input.CorrectCommonGuidErrors());
    }
}
