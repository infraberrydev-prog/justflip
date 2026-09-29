namespace JustFlip.DTO
{
    // DTO para sa bawat row ng table base sa UI Columns
    public record EndDayReportListDto(
        int Id,
        string FormattedReportId, // Halimbawa: "ED05234"
        string ReportName,
        string DateOfReport,     // e.g., "May 16, 2026"
        decimal TotalGrossCash,
        decimal CreditCardPayment,
        decimal Commission,
        decimal SalonExpenses,
        decimal SalaryAdvance,
        decimal TotalCashInRegister,
        string DateCreated,       // e.g., "Jan 9, 2026 - 08:30:22"
        string LastUpdated        // e.g., "Jan 9, 2026 - 09:15:00" o "—"
    );

    // Paged Wrapper Response
    public record PagedEndDayReportResponse(
        List<EndDayReportListDto> Data,
        int TotalRecords,
        int CurrentPage,
        int TotalPages,
        int PageSize
    );
}
