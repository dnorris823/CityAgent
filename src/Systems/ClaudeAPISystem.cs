using CityAgent.Systems.Tools;
using Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CityAgent.Systems
{
    public partial class ClaudeAPISystem : GameSystemBase
    {
        private static readonly HttpClient s_Http = new HttpClient();

        private CityDataSystem         m_CityDataSystem  = null!;
        private NarrativeMemorySystem  m_NarrativeMemory = null!;
        private CityToolRegistry       m_ToolRegistry    = null!;

        // D-14: PendingResult uses Interlocked.Exchange — volatile not needed (Interlocked provides memory barrier)
        public string? PendingResult = null;
        // D-14: m_RequestInFlight gets volatile keyword to prevent double-send races
        private volatile bool m_RequestInFlight = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info($"{nameof(ClaudeAPISystem)}.{nameof(OnCreate)}");

            m_CityDataSystem  = World.GetOrCreateSystemManaged<CityDataSystem>();
            m_NarrativeMemory = World.GetOrCreateSystemManaged<NarrativeMemorySystem>();

            m_ToolRegistry = new CityToolRegistry();
            m_ToolRegistry.Register(new GetPopulationTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetBuildingDemandTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetWorkforceTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetZoningSummaryTool(m_CityDataSystem));

            // Memory tools
            m_ToolRegistry.Register(new ReadMemoryFileTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new WriteMemoryFileTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new AppendNarrativeLogTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new CreateMemoryFileTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new DeleteMemoryFileTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new ListMemoryFilesTool(m_NarrativeMemory));

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
            Interlocked.Exchange(ref PendingResult, null);
            _ = RunRequestAsync(userMessage, base64Png);
        }

        // ── Request orchestration ─────────────────────────────────────────────────────

        private async Task RunRequestAsync(string userMessage, string? base64Png)
        {
            try
            {
                var setting = Mod.ActiveSetting;
                if (setting == null)
                {
                    Interlocked.Exchange(ref PendingResult, "[Error]: Mod settings not loaded.");
                    return;
                }

                string sysPrompt = setting.SystemPrompt ?? "";
                if (m_NarrativeMemory.IsInitialized)
                    sysPrompt += m_NarrativeMemory.GetAlwaysInjectedContext();

                string apiKey = (setting.ClaudeApiKey ?? "").Trim();
                string model  = (setting.ClaudeModel ?? "").Trim();

                // Log masked key (first 4 + last 4 chars)
                string masked = apiKey.Length > 8
                    ? apiKey.Substring(0, 4) + "..." + apiKey.Substring(apiKey.Length - 4)
                    : "(too short)";
                Mod.Log.Info($"[ClaudeAPISystem] Provider: Claude, model={model}, apiKey={masked}");

                string result = await RunClaudeRequestAsync(userMessage, base64Png, apiKey, model, sysPrompt).ConfigureAwait(false);

                if (result == "__429__")
                {
                    // D-06 / D-08: Rate limit fallback
                    string ollamaBase  = (setting.OllamaFallbackBaseUrl ?? "").Trim().TrimEnd('/');
                    string ollamaModel = (setting.OllamaFallbackModel ?? "").Trim();

                    if (string.IsNullOrEmpty(ollamaBase) || string.IsNullOrEmpty(ollamaModel))
                    {
                        // D-08: no fallback configured
                        Interlocked.Exchange(ref PendingResult,
                            "\u26a0\ufe0f Rate limited by Claude. No Ollama fallback configured \u2014 set one up in mod settings.");
                        return;
                    }

                    // D-06: surface rate-limit notice to chat panel before retrying
                    Interlocked.Exchange(ref PendingResult,
                        $"\u26a0\ufe0f Rate limited \u2014 retrying with {ollamaModel}...");
                    Mod.Log.Info($"[ClaudeAPISystem] Claude 429 \u2014 falling back to Ollama {ollamaModel}");

                    string ollamaKey = (setting.OllamaFallbackApiKey ?? "").Trim();
                    await RunOllamaRequestAsync(userMessage, ollamaKey, ollamaModel, ollamaBase, sysPrompt).ConfigureAwait(false);
                }
                // else: PendingResult already set inside RunClaudeRequestAsync (success or non-429 error)
            }
            catch (Exception ex)
            {
                Mod.Log.Error($"[ClaudeAPISystem] RunRequestAsync error: {ex}");
                Interlocked.Exchange(ref PendingResult, $"[Error]: {ex.Message}");
            }
            finally
            {
                m_RequestInFlight = false;
            }
        }

        // ── Claude /v1/messages provider ──────────────────────────────────────────────

        /// <summary>
        /// Sends a request to the Anthropic /v1/messages endpoint.
        /// Returns "__429__" if rate-limited (caller handles fallback).
        /// Sets PendingResult directly on success or non-429 errors.
        /// </summary>
        private async Task<string> RunClaudeRequestAsync(
            string userMessage,
            string? base64Png,
            string apiKey,
            string model,
            string sysPrompt)
        {
            const string endpoint = "https://api.anthropic.com/v1/messages";

            // Build initial user message with optional image content block (Pattern 1)
            var userContent = new JArray();
            if (!string.IsNullOrEmpty(base64Png))
            {
                userContent.Add(new JObject
                {
                    ["type"] = "image",
                    ["source"] = new JObject
                    {
                        ["type"]       = "base64",
                        ["media_type"] = "image/png",
                        ["data"]       = base64Png
                    }
                });
            }
            userContent.Add(new JObject
            {
                ["type"] = "text",
                ["text"] = userMessage
            });

            var messages = new List<JObject>
            {
                new JObject { ["role"] = "user", ["content"] = userContent }
            };

            var toolsArray = JArray.Parse(m_ToolRegistry.GetToolsJson());

            for (int iteration = 0; iteration < 10; iteration++)
            {
                // Build Anthropic request body (Pattern 1)
                // system is a top-level field — NOT in the messages array
                var requestBody = new JObject
                {
                    ["model"]      = model,
                    ["max_tokens"] = 4096,
                    ["system"]     = sysPrompt,
                    ["messages"]   = new JArray(messages),
                    ["tools"]      = toolsArray
                };

                var httpContent = new StringContent(
                    requestBody.ToString(Formatting.None),
                    Encoding.UTF8,
                    "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = httpContent
                };
                // Anthropic uses x-api-key, NOT Authorization: Bearer (Pattern 1)
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                Mod.Log.Info($"[ClaudeAPISystem] POST {endpoint} (iteration {iteration})");

                HttpResponseMessage response = await s_Http.SendAsync(request).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                int statusCode = (int)response.StatusCode;

                if (statusCode == 429)
                {
                    // D-07: 429 only triggers fallback — return sentinel, do NOT set PendingResult here
                    Mod.Log.Warn($"[ClaudeAPISystem] Claude returned 429 (rate limited)");
                    return "__429__";
                }

                if (!response.IsSuccessStatusCode)
                {
                    // D-07: 400/401/500 → [Error]: without fallback
                    Mod.Log.Error($"[ClaudeAPISystem] HTTP {statusCode}: {responseBody}");
                    Interlocked.Exchange(ref PendingResult, $"[Error]: HTTP {statusCode} \u2014 {responseBody}");
                    return string.Empty;
                }

                // Parse Anthropic response (Pattern 2)
                var responseJson  = JObject.Parse(responseBody);
                string? stopReason  = responseJson["stop_reason"]?.Value<string>();
                var contentArray    = responseJson["content"] as JArray;

                if (stopReason == "tool_use" && contentArray != null) // stop_reason: tool_use
                {
                    // Append the full assistant content array (Pattern 4)
                    messages.Add(new JObject
                    {
                        ["role"]    = "assistant",
                        ["content"] = contentArray
                    });

                    // Collect all tool_result blocks into one user message (Pattern 3)
                    var toolResults = new JArray();
                    foreach (var block in contentArray)
                    {
                        if (block["type"]?.Value<string>() == "tool_use")
                        {
                            string toolId    = block["id"]!.Value<string>()!;
                            string toolName  = block["name"]!.Value<string>()!;
                            string toolInput = block["input"]?.ToString(Formatting.None) ?? "{}";

                            Mod.Log.Info($"[ClaudeAPISystem] Tool call: {toolName}({toolInput})");
                            string toolResult = m_ToolRegistry.Dispatch(toolName, toolInput);
                            Mod.Log.Info($"[ClaudeAPISystem] Tool result: {toolResult}");

                            toolResults.Add(new JObject
                            {
                                ["type"]        = "tool_result",
                                ["tool_use_id"] = toolId,
                                ["content"]     = toolResult
                            });
                        }
                    }

                    // Tool result is a user-role message (Pattern 3) — NOT role: "tool"
                    messages.Add(new JObject
                    {
                        ["role"]    = "user",
                        ["content"] = toolResults
                    });
                    // Continue loop with updated messages
                }
                else if (stopReason == "end_turn")
                {
                    // Extract text content block
                    string? finalContent = null;
                    if (contentArray != null)
                    {
                        foreach (var block in contentArray)
                        {
                            if (block["type"]?.Value<string>() == "text")
                            {
                                finalContent = block["text"]?.Value<string>();
                                break;
                            }
                        }
                    }
                    Interlocked.Exchange(ref PendingResult, finalContent ?? "[Error]: Empty response.");
                    return string.Empty;
                }
                else
                {
                    Interlocked.Exchange(ref PendingResult, $"[Error]: Unexpected stop_reason '{stopReason}'.");
                    return string.Empty;
                }
            }

            Interlocked.Exchange(ref PendingResult, "[Error]: Tool call loop exceeded maximum iterations.");
            return string.Empty;
        }

        // ── Ollama /v1/chat/completions fallback provider ─────────────────────────────

        /// <summary>
        /// Sends a request to the Ollama OpenAI-compatible /v1/chat/completions endpoint.
        /// Used only as a 429 fallback from Claude.
        /// Sets PendingResult directly on completion or error.
        /// </summary>
        private async Task RunOllamaRequestAsync(
            string userMessage,
            string apiKey,
            string model,
            string ollamaBase,
            string sysPrompt)
        {
            string endpoint = $"{ollamaBase}/v1/chat/completions";

            // Ollama: system prompt goes in messages array (not top-level)
            // No image support in fallback mode
            var messages = new List<JObject>
            {
                new JObject { ["role"] = "system",  ["content"] = sysPrompt   },
                new JObject { ["role"] = "user",    ["content"] = userMessage  }
            };

            var toolsArray = JArray.Parse(m_ToolRegistry.GetToolsJsonOpenAI());

            for (int iteration = 0; iteration < 10; iteration++)
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

                Mod.Log.Info($"[ClaudeAPISystem] Ollama POST {endpoint} (iteration {iteration})");

                HttpResponseMessage response = await s_Http.SendAsync(request).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Mod.Log.Error($"[ClaudeAPISystem] Ollama HTTP {(int)response.StatusCode}: {responseBody}");
                    Interlocked.Exchange(ref PendingResult, $"[Error]: Ollama HTTP {(int)response.StatusCode} \u2014 {responseBody}");
                    return;
                }

                // OpenAI-compatible response: choices[0].finish_reason, choices[0].message
                var responseJson  = JObject.Parse(responseBody);
                var choices       = responseJson["choices"] as JArray;
                var firstChoice   = choices?[0] as JObject;
                string? finishReason = firstChoice?["finish_reason"]?.Value<string>();
                var assistantMsg     = firstChoice?["message"] as JObject;

                if (finishReason == "tool_calls")
                {
                    // Append assistant message with tool_calls
                    if (assistantMsg != null)
                        messages.Add(assistantMsg);

                    var toolCallsArray = assistantMsg?["tool_calls"] as JArray;
                    if (toolCallsArray != null)
                    {
                        foreach (var tc in toolCallsArray)
                        {
                            string toolCallId = tc["id"]?.Value<string>() ?? "";
                            string funcName   = tc["function"]?["name"]?.Value<string>() ?? "";
                            // Ollama may return arguments as object or string — handle both
                            var argsToken = tc["function"]?["arguments"];
                            string funcArgs = argsToken is JObject
                                ? argsToken.ToString(Formatting.None)
                                : argsToken?.Value<string>() ?? "{}";

                            Mod.Log.Info($"[ClaudeAPISystem] Ollama tool call: {funcName}({funcArgs})");
                            string toolResult = m_ToolRegistry.Dispatch(funcName, funcArgs);
                            Mod.Log.Info($"[ClaudeAPISystem] Ollama tool result: {toolResult}");

                            messages.Add(new JObject
                            {
                                ["role"]         = "tool",
                                ["tool_call_id"] = toolCallId,
                                ["content"]      = toolResult
                            });
                        }
                    }
                    // Continue loop
                }
                else if (finishReason == "stop")
                {
                    string? finalContent = assistantMsg?["content"]?.Value<string>();
                    Interlocked.Exchange(ref PendingResult, finalContent ?? "[Error]: Empty Ollama response.");
                    return;
                }
                else
                {
                    Interlocked.Exchange(ref PendingResult, $"[Error]: Ollama unexpected finish_reason '{finishReason}'.");
                    return;
                }
            }

            Interlocked.Exchange(ref PendingResult, "[Error]: Ollama tool call loop exceeded maximum iterations.");
        }
    }
}
