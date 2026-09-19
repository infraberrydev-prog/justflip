namespace JustFlip.DTO
{
    public class CreateDailySalesDepositRecordDto
    {
        public required string ReportName { get; set; }
        public List<DailySalesDepositRecordRowDto> Rows { get; set; } = [];
    }

    public class DailySalesDepositRecordRowDto
    {
        public required string CashierName { get; set; }
        public required string ControlNo { get; set; }
        public DateOnly DateOfTransaction { get; set; }
        public decimal TotalGrossCash { get; set; }
        public decimal? CreditCardPayment { get; set; }
        public decimal? SalaryAdvance { get; set; }
        public decimal Commission { get; set; }
        public decimal? Expenses { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal AmountDeposited { get; set; }
        public string? Remarks { get; set; }
    }
}
