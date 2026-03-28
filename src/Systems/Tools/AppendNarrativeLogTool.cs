using Newtonsoft.Json.Linq;

namespace CityAgent.Systems.Tools
{
    public class AppendNarrativeLogTool : ICityAgentTool
    {
        private readonly NarrativeMemorySystem m_Memory;

        public AppendNarrativeLogTool(NarrativeMemorySystem memory) => m_Memory = memory;

        public string Name        => "append_narrative_log";
        public string Description => "Append a timestamped narrative entry to the city's narrative log. Use this after every substantive conversation to record what happened — new developments, decisions made, events that occurred. Entries are automatically dated and tagged with the session number.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{\"entry\":{\"type\":\"string\",\"description\":\"The narrative log entry to append (markdown text describing what happened)\"}},\"required\":[\"entry\"]}";

        public string Execute(string inputJson)
        {
            var input = JObject.Parse(inputJson);
            string entry = input["entry"]?.Value<string>() ?? "";
            return m_Memory.AppendToLogAsync(entry).GetAwaiter().GetResult();
        }
    }
}
