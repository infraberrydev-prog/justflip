namespace JustFlip.DTO
{
    // ==========================================
    // 1. ADD ROW REQUEST DTO
    // ==========================================
    public class AddEndDayRowRequest
    {
        public required string EmployeeName { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal? CreditCardPayment { get; set; }
        public decimal Commission { get; set; }
        public decimal? SalonExpenses { get; set; }
        public decimal? SalaryAdvance { get; set; }
    }

    // ==========================================
    // 2. UPDATE ROW REQUEST DTO
    // ==========================================
    public class UpdateEndDayReportRowDto
    {
        public required string EmployeeName { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal? CreditCardPayment { get; set; }
        public decimal Commission { get; set; }
        public decimal? SalonExpenses { get; set; }
        public decimal? SalaryAdvance { get; set; }
    }
}
