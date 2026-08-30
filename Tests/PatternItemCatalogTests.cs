using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

// App-coupled half of the original Tests/PatternExecutorTests.cs (batch 7a): PatternItemCatalog
// (Text-Grab/Models/PatternItemCatalog.cs) reads settings and stays app-side per e677b54, so
// these three tests could not follow PatternExecutorTests to Tests.Core.
public class PatternItemCatalogTests
{
    [Fact]
    public void GetAll_ListsSavedRegexesBeforeRecognizers()
    {
        IReadOnlyList<PatternItem> all = PatternItemCatalog.GetAll();

        int firstRecognizer = -1;
        int lastSaved = -1;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Kind == PatternKind.Recognizer && firstRecognizer < 0)
                firstRecognizer = i;
            if (all[i].Kind == PatternKind.SavedRegex)
                lastSaved = i;
        }

        Assert.True(firstRecognizer >= 0, "expected at least one recognizer item");
        Assert.True(lastSaved < firstRecognizer, "all saved regexes should precede recognizers");
    }

    [Fact]
    public void GetAll_IncludesEveryRecognizerWithSmartGroup()
    {
        List<PatternItem> recognizers = [.. PatternItemCatalog.GetAll().Where(p => p.Kind == PatternKind.Recognizer)];

        Assert.Equal(BuiltInRecognizer.GetAll().Count, recognizers.Count);
        Assert.All(recognizers, p => Assert.Equal(PatternItem.SmartGroup, p.GroupLabel));
    }

    [Fact]
    public void GetByName_FindsRecognizer_CaseInsensitive()
    {
        PatternItem? email = PatternItemCatalog.GetByName("EMAIL");

        Assert.NotNull(email);
        Assert.Equal(PatternKind.Recognizer, email!.Kind);
    }
}
