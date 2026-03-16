using Newtonsoft.Json.Linq;

namespace CityAgent.Systems.Tools
{
    public class CreateMemoryFileTool : ICityAgentTool
    {
        private readonly NarrativeMemorySystem m_Memory;

        public CreateMemoryFileTool(NarrativeMemorySystem memory) => m_Memory = memory;

        public string Name        => "create_memory_file";
        public string Description => "Create a new narrative memory file. Only use this when the player explicitly asks you to create a new memory file (e.g. 'create a memory file for infrastructure'). The filename must end in .md and must not already exist.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{\"filename\":{\"type\":\"string\",\"description\":\"Name for the new file (must end in .md, e.g. infrastructure.md)\"},\"content\":{\"type\":\"string\",\"description\":\"Initial content for the file (markdown format)\"}},\"required\":[\"filename\",\"content\"]}";

        public string Execute(string inputJson)
        {
            var input = JObject.Parse(inputJson);
            string filename = input["filename"]?.Value<string>() ?? "";
            string content  = input["content"]?.Value<string>() ?? "";
            return m_Memory.CreateFile(filename, content);
        }
    }
}
