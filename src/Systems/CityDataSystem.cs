using Game;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Net;
using Game.Simulation;
using Game.Tools;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;

namespace CityAgent.Systems
{
    /// <summary>
    /// Phase 2+: reads live city stats from the ECS every ~128 frames and caches them.
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

        // Budget properties
        public bool  BudgetAvailable     { get; private set; }
        public int   Balance             { get; private set; }
        public int   TotalIncome         { get; private set; }
        public int   TotalExpenses       { get; private set; }
        public int   TaxResidential      { get; private set; }
        public int   TaxCommercial       { get; private set; }
        public int   TaxIndustrial       { get; private set; }
        public int   TaxOffice           { get; private set; }
        public int   ServiceUpkeep       { get; private set; }
        public int   LoanInterestExpense { get; private set; }
        public int   MapTileUpkeep       { get; private set; }
        public int   ImportCosts         { get; private set; }
        public int   Subsidies           { get; private set; }

        // Loan properties
        public bool  LoanActive           { get; private set; }
        public int   LoanBalance          { get; private set; }
        public int   LoanDailyPayment     { get; private set; }
        public float LoanDailyInterestRate { get; private set; }

        // Traffic properties
        public float TrafficFlowScore { get; private set; } = -1f;
        public int   TotalRoads       { get; private set; }
        public int   BottleneckCount  { get; private set; }

        // Services properties
        public int ElecProduction  { get; private set; }
        public int ElecConsumption { get; private set; }
        public int ElecFulfilled   { get; private set; }
        public int WaterCapacity    { get; private set; }
        public int WaterConsumption { get; private set; }
        public int WaterFulfilled   { get; private set; }
        public int SewageCapacity   { get; private set; }
        public int SewageFulfilled  { get; private set; }
        public int SickCitizenCount { get; private set; }

        // ── ECS queries ──────────────────────────────────────────────────────────────

        private EntityQuery m_CitizenQuery;
        private EntityQuery m_HouseholdQuery;
        private EntityQuery m_EmployeeQuery;
        private EntityQuery m_WorkProviderQuery;
        private EntityQuery m_RoadQuery;
        private EntityQuery m_BottleneckQuery;
        private EntityQuery m_SickCitizenQuery;

        // ── Demand system refs ────────────────────────────────────────────────────────

        private SimulationSystem        m_SimulationSystem        = null!;
        private ResidentialDemandSystem m_ResidentialDemandSystem = null!;
        private CommercialDemandSystem  m_CommercialDemandSystem  = null!;
        private IndustrialDemandSystem  m_IndustrialDemandSystem  = null!;

        // ── Budget / services system refs ─────────────────────────────────────────────

        private GameSystemBase m_BudgetSystem      = null!;
        private GameSystemBase m_LoanSystem        = null!;
        private GameSystemBase m_ElectricitySystem = null!;
        private GameSystemBase m_WaterSystem       = null!;

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

            // Road segments (for traffic flow aggregate)
            m_RoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<Road>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            // Bottleneck-marked road segments (congested)
            m_BottleneckQuery = GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<Bottleneck>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            // Citizens with an active health problem
            m_SickCitizenQuery = GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<HealthProblem>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            m_SimulationSystem        = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_ResidentialDemandSystem = World.GetOrCreateSystemManaged<ResidentialDemandSystem>();
            m_CommercialDemandSystem  = World.GetOrCreateSystemManaged<CommercialDemandSystem>();
            m_IndustrialDemandSystem  = World.GetOrCreateSystemManaged<IndustrialDemandSystem>();

            m_BudgetSystem      = World.GetOrCreateSystemManaged<ServiceBudgetUISystem>();
            m_LoanSystem        = World.GetOrCreateSystemManaged<LoanSystem>();
            m_ElectricitySystem = World.GetOrCreateSystemManaged<ElectricityStatisticsSystem>();
            m_WaterSystem       = World.GetOrCreateSystemManaged<WaterStatisticsSystem>();

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

            // ── Budget ────────────────────────────────────────────────────────────────

