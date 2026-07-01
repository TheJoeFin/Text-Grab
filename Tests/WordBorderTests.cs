using Text_Grab.Controls;

namespace Tests;

public class WordBorderTests
{
    [WpfFact]
    public void ParagraphDisplayText_KeepsLogicalWordSingleLine()
    {
        WordBorder wordBorder = new()
        {
            KeepSingleLineOutput = true,
            DisplayLineHeight = 18,
            DisplayText = $"Static cling{Environment.NewLine}is useful"
        };

        Assert.Equal("Static cling is useful", wordBorder.Word);
        Assert.Equal($"Static cling{Environment.NewLine}is useful", wordBorder.DisplayText);
        Assert.True(wordBorder.KeepSingleLineOutput);
    }
}
