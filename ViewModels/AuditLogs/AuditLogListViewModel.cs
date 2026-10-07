using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels.AuditLogs;

public class AuditLogListViewModel
{
    public IEnumerable<AuditLogItemViewModel> AuditLogs { get; set; } = new List<AuditLogItemViewModel>();

    // Filters
    public string? Search { get; set; }
    public string? UserId { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    // Pagination
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    // Filter Dropdown Options
    public IEnumerable<SelectListItem> UsersList { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> ActionsList { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> EntitiesList { get; set; } = new List<SelectListItem>();
}
