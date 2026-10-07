using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Leads;

namespace AcxiomCRM.Services.Implementations;

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ILogger<LeadService> logger)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<LeadListViewModel> GetPagedLeadsAsync(string? search, LeadStatus? status, string? assignedTo, int pageNumber, int pageSize)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;

        var query = _context.Leads
            .AsNoTracking()
            .Include(l => l.AssignedUser)
            .AsQueryable();

        // 1. Search Filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchClean = search.Trim();
            query = query.Where(l => l.LeadName.Contains(searchClean)
                                  || l.Email.Contains(searchClean)
                                  || l.Phone.Contains(searchClean)
                                  || (l.CompanyName != null && l.CompanyName.Contains(searchClean))
                                  || l.LeadCode.Contains(searchClean)
                                  || l.Source.Contains(searchClean));
        }

        // 2. Status Filter
        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        // 3. Assigned User Filter
        if (!string.IsNullOrWhiteSpace(assignedTo) && assignedTo != "All")
        {
            query = query.Where(l => l.AssignedTo == assignedTo);
        }

        var totalCount = await query.CountAsync();

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LeadItemViewModel
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                AssignedToName = l.AssignedUser != null ? l.AssignedUser.FullName : "Unassigned",
                CreatedDate = l.CreatedDate,
                ConvertedCustomerId = l.ConvertedCustomerId
            })
            .ToListAsync();

        return new LeadListViewModel
        {
            Leads = leads,
            Search = search,
            StatusFilter = status,
            AssignedToFilter = assignedTo,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalCount,
            SalesExecutivesList = await GetSalesExecutivesSelectListAsync()
        };
    }

    public async Task<Lead?> GetLeadByIdAsync(int id)
    {
        return await _context.Leads
            .AsNoTracking()
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .FirstOrDefaultAsync(l => l.LeadId == id);
    }

    public async Task<LeadDetailsViewModel?> GetLeadDetailsAsync(int id)
    {
        var lead = await _context.Leads
            .AsNoTracking()
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.Opportunities)
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null) return null;

        return new LeadDetailsViewModel
        {
            Lead = lead,
            ConvertedCustomer = lead.ConvertedCustomer,
            Opportunities = lead.Opportunities.OrderByDescending(o => o.CreatedDate).ToList(),
            FollowUps = lead.FollowUps.OrderBy(f => f.FollowUpDate).ToList(),
            Activities = lead.Activities.OrderByDescending(a => a.ActivityDate).ToList()
        };
    }

    public async Task<LeadCreateEditViewModel?> GetLeadForEditAsync(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return null;

        return new LeadCreateEditViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            AssignedTo = lead.AssignedTo,
            SalesExecutivesList = await GetSalesExecutivesSelectListAsync()
        };
    }

    public async Task<LeadConvertViewModel?> GetLeadForConvertAsync(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return null;

        return new LeadConvertViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            CreateOpportunity = true,
            OpportunityName = $"{lead.LeadName} - Deal",
            OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 10000m,
            OpportunityStage = OpportunityStage.Qualification,
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        };
    }

    public async Task<(bool Success, string? ErrorMessage, Lead? Lead)> CreateLeadAsync(LeadCreateEditViewModel model, string? currentUserId)
    {
        try
        {
            // Auto sequential LeadCode (e.g. LEAD-1001)
            var nextNumber = 1001;
            var lastLead = await _context.Leads
                .OrderByDescending(l => l.LeadId)
                .Select(l => l.LeadId)
                .FirstOrDefaultAsync();

            if (lastLead > 0)
            {
                nextNumber = 1000 + lastLead + 1;
            }

            var lead = new Lead
            {
                LeadCode = $"LEAD-{nextNumber}",
                LeadName = model.LeadName.Trim(),
                Email = model.Email.Trim().ToLowerInvariant(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName?.Trim(),
                Source = model.Source.Trim(),
                Status = model.Status,
                Priority = model.Priority ?? "Medium",
                ExpectedValue = model.ExpectedValue,
                AssignedTo = model.AssignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(currentUserId, "Create", "Lead", lead.LeadId.ToString(), null, JsonSerializer.Serialize(new
            {
                lead.LeadCode,
                lead.LeadName,
                lead.Email,
                lead.Status,
                lead.ExpectedValue,
                lead.AssignedTo
            }));

            _logger.LogInformation("Lead '{LeadCode}' created successfully.", lead.LeadCode);
            return (true, null, lead);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error creating lead '{LeadName}'.", model.LeadName);
            return (false, "A database conflict occurred while saving the lead. Please verify all entries.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating lead '{LeadName}'.", model.LeadName);
            return (false, "An error occurred while creating the lead. Please try again.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateLeadAsync(LeadCreateEditViewModel model, string? currentUserId)
    {
        if (!model.LeadId.HasValue) return (false, "Invalid lead identifier.");

        var lead = await _context.Leads.FindAsync(model.LeadId.Value);
        if (lead == null) return (false, "Lead not found.");

        if (lead.Status == LeadStatus.Converted && model.Status != LeadStatus.Converted)
        {
            return (false, "A converted lead cannot be moved back to an active lead status.");
        }

        try
        {
            var oldValue = JsonSerializer.Serialize(new
            {
                lead.LeadName,
                lead.Email,
                lead.Phone,
                lead.Status,
                lead.ExpectedValue,
                lead.AssignedTo
            });

            lead.LeadName = model.LeadName.Trim();
            lead.Email = model.Email.Trim().ToLowerInvariant();
            lead.Phone = model.Phone.Trim();
            lead.CompanyName = model.CompanyName?.Trim();
            lead.Source = model.Source.Trim();
            lead.Status = model.Status;
            lead.Priority = model.Priority ?? "Medium";
            lead.ExpectedValue = model.ExpectedValue;
            lead.AssignedTo = model.AssignedTo;

            await _context.SaveChangesAsync();

            var newValue = JsonSerializer.Serialize(new
            {
                lead.LeadName,
                lead.Email,
                lead.Phone,
                lead.Status,
                lead.ExpectedValue,
                lead.AssignedTo
            });

            await RecordAuditLogAsync(currentUserId, "Update", "Lead", lead.LeadId.ToString(), oldValue, newValue);
            _logger.LogInformation("Lead '{LeadId}' updated successfully.", lead.LeadId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lead '{LeadId}'.", model.LeadId);
            return (false, "An unexpected error occurred while updating the lead.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteLeadAsync(int id, string? currentUserId)
    {
        var lead = await _context.Leads
            .Include(l => l.Opportunities)
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null) return (false, "Lead not found.");

        if (lead.Status == LeadStatus.Converted)
        {
            return (false, "Converted leads cannot be deleted because they are associated with existing customer accounts.");
        }

        // If lead has related records, set to Lost instead of permanent deletion
        if (lead.Opportunities.Any() || lead.FollowUps.Any() || lead.Activities.Any())
        {
            lead.Status = LeadStatus.Lost;
            await _context.SaveChangesAsync();
            await RecordAuditLogAsync(currentUserId, "Deactivate", "Lead", id.ToString(), null, "Status set to Lost");
            return (true, null);
        }

        try
        {
            var oldSnapshot = JsonSerializer.Serialize(new { lead.LeadCode, lead.LeadName, lead.Email });
            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(currentUserId, "Delete", "Lead", id.ToString(), oldSnapshot, null);
            _logger.LogInformation("Lead '{LeadId}' deleted permanently.", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lead '{LeadId}'.", id);
            return (false, "An error occurred while deleting the lead.");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateStatusAsync(int id, LeadStatus newStatus, string? currentUserId)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return (false, "Lead not found.");

        if (lead.Status == LeadStatus.Converted)
        {
            return (false, "A converted lead status cannot be modified.");
        }

        var oldStatus = lead.Status;
        lead.Status = newStatus;
        await _context.SaveChangesAsync();

        await RecordAuditLogAsync(currentUserId, "StatusChange", "Lead", id.ToString(), oldStatus.ToString(), newStatus.ToString());
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage, int? CustomerId, int? OpportunityId)> ConvertLeadAsync(LeadConvertViewModel model, string? currentUserId)
    {
        var lead = await _context.Leads.FindAsync(model.LeadId);
        if (lead == null) return (false, "Lead not found.", null, null);

        if (lead.Status == LeadStatus.Converted)
        {
            return (false, "This lead has already been converted.", lead.ConvertedCustomerId, null);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Find existing customer or create new one
            var cleanEmail = lead.Email.Trim().ToLowerInvariant();
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == cleanEmail);

            if (customer == null)
            {
                var nextNumber = 1001;
                var lastCust = await _context.Customers.OrderByDescending(c => c.CustomerId).Select(c => c.CustomerId).FirstOrDefaultAsync();
                if (lastCust > 0) nextNumber = 1000 + lastCust + 1;

                customer = new Customer
                {
                    CustomerCode = $"CUST-{nextNumber}",
                    CustomerName = lead.LeadName,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    CompanyName = lead.CompanyName,
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = currentUserId
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                await RecordAuditLogAsync(currentUserId, "Create", "Customer", customer.CustomerId.ToString(), null, 
                    JsonSerializer.Serialize(new { customer.CustomerCode, customer.CustomerName, ConvertedFromLead = lead.LeadCode }));
            }

            int? opportunityId = null;

            // 2. Optionally create sales opportunity
            if (model.CreateOpportunity)
            {
                if (string.IsNullOrWhiteSpace(model.OpportunityName))
                {
                    model.OpportunityName = $"{lead.LeadName} - Opportunity";
                }

                if (!model.OpportunityAmount.HasValue || model.OpportunityAmount.Value <= 0)
                {
                    return (false, "Opportunity Amount must be greater than 0 for an active sales opportunity.", null, null);
                }

                if (model.ExpectedCloseDate.HasValue && model.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
                {
                    return (false, "Expected Close Date cannot be in the past.", null, null);
                }

                var opportunity = new Opportunity
                {
                    OpportunityName = model.OpportunityName.Trim(),
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.OpportunityAmount.Value,
                    Stage = model.OpportunityStage,
                    Probability = Math.Clamp(model.Probability, 0, 100),
                    ExpectedCloseDate = model.ExpectedCloseDate ?? DateTime.UtcNow.AddDays(30),
                    Status = "Open",
                    AssignedTo = lead.AssignedTo ?? currentUserId,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Opportunities.Add(opportunity);
                await _context.SaveChangesAsync();
                opportunityId = opportunity.OpportunityId;

                await RecordAuditLogAsync(currentUserId, "Create", "Opportunity", opportunity.OpportunityId.ToString(), null,
                    JsonSerializer.Serialize(new { opportunity.OpportunityName, opportunity.Amount, opportunity.Stage, opportunity.CustomerId }));
            }

            // 3. Mark Lead as Converted
            lead.Status = LeadStatus.Converted;
            lead.ConvertedCustomerId = customer.CustomerId;
            await _context.SaveChangesAsync();

            await RecordAuditLogAsync(currentUserId, "Convert", "Lead", lead.LeadId.ToString(), 
                JsonSerializer.Serialize(new { PreviousStatus = "Qualified" }), 
                JsonSerializer.Serialize(new { Status = "Converted", ConvertedCustomerId = customer.CustomerId, CreatedOpportunityId = opportunityId }));

            await transaction.CommitAsync();

            _logger.LogInformation("Lead '{LeadCode}' successfully converted to Customer '{CustomerId}' (Opportunity '{OpportunityId}').", 
                lead.LeadCode, customer.CustomerId, opportunityId);

            return (true, null, customer.CustomerId, opportunityId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error executing lead conversion for Lead ID #{LeadId}.", model.LeadId);
            return (false, "An error occurred while converting the lead. Data has been rolled back safely.", null, null);
        }
    }

    public async Task<List<SelectListItem>> GetSalesExecutivesSelectListAsync()
    {
        try
        {
            var users = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FullName} ({u.Email})"
                })
                .ToListAsync();

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load sales executives list from database.");
            return new List<SelectListItem>();
        }
    }

    private async Task RecordAuditLogAsync(string? userId, string action, string entityName, string? recordId, string? oldValue, string? newValue)
    {
        await _auditLogService.LogAsync(userId, action, entityName, recordId, oldValue, newValue);
    }
}
