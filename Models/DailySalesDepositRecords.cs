using System.ComponentModel.DataAnnotations.Schema;

namespace JustFlip.Models
{
    public class DailySalesDepositRecord
    {
        public int Id { get; set; }

        public int BranchId { get; set; }
        public required string BranchCode { get; set; } // 👈 Meron na sa Parent
        public required string ReportName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastDateUpdated { get; set; }

        // Navigation property para sa mga Sub-Rows
        public virtual ICollection<DailySalesDepositRecordRow> Rows { get; set; } = new List<DailySalesDepositRecordRow>();
    }

    public class DailySalesDepositRecordRow
    {
        public int Id { get; set; }

        // 🎯 IMPORTANTE: Foreign Key at Navigation Property papunta sa Parent
        public int DailySalesDepositRecordId { get; set; }

        [ForeignKey("DailySalesDepositRecordId")]
        public virtual DailySalesDepositRecord? DailySalesDepositRecord { get; set; }

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
    }
}