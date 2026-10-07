namespace AcxiomCRM.ViewModels.Customers;

public class CustomerListViewModel
{
    public IEnumerable<CustomerItemViewModel> Customers { get; set; } = new List<CustomerItemViewModel>();

    public string? Search { get; set; }
    public string? StatusFilter { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

public class CustomerItemViewModel
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedDate { get; set; }
    public int OpportunitiesCount { get; set; }
    public int FollowUpsCount { get; set; }
}
