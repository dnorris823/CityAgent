using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace CityAgent.Systems.Tools
{
    public class SearchWebTool : ICityAgentTool
    {
        private static readonly HttpClient s_BraveHttp = new HttpClient();
        private readonly Setting m_Setting;

        public SearchWebTool(Setting setting) => m_Setting = setting;

        public string Name        => "search_web";
        public string Description =>
            "Search the internet for real-world information. Use for urban planning techniques, " +
            "zoning practices, infrastructure design (roads, transit, utilities), traffic solutions, " +
            "historical city examples, or any question that benefits from current external knowledge.";
        public string InputSchema =>
            "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"," +
            "\"description\":\"The search query\"}},\"required\":[\"query\"]}";

        public string Execute(string inputJson)
        {
            try
            {
                var input = JObject.Parse(inputJson);
                string query = input["query"]?.Value<string>() ?? "";
                if (string.IsNullOrWhiteSpace(query))
                    return JsonConvert.SerializeObject(new { error = "Search failed: empty query" });

                string apiKey = (m_Setting?.BraveSearchApiKey ?? "").Trim();
                if (string.IsNullOrEmpty(apiKey))
                    return JsonConvert.SerializeObject(new { error = "Search failed: no Brave Search API key configured" });

                string encodedQuery = Uri.EscapeDataString(query);
                string url = $"https://api.search.brave.com/res/v1/web/search?q={encodedQuery}&count=3&extra_snippets=true";

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("X-Subscription-Token", apiKey);
                    request.Headers.Add("Accept", "application/json");

                    var response = s_BraveHttp.SendAsync(request, cts.Token).GetAwaiter().GetResult();
                    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (!response.IsSuccessStatusCode)
                        return JsonConvert.SerializeObject(new { error = $"Search failed: HTTP {(int)response.StatusCode}" });

                    var json = JObject.Parse(body);
                    var rawResults = json["web"]?["results"] as JArray ?? new JArray();

                    var results = new List<object>();
                    foreach (var r in rawResults)
                    {
                        var extraSnippets = r["extra_snippets"] as JArray;
                        if (extraSnippets != null && extraSnippets.Count > 0)
                        {
                            results.Add(new
                            {
                                title          = r["title"]?.Value<string>() ?? "",
                                url            = r["url"]?.Value<string>() ?? "",
                                description    = r["description"]?.Value<string>() ?? "",
                                extra_snippets = extraSnippets.ToObject<List<string>>()
                            });
                        }
                        else
                        {
                            results.Add(new
                            {
                                title       = r["title"]?.Value<string>() ?? "",
                                url         = r["url"]?.Value<string>() ?? "",
                                description = r["description"]?.Value<string>() ?? ""
                            });
                        }
                    }

                    return JsonConvert.SerializeObject(new { query, results });
                }
            }
            catch (OperationCanceledException)
            {
                return JsonConvert.SerializeObject(new { error = "Search failed: timed out after 5 seconds" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = $"Search failed: {ex.Message}" });
            }
        }
    }
}
