using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.AuditLogs;

namespace AcxiomCRM.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    private static readonly HashSet<string> SensitiveKeyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordhash",
        "confirmpassword",
        "newpassword",
        "currentpassword",
        "oldpassword",
        "securitystamp",
        "concurrencystamp",
        "token",
        "accesstoken",
        "refreshtoken",
        "secret",
        "clientsecret",
        "apikey",
        "privatekey",
        "creditcard",
        "cvv",
        "authorization",
        "cookie"
    };

    public AuditLogService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string? userId,
        string action,
        string entityName,
        string? recordId = null,
        object? oldValue = null,
        object? newValue = null,
        string? result = "Success",
        string? details = null,
        string? customIpAddress = null)
    {
        try
        {
            var ip = customIpAddress ?? ResolveIpAddress();
            var userName = ResolveUserName(userId);

            var sanitizedOld = Sanitize(oldValue);
            var sanitizedNew = Sanitize(newValue);
            var sanitizedDetails = SanitizeString(details);

            var audit = new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = sanitizedOld,
                NewValue = sanitizedNew,
                CreatedDate = DateTime.UtcNow,
                IpAddress = ip,
                Result = result ?? "Success",
                Details = sanitizedDetails
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Audit logging failures must not crash the business operation
            _logger.LogError(ex, "Failed to record audit log for Action='{Action}', Entity='{EntityName}'.", action, entityName);
        }
    }

    public async Task LogAuthEventAsync(
        string? userId,
        string? userName,
        string authAction,
        string result = "Success",
        string? details = null)
    {
        try
        {
            var ip = ResolveIpAddress();
            var audit = new AuditLog
            {
                UserId = userId,
                UserName = userName ?? ResolveUserName(userId),
                Action = authAction,
                EntityName = "Authentication",
                RecordId = userId ?? userName,
                CreatedDate = DateTime.UtcNow,
                IpAddress = ip,
                Result = result,
                Details = SanitizeString(details)
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log auth event '{AuthAction}' for user '{UserName}'.", authAction, userName);
        }
    }

    public async Task LogSecurityEventAsync(
        string? userId,
        string securityAction,
        string description,
        string? recordId = null,
        string? details = null)
    {
        try
        {
            var ip = ResolveIpAddress();
            var audit = new AuditLog
            {
                UserId = userId,
                UserName = ResolveUserName(userId),
                Action = securityAction,
                EntityName = "Security",
                RecordId = recordId,
                CreatedDate = DateTime.UtcNow,
                IpAddress = ip,
                Result = "Warning",
                Details = SanitizeString($"{description} | {details}")
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log security event '{SecurityAction}'.", securityAction);
        }
    }

    public async Task LogRoleChangeEventAsync(
        string? adminUserId,
        string targetUserId,
        string targetUserName,
        string action,
        string roleName,
        string? details = null)
    {
        try
        {
            var ip = ResolveIpAddress();
            var audit = new AuditLog
            {
                UserId = adminUserId,
                UserName = ResolveUserName(adminUserId),
                Action = action,
                EntityName = "Role",
                RecordId = targetUserId,
                NewValue = JsonSerializer.Serialize(new { TargetUser = targetUserName, Role = roleName }),
                CreatedDate = DateTime.UtcNow,
                IpAddress = ip,
                Result = "Success",
                Details = SanitizeString(details ?? $"Role '{roleName}' {action.ToLowerInvariant()} for user '{targetUserName}'.")
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log role change for target user '{TargetUserId}'.", targetUserId);
        }
    }

    public async Task<AuditLogListViewModel> GetPagedAuditLogsAsync(
        string? search,
        string? userId,
        string? action,
        string? entityName,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(a =>
                    (a.RecordId != null && a.RecordId.ToLower().Contains(term)) ||
                    (a.Details != null && a.Details.ToLower().Contains(term)) ||
                    (a.UserName != null && a.UserName.ToLower().Contains(term)) ||
                    (a.IpAddress != null && a.IpAddress.ToLower().Contains(term)) ||
                    a.Action.ToLower().Contains(term) ||
                    a.EntityName.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(a => a.UserId == userId || a.UserName == userId);
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                query = query.Where(a => a.EntityName == entityName);
            }

            if (fromDate.HasValue)
            {
                var start = fromDate.Value.Date;
                query = query.Where(a => a.CreatedDate >= start);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(a => a.CreatedDate <= end);
            }

            var totalItems = await query.CountAsync();

            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogItemViewModel
                {
                    AuditLogId = a.AuditLogId,
                    UserId = a.UserId,
                    UserName = a.UserName,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    RecordId = a.RecordId,
                    OldValue = a.OldValue,
                    NewValue = a.NewValue,
                    CreatedDate = a.CreatedDate,
                    IpAddress = a.IpAddress,
                    Result = a.Result,
                    Details = a.Details
                })
                .ToListAsync();

            // Populate filter options safely
            var distinctActions = await _context.AuditLogs
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            var distinctEntities = await _context.AuditLogs
                .Select(a => a.EntityName)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();

            var distinctUsers = await _context.AuditLogs
                .Where(a => a.UserName != null)
                .Select(a => a.UserName!)
                .Distinct()
                .OrderBy(u => u)
                .ToListAsync();

            return new AuditLogListViewModel
            {
                AuditLogs = logs,
                Search = search,
                UserId = userId,
                Action = action,
                EntityName = entityName,
                FromDate = fromDate,
                ToDate = toDate,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                ActionsList = distinctActions.Select(a => new SelectListItem(a, a, a == action)),
                EntitiesList = distinctEntities.Select(e => new SelectListItem(e, e, e == entityName)),
                UsersList = distinctUsers.Select(u => new SelectListItem(u, u, u == userId))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged audit logs.");
            return new AuditLogListViewModel
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }

    public async Task<AuditLog?> GetAuditLogByIdAsync(int id)
    {
        return await _context.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.AuditLogId == id);
    }

    // --- Sensitive Data Sanitization Helpers ---

    private static string? Sanitize(object? value)
    {
        if (value == null) return null;

        if (value is string strValue)
        {
            return SanitizeJsonOrString(strValue);
        }

        try
        {
            var jsonString = JsonSerializer.Serialize(value);
            return SanitizeJsonOrString(jsonString);
        }
        catch
        {
            return SanitizeString(value.ToString());
        }
    }

    private static string? SanitizeJsonOrString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        try
        {
            var node = JsonNode.Parse(input);
            if (node != null)
            {
                SanitizeJsonNode(node);
                return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch
        {
            // Not valid JSON, apply regex masking
        }

        return SanitizeString(input);
    }

    private static void SanitizeJsonNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(kv => kv.Key).ToList();
            foreach (var key in keys)
            {
                if (SensitiveKeyWords.Contains(key))
                {
                    obj[key] = "[REDACTED]";
                }
                else if (obj[key] != null)
                {
                    SanitizeJsonNode(obj[key]!);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null)
                {
                    SanitizeJsonNode(item);
                }
            }
        }
    }

    private static string? SanitizeString(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var pattern = @"(?i)""?(password|secret|token|hash|salt|key|apikey)""?\s*[:=]\s*""?([^,""\s]+)""?";
        return Regex.Replace(input, pattern, "\"$1\": \"[REDACTED]\"");
    }

    private string ResolveIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return "127.0.0.1";

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var firstIp = forwarded.ToString().Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstIp)) return firstIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    private string? ResolveUserName(string? userId)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User?.Identity != null && context.User.Identity.IsAuthenticated)
        {
            return context.User.Identity.Name;
        }

        return !string.IsNullOrEmpty(userId) ? userId : "System";
    }
}
