using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class AddDailySalesRecordTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailySalesDepositRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ReportName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySalesDepositRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailySalesDepositRecords_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailySalesDepositRecordRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DailySalesReportId = table.Column<int>(type: "integer", nullable: false),
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
                    Remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySalesDepositRecordRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailySalesDepositRecordRows_DailySalesDepositRecords_DailyS~",
                        column: x => x.DailySalesReportId,
                        principalTable: "DailySalesDepositRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailySalesDepositRecordRows_DailySalesReportId",
                table: "DailySalesDepositRecordRows",
                column: "DailySalesReportId");

            migrationBuilder.CreateIndex(
                name: "IX_DailySalesDepositRecordRows_DateOfTransaction",
                table: "DailySalesDepositRecordRows",
                column: "DateOfTransaction");

            migrationBuilder.CreateIndex(
                name: "IX_DailySalesDepositRecords_BranchId",
                table: "DailySalesDepositRecords",
                column: "BranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailySalesDepositRecordRows");

            migrationBuilder.DropTable(
                name: "DailySalesDepositRecords");
        }
    }
}
