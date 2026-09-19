namespace JustFlip.DTO
{
    public class DailySalesDepositRecordListDto
    {
        public int Id { get; set; }
        public string ReportId { get; set; } // Auto-format para sa "DS25234" pattern sa UI
        public required string ReportName { get; set; }

        // Aggregated / Summed totals mula sa child rows
        public decimal TotalGrossCash { get; set; }
        public decimal TotalCreditCardPayment { get; set; }
        public decimal TotalSalaryAdvance { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalAmountExpenses { get; set; } // "Total Expenses" column
        public decimal TotalAmountDeposited { get; set; }

        public string DateAndTimeCreated { get; set; } // Match sa UI column timestamp
        public string? LastUpdated { get; set; } = "—";
    }
}
