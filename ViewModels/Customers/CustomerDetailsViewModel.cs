using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels.Customers;

public class CustomerDetailsViewModel
{
    public Customer Customer { get; set; } = null!;
    public IEnumerable<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public IEnumerable<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public IEnumerable<Activity> Activities { get; set; } = new List<Activity>();
}
