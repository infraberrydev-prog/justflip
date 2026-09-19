namespace JustFlip.DTO
{
    public class SaveEndDayReportDto
    {
        public int Id { get; set; }
        public required string ReportName { get; set; }
        public DateOnly DateOfReport { get; set; }
        public required string CashierName { get; set; }
        public List<SaveEndDayReportRowDto> Rows { get; set; } = [];
    }

    public class SaveEndDayReportRowDto
    {
        public int Id { get; set; }
        public required string EmployeeName { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal? CreditCardPayment { get; set; }
        public decimal Commission { get; set; }
        public decimal? SalonExpenses { get; set; }
        public decimal? SalaryAdvance { get; set; }
    }
}
