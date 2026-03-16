using Colossal.UI.Binding;
using Game.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CityAgent.Systems
{
    public partial class CityAgentUISystem : UISystemBase
    {
        private const string kGroup = "cityAgent";

        private ValueBinding<bool>   m_PanelVisible  = null!;
        private ValueBinding<string> m_MessagesJson  = null!;
        private ValueBinding<bool>   m_IsLoading     = null!;
        private ValueBinding<bool>   m_HasScreenshot = null!;
        private ValueBinding<int>    m_PanelWidth    = null!;
        private ValueBinding<int>    m_PanelHeight   = null!;
        private ValueBinding<int>    m_FontSize      = null!;

        private CityDataSystem        m_CityData       = null!;
        private ClaudeAPISystem       m_ClaudeAPI      = null!;
        private NarrativeMemorySystem m_NarrativeMemory = null!;

        private readonly List<ChatMessage> m_History = new List<ChatMessage>();
        private string? m_PendingBase64Image = null;
        private KeyCode m_ScreenshotKey = KeyCode.F8;

        // File-based screenshot capture (CaptureScreenshotAsTexture returns null in UIUpdate phase)
        private string m_ScreenshotPath = "";
        private int m_ScreenshotWaitFrames = -1; // -1 = not waiting
        private int m_SettingsPollCounter = 0;
        private bool m_MemoryInitialized = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info($"{nameof(CityAgentUISystem)}.{nameof(OnCreate)}");

            m_PanelVisible  = new ValueBinding<bool>  (kGroup, "panelVisible",  false);
            m_MessagesJson  = new ValueBinding<string>(kGroup, "messagesJson",  "[]");
            m_IsLoading     = new ValueBinding<bool>  (kGroup, "isLoading",     false);
            m_HasScreenshot = new ValueBinding<bool>  (kGroup, "hasScreenshot", false);

            var setting = Mod.ActiveSetting;
            m_PanelWidth  = new ValueBinding<int>(kGroup, "panelWidth",  setting?.PanelWidth  ?? 520);
            m_PanelHeight = new ValueBinding<int>(kGroup, "panelHeight", setting?.PanelHeight ?? 650);
            m_FontSize    = new ValueBinding<int>(kGroup, "fontSize",    setting?.FontSize    ?? 14);

            AddBinding(m_PanelVisible);
            AddBinding(m_MessagesJson);
            AddBinding(m_IsLoading);
            AddBinding(m_HasScreenshot);
            AddBinding(m_PanelWidth);
            AddBinding(m_PanelHeight);
            AddBinding(m_FontSize);

            AddBinding(new TriggerBinding        (kGroup, "togglePanel",      TogglePanel));
            AddBinding(new TriggerBinding<string>(kGroup, "sendMessage",      OnSendMessage));
            AddBinding(new TriggerBinding        (kGroup, "clearChat",        OnClearChat));
            AddBinding(new TriggerBinding        (kGroup, "removeScreenshot", OnRemoveScreenshot));
            AddBinding(new TriggerBinding        (kGroup, "captureScreenshot", CaptureScreenshot));

            m_CityData        = World.GetOrCreateSystemManaged<CityDataSystem>();
            m_ClaudeAPI       = World.GetOrCreateSystemManaged<ClaudeAPISystem>();
            m_NarrativeMemory = World.GetOrCreateSystemManaged<NarrativeMemorySystem>();

            // Parse keybind from settings
            if (setting != null && Enum.TryParse<KeyCode>(setting.ScreenshotKeybind, out var key))
                m_ScreenshotKey = key;

            m_ScreenshotPath = Path.Combine(Application.temporaryCachePath, "cityagent_screenshot.png");

            Mod.Log.Info($"CityAgent UI bindings registered. Screenshot key: {m_ScreenshotKey}");
        }

        protected override void OnUpdate()
        {
            // 0. Initialize narrative memory on first update (city name may not be available in OnCreate)
            if (!m_MemoryInitialized && !m_NarrativeMemory.IsInitialized)
            {
                try
                {
                    m_NarrativeMemory.Initialize();
                    m_MemoryInitialized = true;

                    // Restore last session's chat history
                    var lastSession = m_NarrativeMemory.LoadLatestChatSession();
                    if (lastSession != null)
                    {
                        var restored = NarrativeMemorySystem.ParseChatSession(lastSession);
                        foreach (var (role, content, hadImage) in restored)
                            m_History.Add(new ChatMessage { role = role, content = content, hadImage = hadImage });

                        if (m_History.Count > 0)
                        {
                            PushMessagesBinding();
                            Mod.Log.Info($"Restored {m_History.Count} messages from previous session.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Mod.Log.Error($"Memory initialization failed: {ex.Message}");
                    m_MemoryInitialized = true; // Don't retry
                }
            }
            else if (!m_MemoryInitialized && m_NarrativeMemory.IsInitialized)
            {
                m_MemoryInitialized = true;
            }

            // 1. Screenshot keybind
            if (Input.GetKeyDown(m_ScreenshotKey))
                CaptureScreenshot();

            // 2. Poll for screenshot file (written by Unity at end of frame)
            if (m_ScreenshotWaitFrames >= 0)
            {
                m_ScreenshotWaitFrames++;
                if (m_ScreenshotWaitFrames > 10) // give up after ~10 frames
                {
                    Mod.Log.Error("Screenshot file was never written.");
                    m_ScreenshotWaitFrames = -1;
                }
                else if (File.Exists(m_ScreenshotPath))
                {
                    try
                    {
                        byte[] png = File.ReadAllBytes(m_ScreenshotPath);
                        File.Delete(m_ScreenshotPath);
                        m_PendingBase64Image = Convert.ToBase64String(png);
                        m_HasScreenshot.Update(true);
                        Mod.Log.Info($"Screenshot loaded: {png.Length} bytes.");
                    }
                    catch (Exception ex)
                    {
                        Mod.Log.Error($"Screenshot read failed: {ex.Message}");
                    }
                    m_ScreenshotWaitFrames = -1;
                }
            }

            // 3. Drain pending API result
            string? result = m_ClaudeAPI.PendingResult;
            if (result != null)
            {
                m_ClaudeAPI.PendingResult = null;
                m_History.Add(new ChatMessage { role = "assistant", content = result });
                PushMessagesBinding();
                m_IsLoading.Update(false);
                PersistChatSession();
            }

            // 4. Poll settings for UI dimension/font changes (~once per second)
            if (++m_SettingsPollCounter >= 60)
            {
                m_SettingsPollCounter = 0;
                var s = Mod.ActiveSetting;
                if (s != null)
                {
                    if (s.PanelWidth  != m_PanelWidth.value)  m_PanelWidth.Update(s.PanelWidth);
                    if (s.PanelHeight != m_PanelHeight.value) m_PanelHeight.Update(s.PanelHeight);
                    if (s.FontSize    != m_FontSize.value)    m_FontSize.Update(s.FontSize);
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mod.Log.Info($"{nameof(CityAgentUISystem)}.{nameof(OnDestroy)}");
        }

        private void TogglePanel()
        {
            m_PanelVisible.Update(!m_PanelVisible.value);
            Mod.Log.Info($"Panel toggled — now {(m_PanelVisible.value ? "open" : "closed")}");
        }

        private void OnSendMessage(string userText)
        {
            if (string.IsNullOrWhiteSpace(userText)) return;

            bool hadImage = m_PendingBase64Image != null;
            m_History.Add(new ChatMessage { role = "user", content = userText, hadImage = hadImage });
            PushMessagesBinding();

            m_ClaudeAPI.BeginRequest(userText, m_PendingBase64Image);
            m_PendingBase64Image = null;
            m_HasScreenshot.Update(false);
            m_IsLoading.Update(true);

            // Persist chat session after each message
            PersistChatSession();
        }

        private void OnClearChat()
        {
            PersistChatSession();
            m_NarrativeMemory.StartNewSession();
            m_History.Clear();
            PushMessagesBinding();
            m_PendingBase64Image = null;
            m_HasScreenshot.Update(false);
            m_IsLoading.Update(false);
            Mod.Log.Info("New conversation started.");
        }

        private void OnRemoveScreenshot()
        {
            m_PendingBase64Image = null;
            m_HasScreenshot.Update(false);
        }

        private void CaptureScreenshot()
        {
            try
            {
                // Delete any stale file from a previous capture
                if (File.Exists(m_ScreenshotPath))
                    File.Delete(m_ScreenshotPath);

                // Queue capture — Unity writes the file at end of frame
                ScreenCapture.CaptureScreenshot(m_ScreenshotPath);
                m_ScreenshotWaitFrames = 0;
                Mod.Log.Info($"Screenshot queued → {m_ScreenshotPath}");
            }
            catch (Exception ex)
            {
                Mod.Log.Error($"Screenshot capture failed: {ex.Message}");
            }
        }

        private void PushMessagesBinding()
        {
            m_MessagesJson.Update(JsonConvert.SerializeObject(m_History));
        }

        private void PersistChatSession()
        {
            if (!m_NarrativeMemory.IsInitialized || m_History.Count == 0) return;

            try
            {
                var messages = m_History.Select(m => (m.role, m.content, m.hadImage));
                string markdown = NarrativeMemorySystem.ChatHistoryToMarkdown(
                    messages, m_NarrativeMemory.SessionNumber, m_NarrativeMemory.CityName);
                m_NarrativeMemory.SaveChatSession(markdown);
            }
            catch (Exception ex)
            {
                Mod.Log.Error($"Failed to persist chat session: {ex.Message}");
            }
        }

        private class ChatMessage
        {
            public string role     { get; set; } = "";
            public string content  { get; set; } = "";
            public bool   hadImage { get; set; } = false;
        }
    }
}
