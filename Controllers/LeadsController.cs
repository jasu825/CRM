using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Leads;

namespace AcxiomCRM.Controllers;

public class LeadsController : Controller
{
    private readonly ILeadService _leadService;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(ILeadService leadService, ILogger<LeadsController> logger)
    {
        _leadService = leadService;
        _logger = logger;
    }

    // GET: /Leads
    public async Task<IActionResult> Index(string? search, LeadStatus? status, string? assignedTo, int page = 1, int pageSize = 10)
    {
        try
        {
            var model = await _leadService.GetPagedLeadsAsync(search, status, assignedTo, page, pageSize);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading leads list.");
            TempData["ErrorMessage"] = "Unable to load leads list at this time.";
            return View(new LeadListViewModel());
        }
    }

    // GET: /Leads/Details/5
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var details = await _leadService.GetLeadDetailsAsync(id);
            if (details == null)
            {
                TempData["ErrorMessage"] = $"Lead with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lead details for ID #{LeadId}.", id);
            TempData["ErrorMessage"] = "Unable to load lead details.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: /Leads/Create
    public async Task<IActionResult> Create()
    {
        try
        {
            var model = new LeadCreateEditViewModel
            {
                SalesExecutivesList = await _leadService.GetSalesExecutivesSelectListAsync()
            };
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing lead creation view.");
            TempData["ErrorMessage"] = "Unable to load form at this time.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Leads/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.SalesExecutivesList = await _leadService.GetSalesExecutivesSelectListAsync();
            return View(model);
        }

        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _leadService.CreateLeadAsync(model, currentUserId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create lead.");
                model.SalesExecutivesList = await _leadService.GetSalesExecutivesSelectListAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = $"Lead '{result.Lead?.LeadName}' ({result.Lead?.LeadCode}) was created successfully.";
            return RedirectToAction(nameof(Details), new { id = result.Lead?.LeadId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in LeadsController.Create for '{LeadName}'.", model.LeadName);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while creating the lead.");
            model.SalesExecutivesList = await _leadService.GetSalesExecutivesSelectListAsync();
            return View(model);
        }
    }

    // GET: /Leads/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var model = await _leadService.GetLeadForEditAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = $"Lead with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading edit lead form for ID #{LeadId}.", id);
            TempData["ErrorMessage"] = "Unable to load lead for editing.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Leads/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LeadCreateEditViewModel model)
    {
        if (id != model.LeadId)
        {
            TempData["ErrorMessage"] = "Lead identifier mismatch.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            model.SalesExecutivesList = await _leadService.GetSalesExecutivesSelectListAsync();
            return View(model);
        }

        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _leadService.UpdateLeadAsync(model, currentUserId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update lead.");
                model.SalesExecutivesList = await _leadService.GetSalesExecutivesSelectListAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = $"Lead '{model.LeadName}' was updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception updating lead ID #{LeadId}.", id);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while updating the lead.");
            model.SalesExecutivesList = await _leadService.GetSalesExecutivesSelectListAsync();
            return View(model);
        }
    }

    // GET: /Leads/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var lead = await _leadService.GetLeadByIdAsync(id);
            if (lead == null)
            {
                TempData["ErrorMessage"] = $"Lead with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(lead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading delete lead view for ID #{LeadId}.", id);
            TempData["ErrorMessage"] = "Unable to process delete request.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Leads/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _leadService.DeleteLeadAsync(id, currentUserId);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Lead record was removed or marked as Lost.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception deleting lead ID #{LeadId}.", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the lead.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: /Leads/Convert/5
    public async Task<IActionResult> Convert(int id)
    {
        try
        {
            var model = await _leadService.GetLeadForConvertAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = $"Lead with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing lead conversion view for ID #{LeadId}.", id);
            TempData["ErrorMessage"] = "Unable to load lead conversion form.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Leads/Convert/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(LeadConvertViewModel model)
    {
        if (model.CreateOpportunity)
        {
            if (string.IsNullOrWhiteSpace(model.OpportunityName))
            {
                ModelState.AddModelError(nameof(model.OpportunityName), "Opportunity Name is required when creating an opportunity.");
            }

            if (!model.OpportunityAmount.HasValue || model.OpportunityAmount <= 0)
            {
                ModelState.AddModelError(nameof(model.OpportunityAmount), "Opportunity Amount must be greater than 0.");
            }

            if (model.ExpectedCloseDate.HasValue && model.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(model.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _leadService.ConvertLeadAsync(model, currentUserId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Conversion failed.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Lead successfully converted! Customer #{result.CustomerId} was created.";
            return RedirectToAction("Details", "Customers", new { id = result.CustomerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception converting lead ID #{LeadId}.", model.LeadId);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while converting the lead.");
            return View(model);
        }
    }

    // POST: /Leads/ChangeStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, LeadStatus newStatus)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _leadService.UpdateStatusAsync(id, newStatus, currentUserId);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = $"Lead status updated to '{newStatus}'.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing status for Lead ID #{LeadId}.", id);
            TempData["ErrorMessage"] = "Failed to update lead status.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
