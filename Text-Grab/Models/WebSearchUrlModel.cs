using System.Collections.Generic;

namespace Text_Grab.Models;

public record WebSearchUrlModel
{
    public required string Name { get; set; }
    public required string Url { get; set; }


    public static List<WebSearchUrlModel> GetDefaultWebSearchUrls()
    {
        return
        [
            new() { Name = "Google", Url = "https://www.google.com/search?q=" },
            new() { Name = "Bing", Url = "https://www.bing.com/search?q=" },
            new() { Name = "DuckDuckGo", Url = "https://duckduckgo.com/?q=" },
            new() { Name = "Yahoo", Url = "https://search.yahoo.com/search?p=" },
            new() { Name = "Yandex", Url = "https://yandex.com/search/?text=" },
            new() { Name = "Baidu", Url = "https://www.baidu.com/s?wd=" },
            new() { Name = "GitHub Code", Url = "https://github.com/search?type=code&q=" },
            new() { Name = "GitHub Repos", Url = "https://github.com/search?type=repositories&q=" },
        ];
    }
}
