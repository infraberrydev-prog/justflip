using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class RemoveColumnDailySalesReportId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DailySalesReportId",
                table: "DailySalesDepositRecordRows",
                newName: "DailySalesDepositRecordId");

            migrationBuilder.RenameIndex(
                name: "IX_DailySalesDepositRecordRows_DailySalesReportId",
                table: "DailySalesDepositRecordRows",
                newName: "IX_DailySalesDepositRecordRows_DailySalesDepositRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DailySalesDepositRecordId",
                table: "DailySalesDepositRecordRows",
                newName: "DailySalesReportId");

            migrationBuilder.RenameIndex(
                name: "IX_DailySalesDepositRecordRows_DailySalesDepositRecordId",
                table: "DailySalesDepositRecordRows",
                newName: "IX_DailySalesDepositRecordRows_DailySalesReportId");
        }
    }
}
