using Newtonsoft.Json.Linq;

namespace CityAgent.Systems.Tools
{
    public class WriteMemoryFileTool : ICityAgentTool
    {
        private readonly NarrativeMemorySystem m_Memory;

        public WriteMemoryFileTool(NarrativeMemorySystem memory) => m_Memory = memory;

        public string Name        => "write_memory_file";
        public string Description => "Overwrite an existing narrative memory file with new content. Use this to update characters, districts, city plans, challenges, milestones, style notes, economy, lore, or the city index. The entire file is replaced — include all content you want to keep.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{\"filename\":{\"type\":\"string\",\"description\":\"The memory file to overwrite (e.g. characters.md)\"},\"content\":{\"type\":\"string\",\"description\":\"The complete new content for the file (markdown format)\"}},\"required\":[\"filename\",\"content\"]}";

        public string Execute(string inputJson)
        {
            var input = JObject.Parse(inputJson);
            string filename = input["filename"]?.Value<string>() ?? "";
            string content  = input["content"]?.Value<string>() ?? "";
            return m_Memory.WriteFileAsync(filename, content).GetAwaiter().GetResult();
        }
    }
}
