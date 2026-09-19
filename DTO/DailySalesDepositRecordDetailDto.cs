namespace JustFlip.DTO
{
    public class DailySalesDepositRecordDetailDto
    {
        public int Id { get; set; }
        public string ReportId => $"DS{Id:D5}";
        public required string ReportName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<SalesRecordRowDetailDto> Rows { get; set; } = [];
    }

    public class SalesRecordRowDetailDto
    {
        public int Id { get; set; }
        public required string CashierName { get; set; }
        public DateOnly DateOfTransaction { get; set; }
        public decimal TotalGrossCash { get; set; }
        public decimal? CreditCardPayment { get; set; }
        public decimal AmountDeposited { get; set; }
        public decimal? SalaryAdvance { get; set; }
        public decimal Commission { get; set; }
        public decimal? Expenses { get; set; }
        public decimal TotalExpenses { get; set; }
        public string? Remarks { get; set; }
    }
}
