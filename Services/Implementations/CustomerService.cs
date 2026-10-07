using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Customers;

namespace AcxiomCRM.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        ApplicationDbContext context, 
        IAuditLogService auditLogService, 
        ILogger<CustomerService> logger)
    {
        _context = context;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<CustomerListViewModel> GetPagedCustomersAsync(string? search, string? status, int pageNumber, int pageSize)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;

        var query = _context.Customers.AsNoTracking().AsQueryable();

        // 1. Search filter across Name, Email, Phone, Company
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchClean = search.Trim();
            query = query.Where(c => c.CustomerName.Contains(searchClean)
                                  || c.Email.Contains(searchClean)
                                  || c.Phone.Contains(searchClean)
                                  || (c.CompanyName != null && c.CompanyName.Contains(searchClean))
                                  || c.CustomerCode.Contains(searchClean));
        }

        // 2. Status filter
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(c => c.Status == status);
        }

        var totalCount = await query.CountAsync();

        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerItemViewModel
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                OpportunitiesCount = c.Opportunities.Count,
                FollowUpsCount = c.FollowUps.Count
            })
            .ToListAsync();

        return new CustomerListViewModel
        {
            Customers = customers,
            Search = search,
            StatusFilter = status,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalCount
        };
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        return await _context.Customers
            .AsNoTracking()
            .Include(c => c.CreatedByUser)
            .FirstOrDefaultAsync(c => c.CustomerId == id);
    }

    public async Task<CustomerDetailsViewModel?> GetCustomerDetailsAsync(int id)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.CreatedByUser)
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null) return null;

        return new CustomerDetailsViewModel
        {
            Customer = customer,
            Opportunities = customer.Opportunities.OrderByDescending(o => o.CreatedDate).ToList(),
            FollowUps = customer.FollowUps.OrderBy(f => f.FollowUpDate).ToList(),
            Activities = customer.Activities.OrderByDescending(a => a.ActivityDate).ToList()
        };
    }

    public async Task<CustomerCreateEditViewModel?> GetCustomerForEditAsync(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return null;

        return new CustomerCreateEditViewModel
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status
        };
    }

    public async Task<(bool Success, string? ErrorMessage, Customer? Customer)> CreateCustomerAsync(CustomerCreateEditViewModel model, string? userId)
    {
        // 1. Business Validation: Email Uniqueness
        if (!await IsEmailUniqueAsync(model.Email))
        {
            return (false, "A customer with this email address already exists.", null);
        }

        // 2. Business Validation: Phone Uniqueness
        if (!await IsPhoneUniqueAsync(model.Phone))
        {
            return (false, "A customer with this phone number already exists.", null);
        }

        // 3. Business Validation: Duplicate Customer Prevention
        if (await IsDuplicateCustomerAsync(model.CustomerName, model.Email, model.Phone))
        {
            return (false, "A duplicate customer with identical business identification already exists.", null);
        }

        try
        {
            // Generate auto CustomerCode (e.g., CUST-1001)
            var nextNumber = 1001;
            var lastCustomer = await _context.Customers
                .OrderByDescending(c => c.CustomerId)
                .Select(c => c.CustomerId)
                .FirstOrDefaultAsync();

            if (lastCustomer > 0)
            {
                nextNumber = 1000 + lastCustomer + 1;
            }

            var customer = new Customer
            {
                CustomerCode = $"CUST-{nextNumber}",
                CustomerName = model.CustomerName.Trim(),
                Email = model.Email.Trim().ToLowerInvariant(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName?.Trim(),
                Address = model.Address?.Trim(),
                City = model.City?.Trim(),
                State = model.State?.Trim(),
                Status = model.Status ?? "Active",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            // Record Audit Log safely
            await RecordAuditLogAsync(userId, "Create", "Customer", customer.CustomerId.ToString(), null, JsonSerializer.Serialize(new
            {
                customer.CustomerCode,
                customer.CustomerName,
                customer.Email,
                customer.Phone,
                customer.Status
            }));

            _logger.LogInformation("Customer '{CustomerCode}' created successfully by user '{UserId}'.", customer.CustomerCode, userId);
            return (true, null, customer);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error creating customer '{CustomerName}'.", model.CustomerName);
            return (false, "A database conflict occurred while saving the customer record. Please verify that email and phone are unique.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating customer '{CustomerName}'.", model.CustomerName);
            return (false, "An unexpected error occurred while saving the customer. Please try again later.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateCustomerAsync(CustomerCreateEditViewModel model, string? userId)
    {
        if (!model.CustomerId.HasValue)
        {
            return (false, "Invalid customer identifier.");
        }

        var customer = await _context.Customers.FindAsync(model.CustomerId.Value);
        if (customer == null)
        {
            return (false, "Customer not found.");
        }

        // 1. Email Uniqueness check excluding current customer
        if (!await IsEmailUniqueAsync(model.Email, customer.CustomerId))
        {
            return (false, "A customer with this email address already exists.");
        }

        // 2. Phone Uniqueness check excluding current customer
        if (!await IsPhoneUniqueAsync(model.Phone, customer.CustomerId))
        {
            return (false, "A customer with this phone number already exists.");
        }

        // 3. Duplicate check excluding current customer
        if (await IsDuplicateCustomerAsync(model.CustomerName, model.Email, model.Phone, customer.CustomerId))
        {
            return (false, "Another customer with identical business identification already exists.");
        }

        try
        {
            var oldValue = JsonSerializer.Serialize(new
            {
                customer.CustomerName,
                customer.Email,
                customer.Phone,
                customer.CompanyName,
                customer.Status
            });

            customer.CustomerName = model.CustomerName.Trim();
            customer.Email = model.Email.Trim().ToLowerInvariant();
            customer.Phone = model.Phone.Trim();
            customer.CompanyName = model.CompanyName?.Trim();
            customer.Address = model.Address?.Trim();
            customer.City = model.City?.Trim();
            customer.State = model.State?.Trim();
            customer.Status = model.Status;
            customer.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var newValue = JsonSerializer.Serialize(new
            {
                customer.CustomerName,
                customer.Email,
                customer.Phone,
                customer.CompanyName,
                customer.Status
            });

            await RecordAuditLogAsync(userId, "Update", "Customer", customer.CustomerId.ToString(), oldValue, newValue);

            _logger.LogInformation("Customer '{CustomerId}' updated successfully by user '{UserId}'.", customer.CustomerId, userId);
            return (true, null);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error updating customer '{CustomerId}'.", model.CustomerId);
            return (false, "A database conflict occurred while saving changes. Please ensure email and phone are unique.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating customer '{CustomerId}'.", model.CustomerId);
            return (false, "An unexpected error occurred while updating the customer.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeactivateCustomerAsync(int id, string? userId)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return (false, "Customer not found.");

        try
        {
            var oldStatus = customer.Status;
            customer.Status = "Inactive";
            customer.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(userId, "Deactivate", "Customer", customer.CustomerId.ToString(), 
                JsonSerializer.Serialize(new { Status = oldStatus }), 
                JsonSerializer.Serialize(new { Status = "Inactive" }));

            _logger.LogInformation("Customer '{CustomerId}' deactivated by user '{UserId}'.", id, userId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating customer '{CustomerId}'.", id);
            return (false, "An error occurred while deactivating the customer.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteCustomerAsync(int id, string? userId)
    {
        var customer = await _context.Customers
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null) return (false, "Customer not found.");

        // If customer has linked historical transactions, soft-deactivate instead of hard delete
        if (customer.Opportunities.Any() || customer.FollowUps.Any() || customer.Activities.Any())
        {
            return await DeactivateCustomerAsync(id, userId);
        }

        try
        {
            var oldSnapshot = JsonSerializer.Serialize(new
            {
                customer.CustomerCode,
                customer.CustomerName,
                customer.Email
            });

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(userId, "Delete", "Customer", id.ToString(), oldSnapshot, null);
            _logger.LogInformation("Customer '{CustomerId}' permanently deleted by '{UserId}'.", id, userId);
            return (true, null);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error deleting customer '{CustomerId}'.", id);
            return (false, "Customer cannot be deleted because related records depend on it. It has been deactivated instead.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting customer '{CustomerId}'.", id);
            return (false, "An error occurred while deleting the customer.");
        }
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeCustomerId = null)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        return !await _context.Customers
            .AnyAsync(c => c.Email.ToLower() == cleanEmail && (!excludeCustomerId.HasValue || c.CustomerId != excludeCustomerId.Value));
    }

    public async Task<bool> IsPhoneUniqueAsync(string phone, int? excludeCustomerId = null)
    {
        var cleanPhone = phone.Trim();
        return !await _context.Customers
            .AnyAsync(c => c.Phone == cleanPhone && (!excludeCustomerId.HasValue || c.CustomerId != excludeCustomerId.Value));
    }

    public async Task<bool> IsDuplicateCustomerAsync(string name, string email, string phone, int? excludeCustomerId = null)
    {
        var cleanName = name.Trim().ToLowerInvariant();
        var cleanEmail = email.Trim().ToLowerInvariant();
        var cleanPhone = phone.Trim();

        return await _context.Customers.AnyAsync(c =>
            (c.Email.ToLower() == cleanEmail || c.Phone == cleanPhone || c.CustomerName.ToLower() == cleanName)
            && (!excludeCustomerId.HasValue || c.CustomerId != excludeCustomerId.Value));
    }

    private async Task RecordAuditLogAsync(string? userId, string action, string entityName, string? recordId, string? oldValue, string? newValue)
    {
        await _auditLogService.LogAsync(userId, action, entityName, recordId, oldValue, newValue);
    }
}
