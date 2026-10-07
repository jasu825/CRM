using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.ViewModels.Leads;

public class LeadListViewModel
{
    public IEnumerable<LeadItemViewModel> Leads { get; set; } = new List<LeadItemViewModel>();

    public string? Search { get; set; }
    public LeadStatus? StatusFilter { get; set; }
    public string? AssignedToFilter { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public IEnumerable<SelectListItem> SalesExecutivesList { get; set; } = new List<SelectListItem>();
}

public class LeadItemViewModel
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Source { get; set; } = string.Empty;
    public LeadStatus Status { get; set; }
    public string Priority { get; set; } = "Medium";
    public decimal ExpectedValue { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? ConvertedCustomerId { get; set; }
}
