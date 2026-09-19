namespace JustFlip.DTO
{
    public record AddSingleRowRequest(
        string CashierName,
        string ControlNo,
        DateOnly DateOfTransaction,
        decimal TotalGrossCash,
        decimal CreditCardPayment,
        decimal SalaryAdvance,
        decimal Commission,
        decimal Expenses,
        decimal TotalExpenses,
        decimal AmountDeposited,
        string Remarks
    );

    public class AddSingleRowResponse
    {
        public string message { get; set; }
        public int newRowId { get; set; }
    }

}
