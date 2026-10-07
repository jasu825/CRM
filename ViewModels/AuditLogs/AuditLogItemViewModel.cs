namespace AcxiomCRM.ViewModels.AuditLogs;

public class AuditLogItemViewModel
{
    public int AuditLogId { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? IpAddress { get; set; }
    public string Result { get; set; } = "Success";
    public string? Details { get; set; }

    // Helpers for UI display
    public bool HasDiff => !string.IsNullOrEmpty(OldValue) || !string.IsNullOrEmpty(NewValue);
}
