using CityAgent.Systems;
using Newtonsoft.Json;

namespace CityAgent.Systems.Tools
{
    public class GetWorkforceTool : ICityAgentTool
    {
        private readonly CityDataSystem m_Data;

        public GetWorkforceTool(CityDataSystem data) => m_Data = data;

        public string Name        => "get_workforce";
        public string Description => "Returns total workplaces, employed citizens, and an estimated unemployment figure derived from population minus employed count.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

        public string Execute(string inputJson)
        {
            int unemploymentEstimate = m_Data.TotalPopulation - m_Data.TotalEmployed;
            return JsonConvert.SerializeObject(new
            {
                total_workplaces      = m_Data.TotalWorkplaces,
                total_employed        = m_Data.TotalEmployed,
                unemployment_estimate = unemploymentEstimate,
                note                  = "unemployment_estimate is population minus employed; not a direct ECS query"
            });
        }
    }
}
