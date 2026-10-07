using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.ViewModels.Leads;

public class LeadCreateEditViewModel
{
    public int? LeadId { get; set; }

    [Display(Name = "Lead Code")]
    public string? LeadCode { get; set; }

    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Lead Name must be between 2 and 150 characters.")]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Phone number must be a valid 10-digit number.")]
    [Display(Name = "Phone Number")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Lead source is required.")]
    [StringLength(100, ErrorMessage = "Source cannot exceed 100 characters.")]
    [Display(Name = "Lead Source")]
    public string Source { get; set; } = "Website";

    [Required(ErrorMessage = "Lead status is required.")]
    [Display(Name = "Lead Status")]
    public LeadStatus Status { get; set; } = LeadStatus.New;

    [Display(Name = "Priority")]
    public string Priority { get; set; } = "Medium";

    [Required(ErrorMessage = "Expected Value is required.")]
    [Range(0, 100000000, ErrorMessage = "Expected Value must be a valid non-negative amount.")]
    [Display(Name = "Expected Value ($)")]
    public decimal ExpectedValue { get; set; }

    [Display(Name = "Assign To Sales Executive")]
    public string? AssignedTo { get; set; }

    public IEnumerable<SelectListItem> SalesExecutivesList { get; set; } = new List<SelectListItem>();
}
