using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.AuditLogs;

namespace AcxiomCRM.Services.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(
        string? userId,
        string action,
        string entityName,
        string? recordId = null,
        object? oldValue = null,
        object? newValue = null,
        string? result = "Success",
        string? details = null,
        string? customIpAddress = null);

    Task LogAuthEventAsync(
        string? userId,
        string? userName,
        string authAction, // e.g., "LoginSuccess", "LoginFailed", "Logout", "AccountLocked", "PasswordChanged"
        string result = "Success",
        string? details = null);

    Task LogSecurityEventAsync(
        string? userId,
        string securityAction,
        string description,
        string? recordId = null,
        string? details = null);

    Task LogRoleChangeEventAsync(
        string? adminUserId,
        string targetUserId,
        string targetUserName,
        string action, // "RoleAssigned", "RoleRemoved"
        string roleName,
        string? details = null);

    Task<AuditLogListViewModel> GetPagedAuditLogsAsync(
        string? search,
        string? userId,
        string? action,
        string? entityName,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber = 1,
        int pageSize = 20);

    Task<AuditLog?> GetAuditLogByIdAsync(int id);
}
