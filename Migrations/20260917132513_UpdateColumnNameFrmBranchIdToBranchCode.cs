using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class UpdateColumnNameFrmBranchIdToBranchCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailySalesDepositRecordRows_DailySalesDepositRecords_DailyS~",
                table: "DailySalesDepositRecordRows");

            migrationBuilder.AlterColumn<int>(
                name: "DailySalesReportId",
                table: "DailySalesDepositRecordRows",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "BranchCode",
                table: "DailySalesDepositRecordRows",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_DailySalesDepositRecordRows_DailySalesDepositRecords_DailyS~",
                table: "DailySalesDepositRecordRows",
                column: "DailySalesReportId",
                principalTable: "DailySalesDepositRecords",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailySalesDepositRecordRows_DailySalesDepositRecords_DailyS~",
                table: "DailySalesDepositRecordRows");

            migrationBuilder.DropColumn(
                name: "BranchCode",
                table: "DailySalesDepositRecordRows");

            migrationBuilder.AlterColumn<int>(
                name: "DailySalesReportId",
                table: "DailySalesDepositRecordRows",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DailySalesDepositRecordRows_DailySalesDepositRecords_DailyS~",
                table: "DailySalesDepositRecordRows",
                column: "DailySalesReportId",
                principalTable: "DailySalesDepositRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
