using CityAgent.Systems.Tools;
using Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CityAgent.Systems
{
    /// <summary>
    /// Background periodic advisor system (HB-01, HB-03).
    /// Fires an async API request every N minutes, drains the result into
    /// PendingHeartbeatResult for CityAgentUISystem to pick up via Interlocked.Exchange.
    /// </summary>
    public partial class HeartbeatSystem : GameSystemBase
    {
        // D-01: Own static HttpClient — independent pipeline from ClaudeAPISystem
        private static readonly HttpClient s_Http = new HttpClient();

        // Dependencies
        private CityDataSystem        m_CityDataSystem  = null!;
        private NarrativeMemorySystem m_NarrativeMemory = null!;
        private CityAgentUISystem     m_UISystem        = null!;
        private CityToolRegistry      m_ToolRegistry    = null!;

        // D-14: PendingHeartbeatResult is a public field — Interlocked.Exchange requires ref to field, not property
        public string? PendingHeartbeatResult = null;

        // D-01, D-11: volatile guards on state touched from both threads
        private volatile bool m_HeartbeatInFlight = false;

        // D-08, D-10: wall-clock timer
        private DateTime m_LastFireTime;

        // D-15: backoff counter — set to 3 on error, decremented each would-be fire cycle
        private int m_BackoffCycles = 0;

        // D-07 (Pitfall 4): separate screenshot path so heartbeat and chat don't overwrite each other
        private string m_HeartbeatScreenshotPath = "";

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info($"{nameof(HeartbeatSystem)}.{nameof(OnCreate)}");

            m_CityDataSystem  = World.GetOrCreateSystemManaged<CityDataSystem>();
            m_NarrativeMemory = World.GetOrCreateSystemManaged<NarrativeMemorySystem>();
            m_UISystem        = World.GetOrCreateSystemManaged<CityAgentUISystem>();

            // D-13: own tool registry with the full city-data + memory tool set
            m_ToolRegistry = new CityToolRegistry();
            m_ToolRegistry.Register(new GetPopulationTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetBuildingDemandTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetWorkforceTool(m_CityDataSystem));
            m_ToolRegistry.Register(new GetZoningSummaryTool(m_CityDataSystem));
            m_ToolRegistry.Register(new ReadMemoryFileTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new WriteMemoryFileTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new AppendNarrativeLogTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new CreateMemoryFileTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new DeleteMemoryFileTool(m_NarrativeMemory));
            m_ToolRegistry.Register(new ListMemoryFilesTool(m_NarrativeMemory));

            // D-10: first fire after a full interval delay from load
            m_LastFireTime = DateTime.UtcNow;

            // D-07: separate path prevents conflicts with the user-triggered screenshot at cityagent_screenshot.png
            m_HeartbeatScreenshotPath = Path.Combine(
                Application.temporaryCachePath, "cityagent_heartbeat_screenshot.png");

            Mod.Log.Info($"[HeartbeatSystem] Initialized with {m_ToolRegistry.ToolCount} tools. " +
                         $"Screenshot path: {m_HeartbeatScreenshotPath}");
        }

        protected override void OnUpdate()
        {
            // D-09: live-read settings each frame — never cached
            var setting = Mod.ActiveSetting;

            // D-09: off by default; return immediately when disabled
            if (setting == null || !setting.HeartbeatEnabled) return;

            // D-15: backoff — decrement and skip when recovering from API error
            if (m_BackoffCycles > 0)
            {
                m_BackoffCycles--;
                // Pitfall 7: reset timer from the end of the backoff so the next fire
                // is a full interval away, not the residual time from before the error
                m_LastFireTime = DateTime.UtcNow;
                return;
            }

            // D-11: if a prior heartbeat is still in flight, skip and reset timer
            if (m_HeartbeatInFlight)
            {
                double elapsed = (DateTime.UtcNow - m_LastFireTime).TotalMinutes;
                if (elapsed >= setting.HeartbeatIntervalMinutes)
                    m_LastFireTime = DateTime.UtcNow;
                return;
            }

            // D-08: wall-clock timer check
            double minutesElapsed = (DateTime.UtcNow - m_LastFireTime).TotalMinutes;
            if (minutesElapsed < setting.HeartbeatIntervalMinutes) return;

            // Timer fired — set up for this cycle
            m_LastFireTime = DateTime.UtcNow;
            m_HeartbeatInFlight = true;
            Interlocked.Exchange(ref PendingHeartbeatResult, null);

            // D-06, D-07: screenshot handling
            // Read the screenshot from the PREVIOUS heartbeat cycle (prior file on disk),
            // then queue a new capture for the NEXT cycle.
            // This one-cycle delay is intentional: ScreenCapture writes the file at end of frame,
            // so we cannot read a file we just queued in the same OnUpdate call.
            string? base64Png = null;
            if (setting.HeartbeatIncludeScreenshot && !m_UISystem.IsScreenshotCapturePending)
            {
                // Attempt to read the screenshot captured in the prior cycle
                if (File.Exists(m_HeartbeatScreenshotPath))
                {
                    try
                    {
                        byte[] png = File.ReadAllBytes(m_HeartbeatScreenshotPath);
                        base64Png = Convert.ToBase64String(png);
                        Mod.Log.Info($"[HeartbeatSystem] Loaded prior screenshot: {png.Length} bytes.");
                    }
                    catch (Exception ex)
                    {
                        Mod.Log.Warn($"[HeartbeatSystem] Failed to read heartbeat screenshot: {ex.Message}");
                        // Continue without screenshot this cycle — D-07 behavior
                    }
                }

                // Queue a new capture for the NEXT heartbeat cycle
                try
                {
                    ScreenCapture.CaptureScreenshot(m_HeartbeatScreenshotPath);
                    Mod.Log.Info($"[HeartbeatSystem] Screenshot queued for next cycle.");
                }
                catch (Exception ex)
                {
                    Mod.Log.Warn($"[HeartbeatSystem] Screenshot capture failed: {ex.Message}");
                }
            }

            Mod.Log.Info($"[HeartbeatSystem] Firing heartbeat (interval={setting.HeartbeatIntervalMinutes}min, " +
                         $"hasScreenshot={base64Png != null})");
            _ = RunHeartbeatAsync(base64Png);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mod.Log.Info($"{nameof(HeartbeatSystem)}.{nameof(OnDestroy)}");
        }

        // ── Async request orchestration ───────────────────────────────────────────────

        /// <summary>
        /// Fires an Ollama-format API request with the heartbeat system prompt.
        /// Mirrors ClaudeAPISystem.RunOllamaRequestAsync but uses HeartbeatSystemPrompt,
        /// a fixed observation user message, and writes to PendingHeartbeatResult instead
        /// of PendingResult. Errors are logged but NOT surfaced to the chat panel (D-15).
        /// </summary>
        private async Task RunHeartbeatAsync(string? base64Png)
        {
            try
            {
                var setting = Mod.ActiveSetting;
                if (setting == null)
                {
                    Mod.Log.Error("[HeartbeatSystem] Mod settings not loaded — skipping heartbeat.");
                    return; // D-15: not surfaced to panel
                }

                string ollamaBase  = (setting.OllamaBaseUrl ?? "").Trim().TrimEnd('/');
                string apiKey      = (setting.OllamaApiKey  ?? "").Trim();
                string model       = (setting.OllamaModel   ?? "").Trim();

                if (string.IsNullOrEmpty(ollamaBase) || string.IsNullOrEmpty(model))
                {
                    Mod.Log.Warn("[HeartbeatSystem] OllamaBaseUrl or OllamaModel not configured — skipping heartbeat.");
                    return; // Silent skip — not an error, just unconfigured
                }

                // D-12: heartbeat system prompt with memory injection
                string sysPrompt = setting.HeartbeatSystemPrompt ?? "";
                if (m_NarrativeMemory.IsInitialized)
                    sysPrompt += m_NarrativeMemory.GetAlwaysInjectedContext();

                // Ollama format: system message in the messages array (not top-level)
                var messages = new List<JObject>
                {
                    new JObject { ["role"] = "system", ["content"] = sysPrompt }
                };

                // Heartbeat user turn — brief observation prompt; attach screenshot if available
                var userContent = new JObject
                {
                    ["role"]    = "user",
                    ["content"] = "Observe the current state of the city and report if anything is noteworthy."
                };
                if (!string.IsNullOrEmpty(base64Png))
                {
                    // Ollama image attachment: base64 array on the message
                    userContent["images"] = new JArray(base64Png);
                }
                messages.Add(userContent);

                var toolsArray = JArray.Parse(m_ToolRegistry.GetToolsJsonOpenAI());
                string endpoint = $"{ollamaBase}/v1/chat/completions";

                // Tool-use loop — mirrors ClaudeAPISystem.RunOllamaRequestAsync (max 10 iterations)
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

                    Mod.Log.Info($"[HeartbeatSystem] POST {endpoint} (iteration {iteration})");

                    HttpResponseMessage response = await s_Http.SendAsync(request).ConfigureAwait(false);
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        Mod.Log.Error($"[HeartbeatSystem] HTTP {(int)response.StatusCode}: {responseBody}");
                        m_BackoffCycles = 3; // D-15: 3-cycle backoff on error
                        return; // D-15: errors not surfaced to panel
                    }

                    // OpenAI-compatible response: choices[0].finish_reason + choices[0].message
                    var responseJson     = JObject.Parse(responseBody);
                    var choices          = responseJson["choices"] as JArray;
                    var firstChoice      = choices?[0] as JObject;
                    string? finishReason = firstChoice?["finish_reason"]?.Value<string>();
                    var assistantMsg     = firstChoice?["message"] as JObject;

                    if (finishReason == "tool_calls")
                    {
                        // Append assistant message (contains tool_calls array)
                        if (assistantMsg != null)
                            messages.Add(assistantMsg);

                        var toolCallsArray = assistantMsg?["tool_calls"] as JArray;
                        if (toolCallsArray != null)
                        {
                            foreach (var tc in toolCallsArray)
                            {
                                string toolCallId = tc["id"]?.Value<string>() ?? "";
                                string funcName   = tc["function"]?["name"]?.Value<string>() ?? "";

                                // Ollama may return arguments as a JObject or as a JSON string
                                var argsToken = tc["function"]?["arguments"];
                                string funcArgs = argsToken is JObject
                                    ? argsToken.ToString(Formatting.None)
                                    : argsToken?.Value<string>() ?? "{}";

                                Mod.Log.Info($"[HeartbeatSystem] Tool call: {funcName}({funcArgs})");
                                string toolResult = m_ToolRegistry.Dispatch(funcName, funcArgs);
                                Mod.Log.Info($"[HeartbeatSystem] Tool result: {toolResult}");

                                messages.Add(new JObject
                                {
                                    ["role"]         = "tool",
                                    ["tool_call_id"] = toolCallId,
                                    ["content"]      = toolResult
                                });
                            }
                        }
                        // Continue loop with updated messages
                    }
                    else if (finishReason == "stop")
                    {
                        string? finalContent = assistantMsg?["content"]?.Value<string>();
                        // D-05: write result (including "[silent]") — CityAgentUISystem filters in Plan 03
                        Interlocked.Exchange(ref PendingHeartbeatResult, finalContent ?? "");
                        Mod.Log.Info($"[HeartbeatSystem] Result: " +
                            $"{(finalContent?.Length > 80 ? finalContent.Substring(0, 80) + "..." : finalContent ?? "(null)")}");
                        return;
                    }
                    else
                    {
                        Mod.Log.Warn($"[HeartbeatSystem] Unexpected finish_reason '{finishReason}' — skipping.");
                        return; // D-15: not surfaced to panel
                    }
                }

                Mod.Log.Warn("[HeartbeatSystem] Tool call loop exceeded maximum iterations.");
            }
            catch (Exception ex)
            {
                Mod.Log.Error($"[HeartbeatSystem] RunHeartbeatAsync error: {ex}");
                m_BackoffCycles = 3; // D-15: 3-cycle backoff on catch
            }
            finally
            {
                m_HeartbeatInFlight = false;
            }
        }
    }
}
