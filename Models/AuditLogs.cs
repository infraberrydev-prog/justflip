using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JustFlip.Models
{
    public class AuditLogs
    {
        [Key]
        public int Id { get; set; }

        public string EntityName { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string ModifiedBy { get; set; } = "System";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "jsonb")]
        public string? OldValues { get; set; }

        [Column(TypeName = "jsonb")]
        public string? NewValues { get; set; }

        public string? ChangedColumns { get; set; }
    }
}
