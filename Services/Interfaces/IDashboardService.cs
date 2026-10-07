using AcxiomCRM.ViewModels.Dashboard;

namespace AcxiomCRM.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardMetricsAsync(
        string? userId, 
        bool isAdminOrManager, 
        string? userDisplayName = null, 
        string? roleName = null);
}
