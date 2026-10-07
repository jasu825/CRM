using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.Services.Implementations;

namespace AcxiomCRM.Controllers;

[Route("AuditLogs")]
public class AuditLogsController : Controller
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(IAuditLogService auditLogService, ILogger<AuditLogsController> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    // GET: /AuditLogs
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? search,
        string? userId,
        string? action,
        string? entityName,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 20)
    {
        // Enforce Admin access check: non-admin users cannot access security audit trails
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            _logger.LogWarning("Unauthorized attempt by user '{UserName}' to access Audit Logs.", User.Identity.Name);
            await _auditLogService.LogSecurityEventAsync(
                User.Identity.Name,
                "UnauthorizedAccessAttempt",
                "Non-admin user attempted to access the Audit Log administrative portal.",
                details: $"IP={HttpContext.Connection.RemoteIpAddress}");
            return Forbid();
        }

        try
        {
            var model = await _auditLogService.GetPagedAuditLogsAsync(
                search, userId, action, entityName, fromDate, toDate, page, pageSize);

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Audit Logs.");
            TempData["ErrorMessage"] = "Unable to load audit logs at this time.";
            return View(new ViewModels.AuditLogs.AuditLogListViewModel());
        }
    }

    // GET: /AuditLogs/Details/5
    [HttpGet("Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            var log = await _auditLogService.GetAuditLogByIdAsync(id);
            if (log == null)
            {
                TempData["ErrorMessage"] = $"Audit Log entry #{id} was not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading audit log entry #{AuditLogId}.", id);
            TempData["ErrorMessage"] = "Unable to load audit log details.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: /AuditLogs/VerifyAuditSecurity
    // Test endpoint to verify secret sanitization and EF Core immutability
    [HttpGet("VerifyAuditSecurity")]
    public async Task<IActionResult> VerifyAuditSecurity()
    {
        var results = new List<string>();
        var passedCount = 0;

        // --- TEST 1: Immutability Protection (Update Prevention) ---
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using (var db = new ApplicationDbContext(options))
        {
            var auditEntry = new AuditLog
            {
                UserId = "user-101",
                Action = "Create",
                EntityName = "Customer",
                RecordId = "CUST-1001",
                Result = "Success",
                Details = "Initial creation"
            };
            db.AuditLogs.Add(auditEntry);
            await db.SaveChangesAsync();

            // Attempt unauthorized update
            auditEntry.Details = "Hacked/Tampered modification!";
            try
            {
                await db.SaveChangesAsync();
                results.Add("FAIL: AuditLog modification was permitted.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("immutable"))
            {
                results.Add($"PASS: AuditLog update blocked with InvalidOperationException: '{ex.Message}'");
                passedCount++;
            }
        }

        // --- TEST 2: Immutability Protection (Delete Prevention) ---
        using (var db = new ApplicationDbContext(options))
        {
            var auditEntry = await db.AuditLogs.FirstAsync();
            db.AuditLogs.Remove(auditEntry);
            try
            {
                await db.SaveChangesAsync();
                results.Add("FAIL: AuditLog deletion was permitted.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("immutable"))
            {
                results.Add($"PASS: AuditLog deletion blocked with InvalidOperationException: '{ex.Message}'");
                passedCount++;
            }
        }

        // --- TEST 3: Secret & Password Sanitization ---
        using (var db = new ApplicationDbContext(options))
        {
            var testAuditService = new AuditLogService(
                db, 
                new HttpContextAccessor(), 
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuditLogService>());

            await testAuditService.LogAsync(
                "user-99",
                "PasswordChange",
                "User",
                "USR-99",
                oldValue: new { Password = "OldPlainTextP@ss123!", Username = "admin" },
                newValue: new 
                { 
                    Password = "NewSecretPassword456!", 
                    PasswordHash = "AQAAAAEAACcQAAAAEHASH...", 
                    Token = "eyJhbGciOiJIUzI1NiIsIn...", 
                    SecurityStamp = "SEC-STAMP-XYZ",
                    ApiKey = "API-KEY-123456",
                    Status = "Active" 
                },
                details: "Sensitive payload: password=SuperSecretPassword; token=TokenValue123;");

            var loggedEntry = await db.AuditLogs
                .OrderByDescending(a => a.AuditLogId)
                .FirstOrDefaultAsync(a => a.Action == "PasswordChange");

            if (loggedEntry != null)
            {
                var leaksOldSecret = loggedEntry.OldValue?.Contains("OldPlainTextP@ss123!") ?? false;
                var leaksNewSecret = loggedEntry.NewValue?.Contains("NewSecretPassword456!") ?? false;
                var leaksHash = loggedEntry.NewValue?.Contains("AQAAAAEAACcQAAAAEHASH...") ?? false;
                var leaksToken = loggedEntry.NewValue?.Contains("eyJhbGciOiJIUzI1NiIsIn...") ?? false;
                var leaksStamp = loggedEntry.NewValue?.Contains("SEC-STAMP-XYZ") ?? false;
                var leaksApiKey = loggedEntry.NewValue?.Contains("API-KEY-123456") ?? false;
                var hasRedactedToken = (loggedEntry.NewValue?.Contains("[REDACTED]") ?? false) && 
                                       (loggedEntry.OldValue?.Contains("[REDACTED]") ?? false);

                if (!leaksOldSecret && !leaksNewSecret && !leaksHash && !leaksToken && !leaksStamp && !leaksApiKey && hasRedactedToken)
                {
                    results.Add("PASS: Secret Sanitization fully verified (passwords, hashes, tokens, security stamps, and API keys are redacted).");
                    passedCount++;
                }
                else
                {
                    results.Add("FAIL: Secrets were detected unmasked in audit log payload.");
                }
            }
            else
            {
                results.Add("FAIL: Log entry was not saved.");
            }
        }

        // --- TEST 4: Authentication Events ---
        using (var db = new ApplicationDbContext(options))
        {
            var testAuditService = new AuditLogService(
                db, 
                new HttpContextAccessor(), 
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuditLogService>());

            await testAuditService.LogAuthEventAsync("admin-1", "admin@acxiom.crm", "LoginSuccess", "Success");
            await testAuditService.LogAuthEventAsync(null, "intruder@bad.com", "LoginFailed", "Failed", "Invalid password attempt");
            await testAuditService.LogAuthEventAsync("admin-1", "admin@acxiom.crm", "Logout", "Success");

            var authLogs = await db.AuditLogs.Where(a => a.EntityName == "Authentication").ToListAsync();
            if (authLogs.Count == 3)
            {
                results.Add("PASS: Auth events (LoginSuccess, LoginFailed, Logout) recorded accurately.");
                passedCount++;
            }
            else
            {
                results.Add($"FAIL: Expected 3 auth logs, found {authLogs.Count}.");
            }
        }

        // --- TEST 5: Role Changes & Security Events ---
        using (var db = new ApplicationDbContext(options))
        {
            var testAuditService = new AuditLogService(
                db, 
                new HttpContextAccessor(), 
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuditLogService>());

            await testAuditService.LogRoleChangeEventAsync("admin-1", "user-2", "sales@acxiom.crm", "RoleAssigned", "Manager");
            await testAuditService.LogSecurityEventAsync("admin-1", "AccountLocked", "User account locked due to excessive failed attempts", "user-3");

            var roleLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityName == "Role");
            var secLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityName == "Security");

            if (roleLog != null && secLog != null)
            {
                results.Add("PASS: Role modification and Security Alert events recorded successfully.");
                passedCount++;
            }
            else
            {
                results.Add("FAIL: Role change or Security Alert logs missing.");
            }
        }

        return Json(new
        {
            success = passedCount == 5,
            passedTests = $"{passedCount} / 5",
            verificationResults = results
        });
    }
}
