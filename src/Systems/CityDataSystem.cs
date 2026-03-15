using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Simulation;
using Game.Tools;
using Unity.Entities;

namespace CityAgent.Systems
{
    /// <summary>
    /// Phase 2: reads live city stats from the ECS every ~128 frames and caches them.
    /// All tool implementations read from the public properties — no direct ECS access needed.
    /// </summary>
    public partial class CityDataSystem : GameSystemBase
    {
        // ── Public cached properties (written on update, read by tools) ──────────────

        public int TotalPopulation   { get; private set; }
        public int TotalHouseholds   { get; private set; }
        public int TotalEmployed     { get; private set; }
        public int TotalWorkplaces   { get; private set; }
        public int ResidentialDemand { get; private set; }   // int3.x (low density), 0–100
        public int CommercialDemand  { get; private set; }   // int, 0–100
        public int IndustrialDemand  { get; private set; }   // industrialBuildingDemand, 0–100
        public int OfficeDemand      { get; private set; }   // officeBuildingDemand, 0–100

        // ── ECS queries ──────────────────────────────────────────────────────────────

        private EntityQuery m_CitizenQuery;
        private EntityQuery m_HouseholdQuery;
        private EntityQuery m_EmployeeQuery;
        private EntityQuery m_WorkProviderQuery;

        // ── Demand system refs ────────────────────────────────────────────────────────

        private SimulationSystem        m_SimulationSystem        = null!;
        private ResidentialDemandSystem m_ResidentialDemandSystem = null!;
        private CommercialDemandSystem  m_CommercialDemandSystem  = null!;
        private IndustrialDemandSystem  m_IndustrialDemandSystem  = null!;

        // ── Lifecycle ─────────────────────────────────────────────────────────────────

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info($"{nameof(CityDataSystem)}.{nameof(OnCreate)}");

            // Citizens — excludes tourists (TravelPurpose) and temporary entities
            m_CitizenQuery = GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<Citizen>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<TravelPurpose>() }
            });

            // Households that own a property
            m_HouseholdQuery = GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<Household>(), ComponentType.ReadOnly<PropertyRenter>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            // Employed citizens
            m_EmployeeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<Employee>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            // Buildings / companies that provide jobs
            m_WorkProviderQuery = GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<WorkProvider>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Abandoned>() }
            });

            m_SimulationSystem        = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_ResidentialDemandSystem = World.GetOrCreateSystemManaged<ResidentialDemandSystem>();
            m_CommercialDemandSystem  = World.GetOrCreateSystemManaged<CommercialDemandSystem>();
            m_IndustrialDemandSystem  = World.GetOrCreateSystemManaged<IndustrialDemandSystem>();

            Mod.Log.Info($"{nameof(CityDataSystem)} initialised.");
        }

        protected override void OnUpdate()
        {
            // Throttle: update roughly every 4 real seconds (128 frames at ~30 fps)
            if (m_SimulationSystem.frameIndex % 128 != 77) return;

            TotalPopulation  = m_CitizenQuery.CalculateEntityCount();
            TotalHouseholds  = m_HouseholdQuery.CalculateEntityCount();
            TotalEmployed    = m_EmployeeQuery.CalculateEntityCount();
            TotalWorkplaces  = m_WorkProviderQuery.CalculateEntityCount();

            // ResidentialDemandSystem.buildingDemand is int3 (low/medium/high density demand).
            // Use .x (low density) as the primary residential demand signal.
            ResidentialDemand = m_ResidentialDemandSystem.buildingDemand.x;

            // CommercialDemandSystem.buildingDemand is a plain int.
            CommercialDemand = m_CommercialDemandSystem.buildingDemand;

            // IndustrialDemandSystem splits industrial and office; capture both.
            IndustrialDemand = m_IndustrialDemandSystem.industrialBuildingDemand;
            OfficeDemand     = m_IndustrialDemandSystem.officeBuildingDemand;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mod.Log.Info($"{nameof(CityDataSystem)}.{nameof(OnDestroy)}");
        }
    }
}
