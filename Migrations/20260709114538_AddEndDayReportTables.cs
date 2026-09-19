using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class AddEndDayReportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EndDayReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ReportName = table.Column<string>(type: "text", nullable: false),
                    DateOfReport = table.Column<DateOnly>(type: "date", nullable: false),
                    CashierName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndDayReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EndDayReports_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EndDayReportRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EndDayReportId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditCardPayment = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Commission = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SalonExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SalaryAdvance = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndDayReportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EndDayReportRows_EndDayReports_EndDayReportId",
                        column: x => x.EndDayReportId,
                        principalTable: "EndDayReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EndDayReportRows_EndDayReportId",
                table: "EndDayReportRows",
                column: "EndDayReportId");

            migrationBuilder.CreateIndex(
                name: "IX_EndDayReports_BranchId",
                table: "EndDayReports",
                column: "BranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EndDayReportRows");

            migrationBuilder.DropTable(
                name: "EndDayReports");
        }
    }
}
