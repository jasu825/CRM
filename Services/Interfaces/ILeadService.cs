using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels.Leads;

namespace AcxiomCRM.Services.Interfaces;

public interface ILeadService
{
    Task<LeadListViewModel> GetPagedLeadsAsync(string? search, LeadStatus? status, string? assignedTo, int pageNumber, int pageSize);
    Task<Lead?> GetLeadByIdAsync(int id);
    Task<LeadDetailsViewModel?> GetLeadDetailsAsync(int id);
    Task<LeadCreateEditViewModel?> GetLeadForEditAsync(int id);
    Task<LeadConvertViewModel?> GetLeadForConvertAsync(int id);
    Task<(bool Success, string? ErrorMessage, Lead? Lead)> CreateLeadAsync(LeadCreateEditViewModel model, string? currentUserId);
    Task<(bool Success, string? ErrorMessage)> UpdateLeadAsync(LeadCreateEditViewModel model, string? currentUserId);
    Task<(bool Success, string? ErrorMessage)> DeleteLeadAsync(int id, string? currentUserId);
    Task<(bool Success, string? ErrorMessage)> UpdateStatusAsync(int id, LeadStatus newStatus, string? currentUserId);
    Task<(bool Success, string? ErrorMessage, int? CustomerId, int? OpportunityId)> ConvertLeadAsync(LeadConvertViewModel model, string? currentUserId);
    Task<List<SelectListItem>> GetSalesExecutivesSelectListAsync();
}
