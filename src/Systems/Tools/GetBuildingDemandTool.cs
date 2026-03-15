using CityAgent.Systems;
using Newtonsoft.Json;

namespace CityAgent.Systems.Tools
{
    public class GetBuildingDemandTool : ICityAgentTool
    {
        private readonly CityDataSystem m_Data;

        public GetBuildingDemandTool(CityDataSystem data) => m_Data = data;

        public string Name        => "get_building_demand";
        public string Description => "Returns current residential, commercial, and industrial building demand indices (0–100, higher means more demand pressure).";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

        public string Execute(string inputJson)
        {
            return JsonConvert.SerializeObject(new
            {
                residential_demand = m_Data.ResidentialDemand,
                commercial_demand  = m_Data.CommercialDemand,
                industrial_demand  = m_Data.IndustrialDemand,
                office_demand      = m_Data.OfficeDemand,
                scale              = "0-100 where 100 is maximum demand pressure"
            });
        }
    }
}
