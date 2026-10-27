using System.Windows.Controls;
using System.Windows.Media;
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

    [WpfFact]
    public void SetAsBarcode_MarksOnlySupportedLinks()
    {
        foreach (string target in new[] { @"\\192.0.2.5\share\evil.jpg", "file://example.invalid/share/evil.jpg", "ms-settings:display" })
        {
            WordBorder wordBorder = new() { Word = target };
            wordBorder.SetAsBarcode();

            TextBox textBox = Assert.IsType<TextBox>(wordBorder.FindName("EditWordTextBox"));
            Assert.NotEqual(Colors.Blue, (textBox.Background as SolidColorBrush)?.Color);
        }

        foreach (string target in new[] { "https://example.invalid/", "mailto:user@example.invalid", "tel:+15551234567" })
        {
            WordBorder supportedWordBorder = new() { Word = target };
            supportedWordBorder.SetAsBarcode();

            TextBox supportedTextBox = Assert.IsType<TextBox>(supportedWordBorder.FindName("EditWordTextBox"));
            Assert.Equal(Colors.Blue, Assert.IsType<SolidColorBrush>(supportedTextBox.Background).Color);
        }
    }
}
