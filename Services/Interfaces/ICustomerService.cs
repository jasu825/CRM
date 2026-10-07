using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Customers;

namespace AcxiomCRM.Services.Interfaces;

public interface ICustomerService
{
    Task<CustomerListViewModel> GetPagedCustomersAsync(string? search, string? status, int pageNumber, int pageSize);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<CustomerDetailsViewModel?> GetCustomerDetailsAsync(int id);
    Task<CustomerCreateEditViewModel?> GetCustomerForEditAsync(int id);
    Task<(bool Success, string? ErrorMessage, Customer? Customer)> CreateCustomerAsync(CustomerCreateEditViewModel model, string? userId);
    Task<(bool Success, string? ErrorMessage)> UpdateCustomerAsync(CustomerCreateEditViewModel model, string? userId);
    Task<(bool Success, string? ErrorMessage)> DeactivateCustomerAsync(int id, string? userId);
    Task<(bool Success, string? ErrorMessage)> DeleteCustomerAsync(int id, string? userId);
    Task<bool> IsEmailUniqueAsync(string email, int? excludeCustomerId = null);
    Task<bool> IsPhoneUniqueAsync(string phone, int? excludeCustomerId = null);
    Task<bool> IsDuplicateCustomerAsync(string name, string email, string phone, int? excludeCustomerId = null);
}
