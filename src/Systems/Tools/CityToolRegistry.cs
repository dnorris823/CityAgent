using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CityAgent.Systems.Tools
{
    /// <summary>
    /// Holds all registered ICityAgentTool implementations.
    /// Produces the JSON "tools" array for the Claude API and dispatches tool_use calls.
    /// </summary>
    public class CityToolRegistry
    {
        private readonly Dictionary<string, ICityAgentTool> m_Tools =
            new Dictionary<string, ICityAgentTool>(StringComparer.Ordinal);

        public int ToolCount => m_Tools.Count;

        public void Register(ICityAgentTool tool)
        {
            m_Tools[tool.Name] = tool;
        }

        /// <summary>
        /// Returns the Claude API "tools" array JSON string.
        /// Format: [{ "name": "...", "description": "...", "input_schema": {...} }, ...]
        /// </summary>
        public string GetToolsJson()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var tool in m_Tools.Values)
            {
                if (!IsToolEnabled(tool.Name)) continue;
                if (!first) sb.Append(',');
                first = false;

                sb.Append('{');
                sb.Append($"\"name\":{JsonConvert.SerializeObject(tool.Name)},");
                sb.Append($"\"description\":{JsonConvert.SerializeObject(tool.Description)},");
                sb.Append($"\"input_schema\":{tool.InputSchema}");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Produces an OpenAI-compatible tools array for Ollama /v1/chat/completions.
        /// Format: [{"type":"function","function":{"name":"...","description":"...","parameters":{...}}}]
        /// </summary>
        public string GetToolsJsonOpenAI()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var tool in m_Tools.Values)
            {
                if (!IsToolEnabled(tool.Name)) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"type\":\"function\",\"function\":{");
                sb.Append($"\"name\":{JsonConvert.SerializeObject(tool.Name)},");
                sb.Append($"\"description\":{JsonConvert.SerializeObject(tool.Description)},");
                sb.Append($"\"parameters\":{tool.InputSchema}");
                sb.Append("}}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Returns false if the player has disabled this tool in mod settings.
        /// Memory tools are always enabled (default case returns true).
        /// </summary>
        private static bool IsToolEnabled(string toolName)
        {
            var s = Mod.ActiveSetting;
            if (s == null) return true; // safety: settings not loaded yet

            return toolName switch
            {
                "get_population"       => s.EnablePopulationTool,
                "get_building_demand"  => s.EnableBuildingDemandTool,
                "get_workforce"        => s.EnableWorkforceTool,
                "get_zoning_summary"   => s.EnableZoningSummaryTool,
                "get_budget"           => s.EnableBudgetTool,
                "get_traffic_summary"  => s.EnableTrafficSummaryTool,
                "get_services_summary" => s.EnableServicesSummaryTool,
                _                      => true  // memory tools and any future non-data tools
            };
        }

        /// <summary>
        /// Dispatches a tool_use call by name. Returns a JSON result string.
        /// On error, returns a JSON error object so the conversation can continue gracefully.
        /// </summary>
        public string Dispatch(string toolName, string inputJson)
        {
            if (!m_Tools.TryGetValue(toolName, out var tool))
            {
                return JsonConvert.SerializeObject(new
                {
                    error = $"Unknown tool: {toolName}"
                });
            }

            try
            {
                return tool.Execute(inputJson);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    error = $"Tool '{toolName}' threw an exception: {ex.Message}"
                });
            }
        }
    }
}
