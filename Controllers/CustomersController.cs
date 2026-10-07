using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Customers;

namespace AcxiomCRM.Controllers;

public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(ICustomerService customerService, ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    // GET: /Customers
    public async Task<IActionResult> Index(string? search, string? status, int page = 1, int pageSize = 10)
    {
        try
        {
            var model = await _customerService.GetPagedCustomersAsync(search, status, page, pageSize);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customers list.");
            TempData["ErrorMessage"] = "Unable to load customer list at this time. Please try again.";
            return View(new CustomerListViewModel());
        }
    }

    // GET: /Customers/Details/5
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var details = await _customerService.GetCustomerDetailsAsync(id);
            if (details == null)
            {
                TempData["ErrorMessage"] = $"Customer with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customer details for ID #{CustomerId}.", id);
            TempData["ErrorMessage"] = "Unable to load customer details. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: /Customers/Create
    public IActionResult Create()
    {
        return View(new CustomerCreateEditViewModel());
    }

    // POST: /Customers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerCreateEditViewModel model)
    {
        // 1. Client-Side + Model-State Validation
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 2. Server-Side Email Uniqueness Check
        if (!await _customerService.IsEmailUniqueAsync(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "A customer with this email address already exists.");
        }

        // 3. Server-Side Phone Uniqueness Check
        if (!await _customerService.IsPhoneUniqueAsync(model.Phone))
        {
            ModelState.AddModelError(nameof(model.Phone), "A customer with this phone number already exists.");
        }

        // 4. Duplicate Customer Prevention
        if (await _customerService.IsDuplicateCustomerAsync(model.CustomerName, model.Email, model.Phone))
        {
            ModelState.AddModelError(string.Empty, "A duplicate customer with identical identification already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _customerService.CreateCustomerAsync(model, currentUserId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create customer.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Customer '{result.Customer?.CustomerName}' ({result.Customer?.CustomerCode}) was created successfully.";
            return RedirectToAction(nameof(Details), new { id = result.Customer?.CustomerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CustomersController.Create for '{CustomerName}'.", model.CustomerName);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while saving the customer. Please verify your entries.");
            return View(model);
        }
    }

    // GET: /Customers/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var model = await _customerService.GetCustomerForEditAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = $"Customer with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing customer edit form for ID #{CustomerId}.", id);
            TempData["ErrorMessage"] = "Unable to load customer for editing.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Customers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerCreateEditViewModel model)
    {
        if (id != model.CustomerId)
        {
            TempData["ErrorMessage"] = "Customer identifier mismatch.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Server-Side Uniqueness Checks (excluding current customer ID)
        if (!await _customerService.IsEmailUniqueAsync(model.Email, id))
        {
            ModelState.AddModelError(nameof(model.Email), "A customer with this email address already exists.");
        }

        if (!await _customerService.IsPhoneUniqueAsync(model.Phone, id))
        {
            ModelState.AddModelError(nameof(model.Phone), "A customer with this phone number already exists.");
        }

        if (await _customerService.IsDuplicateCustomerAsync(model.CustomerName, model.Email, model.Phone, id))
        {
            ModelState.AddModelError(string.Empty, "Another customer with identical business identification already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _customerService.UpdateCustomerAsync(model, currentUserId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update customer.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Customer '{model.CustomerName}' was updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception updating customer ID #{CustomerId}.", id);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while updating the customer.");
            return View(model);
        }
    }

    // GET: /Customers/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var customer = await _customerService.GetCustomerByIdAsync(id);
            if (customer == null)
            {
                TempData["ErrorMessage"] = $"Customer with ID #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading delete confirmation for ID #{CustomerId}.", id);
            TempData["ErrorMessage"] = "Unable to process delete request.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Customers/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _customerService.DeleteCustomerAsync(id, currentUserId);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Customer removed or deactivated successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception deleting customer ID #{CustomerId}.", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the customer.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: /Customers/Deactivate/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _customerService.DeactivateCustomerAsync(id, currentUserId);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Customer has been deactivated.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception deactivating customer ID #{CustomerId}.", id);
            TempData["ErrorMessage"] = "An error occurred while deactivating the customer.";
            return RedirectToAction(nameof(Index));
        }
    }
}
