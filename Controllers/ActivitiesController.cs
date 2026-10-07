using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Activities;

namespace AcxiomCRM.Controllers;

public class ActivitiesController : Controller
{
    private readonly IActivityService _activityService;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(IActivityService activityService, ILogger<ActivitiesController> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    // GET: /Activities
    public async Task<IActionResult> Index(
        string? search,
        ActivityType? type,
        string? status,
        int? customerId,
        int? leadId,
        int page = 1,
        int pageSize = 10)
    {
        try
        {
            var model = await _activityService.GetPagedActivitiesAsync(search, type, status, customerId, leadId, page, pageSize);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activities list.");
            TempData["ErrorMessage"] = "Unable to load activities at this time.";
            return View(new ActivityListViewModel());
        }
    }

    // GET: /Activities/Details/5
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var activity = await _activityService.GetActivityByIdAsync(id);
            if (activity == null)
            {
                TempData["ErrorMessage"] = $"Activity with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activity details for ID #{ActivityId}.", id);
            TempData["ErrorMessage"] = "Unable to load activity details.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: /Activities/Create
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        try
        {
            var model = new ActivityCreateEditViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                ActivityDate = DateTime.Now,
                CustomersList = await _activityService.GetCustomersSelectListAsync(),
                LeadsList = await _activityService.GetLeadsSelectListAsync(),
                SalesExecutivesList = await _activityService.GetSalesExecutivesSelectListAsync()
            };
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing activity creation view.");
            TempData["ErrorMessage"] = "Unable to load activity form.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Activities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _activityService.CreateActivityAsync(model, currentUserId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create activity.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Activity '{model.Subject}' was logged successfully.";

            if (model.CustomerId.HasValue)
            {
                return RedirectToAction("Details", "Customers", new { id = model.CustomerId.Value });
            }
            if (model.LeadId.HasValue)
            {
                return RedirectToAction("Details", "Leads", new { id = model.LeadId.Value });
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception creating activity '{Subject}'.", model.Subject);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while logging the activity.");
            await PopulateDropdownsAsync(model);
            return View(model);
        }
    }

    // GET: /Activities/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var model = await _activityService.GetActivityForEditAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = $"Activity with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading edit activity form for ID #{ActivityId}.", id);
            TempData["ErrorMessage"] = "Unable to load activity for editing.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Activities/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityCreateEditViewModel model)
    {
        if (id != model.ActivityId)
        {
            TempData["ErrorMessage"] = "Activity identifier mismatch.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _activityService.UpdateActivityAsync(model, currentUserId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update activity.");
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = "Activity updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception updating activity ID #{ActivityId}.", id);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while saving the activity.");
            await PopulateDropdownsAsync(model);
            return View(model);
        }
    }

    // GET: /Activities/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var activity = await _activityService.GetActivityByIdAsync(id);
            if (activity == null)
            {
                TempData["ErrorMessage"] = $"Activity with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading delete view for Activity #{ActivityId}.", id);
            TempData["ErrorMessage"] = "Unable to process delete request.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Activities/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _activityService.DeleteActivityAsync(id, currentUserId);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Activity removed successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception deleting activity ID #{ActivityId}.", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the activity.";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task PopulateDropdownsAsync(ActivityCreateEditViewModel model)
    {
        model.CustomersList = await _activityService.GetCustomersSelectListAsync();
        model.LeadsList = await _activityService.GetLeadsSelectListAsync();
        model.SalesExecutivesList = await _activityService.GetSalesExecutivesSelectListAsync();
    }
}
