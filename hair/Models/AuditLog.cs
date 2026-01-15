namespace hair.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public string Action { get; set; } = string.Empty; // INSERT, UPDATE, DELETE
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public int? ChangedByUserId { get; set; }
        public DateTime ChangedDate { get; set; } = DateTime.Now;
    }
}
