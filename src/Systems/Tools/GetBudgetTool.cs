using CityAgent.Systems;
using Newtonsoft.Json;

namespace CityAgent.Systems.Tools
{
    /// <summary>
    /// Returns city financial data: per-category income and expenses, current balance, and loan details.
    /// If the budget system is not yet initialized, returns status="unavailable".
    /// </summary>
    public class GetBudgetTool : ICityAgentTool
    {
        private readonly CityDataSystem m_Data;

        public GetBudgetTool(CityDataSystem data) => m_Data = data;

        public string Name        => "get_budget";
        public string Description => "Returns the city's financial data: per-category income (taxes by zone type, service fees, government subsidies, export revenue), per-category expenses (service upkeep, loan interest, imports, import services, subsidies, map tiles), current balance, and active loan details. All values are raw integers in CS2 city funds units.";
        public string InputSchema => "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

        public string Execute(string inputJson)
        {
            if (!m_Data.BudgetAvailable)
            {
                return JsonConvert.SerializeObject(new
                {
                    status = "unavailable",
                    reason = "Budget system not yet initialized"
                });
            }

            return JsonConvert.SerializeObject(new
            {
                balance        = m_Data.Balance,
                total_income   = m_Data.TotalIncome,
                total_expenses = m_Data.TotalExpenses,
                income = new
                {
                    residential_tax    = m_Data.TaxResidential,
                    commercial_tax     = m_Data.TaxCommercial,
                    industrial_tax     = m_Data.TaxIndustrial,
                    office_tax         = m_Data.TaxOffice,
                    service_fees       = m_Data.ServiceFees,       // healthcare, education, parking, transit, utilities fees
                    government_subsidy = m_Data.GovernmentSubsidy,
                    export_revenue     = m_Data.ExportRevenue       // electricity + water exports
                },
                expenses = new
                {
                    service_upkeep   = m_Data.ServiceUpkeep,
                    loan_interest    = m_Data.LoanInterestExpense,
                    map_tiles        = m_Data.MapTileUpkeep,
                    imports          = m_Data.ImportCosts,          // electricity/water imports, sewage export
                    import_services  = m_Data.ImportSvcCosts,       // police/ambulance/hearse/fire/garbage imports
                    subsidies        = m_Data.Subsidies
                },
                loan = m_Data.LoanActive
                    ? (object)new
                    {
                        active              = true,
                        balance             = m_Data.LoanBalance,
                        daily_payment       = m_Data.LoanDailyPayment,
                        daily_interest_rate = m_Data.LoanDailyInterestRate
                    }
                    : (object)new { active = false }
            });
        }
    }
}
