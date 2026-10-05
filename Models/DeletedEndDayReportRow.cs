using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JustFlip.Models
{
    [Table("DeletedEndDayReportRows")]
    public class DeletedEndDayReportRow
    {
        [Key]
        public int Id { get; set; }

        public int OriginalRowId { get; set; }
        public int EndDayReportId { get; set; }
        public required string BranchCode { get; set; }
        public required string EmployeeName { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal GrossAmount { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? CreditCardPayment { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal Commission { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? SalonExpenses { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? SalaryAdvance { get; set; }

        // 🛡️ Deletion Metadata
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
        public string DeletedBy { get; set; } = string.Empty;
        public string? DeletionReason { get; set; }
    }
}