using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers;

public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    // GET: /Dashboard
    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userDisplayName = User.Identity?.Name;
            
            // Check roles: Admin and Manager see full organizational overview
            // SalesExecutive sees only assigned records
            var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");

            // For unauthenticated/initial development access, show complete overview
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                isAdminOrManager = true;
                userDisplayName = "Development Admin";
            }

            string roleName = "Admin";
            if (User.IsInRole("Admin")) roleName = "Admin";
            else if (User.IsInRole("Manager")) roleName = "Manager";
            else if (User.IsInRole("SalesExecutive")) roleName = "SalesExecutive";
            else if (User.Identity == null || !User.Identity.IsAuthenticated) roleName = "Admin (Development Mode)";

            var model = await _dashboardService.GetDashboardMetricsAsync(userId, isAdminOrManager, userDisplayName, roleName);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading AcxiomCRM Dashboard.");
            TempData["ErrorMessage"] = "Unable to load dashboard data at this time.";
            return View(new ViewModels.Dashboard.DashboardViewModel());
        }
    }
}
