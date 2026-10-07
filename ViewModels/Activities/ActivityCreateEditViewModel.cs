using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.ViewModels.Activities;

public class ActivityCreateEditViewModel
{
    public int? ActivityId { get; set; }

    [Required(ErrorMessage = "Activity type is required.")]
    [Display(Name = "Activity Type")]
    public ActivityType ActivityType { get; set; } = ActivityType.Call;

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Subject must be between 2 and 200 characters.")]
    [Display(Name = "Subject / Headline")]
    public string Subject { get; set; } = string.Empty;

    [Display(Name = "Detailed Notes / Outcome")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Activity date and time is required.")]
    [Display(Name = "Activity Date & Time")]
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    [Display(Name = "Associated Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Associated Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Assigned Team Member")]
    public string? AssignedTo { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Completed";

    public IEnumerable<SelectListItem> CustomersList { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> LeadsList { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> SalesExecutivesList { get; set; } = new List<SelectListItem>();
}
