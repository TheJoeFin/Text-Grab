using System.Collections.Generic;
using System.Linq;
using Text_Grab.Utilities;

namespace Text_Grab.Models;

/// <summary>
/// Settings-backed catalog of <see cref="WebSearchUrlModel"/> web-search endpoints, plus which
/// one is the default. Split out from <see cref="WebSearchUrlModel"/> because it depends on
/// <see cref="AppUtilities.TextGrabSettings"/>/<see cref="AppUtilities.TextGrabSettingsService"/>,
/// which only exist in the app. Accessed through
/// <c>Singleton&lt;WebSearchUrlCatalog&gt;.Instance</c> so the cached list and default selection
/// persist across the call sites within a session, exactly as they did on the old
/// <c>WebSearchUrlModel</c> singleton instance.
/// </summary>
public class WebSearchUrlCatalog
{
    private WebSearchUrlModel? defaultSearcher;

    public WebSearchUrlModel DefaultSearcher
    {
        get
        {
            defaultSearcher ??= GetDefaultSearcher();
            return defaultSearcher;
        }
        set
        {
            defaultSearcher = value;
            SaveDefaultSearcher(defaultSearcher);
        }
    }

    private List<WebSearchUrlModel> webSearchers = [];

    public List<WebSearchUrlModel> WebSearchers
    {
        get
        {
            if (webSearchers.Count == 0)
                webSearchers = GetWebSearchUrls();

            return webSearchers;
        }
        set
        {
            webSearchers = value;
            SaveWebSearchUrls(webSearchers);
        }
    }

    private WebSearchUrlModel GetDefaultSearcher()
    {
        string searcherName = AppUtilities.TextGrabSettings.DefaultWebSearch;
        if (string.IsNullOrWhiteSpace(searcherName))
            return WebSearchers[0];

        WebSearchUrlModel? searcher = WebSearchers
            .FirstOrDefault(searcher => searcher.Name == searcherName);

        return searcher ?? WebSearchers[0];
    }

    private void SaveDefaultSearcher(WebSearchUrlModel webSearchUrl)
    {
        AppUtilities.TextGrabSettings.DefaultWebSearch = webSearchUrl.Name;
        AppUtilities.TextGrabSettings.Save();
    }

    private static List<WebSearchUrlModel> GetDefaultWebSearchUrls()
    {
        return
        [
            new() { Name = "Google", Url = "https://www.google.com/search?q=" },
            new() { Name = "Bing", Url = "https://www.bing.com/search?q=" },
            new() { Name = "DuckDuckGo", Url = "https://duckduckgo.com/?q=" },
            new() { Name = "Brave", Url = "https://search.brave.com/search?q=" },
            new() { Name = "GitHub Code", Url = "https://github.com/search?type=code&q=" },
            new() { Name = "GitHub Repos", Url = "https://github.com/search?type=repositories&q=" },
        ];
    }

    public static List<WebSearchUrlModel> GetWebSearchUrls()
    {
        List<WebSearchUrlModel> webSearchUrls = AppUtilities.TextGrabSettingsService.LoadWebSearchUrls();
        if (webSearchUrls.Count == 0)
            return GetDefaultWebSearchUrls();

        return webSearchUrls;
    }

    public static void SaveWebSearchUrls(List<WebSearchUrlModel> webSearchUrls)
    {
        AppUtilities.TextGrabSettingsService.SaveWebSearchUrls(webSearchUrls);
    }
}
