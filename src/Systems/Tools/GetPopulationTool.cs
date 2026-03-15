using CityAgent.Systems;
using Newtonsoft.Json;

namespace CityAgent.Systems.Tools
{
    public class GetPopulationTool : ICityAgentTool
    {
        private readonly CityDataSystem m_Data;

        public GetPopulationTool(CityDataSystem data) => m_Data = data;

        public string Name        => "get_population";
        public string Description => "Returns the current city population (citizen count) and number of occupied households.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

        public string Execute(string inputJson)
        {
            return JsonConvert.SerializeObject(new
            {
                total_population  = m_Data.TotalPopulation,
                total_households  = m_Data.TotalHouseholds
            });
        }
    }
}
