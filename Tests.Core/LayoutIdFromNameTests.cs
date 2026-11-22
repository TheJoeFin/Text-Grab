using Text_Grab.Core.Layouts;

namespace Text_Grab.Tests.Core;

/// <summary>The ID "Save current layout as..." gives a layout from the name a person typed (v5 E6 <c>picker</c>).</summary>
public class LayoutIdFromNameTests
{
    [Theory]
    [InlineData("My layout", "my-layout")]
    [InlineData("  Wide  Calc!! ", "wide-calc")]
    [InlineData("Ünïcode & more", "n-code-more")]
    [InlineData("!!!", "layout")]
    [InlineData("", "layout")]
    [InlineData("con", "con-2")]
    public void TheNameBecomesAValidId(string name, string expected)
    {
        string id = LayoutIds.FromName(name, _ => false);

        Assert.True(LayoutIds.IsValid(id));
        Assert.Equal(expected, id);
    }

    [Fact]
    public void ATakenId_IsNumbered()
    {
        HashSet<string> taken = ["notes", "notes-2"];

        Assert.Equal("notes-3", LayoutIds.FromName("Notes", taken.Contains));
    }

    [Fact]
    public void ABuiltInName_IsNeverReturned()
        => Assert.Equal("classic-2", LayoutIds.FromName("Classic", BuiltInLayouts.IsBuiltIn));

    [Fact]
    public void ALongName_IsCutToALegalId()
        => Assert.True(LayoutIds.IsValid(LayoutIds.FromName(new string('a', 500), _ => false)));
}
