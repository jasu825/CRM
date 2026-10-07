using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels.Leads;

public class LeadDetailsViewModel
{
    public Lead Lead { get; set; } = null!;
    public Customer? ConvertedCustomer { get; set; }
    public IEnumerable<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public IEnumerable<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public IEnumerable<Activity> Activities { get; set; } = new List<Activity>();
}
