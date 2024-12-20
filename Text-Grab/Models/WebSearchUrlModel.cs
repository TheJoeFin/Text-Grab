using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Text_Grab.Utilities;

namespace Text_Grab.Models;

public record WebSearchUrlModel
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

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

    public override string ToString() => Name;

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
        string json = AppUtilities.TextGrabSettings.WebSearchItemsJson;
        if (string.IsNullOrWhiteSpace(json))
            return GetDefaultWebSearchUrls();
        List<WebSearchUrlModel>? webSearchUrls = JsonSerializer.Deserialize<List<WebSearchUrlModel>>(json);
        if (webSearchUrls is null || webSearchUrls.Count == 0)
            return GetDefaultWebSearchUrls();

        return webSearchUrls;
    }

    public static void SaveWebSearchUrls(List<WebSearchUrlModel> webSearchUrls)
    {
        string json = JsonSerializer.Serialize(webSearchUrls);
        AppUtilities.TextGrabSettings.WebSearchItemsJson = json;
        AppUtilities.TextGrabSettings.Save();
    }
}
