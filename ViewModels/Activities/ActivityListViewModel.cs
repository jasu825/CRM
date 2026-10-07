using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.ViewModels.Activities;

public class ActivityListViewModel
{
    public IEnumerable<ActivityItemViewModel> Activities { get; set; } = new List<ActivityItemViewModel>();

    public string? Search { get; set; }
    public ActivityType? TypeFilter { get; set; }
    public string? StatusFilter { get; set; }
    public int? CustomerIdFilter { get; set; }
    public int? LeadIdFilter { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public IEnumerable<SelectListItem> CustomersList { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> LeadsList { get; set; } = new List<SelectListItem>();
}

public class ActivityItemViewModel
{
    public int ActivityId { get; set; }
    public ActivityType ActivityType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public string? AssignedToName { get; set; }
    public string Status { get; set; } = "Completed";
    public DateTime CreatedDate { get; set; }
}
