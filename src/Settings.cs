using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;

namespace CityAgent
{
    public enum ProviderChoice { Claude, Ollama }

    [FileLocation(nameof(CityAgent))]
    [SettingsUIGroupOrder(kProviderGroup, kClaudeGroup, kOllamaGroup, kUIGroup, kMemoryGroup)]
    [SettingsUIShowGroupName(kProviderGroup, kClaudeGroup, kOllamaGroup, kUIGroup, kMemoryGroup)]
    public class Setting : ModSetting
    {
        public const string kSection       = "Main";
        public const string kProviderGroup = "Provider";
        public const string kClaudeGroup   = "Claude";
        public const string kOllamaGroup   = "Ollama";
        public const string kUIGroup       = "UI";
        public const string kMemoryGroup   = "Memory";

        private const string DefaultSystemPrompt =
            "You are CityAgent, an AI city planning advisor in the style of CityPlannerPlays. " +
            "Analyze the city screenshot and data, then provide engaging narrative commentary and " +
            "specific build recommendations. Be enthusiastic but practical. Focus on what would " +
            "make the most impact for the city's current challenges.";

        public Setting(IMod mod) : base(mod) { }

        // --- Provider selection ---

        [SettingsUISection(kSection, kProviderGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetProviderOptions))]
        public ProviderChoice Provider { get; set; } = ProviderChoice.Ollama;

        public DropdownItem<ProviderChoice>[] GetProviderOptions() => new[]
        {
            new DropdownItem<ProviderChoice> { value = ProviderChoice.Claude, displayName = "Claude API (Anthropic)" },
            new DropdownItem<ProviderChoice> { value = ProviderChoice.Ollama, displayName = "Ollama (local)" }
        };

        [SettingsUISection(kSection, kProviderGroup)]
        public string ActiveProvider => Provider == ProviderChoice.Ollama
            ? $"Active: Ollama — {(string.IsNullOrWhiteSpace(OllamaBaseUrl) ? "no URL set" : OllamaBaseUrl)}"
            : (string.IsNullOrWhiteSpace(ClaudeApiKey) ? "Active: Claude API — no key set" : "Active: Claude API");

        // --- Claude API section ---

        [SettingsUISection(kSection, kClaudeGroup)]
        [SettingsUITextInput]
        public string ClaudeApiKey { get; set; } = string.Empty;

        [SettingsUISection(kSection, kClaudeGroup)]
        [SettingsUITextInput]
        public string ClaudeModel { get; set; } = "claude-sonnet-4-6";

        [SettingsUISection(kSection, kClaudeGroup)]
        [SettingsUITextInput]
        public string SystemPrompt { get; set; } = DefaultSystemPrompt;

        [SettingsUISection(kSection, kClaudeGroup)]
        [SettingsUITextInput]
        public string ScreenshotKeybind { get; set; } = "F8";

        // --- Ollama section ---

        [SettingsUISection(kSection, kOllamaGroup)]
        [SettingsUITextInput]
        public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

        [SettingsUISection(kSection, kOllamaGroup)]
        [SettingsUITextInput]
        public string OllamaApiKey { get; set; } = string.Empty;

        [SettingsUISection(kSection, kOllamaGroup)]
        [SettingsUITextInput]
        public string OllamaModel { get; set; } = "llama3.1:8b";

        // --- UI section ---

        [SettingsUISection(kSection, kUIGroup)]
        [SettingsUISlider(min = 400, max = 1600, step = 10)]
        public int PanelWidth { get; set; } = 520;

        [SettingsUISection(kSection, kUIGroup)]
        [SettingsUISlider(min = 400, max = 1200, step = 10)]
        public int PanelHeight { get; set; } = 650;

        [SettingsUISection(kSection, kUIGroup)]
        [SettingsUISlider(min = 11, max = 32, step = 1)]
        public int FontSize { get; set; } = 14;

        // --- Memory section ---

        [SettingsUISection(kSection, kMemoryGroup)]
        [SettingsUISlider(min = 10, max = 200, step = 5)]
        public int MaxNarrativeLogEntries { get; set; } = 50;

        [SettingsUISection(kSection, kMemoryGroup)]
        [SettingsUISlider(min = 5, max = 100, step = 5)]
        public int MaxChatHistorySessions { get; set; } = 20;

        public override void SetDefaults()
        {
            Provider               = ProviderChoice.Ollama;
            ClaudeApiKey           = string.Empty;
            ClaudeModel            = "claude-sonnet-4-6";
            OllamaBaseUrl          = "http://localhost:11434";
            OllamaApiKey           = string.Empty;
            OllamaModel            = "llama3.1:8b";
            SystemPrompt           = DefaultSystemPrompt;
            ScreenshotKeybind      = "F8";
            PanelWidth             = 520;
            PanelHeight            = 650;
            FontSize               = 14;
            MaxNarrativeLogEntries = 50;
            MaxChatHistorySessions = 20;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting) => m_Setting = setting;

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "CityAgent" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                // Provider group
                { m_Setting.GetOptionGroupLocaleID(Setting.kProviderGroup), "AI Provider" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Provider)),        "Provider" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Provider)),         "Choose which AI provider to use. Claude API requires an Anthropic API key. Ollama runs locally with no key needed." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ActiveProvider)),  "Status" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ActiveProvider)),   "Shows which AI provider is currently active." },

                // Claude API group
                { m_Setting.GetOptionGroupLocaleID(Setting.kClaudeGroup), "Claude API" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ClaudeApiKey)),      "API Key" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ClaudeApiKey)),       "Your Anthropic API key. Never share this." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ClaudeModel)),       "Model" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ClaudeModel)),        "Claude model ID (default: claude-sonnet-4-6)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SystemPrompt)),      "System Prompt" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SystemPrompt)),       "Advisor persona prompt sent to the AI at the start of every conversation." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ScreenshotKeybind)), "Screenshot Keybind" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ScreenshotKeybind)),  "Unity KeyCode name for capturing a screenshot (default: F8)." },

                // Ollama group
                { m_Setting.GetOptionGroupLocaleID(Setting.kOllamaGroup), "Ollama" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OllamaBaseUrl)),  "Base URL" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OllamaBaseUrl)),   "Ollama server base URL (default: http://localhost:11434)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OllamaApiKey)),   "API Key" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OllamaApiKey)),    "Optional API key if your Ollama endpoint requires authentication." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OllamaModel)),    "Model" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OllamaModel)),     "Ollama model name (e.g. llama3.1:8b, mistral, etc.)" },

                // UI group
                { m_Setting.GetOptionGroupLocaleID(Setting.kUIGroup), "UI Settings" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PanelWidth)),  "Panel Width" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PanelWidth)),   "Default width of the CityAgent panel in pixels (400–1600). You can also drag panel edges to resize." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PanelHeight)), "Panel Height" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PanelHeight)),  "Default height of the CityAgent panel in pixels (400–1200). You can also drag panel edges to resize." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FontSize)),    "Font Size" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FontSize)),     "Base font size for all panel text (11–32)." },

                // Memory group
                { m_Setting.GetOptionGroupLocaleID(Setting.kMemoryGroup), "Memory Settings" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MaxNarrativeLogEntries)),  "Max Narrative Log Entries" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MaxNarrativeLogEntries)),   "Maximum entries in narrative-log.md before older entries are archived (10–200)." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MaxChatHistorySessions)),  "Max Chat History Sessions" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MaxChatHistorySessions)),   "Number of recent chat sessions to keep. Older sessions are auto-deleted (5–100)." },
            };
        }

        public void Unload() { }
    }
}
