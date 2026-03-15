using CityAgent.Systems.Tools;
using Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CityAgent.Systems
{
    public partial class ClaudeAPISystem : GameSystemBase
    {
        private static readonly HttpClient s_Http = new HttpClient();

        private CityDataSystem   m_CityDataSystem = null!;
        private CityToolRegistry m_ToolRegistry   = null!;

        public volatile string? PendingResult = null;
        private bool m_RequestInFlight = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info($"{nameof(ClaudeAPISystem)}.{nameof(OnCreate)}");

            m_CityDataSystem = World.GetOrCreateSystemManaged<CityDataSystem>();

            m_ToolRegistry = new CityToolRegistry();
            m_ToolRegistry.Register(new GetPopulationTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetBuildingDemandTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetWorkforceTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetZoningSummaryTool(m_CityDataSystem));

            Mod.Log.Info($"Tool registry initialised with {m_ToolRegistry.ToolCount} tools.");
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mod.Log.Info($"{nameof(ClaudeAPISystem)}.{nameof(OnDestroy)}");
        }

        // ── Public integration seam ───────────────────────────────────────────────────

        public string GetToolsJson() => m_ToolRegistry.GetToolsJson();

        public string DispatchTool(string toolName, string inputJson) =>
            m_ToolRegistry.Dispatch(toolName, inputJson);

        /// <summary>
        /// Kicks off an async request. Safe to call from the game thread.
        /// Result is written to PendingResult when complete.
        /// </summary>
        public void BeginRequest(string userMessage, string? base64Png)
        {
            if (m_RequestInFlight) return;
            m_RequestInFlight = true;
            PendingResult = null;
            _ = RunRequestAsync(userMessage, base64Png);
        }

        private async Task RunRequestAsync(string userMessage, string? base64Png)
        {
            try
            {
                var setting = Mod.ActiveSetting;
                if (setting == null)
                {
                    PendingResult = "[Error]: Mod settings not loaded.";
                    return;
                }

                string baseUrl   = (setting.OllamaBaseUrl ?? "").Trim().TrimEnd('/');
                string apiKey    = (setting.OllamaApiKey ?? "").Trim();
                string model     = (setting.OllamaModel ?? "").Trim();
                string sysPrompt = setting.SystemPrompt ?? "";

                string masked = apiKey.Length > 8
                    ? apiKey.Substring(0, 4) + "..." + apiKey.Substring(apiKey.Length - 4)
                    : "(too short)";
                Mod.Log.Info($"[ClaudeAPISystem] baseUrl={baseUrl}, model={model}, apiKey length={apiKey.Length}, key={masked}");

                var messages = new List<JObject>
                {
                    JObject.FromObject(new { role = "system", content = sysPrompt })
                };

                // Build user message — Ollama native uses "images" array, not OpenAI content blocks
                var userMsg = new JObject
                {
                    ["role"]    = "user",
                    ["content"] = userMessage
                };
                if (!string.IsNullOrEmpty(base64Png))
                {
                    userMsg["images"] = new JArray(base64Png);
                }
                messages.Add(userMsg);

                var toolsArray = JArray.Parse(m_ToolRegistry.GetToolsJsonOpenAI());
                string endpoint = $"{baseUrl}/api/chat";

                for (int iteration = 0; iteration < 5; iteration++)
                {
                    var requestBody = new JObject
                    {
                        ["model"]    = model,
                        ["messages"] = new JArray(messages),
                        ["tools"]    = toolsArray,
                        ["stream"]   = false
                    };

                    var httpContent = new StringContent(
                        requestBody.ToString(Formatting.None),
                        Encoding.UTF8,
                        "application/json");

                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = httpContent
                    };
                    if (!string.IsNullOrEmpty(apiKey))
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                    Mod.Log.Info($"[ClaudeAPISystem] POST {endpoint} (iteration {iteration})");

                    HttpResponseMessage response = await s_Http.SendAsync(request).ConfigureAwait(false);
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        Mod.Log.Error($"[ClaudeAPISystem] HTTP {(int)response.StatusCode}: {responseBody}");
                        PendingResult = $"[Error]: HTTP {(int)response.StatusCode} — {responseBody}";
                        return;
                    }

                    // Ollama native response: { "message": { "role":"assistant", "content":"...", "tool_calls":[...] }, "done": true }
                    var responseJson = JObject.Parse(responseBody);
                    var message      = responseJson["message"] as JObject;
                    var toolCalls    = message?["tool_calls"] as JArray;

                    bool hasToolCalls = toolCalls != null && toolCalls.Count > 0;

                    if (hasToolCalls)
                    {
                        // Append assistant message with tool_calls
                        messages.Add(message!);

                        foreach (var tc in toolCalls!)
                        {
                            string funcName = tc["function"]?["name"]?.Value<string>() ?? "";
                            // Ollama returns arguments as object, not string — serialize it for our dispatcher
                            var argsToken = tc["function"]?["arguments"];
                            string funcArgs = argsToken is JObject
                                ? argsToken.ToString(Formatting.None)
                                : argsToken?.Value<string>() ?? "{}";

                            Mod.Log.Info($"[ClaudeAPISystem] Tool call: {funcName}({funcArgs})");
                            string toolResult = m_ToolRegistry.Dispatch(funcName, funcArgs);
                            Mod.Log.Info($"[ClaudeAPISystem] Tool result: {toolResult}");

                            messages.Add(new JObject
                            {
                                ["role"]    = "tool",
                                ["content"] = toolResult
                            });
                        }
                        // Loop again with updated messages
                    }
                    else
                    {
                        string? finalContent = message?["content"]?.Value<string>();
                        PendingResult = finalContent ?? "[Error]: Empty response content.";
                        return;
                    }
                }

                PendingResult = "[Error]: Tool call loop exceeded maximum iterations.";
            }
            catch (Exception ex)
            {
                Mod.Log.Error($"[ClaudeAPISystem] RunRequestAsync error: {ex}");
                PendingResult = $"[Error]: {ex.Message}";
            }
            finally
            {
                m_RequestInFlight = false;
            }
        }
    }
}