            var budget = m_BudgetSystem as ICityServiceBudgetSystem;
            if (budget != null)
            {
                BudgetAvailable     = true;
                Balance             = budget.GetBalance();
                TotalIncome         = budget.GetTotalIncome();
                TotalExpenses       = budget.GetTotalExpenses();
                TaxResidential      = budget.GetIncome(IncomeSource.TaxResidential);
                TaxCommercial       = budget.GetIncome(IncomeSource.TaxCommercial);
                TaxIndustrial       = budget.GetIncome(IncomeSource.TaxIndustrial);
                TaxOffice           = budget.GetIncome(IncomeSource.TaxOffice);
                ServiceUpkeep       = budget.GetExpense(ExpenseSource.ServiceUpkeep);
                LoanInterestExpense = budget.GetExpense(ExpenseSource.LoanInterest);
                MapTileUpkeep       = budget.GetExpense(ExpenseSource.MapTileUpkeep);
                ImportCosts         = budget.GetExpense(ExpenseSource.ImportElectricity)
                                    + budget.GetExpense(ExpenseSource.ImportWater)
                                    + budget.GetExpense(ExpenseSource.ExportSewage);
                Subsidies           = budget.GetExpense(ExpenseSource.SubsidyResidential)
                                    + budget.GetExpense(ExpenseSource.SubsidyCommercial)
                                    + budget.GetExpense(ExpenseSource.SubsidyIndustrial)
                                    + budget.GetExpense(ExpenseSource.SubsidyOffice);
            }
            else
            {
                BudgetAvailable = false;
            }

            // ── Loans ─────────────────────────────────────────────────────────────────

            var loan = (m_LoanSystem as ILoanSystem)?.CurrentLoan;
            if (loan.HasValue && loan.Value.m_Amount > 0)
            {
                LoanActive            = true;
                LoanBalance           = loan.Value.m_Amount;
                LoanDailyPayment      = loan.Value.m_DailyPayment;
                LoanDailyInterestRate = loan.Value.m_DailyInterestRate;
            }
            else
            {
                LoanActive            = false;
                LoanBalance           = 0;
                LoanDailyPayment      = 0;
                LoanDailyInterestRate = 0f;
            }

            // ── Traffic ───────────────────────────────────────────────────────────────

            TotalRoads      = m_RoadQuery.CalculateEntityCount();
            BottleneckCount = m_BottleneckQuery.CalculateEntityCount();

            if (TotalRoads > 0)
            {
                var roads = m_RoadQuery.ToComponentDataArray<Road>(Allocator.TempJob);
                float totalDuration = 0f, totalDistance = 0f;
                foreach (var road in roads)
                {
                    totalDuration += road.m_TrafficFlowDuration0.x + road.m_TrafficFlowDuration1.x;
                    totalDistance  += road.m_TrafficFlowDistance0.x + road.m_TrafficFlowDistance1.x;
                }
                roads.Dispose();
                TrafficFlowScore = (totalDuration > 0.001f) ? (totalDistance / totalDuration) : -1f;
            }
            else
            {
                TrafficFlowScore = -1f;
            }

            // ── Services ──────────────────────────────────────────────────────────────

            var elec = m_ElectricitySystem as IElectricityStatisticsSystem;
            ElecProduction  = elec?.production ?? 0;
            ElecConsumption = elec?.consumption ?? 0;
            ElecFulfilled   = elec?.fulfilledConsumption ?? 0;

            var water = m_WaterSystem as IWaterStatisticsSystem;
            WaterCapacity    = water?.freshCapacity ?? 0;
            WaterConsumption = water?.freshConsumption ?? 0;
            WaterFulfilled   = water?.fulfilledFreshConsumption ?? 0;
            SewageCapacity   = water?.sewageCapacity ?? 0;
            SewageFulfilled  = water?.fulfilledSewageConsumption ?? 0;

            SickCitizenCount = m_SickCitizenQuery.CalculateEntityCount();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mod.Log.Info($"{nameof(CityDataSystem)}.{nameof(OnDestroy)}");
        }
    }
}
