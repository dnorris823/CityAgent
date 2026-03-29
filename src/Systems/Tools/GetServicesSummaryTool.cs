using CityAgent.Systems;
using Newtonsoft.Json;

namespace CityAgent.Systems.Tools
{
    /// <summary>
    /// Returns city service coverage data: electricity, water supply, sewage, and health metrics.
    /// </summary>
    public class GetServicesSummaryTool : ICityAgentTool
    {
        private readonly CityDataSystem m_Data;

        public GetServicesSummaryTool(CityDataSystem data) => m_Data = data;

        public string Name        => "get_services_summary";
        public string Description => "Returns city service coverage data: electricity (production/consumption/fulfilled), water supply (capacity/consumption/fulfilled), sewage (capacity/fulfilled), and health (sick citizen count vs total population for coverage proxy).";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

        public string Execute(string inputJson)
        {
            return JsonConvert.SerializeObject(new
            {
                electricity = new
                {
                    production  = m_Data.ElecProduction,
                    consumption = m_Data.ElecConsumption,
                    fulfilled   = m_Data.ElecFulfilled
                },
                water = new
                {
                    capacity    = m_Data.WaterCapacity,
                    consumption = m_Data.WaterConsumption,
                    fulfilled   = m_Data.WaterFulfilled
                },
                sewage = new
                {
                    capacity  = m_Data.SewageCapacity,
                    fulfilled = m_Data.SewageFulfilled
                },
                health = new
                {
                    sick_citizens    = m_Data.SickCitizenCount,
                    total_population = m_Data.TotalPopulation
                }
            });
        }
    }
}
