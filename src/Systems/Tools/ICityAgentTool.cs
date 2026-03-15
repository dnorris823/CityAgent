namespace CityAgent.Systems.Tools
{
    /// <summary>
    /// Contract for a tool that Claude can invoke during an agentic conversation.
    /// Each tool maps to one entry in the Claude API "tools" array.
    /// </summary>
    public interface ICityAgentTool
    {
        /// <summary>Tool name as Claude will reference it (e.g. "get_population").</summary>
        string Name { get; }

        /// <summary>Human-readable description sent to Claude so it knows when to call the tool.</summary>
        string Description { get; }

        /// <summary>Raw JSON Schema object string for the tool's input parameters.</summary>
        string InputSchema { get; }

        /// <summary>
        /// Execute the tool with the given JSON input and return a JSON result string.
        /// </summary>
        string Execute(string inputJson);
    }
}
