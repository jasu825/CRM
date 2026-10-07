using System.Text.Json;

namespace AcxiomCRM.ViewModels.Dashboard;

public class DashboardViewModel
{
    // --- 8 Required KPI Cards ---
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }

    // --- Authorization Scoping Metadata ---
    public string UserRole { get; set; } = "Admin";
    public string UserDisplayName { get; set; } = "Administrator";
    public bool IsAdminOrManager { get; set; } = true;
    public string ScopeDescription { get; set; } = "Enterprise Overview (All Records)";

    // --- Chart.js Data Payloads ---
    public LeadStatusChartData LeadStatusChart { get; set; } = new();
    public OpportunityPipelineChartData OpportunityPipelineChart { get; set; } = new();
    public MonthlySalesChartData MonthlySalesChart { get; set; } = new();

    // Helper JSON serializations for razor inline injection
    public string LeadStatusChartJson => JsonSerializer.Serialize(LeadStatusChart);
    public string OpportunityPipelineChartJson => JsonSerializer.Serialize(OpportunityPipelineChart);
    public string MonthlySalesChartJson => JsonSerializer.Serialize(MonthlySalesChart);
}

public class LeadStatusChartData
{
    public List<string> Labels { get; set; } = new();
    public List<int> Counts { get; set; } = new();
    public List<string> BackgroundColors { get; set; } = new();
}

public class OpportunityPipelineChartData
{
    public List<string> Labels { get; set; } = new();
    public List<int> Counts { get; set; } = new();
    public List<decimal> Amounts { get; set; } = new();
    public List<string> BackgroundColors { get; set; } = new();
}

public class MonthlySalesChartData
{
    public List<string> Months { get; set; } = new();
    public List<decimal> Revenue { get; set; } = new();
    public List<int> WonDealsCount { get; set; } = new();
}
