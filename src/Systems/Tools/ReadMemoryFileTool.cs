using Newtonsoft.Json.Linq;

namespace CityAgent.Systems.Tools
{
    public class ReadMemoryFileTool : ICityAgentTool
    {
        private readonly NarrativeMemorySystem m_Memory;

        public ReadMemoryFileTool(NarrativeMemorySystem memory) => m_Memory = memory;

        public string Name        => "read_memory_file";
        public string Description => "Read a narrative memory file by filename. Returns the full markdown contents. Use this to check character details, district info, city plans, challenges, milestones, lore, or economy notes before referencing them in conversation.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{\"filename\":{\"type\":\"string\",\"description\":\"The memory file to read (e.g. characters.md, districts.md, city-plan.md)\"}},\"required\":[\"filename\"]}";

        public string Execute(string inputJson)
        {
            var input = JObject.Parse(inputJson);
            string filename = input["filename"]?.Value<string>() ?? "";
            return m_Memory.ReadFile(filename);
        }
    }
}
