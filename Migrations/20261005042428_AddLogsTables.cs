using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class AddLogsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityName = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OldValues = table.Column<string>(type: "jsonb", nullable: true),
                    NewValues = table.Column<string>(type: "jsonb", nullable: true),
                    ChangedColumns = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeletedDailySalesDepositRecordRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalRowId = table.Column<int>(type: "integer", nullable: false),
                    DailySalesDepositRecordId = table.Column<int>(type: "integer", nullable: false),
                    BranchCode = table.Column<string>(type: "text", nullable: false),
                    CashierName = table.Column<string>(type: "text", nullable: false),
                    ControlNo = table.Column<string>(type: "text", nullable: false),
                    DateOfTransaction = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalGrossCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditCardPayment = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SalaryAdvance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Commission = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Expenses = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountDeposited = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: false),
                    DeletionReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedDailySalesDepositRecordRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeletedDailySalesDepositRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalRecordId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    BranchCode = table.Column<string>(type: "text", nullable: false),
                    ReportName = table.Column<string>(type: "text", nullable: false),
                    OriginalCreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastDateUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: false),
                    DeletionReason = table.Column<string>(type: "text", nullable: true),
                    ArchivedRowsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedDailySalesDepositRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeletedEndDayReportRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalRowId = table.Column<int>(type: "integer", nullable: false),
                    EndDayReportId = table.Column<int>(type: "integer", nullable: false),
                    BranchCode = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditCardPayment = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Commission = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SalonExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SalaryAdvance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: false),
                    DeletionReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedEndDayReportRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeletedEndDayReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalReportId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    BranchCode = table.Column<string>(type: "text", nullable: false),
                    ReportName = table.Column<string>(type: "text", nullable: false),
                    DateOfReport = table.Column<DateOnly>(type: "date", nullable: false),
                    CashierName = table.Column<string>(type: "text", nullable: false),
                    OriginalCreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastDateUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: false),
                    DeletionReason = table.Column<string>(type: "text", nullable: true),
                    ArchivedRowsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedEndDayReports", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DeletedDailySalesDepositRecordRows");

            migrationBuilder.DropTable(
                name: "DeletedDailySalesDepositRecords");

            migrationBuilder.DropTable(
                name: "DeletedEndDayReportRows");

            migrationBuilder.DropTable(
                name: "DeletedEndDayReports");
        }
    }
}
