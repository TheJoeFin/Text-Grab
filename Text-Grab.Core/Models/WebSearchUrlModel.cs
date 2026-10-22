namespace Text_Grab.Models;

/// <summary>
/// A single named web-search endpoint (e.g. "Google" -&gt; "https://www.google.com/search?q=").
///
/// Settings-backed catalog loading/saving and default-searcher tracking depend on
/// <c>AppUtilities.TextGrabSettings</c>/<c>TextGrabSettingsService</c>, which only exist in the
/// app, so they live in the app-side <see cref="Text_Grab.Models.WebSearchUrlCatalog"/> instead -
/// the same split shape as <c>PatternItem</c>/<c>PatternItemCatalog</c> in <c>e677b54</c>.
/// </summary>
public record WebSearchUrlModel
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public override string ToString() => Name;
}
