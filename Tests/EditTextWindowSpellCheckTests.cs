using Text_Grab;

namespace Tests;

public class EditTextWindowSpellCheckTests
{
    [Fact]
    public void NormalSentence_SpellCheckEnabled()
    {
        string text = "The quick brown fox jumps over the lazy dog.";
        Assert.True(EditTextWindow.ShouldEnableSpellCheck(text));
    }

    [Fact]
    public void EmptyString_SpellCheckEnabled()
    {
        Assert.True(EditTextWindow.ShouldEnableSpellCheck(string.Empty));
    }

    [Fact]
    public void TextExceedsLengthThreshold_SpellCheckDisabled()
    {
        string longText = new string('a', 10_001);
        Assert.False(EditTextWindow.ShouldEnableSpellCheck(longText));
    }

    [Fact]
    public void TwoLongWords_SpellCheckEnabled()
    {
        // Only 2 long words — below the threshold of 3
        string text = "normal words then SomeVeryLongManifestTokenThatIsOver25Chars and AnotherReallyLongTokenHere123 end";
        Assert.True(EditTextWindow.ShouldEnableSpellCheck(text));
    }

    [Fact]
    public void ThreeLongWords_SpellCheckDisabled()
    {
        // 3 words each >= 25 chars → should disable spell check
        string text = "Microsoft.Windows.AppManifest.Version1234 " +
                      "com.example.application.package.name.v2 " +
                      "SomeGuidLike_1234567890abcdef1234 " +
                      "normal short words";
        Assert.False(EditTextWindow.ShouldEnableSpellCheck(text));
    }

    [Fact]
    public void AppManifestLikeContent_SpellCheckDisabled()
    {
        string manifest = """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
                     IgnorableNamespaces="uap mp">
              <Identity Name="Microsoft.TextGrab" Publisher="CN=TheJoeFin" Version="4.0.0.0" />
            </Package>
            """;
        Assert.False(EditTextWindow.ShouldEnableSpellCheck(manifest));
    }

    [Fact]
    public void WordExactlyAtLongWordLength_NotCountedAsLong()
    {
        // Word of exactly 24 chars should NOT count as "very long"
        string word24 = new string('x', 24);
        string word25 = new string('y', 25);
        // Two words of 24 + one of 25 = only one long word → still enabled
        string text = $"{word24} {word24} {word25} normal text";
        Assert.True(EditTextWindow.ShouldEnableSpellCheck(text));
    }

    [Fact]
    public void GuidTokens_SpellCheckDisabled()
    {
        // GUIDs are 32+ chars without hyphens when copy-pasted from some apps
        string text = "id=a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4 " +
                      "token=f6e5d4c3b2a1f6e5d4c3b2a1f6e5d4c3b2 " +
                      "hash=1234567890abcdef1234567890abcdef12";
        Assert.False(EditTextWindow.ShouldEnableSpellCheck(text));
    }

    [Fact]
    public void AlwaysOnMode_EnabledEvenForContentAutoWouldReject()
    {
        // Content Auto mode would disable (3+ long tokens), but Always On forces it on
        string text = "Microsoft.Windows.AppManifest.Version1234 " +
                      "com.example.application.package.name.v2 " +
                      "SomeGuidLike_1234567890abcdef1234";
        Assert.False(EditTextWindow.ShouldEnableSpellCheck(SpellCheckMode.Auto, text));
        Assert.True(EditTextWindow.ShouldEnableSpellCheck(SpellCheckMode.AlwaysOn, text));
    }

    [Fact]
    public void OffMode_DisabledEvenForNormalText()
    {
        string text = "The quick brown fox jumps over the lazy dog.";
        Assert.True(EditTextWindow.ShouldEnableSpellCheck(SpellCheckMode.Auto, text));
        Assert.False(EditTextWindow.ShouldEnableSpellCheck(SpellCheckMode.Off, text));
    }

    [Fact]
    public void AutoMode_MatchesContentHeuristic()
    {
        string normal = "The quick brown fox.";
        string longTokens = new string('a', 10_001);
        Assert.True(EditTextWindow.ShouldEnableSpellCheck(SpellCheckMode.Auto, normal));
        Assert.False(EditTextWindow.ShouldEnableSpellCheck(SpellCheckMode.Auto, longTokens));
    }
}
