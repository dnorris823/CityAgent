using Newtonsoft.Json.Linq;

namespace CityAgent.Systems.Tools
{
    public class DeleteMemoryFileTool : ICityAgentTool
    {
        private readonly NarrativeMemorySystem m_Memory;

        public DeleteMemoryFileTool(NarrativeMemorySystem memory) => m_Memory = memory;

        public string Name        => "delete_memory_file";
        public string Description => "Delete a dynamically-created memory file. Core files (_index.md, characters.md, districts.md, city-plan.md, narrative-log.md, challenges.md, milestones.md, style-notes.md, economy.md, lore.md) cannot be deleted. Use this to clean up custom memory files that are no longer needed.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{\"filename\":{\"type\":\"string\",\"description\":\"The memory file to delete (must not be a core file)\"}},\"required\":[\"filename\"]}";

        public string Execute(string inputJson)
        {
            var input = JObject.Parse(inputJson);
            string filename = input["filename"]?.Value<string>() ?? "";
            return m_Memory.DeleteFile(filename);
        }
    }
}
