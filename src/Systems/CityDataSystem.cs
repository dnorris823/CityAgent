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

        // Budget properties — totals from ICityStatisticsSystem (matches game HUD)
        public bool  BudgetAvailable     { get; private set; }
        public int   Balance             { get; private set; }
        public int   TotalIncome         { get; private set; }
        public int   TotalExpenses       { get; private set; }
        // Per-category income from ICityServiceBudgetSystem
        public int   TaxResidential      { get; private set; }
        public int   TaxCommercial       { get; private set; }
        public int   TaxIndustrial       { get; private set; }
        public int   TaxOffice           { get; private set; }
        public int   ServiceFees         { get; private set; }  // healthcare+elec+edu+parking+transit+garbage+water fees
        public int   GovernmentSubsidy   { get; private set; }
        public int   ExportRevenue       { get; private set; }  // electricity+water exports
        // Per-category expenses
        public int   ServiceUpkeep       { get; private set; }
        public int   LoanInterestExpense { get; private set; }
        public int   MapTileUpkeep       { get; private set; }
        public int   ImportCosts         { get; private set; }  // electricity+water imports, sewage export
        public int   ImportSvcCosts      { get; private set; }  // police/ambulance/hearse/fire/garbage imports
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

        // ── Cache invalidation ───────────────────────────────────────────────────────

        private volatile bool m_ForceUpdate = false;

        // Stabilization state: after city load, re-read every 64 frames until count is
        // non-zero and stable for 2 consecutive reads, then revert to the 128-frame throttle.
        private int m_StabilizingCyclesLeft = 0;
        private int m_StabilizingFrameCounter = 0;
        private int m_LastStablePopulation = -1;
        private int m_StableReadCount = 0;

        /// <summary>
        /// Forces the next OnUpdate cycle to bypass the 128-frame throttle and re-read all
        /// ECS values immediately. Also enters a short stabilization period (up to 8 reads,
        /// 64 frames apart) until the citizen count is non-zero and stable across 2 reads.
        /// Call this when a new city is detected (e.g., after city name resolves).
        /// </summary>
        public void InvalidateCache()
        {
            m_ForceUpdate = true;
            m_StabilizingCyclesLeft  = 8;   // up to 8 extra re-reads after the forced one
            m_StabilizingFrameCounter = 0;
            m_LastStablePopulation   = -1;
            m_StableReadCount        = 0;
        }

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

        // ── Statistics / budget / services system refs ────────────────────────────────

        // CityStatisticsSystem computes pre-aggregated city-wide totals (same source as the
        // game's own HUD). Use this to read population and household count rather than counting
        // raw Citizen/Household entities — entity queries can under-count because citizens
        // temporarily carry additional components (e.g., TravelPurpose) while active.
        private GameSystemBase m_CityStatisticsSystem = null!;

        // Stored already cast so we know at init time which class actually implements the interface.
        // We try ServiceBudgetUISystem (UI layer) first, then CityServiceBudgetSystem (simulation
        // layer) as fallback — one of the two is the real implementor depending on CS2 version.
        private ICityServiceBudgetSystem? m_Budget = null;
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

            m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>();

            // Determine which budget system actually implements ICityServiceBudgetSystem.
            // Try the UI layer first; fall back to the simulation layer.
            m_Budget = World.GetOrCreateSystemManaged<ServiceBudgetUISystem>() as ICityServiceBudgetSystem
                    ?? World.GetOrCreateSystemManaged<CityServiceBudgetSystem>() as ICityServiceBudgetSystem;
            Mod.Log.Info($"[CityDataSystem] Budget system resolved: {(m_Budget != null ? m_Budget.GetType().Name : "none")}");

            m_LoanSystem        = World.GetOrCreateSystemManaged<LoanSystem>();
            m_ElectricitySystem = World.GetOrCreateSystemManaged<ElectricityStatisticsSystem>();
            m_WaterSystem       = World.GetOrCreateSystemManaged<WaterStatisticsSystem>();

            Mod.Log.Info($"{nameof(CityDataSystem)} initialised.");
        }

        protected override void OnUpdate()
        {
            // Throttle: update roughly every 4 real seconds (128 frames at ~30 fps).
            // Exception 1: bypass the throttle if InvalidateCache() was called (e.g., on city load).
            // Exception 2: if in stabilization mode (after a force-invalidation), re-read every 64
            //   frames until the citizen count is non-zero and stable for 2 consecutive reads.
            bool forceThisFrame = m_ForceUpdate;
            if (forceThisFrame)
            {
                m_ForceUpdate = false;
                Mod.Log.Info("[CityDataSystem] Cache invalidated — forcing immediate refresh.");
            }
            else if (m_StabilizingCyclesLeft > 0)
            {
                // Stabilization mode: fire every 64 frames until stable
                m_StabilizingFrameCounter++;
                if (m_StabilizingFrameCounter < 64)
                    return;
                m_StabilizingFrameCounter = 0;
                m_StabilizingCyclesLeft--;
                Mod.Log.Info($"[CityDataSystem] Stabilizing re-read (cycles left: {m_StabilizingCyclesLeft}).");
            }
            else if (m_SimulationSystem.frameIndex % 128 != 77)
            {
                return;
            }

            // Read population and household count from CityStatisticsSystem — the same
            // pre-aggregated source the game's HUD uses. Raw Citizen entity counts are
            // unreliable because a large fraction of citizens carry TravelPurpose at any
            // frame (going to work, school, shopping…), and any None-filter on that component
            // would under-count severely.
            var cityStats = m_CityStatisticsSystem as ICityStatisticsSystem;
            if (cityStats != null)
            {
                TotalPopulation = cityStats.GetStatisticValue(StatisticType.Population, 0);
                TotalHouseholds = cityStats.GetStatisticValue(StatisticType.HouseholdCount, 0);
                // Income/expense totals from the statistics system match the game's own HUD/economy
                // panel, which aggregates across all budget subsystems (service budget, trade, etc.).
                TotalIncome   = cityStats.GetStatisticValue(StatisticType.Income, 0);
                TotalExpenses = cityStats.GetStatisticValue(StatisticType.Expense, 0);
                Balance       = cityStats.GetStatisticValue(StatisticType.Money, 0);
                BudgetAvailable = true;
            }
            else
            {
                // Fallback: raw entity count (may under-count — see comment above)
                TotalPopulation = m_CitizenQuery.CalculateEntityCount();
                TotalHouseholds = m_HouseholdQuery.CalculateEntityCount();
            }

            TotalEmployed   = m_EmployeeQuery.CalculateEntityCount();
            TotalWorkplaces = m_WorkProviderQuery.CalculateEntityCount();

            // Diagnostic: log counts on every refresh so they can be compared against
            // the in-game UI population counter. Remove once values are confirmed correct.
            Mod.Log.Info($"[CityDataSystem] DIAG — population={TotalPopulation} households={TotalHouseholds} employed={TotalEmployed} workplaces={TotalWorkplaces} statsOk={cityStats != null} budgetOk={m_Budget != null} stabilizing={m_StabilizingCyclesLeft > 0}");

            // Stabilization check: if in stabilizing mode, exit early once population is
            // non-zero and matches the previous read (i.e., the city is fully streamed in).
            if (m_StabilizingCyclesLeft > 0 && TotalPopulation > 0)
            {
                if (TotalPopulation == m_LastStablePopulation)
                {
                    m_StableReadCount++;
                    if (m_StableReadCount >= 2)
                    {
                        m_StabilizingCyclesLeft = 0;
                        Mod.Log.Info($"[CityDataSystem] Population stabilized at {TotalPopulation} — exiting stabilization mode.");
                    }
                }
                else
                {
                    m_StableReadCount = 0;
                }
                m_LastStablePopulation = TotalPopulation;
            }

            // ResidentialDemandSystem.buildingDemand is int3 (low/medium/high density demand).
            // Use .x (low density) as the primary residential demand signal.
            ResidentialDemand = m_ResidentialDemandSystem.buildingDemand.x;

            // CommercialDemandSystem.buildingDemand is a plain int.
            CommercialDemand = m_CommercialDemandSystem.buildingDemand;

            // IndustrialDemandSystem splits industrial and office; capture both.
            IndustrialDemand = m_IndustrialDemandSystem.industrialBuildingDemand;
            OfficeDemand     = m_IndustrialDemandSystem.officeBuildingDemand;

            // ── Budget ────────────────────────────────────────────────────────────────

            // Per-category breakdown from ICityServiceBudgetSystem.
            // Totals (TotalIncome/TotalExpenses/Balance) come from ICityStatisticsSystem above
            // because GetTotalIncome() only covers the service budget subsystem; the statistics
            // system aggregates all budget subsystems and matches the game's economy panel.
            if (m_Budget != null)
            {
                TaxResidential   = m_Budget.GetIncome(IncomeSource.TaxResidential);
                TaxCommercial    = m_Budget.GetIncome(IncomeSource.TaxCommercial);
                TaxIndustrial    = m_Budget.GetIncome(IncomeSource.TaxIndustrial);
                TaxOffice        = m_Budget.GetIncome(IncomeSource.TaxOffice);
                ServiceFees      = m_Budget.GetIncome(IncomeSource.FeeHealthcare)
                                 + m_Budget.GetIncome(IncomeSource.FeeElectricity)
                                 + m_Budget.GetIncome(IncomeSource.FeeEducation)
                                 + m_Budget.GetIncome(IncomeSource.FeeParking)
                                 + m_Budget.GetIncome(IncomeSource.FeePublicTransport)
                                 + m_Budget.GetIncome(IncomeSource.FeeGarbage)
                                 + m_Budget.GetIncome(IncomeSource.FeeWater);
                GovernmentSubsidy = m_Budget.GetIncome(IncomeSource.GovernmentSubsidy);
                ExportRevenue    = m_Budget.GetIncome(IncomeSource.ExportElectricity)
                                 + m_Budget.GetIncome(IncomeSource.ExportWater);
                ServiceUpkeep    = m_Budget.GetExpense(ExpenseSource.ServiceUpkeep);
                LoanInterestExpense = m_Budget.GetExpense(ExpenseSource.LoanInterest);
                MapTileUpkeep    = m_Budget.GetExpense(ExpenseSource.MapTileUpkeep);
                ImportCosts      = m_Budget.GetExpense(ExpenseSource.ImportElectricity)
                                 + m_Budget.GetExpense(ExpenseSource.ImportWater)
                                 + m_Budget.GetExpense(ExpenseSource.ExportSewage);
                ImportSvcCosts   = m_Budget.GetExpense(ExpenseSource.ImportPoliceService)
                                 + m_Budget.GetExpense(ExpenseSource.ImportAmbulanceService)
                                 + m_Budget.GetExpense(ExpenseSource.ImportHearseService)
                                 + m_Budget.GetExpense(ExpenseSource.ImportFireEngineService)
                                 + m_Budget.GetExpense(ExpenseSource.ImportGarbageService);
                Subsidies        = m_Budget.GetExpense(ExpenseSource.SubsidyResidential)
                                 + m_Budget.GetExpense(ExpenseSource.SubsidyCommercial)
                                 + m_Budget.GetExpense(ExpenseSource.SubsidyIndustrial)
                                 + m_Budget.GetExpense(ExpenseSource.SubsidyOffice);
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
