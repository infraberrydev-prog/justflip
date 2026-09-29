namespace JustFlip.DTO
{
    public record PagedReportResponse(
        List<DailySalesReportListDto> Data,
        int TotalRecords,
        int CurrentPage,
        int TotalPages,
        int PageSize
    );

    public record DailySalesReportListDto(
        int Id,
        string ReportId,
        string ReportName,
        string DateAndTimeCreated,
        decimal TotalGrossCash,
        decimal TotalCreditCardPayment,
        decimal TotalSalaryAdvance,
        decimal TotalCommission,
        decimal TotalExpenses,
        decimal TotalAmountExpenses,
        decimal TotalAmountDeposited
    );
}
