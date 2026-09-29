namespace JustFlip.DTO
{
    // DTO para sa mga row items sa loob ng Modal Table
    public record EndDayReportRowDetailDto(
        int Id,
        string EmployeeName,
        decimal GrossAmount,
        decimal? CreditCardPayment,
        decimal Commission,
        decimal? SalonExpenses,
        decimal? SalaryAdvance,
        decimal CashInRegister // GrossAmount - CreditCard - SalonExpenses - SalaryAdvance
    );

    // Parent DTO na naglalaman ng Report Header Metadata at listahan ng Rows
    public class EndDayReportDetailDto
    {
        public int Id { get; set; }
        public string FormattedReportId { get; set; } = string.Empty; // e.g., "ED05234"
        public string ReportName { get; set; } = string.Empty;
        public string DateOfReport { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string DateCreated { get; set; } = string.Empty;
        public string? LastUpdated { get; set; }

        // Dynamic Totals para sa Summary Row o Header Totals
        public decimal TotalGrossCash { get; set; }
        public decimal TotalCreditCard { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalSalonExpenses { get; set; }
        public decimal TotalSalaryAdvance { get; set; }
        public decimal TotalCashInRegister { get; set; }

        public List<EndDayReportRowDetailDto> Rows { get; set; } = new();
    }
}
