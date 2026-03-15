using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using System.Collections.Generic;

namespace CityAgent
{
    [FileLocation(nameof(CityAgent))]
    [SettingsUIGroupOrder(kGeneralGroup)]
    [SettingsUIShowGroupName(kGeneralGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kGeneralGroup = "General";

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

        public override void SetDefaults()
        {
            OllamaApiKey      = string.Empty;
            OllamaModel       = "kimi-k2.5:cloud";
            OllamaBaseUrl     = "https://ollama.com";
            SystemPrompt      = DefaultSystemPrompt;
            ScreenshotKeybind = "F8";
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
            };
        }

        public void Unload() { }
    }
}
