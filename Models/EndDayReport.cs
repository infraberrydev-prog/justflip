using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JustFlip.Models
{
    public class EndDayReport
    {
        public int Id { get; set; }

        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        public string ReportName { get; set; } = null!;

        public DateOnly DateOfReport { get; set; }

        [Required]
        public string CashierName { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<EndDayReportRow> Rows { get; set; } = new List<EndDayReportRow>();
    }

    public class EndDayReportRow
    {
        public int Id { get; set; }

        public int EndDayReportId { get; set; }
        public EndDayReport? EndDayReport { get; set; }

        [Required]
        public string EmployeeName { get; set; } = null!;

        [Column(TypeName = "numeric(18,2)")]
        public decimal GrossAmount { get; set; }

        // Deductions
        [Column(TypeName = "numeric(18,2)")]
        public decimal? CreditCardPayment { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal Commission { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? SalonExpenses { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? SalaryAdvance { get; set; }
    }
}
