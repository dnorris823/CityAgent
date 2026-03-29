using CityAgent.Systems;
using Newtonsoft.Json;

namespace CityAgent.Systems.Tools
{
    /// <summary>
    /// Returns a summary of city traffic conditions: average flow score, total road segments,
    /// and number of bottleneck (congested) segments.
    /// </summary>
    public class GetTrafficSummaryTool : ICityAgentTool
    {
        private readonly CityDataSystem m_Data;

        public GetTrafficSummaryTool(CityDataSystem data) => m_Data = data;

        public string Name        => "get_traffic_summary";
        public string Description => "Returns a summary of city traffic conditions: average flow score (higher = faster traffic, -1 = no data), total road segments, and number of bottleneck (congested) segments.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

        public string Execute(string inputJson)
        {
            return JsonConvert.SerializeObject(new
            {
                flow_score       = m_Data.TrafficFlowScore > -0.5f
                    ? (object)m_Data.TrafficFlowScore
                    : (object)"unavailable",
                total_roads      = m_Data.TotalRoads,
                bottleneck_count = m_Data.BottleneckCount
            });
        }
    }
}
