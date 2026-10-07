using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Dashboard;

namespace AcxiomCRM.Services.Implementations;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(ApplicationDbContext context, ILogger<DashboardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DashboardViewModel> GetDashboardMetricsAsync(
        string? userId, 
        bool isAdminOrManager, 
        string? userDisplayName = null, 
        string? roleName = null)
    {
        var model = new DashboardViewModel
        {
            UserRole = roleName ?? (isAdminOrManager ? "Admin" : "SalesExecutive"),
            UserDisplayName = userDisplayName ?? (isAdminOrManager ? "Enterprise Manager" : "Sales Executive"),
            IsAdminOrManager = isAdminOrManager,
            ScopeDescription = isAdminOrManager
                ? "Enterprise Overview (All Accounts & Pipelines)"
                : "Personal Workspace (Assigned Accounts, Leads & Deals)"
        };

        try
        {
            // 1. Build Authorized Base Queries
            var customerQuery = _context.Customers.AsNoTracking().AsQueryable();
            var leadQuery = _context.Leads.AsNoTracking().AsQueryable();
            var oppQuery = _context.Opportunities.AsNoTracking().AsQueryable();

            // Row-level scoping for Sales Executives
            if (!isAdminOrManager && !string.IsNullOrEmpty(userId))
            {
                leadQuery = leadQuery.Where(l => l.AssignedTo == userId);
                oppQuery = oppQuery.Where(o => o.AssignedTo == userId);
                customerQuery = customerQuery.Where(c => 
                    c.CreatedBy == userId || 
                    c.Opportunities.Any(o => o.AssignedTo == userId) || 
                    c.Activities.Any(a => a.AssignedTo == userId));
            }

            // 2. Compute the 8 Required KPI Cards
            model.TotalCustomers = await customerQuery.CountAsync();
            model.TotalLeads = await leadQuery.CountAsync();
            model.OpenLeads = await leadQuery.CountAsync(l => 
                l.Status == LeadStatus.New || 
                l.Status == LeadStatus.Contacted || 
                l.Status == LeadStatus.Qualified);

            model.TotalOpportunities = await oppQuery.CountAsync();
            model.OpenOpportunities = await oppQuery.CountAsync(o => 
                o.Status == "Open" || 
                (o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost));

            model.WonOpportunities = await oppQuery.CountAsync(o => 
                o.Status == "Won" || o.Stage == OpportunityStage.Won);

            model.LostOpportunities = await oppQuery.CountAsync(o => 
                o.Status == "Lost" || o.Stage == OpportunityStage.Lost);

            // Total Pipeline Value: sum of open active deals
            model.TotalPipelineValue = await oppQuery
                .Where(o => o.Status == "Open" || (o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost))
                .SumAsync(o => (decimal?)o.Amount) ?? 0m;

            // Weighted Pipeline Value: sum of (Amount * Probability / 100)
            var openOpps = await oppQuery
                .Where(o => o.Status == "Open" || (o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost))
                .Select(o => new { o.Amount, o.Probability })
                .ToListAsync();

            model.WeightedPipelineValue = openOpps.Sum(o => (o.Amount * o.Probability) / 100m);

            // 3. Chart 1: Lead Status Breakdown
            var leadStatuses = await leadQuery
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var leadStatusMap = leadStatuses.ToDictionary(k => k.Status, v => v.Count);
            var leadEnumValues = Enum.GetValues<LeadStatus>();

            var leadColors = new Dictionary<LeadStatus, string>
            {
                { LeadStatus.New, "#0dcaf0" },          // Cyan
                { LeadStatus.Contacted, "#0d6efd" },    // Blue
                { LeadStatus.Qualified, "#198754" },    // Green
                { LeadStatus.Unqualified, "#ffc107" },  // Yellow
                { LeadStatus.Converted, "#20c997" },    // Teal
                { LeadStatus.Lost, "#dc3545" }          // Red
            };

            foreach (var status in leadEnumValues)
            {
                model.LeadStatusChart.Labels.Add(status.ToString());
                model.LeadStatusChart.Counts.Add(leadStatusMap.GetValueOrDefault(status, 0));
                model.LeadStatusChart.BackgroundColors.Add(leadColors.GetValueOrDefault(status, "#6c757d"));
            }

            // 4. Chart 2: Opportunity Pipeline by Stage
            var oppStages = await oppQuery
                .GroupBy(o => o.Stage)
                .Select(g => new 
                { 
                    Stage = g.Key, 
                    Count = g.Count(), 
                    Amount = g.Sum(x => x.Amount) 
                })
                .ToListAsync();

            var oppStageMap = oppStages.ToDictionary(k => k.Stage, v => v);
            var stageEnumValues = Enum.GetValues<OpportunityStage>();

            var stageColors = new Dictionary<OpportunityStage, string>
            {
                { OpportunityStage.Qualification, "#6c757d" },  // Gray
                { OpportunityStage.Proposal, "#0d6efd" },       // Blue
                { OpportunityStage.Negotiation, "#fd7e14" },    // Orange
                { OpportunityStage.Won, "#198754" },            // Green
                { OpportunityStage.Lost, "#dc3545" }            // Red
            };

            foreach (var stage in stageEnumValues)
            {
                model.OpportunityPipelineChart.Labels.Add(stage.ToString());
                if (oppStageMap.TryGetValue(stage, out var data))
                {
                    model.OpportunityPipelineChart.Counts.Add(data.Count);
                    model.OpportunityPipelineChart.Amounts.Add(data.Amount);
                }
                else
                {
                    model.OpportunityPipelineChart.Counts.Add(0);
                    model.OpportunityPipelineChart.Amounts.Add(0m);
                }
                model.OpportunityPipelineChart.BackgroundColors.Add(stageColors.GetValueOrDefault(stage, "#0d6efd"));
            }

            // 5. Chart 3: Monthly Sales (Past 6 Months Won Deals)
            var now = DateTime.UtcNow;
            var pastMonths = Enumerable.Range(0, 6)
                .Select(i => new DateTime(now.Year, now.Month, 1).AddMonths(-5 + i))
                .ToList();

            var firstMonth = pastMonths.First();
            var wonOppsInPeriod = await oppQuery
                .Where(o => (o.Status == "Won" || o.Stage == OpportunityStage.Won) && o.ExpectedCloseDate >= firstMonth)
                .Select(o => new { o.Amount, o.ExpectedCloseDate })
                .ToListAsync();

            foreach (var monthDate in pastMonths)
            {
                var monthLabel = monthDate.ToString("MMM yyyy");
                model.MonthlySalesChart.Months.Add(monthLabel);

                var dealsInMonth = wonOppsInPeriod.Where(o => 
                    o.ExpectedCloseDate.Year == monthDate.Year && 
                    o.ExpectedCloseDate.Month == monthDate.Month).ToList();

                model.MonthlySalesChart.Revenue.Add(dealsInMonth.Sum(d => d.Amount));
                model.MonthlySalesChart.WonDealsCount.Add(dealsInMonth.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error gathering dashboard metrics. Returning defensive defaults.");
            PopulateFallbackData(model);
        }

        return model;
    }

    private static void PopulateFallbackData(DashboardViewModel model)
    {
        // Safe default charts so UI never crashes or errors even during startup/empty DB
        if (!model.LeadStatusChart.Labels.Any())
        {
            model.LeadStatusChart.Labels.AddRange(new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" });
            model.LeadStatusChart.Counts.AddRange(new[] { 0, 0, 0, 0, 0, 0 });
            model.LeadStatusChart.BackgroundColors.AddRange(new[] { "#0dcaf0", "#0d6efd", "#198754", "#ffc107", "#20c997", "#dc3545" });
        }

        if (!model.OpportunityPipelineChart.Labels.Any())
        {
            model.OpportunityPipelineChart.Labels.AddRange(new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" });
            model.OpportunityPipelineChart.Counts.AddRange(new[] { 0, 0, 0, 0, 0 });
            model.OpportunityPipelineChart.Amounts.AddRange(new[] { 0m, 0m, 0m, 0m, 0m });
            model.OpportunityPipelineChart.BackgroundColors.AddRange(new[] { "#6c757d", "#0d6efd", "#fd7e14", "#198754", "#dc3545" });
        }

        if (!model.MonthlySalesChart.Months.Any())
        {
            var now = DateTime.UtcNow;
            for (var i = 5; i >= 0; i--)
            {
                model.MonthlySalesChart.Months.Add(now.AddMonths(-i).ToString("MMM yyyy"));
                model.MonthlySalesChart.Revenue.Add(0m);
                model.MonthlySalesChart.WonDealsCount.Add(0);
            }
        }
    }
}
