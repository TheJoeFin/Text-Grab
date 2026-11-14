using Text_Grab.Core.Editing;

namespace Tests;

public class FoldStateTests
{
    private const string Sample =
        "class A\n{\n    void One()\n    {\n        x();\n    }\n\n    void Two()\n    {\n        y();\n    }\n}\n";

    private static int Offset(string text, string marker) => text.IndexOf(marker, StringComparison.Ordinal);

    [Fact]
    public void UnchangedText_RefoldsTheSameRegions()
    {
        int one = Offset(Sample, "    {\n        x");
        int two = Offset(Sample, "    {\n        y");
        int whole = Offset(Sample, "{\n    void One");

        FoldSnapshot captured = FoldState.Capture(Sample, [one, two]);

        IReadOnlyList<int> matched = FoldState.Match(captured, Sample, [whole, one, two]);

        Assert.Equal([one, two], matched.Order());
    }

    [Fact]
    public void EditedText_KeepsFoldsWhoseStartLineIsUnchanged()
    {
        int one = Offset(Sample, "    {\n        x");
        int two = Offset(Sample, "    {\n        y");
        FoldSnapshot captured = FoldState.Capture(Sample, [one, two]);

        // A new method added above shifts every region down by several lines.
        string edited = Sample.Replace("class A\n{\n", "class A\n{\n    int a;\n    int b;\n    int c;\n");
        int editedOne = Offset(edited, "    {\n        x");
        int editedTwo = Offset(edited, "    {\n        y");

        IReadOnlyList<int> matched = FoldState.Match(captured, edited, [editedOne, editedTwo]);

        Assert.Equal([editedOne, editedTwo], matched.Order());
    }

    [Fact]
    public void EditedText_DropsAFoldWhoseStartLineChanged()
    {
        int one = Offset(Sample, "    {\n        x");
        int two = Offset(Sample, "    {\n        y");
        FoldSnapshot captured = FoldState.Capture(Sample, [one, two]);

        string edited = Sample.Replace("void Two()", "void Renamed()");
        int editedOne = Offset(edited, "    {\n        x");
        int editedTwo = Offset(edited, "    {\n        y");

        IReadOnlyList<int> matched = FoldState.Match(captured, edited, [editedOne, editedTwo]);

        Assert.Equal([editedOne], matched);
    }

    [Fact]
    public void BareBraceLines_AreAnchoredToTheSignatureAboveThem()
    {
        // Two identical "{" lines under different signatures: only the one under "One()" was folded,
        // and it's still found after the methods swap places.
        string text = "void One()\n{\n    a();\n}\nvoid Two()\n{\n    b();\n}\n";
        FoldSnapshot captured = FoldState.Capture(text, [Offset(text, "{\n    a")]);

        string edited = "void Two()\n{\n    b();\n}\n" + text.Replace("void Two()\n{\n    b();\n}\n", "");
        IReadOnlyList<int> matched = FoldState.Match(captured, edited, [Offset(edited, "{\n    b"), Offset(edited, "{\n    a")]);

        Assert.Equal([Offset(edited, "{\n    a")], matched);
    }

    [Fact]
    public void NothingCollapsed_MatchesNothing()
    {
        FoldSnapshot captured = FoldState.Capture(Sample, []);

        Assert.Empty(FoldState.Match(captured, Sample, [Offset(Sample, "{")]));
    }

    [Fact]
    public void Capture_LimitsHowManyRegionsAreRemembered()
    {
        string text = string.Concat(Enumerable.Range(0, 500).Select(i => $"line {i}\n"));
        int[] offsets = [.. Enumerable.Range(0, 500).Select(i => Offset(text, $"line {i}\n"))];

        Assert.Equal(FoldState.MaxMarks, FoldState.Capture(text, offsets).Marks.Count);
    }
}
