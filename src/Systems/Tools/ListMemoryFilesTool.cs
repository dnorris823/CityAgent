namespace CityAgent.Systems.Tools
{
    public class ListMemoryFilesTool : ICityAgentTool
    {
        private readonly NarrativeMemorySystem m_Memory;

        public ListMemoryFilesTool(NarrativeMemorySystem memory) => m_Memory = memory;

        public string Name        => "list_memory_files";
        public string Description => "List all narrative memory files for the current city with their sizes, last-modified dates, and whether they are core (non-deletable) files. Use this to see what memory files exist before reading or managing them.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

        public string Execute(string inputJson)
        {
            return m_Memory.ListFiles();
        }
    }
}
