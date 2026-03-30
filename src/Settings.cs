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
    [SettingsUIGroupOrder(kProviderGroup, kClaudeGroup, kOllamaGroup, kUIGroup, kMemoryGroup, kDataToolsGroup, kWebSearchGroup)]
    [SettingsUIShowGroupName(kProviderGroup, kClaudeGroup, kOllamaGroup, kUIGroup, kMemoryGroup, kDataToolsGroup, kWebSearchGroup)]
    public class Setting : ModSetting
    {
        public const string kSection        = "Main";
        public const string kProviderGroup  = "Provider";
        public const string kClaudeGroup    = "Claude";
        public const string kOllamaGroup    = "Ollama";
        public const string kUIGroup        = "UI";
        public const string kMemoryGroup    = "Memory";
        public const string kDataToolsGroup = "DataTools";
        public const string kWebSearchGroup = "WebSearch";

        private const string DefaultSystemPrompt =
            "You are CityAgent, an AI city planning advisor in the style of CityPlannerPlays. " +
            "Analyze the city screenshot and data, then provide engaging narrative commentary and " +
            "specific build recommendations. Be enthusiastic but practical. Focus on what would " +
            "make the most impact for the city's current challenges.\n\n" +
            "You have access to live city data tools. Use them proactively when relevant:\n" +
            "- get_population: Call when discussing growth, housing, or demographics.\n" +
            "- get_building_demand: Call when advising on what to zone or build next.\n" +
            "- get_workforce: Call when discussing employment, jobs, or economic productivity.\n" +
            "- get_zoning_summary: Call when analyzing zone balance or development patterns.\n" +
            "- get_budget: Call when discussing city finances, taxes, expenses, loans, or fiscal health.\n" +
            "- get_traffic_summary: Call when discussing traffic, congestion, road networks, or commute quality.\n" +
            "- get_services_summary: Call when discussing utilities (power, water, sewage) or public health.\n\n" +
            "Always call relevant tools before answering questions about these topics. " +
            "Combine tool data with the screenshot for comprehensive analysis.";

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


        // --- Data Tools section ---

        [SettingsUISection(kSection, kDataToolsGroup)]
        public bool EnablePopulationTool { get; set; } = true;

        [SettingsUISection(kSection, kDataToolsGroup)]
        public bool EnableBuildingDemandTool { get; set; } = true;

        [SettingsUISection(kSection, kDataToolsGroup)]
        public bool EnableWorkforceTool { get; set; } = true;

        [SettingsUISection(kSection, kDataToolsGroup)]
        public bool EnableZoningSummaryTool { get; set; } = true;

        [SettingsUISection(kSection, kDataToolsGroup)]
        public bool EnableBudgetTool { get; set; } = true;

        [SettingsUISection(kSection, kDataToolsGroup)]
        public bool EnableTrafficSummaryTool { get; set; } = true;

        [SettingsUISection(kSection, kDataToolsGroup)]
        public bool EnableServicesSummaryTool { get; set; } = true;

        // --- Web Search section ---

        [SettingsUISection(kSection, kWebSearchGroup)]
        [SettingsUITextInput]
        public string BraveSearchApiKey { get; set; } = string.Empty;

        [SettingsUISection(kSection, kWebSearchGroup)]
        public bool WebSearchEnabled { get; set; } = false;

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
            EnablePopulationTool      = true;
            EnableBuildingDemandTool  = true;
            EnableWorkforceTool       = true;
            EnableZoningSummaryTool   = true;
            EnableBudgetTool          = true;
            EnableTrafficSummaryTool  = true;
            EnableServicesSummaryTool = true;
            BraveSearchApiKey         = string.Empty;
            WebSearchEnabled          = false;
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

                // Data Tools group
                { m_Setting.GetOptionGroupLocaleID(Setting.kDataToolsGroup), "Data Tools" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnablePopulationTool)),      "Population" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnablePopulationTool)),       "Include population data tool in AI requests." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableBuildingDemandTool)),  "Building Demand" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableBuildingDemandTool)),   "Include building demand data tool in AI requests." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableWorkforceTool)),       "Workforce" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableWorkforceTool)),        "Include workforce data tool in AI requests." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableZoningSummaryTool)),   "Zoning Summary" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableZoningSummaryTool)),    "Include zoning summary data tool in AI requests." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableBudgetTool)),          "City Finances" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableBudgetTool)),           "Include city budget and loan data tool in AI requests." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableTrafficSummaryTool)),  "Traffic" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableTrafficSummaryTool)),   "Include traffic flow and congestion data tool in AI requests." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableServicesSummaryTool)), "City Services" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableServicesSummaryTool)),  "Include electricity, water, sewage, and health data tool in AI requests." },

                // Web Search group
                { m_Setting.GetOptionGroupLocaleID(Setting.kWebSearchGroup),                    "Web Search" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BraveSearchApiKey)),          "Brave Search API Key" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BraveSearchApiKey)),           "Your Brave Search API key for web search. Get one free at api-dashboard.search.brave.com." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.WebSearchEnabled)),           "Enable Web Search" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.WebSearchEnabled)),            "Allow the AI advisor to search the web for real-world urban planning information." },
            };
        }

        public void Unload() { }
    }
}
