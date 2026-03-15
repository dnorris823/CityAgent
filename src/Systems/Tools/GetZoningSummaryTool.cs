using CityAgent.Systems;
using Newtonsoft.Json;

namespace CityAgent.Systems.Tools
{
    public class GetZoningSummaryTool : ICityAgentTool
    {
        private readonly CityDataSystem m_Data;

        public GetZoningSummaryTool(CityDataSystem data) => m_Data = data;

        public string Name        => "get_zoning_summary";
        public string Description => "Returns a summary of zone demand for residential, commercial, and industrial areas. Direct zone cell counts are not yet implemented; demand indices are used as a proxy.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

        public string Execute(string inputJson)
        {
            return JsonConvert.SerializeObject(new
            {
                residential_demand_index = m_Data.ResidentialDemand,
                commercial_demand_index  = m_Data.CommercialDemand,
                industrial_demand_index  = m_Data.IndustrialDemand,
                office_demand_index      = m_Data.OfficeDemand,
                note                     = "Direct zone cell counts not yet implemented; demand indices used as proxy"
            });
        }
    }
}
