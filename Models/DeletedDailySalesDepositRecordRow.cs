using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JustFlip.Models
{
    [Table("DeletedDailySalesDepositRecordRows")]
    public class DeletedDailySalesDepositRecordRow
    {
        [Key]
        public int Id { get; set; }

        public int OriginalRowId { get; set; }
        public int DailySalesDepositRecordId { get; set; }

        public required string BranchCode { get; set; }
        public required string CashierName { get; set; }
        public required string ControlNo { get; set; }
        public DateOnly DateOfTransaction { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal TotalGrossCash { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? CreditCardPayment { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? SalaryAdvance { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal Commission { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? Expenses { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal TotalExpenses { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal AmountDeposited { get; set; }

        public string? Remarks { get; set; }

        // 🛡️ Deletion Metadata
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
        public string DeletedBy { get; set; } = string.Empty;
        public string? DeletionReason { get; set; }
    }
}