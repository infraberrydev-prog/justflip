namespace JustFlip.DTO
{
    public class UpdateDailySalesDepositRecordDto
    {
        public int Id { get; set; } // 0 kung bagong report (Create); > 0 kung ie-edit (Update)
        public required string ReportName { get; set; }
        public List<UpdateSaveSalesRecordRowDto> Rows { get; set; } = [];
    }

    public class UpdateSaveSalesRecordRowDto
    {
        public int Id { get; set; } // 0 kung bagong row; > 0 kung existing row
        public required string CashierName { get; set; }
        public required string ControlNo { get; set; }
        public DateOnly DateOfTransaction { get; set; }
        public decimal TotalGrossCash { get; set; }
        public decimal CreditCardPayment { get; set; }
        public decimal SalaryAdvance { get; set; }
        public decimal Commission { get; set; }
        public decimal Expenses { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal AmountDeposited { get; set; }
        public string? Remarks { get; set; }
    }
}
