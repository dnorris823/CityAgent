using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using System.Collections.Generic;

namespace CityAgent
{
    [FileLocation(nameof(CityAgent))]
    [SettingsUIGroupOrder(kGeneralGroup, kUIGroup, kMemoryGroup)]
    [SettingsUIShowGroupName(kGeneralGroup, kUIGroup, kMemoryGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kGeneralGroup = "General";
        public const string kUIGroup = "UI";
        public const string kMemoryGroup = "Memory";

        private const string DefaultSystemPrompt =
            "You are CityAgent, an AI city planning advisor in the style of CityPlannerPlays. " +
            "Analyze the city screenshot and data, then provide engaging narrative commentary and " +
            "specific build recommendations. Be enthusiastic but practical. Focus on what would " +
            "make the most impact for the city's current challenges.";

        public Setting(IMod mod) : base(mod) { }

        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUITextInput]
        public string OllamaApiKey { get; set; } = string.Empty;

        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUITextInput]
        public string OllamaModel { get; set; } = "kimi-k2.5:cloud";

        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUITextInput]
        public string OllamaBaseUrl { get; set; } = "https://ollama.com";

        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUITextInput]
        public string SystemPrompt { get; set; } = DefaultSystemPrompt;

        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUITextInput]
        public string ScreenshotKeybind { get; set; } = "F8";

        [SettingsUISection(kSection, kUIGroup)]
        [SettingsUISlider(min = 400, max = 1600, step = 10)]
        public int PanelWidth { get; set; } = 520;

        [SettingsUISection(kSection, kUIGroup)]
        [SettingsUISlider(min = 400, max = 1200, step = 10)]
        public int PanelHeight { get; set; } = 650;

        [SettingsUISection(kSection, kUIGroup)]
        [SettingsUISlider(min = 11, max = 32, step = 1)]
        public int FontSize { get; set; } = 14;

        [SettingsUISection(kSection, kMemoryGroup)]
        [SettingsUISlider(min = 10, max = 200, step = 5)]
        public int MaxNarrativeLogEntries { get; set; } = 50;

        [SettingsUISection(kSection, kMemoryGroup)]
        [SettingsUISlider(min = 5, max = 100, step = 5)]
        public int MaxChatHistorySessions { get; set; } = 20;

        public override void SetDefaults()
        {
            OllamaApiKey      = string.Empty;
            OllamaModel       = "kimi-k2.5:cloud";
            OllamaBaseUrl     = "https://ollama.com";
            SystemPrompt      = DefaultSystemPrompt;
            ScreenshotKeybind = "F8";
            PanelWidth                = 520;
            PanelHeight               = 650;
            FontSize                  = 14;
            MaxNarrativeLogEntries    = 50;
            MaxChatHistorySessions    = 20;
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
                { m_Setting.GetOptionGroupLocaleID(Setting.kGeneralGroup), "General" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OllamaApiKey)),      "Ollama API Key" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OllamaApiKey)),       "Your Ollama cloud API key. Never share this." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OllamaModel)),       "Model" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OllamaModel)),        "Model ID (e.g. kimi-k2.5:cloud)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OllamaBaseUrl)),     "API Base URL" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OllamaBaseUrl)),      "OpenAI-compatible endpoint base URL (e.g. https://ollama.com)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SystemPrompt)),      "System Prompt" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SystemPrompt)),       "Advisor persona prompt sent to the AI at the start of every conversation." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ScreenshotKeybind)), "Screenshot Keybind" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ScreenshotKeybind)),  "Unity KeyCode name for capturing a screenshot (default: F8)." },
                { m_Setting.GetOptionGroupLocaleID(Setting.kUIGroup), "UI Settings" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PanelWidth)),  "Panel Width" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PanelWidth)),   "Default width of the CityAgent panel in pixels (400–1600). You can also drag panel edges to resize." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PanelHeight)), "Panel Height" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PanelHeight)),  "Default height of the CityAgent panel in pixels (400–1200). You can also drag panel edges to resize." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FontSize)),    "Font Size" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FontSize)),     "Base font size for all panel text (11–32)." },
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
