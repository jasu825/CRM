using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Activities;

namespace AcxiomCRM.Services.Implementations;

public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ILogger<ActivityService> logger)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ActivityListViewModel> GetPagedActivitiesAsync(
        string? search,
        ActivityType? type,
        string? status,
        int? customerId,
        int? leadId,
        int pageNumber,
        int pageSize)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;

        var query = _context.Activities
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .AsQueryable();

        // 1. Search Query
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchClean = search.Trim();
            query = query.Where(a => a.Subject.Contains(searchClean)
                                  || (a.Description != null && a.Description.Contains(searchClean))
                                  || (a.Customer != null && a.Customer.CustomerName.Contains(searchClean))
                                  || (a.Lead != null && a.Lead.LeadName.Contains(searchClean)));
        }

        // 2. Type Filter
        if (type.HasValue)
        {
            query = query.Where(a => a.ActivityType == type.Value);
        }

        // 3. Status Filter
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(a => a.Status == status);
        }

        // 4. Customer Filter
        if (customerId.HasValue && customerId > 0)
        {
            query = query.Where(a => a.CustomerId == customerId.Value);
        }

        // 5. Lead Filter
        if (leadId.HasValue && leadId > 0)
        {
            query = query.Where(a => a.LeadId == leadId.Value);
        }

        var totalCount = await query.CountAsync();

        var activities = await query
            .OrderByDescending(a => a.ActivityDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityItemViewModel
            {
                ActivityId = a.ActivityId,
                ActivityType = a.ActivityType,
                Subject = a.Subject,
                Description = a.Description,
                ActivityDate = a.ActivityDate,
                CustomerId = a.CustomerId,
                CustomerName = a.Customer != null ? a.Customer.CustomerName : null,
                LeadId = a.LeadId,
                LeadName = a.Lead != null ? a.Lead.LeadName : null,
                AssignedToName = a.AssignedUser != null ? a.AssignedUser.FullName : "Unassigned",
                Status = a.Status,
                CreatedDate = a.CreatedDate
            })
            .ToListAsync();

        return new ActivityListViewModel
        {
            Activities = activities,
            Search = search,
            TypeFilter = type,
            StatusFilter = status,
            CustomerIdFilter = customerId,
            LeadIdFilter = leadId,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalCount,
            CustomersList = await GetCustomersSelectListAsync(),
            LeadsList = await GetLeadsSelectListAsync()
        };
    }

    public async Task<Activity?> GetActivityByIdAsync(int id)
    {
        return await _context.Activities
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.ActivityId == id);
    }

    public async Task<ActivityCreateEditViewModel?> GetActivityForEditAsync(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null) return null;

        return new ActivityCreateEditViewModel
        {
            ActivityId = activity.ActivityId,
            ActivityType = activity.ActivityType,
            Subject = activity.Subject,
            Description = activity.Description,
            ActivityDate = activity.ActivityDate,
            CustomerId = activity.CustomerId,
            LeadId = activity.LeadId,
            AssignedTo = activity.AssignedTo,
            Status = activity.Status,
            CustomersList = await GetCustomersSelectListAsync(),
            LeadsList = await GetLeadsSelectListAsync(),
            SalesExecutivesList = await GetSalesExecutivesSelectListAsync()
        };
    }

    public async Task<(bool Success, string? ErrorMessage, Activity? Activity)> CreateActivityAsync(ActivityCreateEditViewModel model, string? currentUserId)
    {
        try
        {
            var activity = new Activity
            {
                ActivityType = model.ActivityType,
                Subject = model.Subject.Trim(),
                Description = model.Description?.Trim(),
                ActivityDate = model.ActivityDate,
                CustomerId = model.CustomerId > 0 ? model.CustomerId : null,
                LeadId = model.LeadId > 0 ? model.LeadId : null,
                AssignedTo = model.AssignedTo,
                Status = model.Status ?? "Completed",
                CreatedDate = DateTime.UtcNow
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(currentUserId, "Create", "Activity", activity.ActivityId.ToString(), null, JsonSerializer.Serialize(new
            {
                activity.ActivityType,
                activity.Subject,
                activity.CustomerId,
                activity.LeadId,
                activity.Status
            }));

            _logger.LogInformation("Activity '{ActivityId}' created successfully.", activity.ActivityId);
            return (true, null, activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating activity '{Subject}'.", model.Subject);
            return (false, "An unexpected error occurred while saving the activity.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateActivityAsync(ActivityCreateEditViewModel model, string? currentUserId)
    {
        if (!model.ActivityId.HasValue) return (false, "Invalid activity identifier.");

        var activity = await _context.Activities.FindAsync(model.ActivityId.Value);
        if (activity == null) return (false, "Activity not found.");

        try
        {
            var oldValue = JsonSerializer.Serialize(new
            {
                activity.ActivityType,
                activity.Subject,
                activity.CustomerId,
                activity.LeadId,
                activity.Status
            });

            activity.ActivityType = model.ActivityType;
            activity.Subject = model.Subject.Trim();
            activity.Description = model.Description?.Trim();
            activity.ActivityDate = model.ActivityDate;
            activity.CustomerId = model.CustomerId > 0 ? model.CustomerId : null;
            activity.LeadId = model.LeadId > 0 ? model.LeadId : null;
            activity.AssignedTo = model.AssignedTo;
            activity.Status = model.Status;

            await _context.SaveChangesAsync();

            var newValue = JsonSerializer.Serialize(new
            {
                activity.ActivityType,
                activity.Subject,
                activity.CustomerId,
                activity.LeadId,
                activity.Status
            });

            await RecordAuditLogAsync(currentUserId, "Update", "Activity", activity.ActivityId.ToString(), oldValue, newValue);
            _logger.LogInformation("Activity '{ActivityId}' updated successfully.", activity.ActivityId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating activity '{ActivityId}'.", model.ActivityId);
            return (false, "An unexpected error occurred while updating the activity.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteActivityAsync(int id, string? currentUserId)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null) return (false, "Activity not found.");

        try
        {
            var oldSnapshot = JsonSerializer.Serialize(new { activity.ActivityType, activity.Subject, activity.ActivityDate });
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(currentUserId, "Delete", "Activity", id.ToString(), oldSnapshot, null);
            _logger.LogInformation("Activity '{ActivityId}' deleted successfully.", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting activity '{ActivityId}'.", id);
            return (false, "An error occurred while deleting the activity.");
        }
    }

    public async Task<List<SelectListItem>> GetCustomersSelectListAsync()
    {
        try
        {
            return await _context.Customers
                .AsNoTracking()
                .OrderBy(c => c.CustomerName)
                .Select(c => new SelectListItem
                {
                    Value = c.CustomerId.ToString(),
                    Text = $"{c.CustomerName} ({c.CustomerCode})"
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load customers for dropdown.");
            return new List<SelectListItem>();
        }
    }

    public async Task<List<SelectListItem>> GetLeadsSelectListAsync()
    {
        try
        {
            return await _context.Leads
                .AsNoTracking()
                .OrderBy(l => l.LeadName)
                .Select(l => new SelectListItem
                {
                    Value = l.LeadId.ToString(),
                    Text = $"{l.LeadName} ({l.LeadCode})"
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load leads for dropdown.");
            return new List<SelectListItem>();
        }
    }

    public async Task<List<SelectListItem>> GetSalesExecutivesSelectListAsync()
    {
        try
        {
            return await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FullName} ({u.Email})"
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load sales executives for dropdown.");
            return new List<SelectListItem>();
        }
    }

    private async Task RecordAuditLogAsync(string? userId, string action, string entityName, string? recordId, string? oldValue, string? newValue)
    {
        await _auditLogService.LogAsync(userId, action, entityName, recordId, oldValue, newValue);
    }
}
