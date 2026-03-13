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

        public Setting(IMod mod) : base(mod) { }

        /// <summary>
        /// Anthropic API key. Stored in the mod's settings file (not in source control).
        /// </summary>
        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUITextInput]
        public string AnthropicApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Claude model to use. Defaults to the latest Sonnet.
        /// </summary>
        [SettingsUISection(kSection, kGeneralGroup)]
        [SettingsUITextInput]
        public string ClaudeModel { get; set; } = "claude-sonnet-4-6";

        public override void SetDefaults()
        {
            AnthropicApiKey = string.Empty;
            ClaudeModel = "claude-sonnet-4-6";
        }
    }

    /// <summary>
    /// English locale strings for the settings panel.
    /// </summary>
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
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AnthropicApiKey)), "Anthropic API Key" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AnthropicApiKey)), "Your Anthropic API key for Claude access. Never share this." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ClaudeModel)), "Claude Model" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ClaudeModel)), "Claude model ID (e.g. claude-sonnet-4-6)" },
            };
        }

        public void Unload() { }
    }
}
