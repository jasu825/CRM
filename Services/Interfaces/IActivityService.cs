using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels.Activities;

namespace AcxiomCRM.Services.Interfaces;

public interface IActivityService
{
    Task<ActivityListViewModel> GetPagedActivitiesAsync(string? search, ActivityType? type, string? status, int? customerId, int? leadId, int pageNumber, int pageSize);
    Task<Activity?> GetActivityByIdAsync(int id);
    Task<ActivityCreateEditViewModel?> GetActivityForEditAsync(int id);
    Task<(bool Success, string? ErrorMessage, Activity? Activity)> CreateActivityAsync(ActivityCreateEditViewModel model, string? currentUserId);
    Task<(bool Success, string? ErrorMessage)> UpdateActivityAsync(ActivityCreateEditViewModel model, string? currentUserId);
    Task<(bool Success, string? ErrorMessage)> DeleteActivityAsync(int id, string? currentUserId);
    Task<List<SelectListItem>> GetCustomersSelectListAsync();
    Task<List<SelectListItem>> GetLeadsSelectListAsync();
    Task<List<SelectListItem>> GetSalesExecutivesSelectListAsync();
}
