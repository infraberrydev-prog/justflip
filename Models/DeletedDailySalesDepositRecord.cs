using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JustFlip.Models
{
    [Table("DeletedDailySalesDepositRecords")]
    public class DeletedDailySalesDepositRecord
    {
        [Key]
        public int Id { get; set; }

        public int OriginalRecordId { get; set; }
        public int BranchId { get; set; }
        public required string BranchCode { get; set; }
        public required string ReportName { get; set; }
        public DateTime OriginalCreatedAt { get; set; }
        public DateTime? LastDateUpdated { get; set; }

        // 🛡️ Deletion Metadata
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
        public string DeletedBy { get; set; } = string.Empty;
        public string? DeletionReason { get; set; }

        // JSON Snapshot ng kasamang rows noong binura ang parent report
        [Column(TypeName = "jsonb")]
        public string? ArchivedRowsJson { get; set; }
    }
}