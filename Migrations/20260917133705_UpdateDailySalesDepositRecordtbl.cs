using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDailySalesDepositRecordtbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailySalesDepositRecords_Branches_BranchId",
                table: "DailySalesDepositRecords");

            migrationBuilder.DropIndex(
                name: "IX_DailySalesDepositRecords_BranchId",
                table: "DailySalesDepositRecords");

            migrationBuilder.AddColumn<string>(
                name: "BranchCode",
                table: "DailySalesDepositRecords",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchCode",
                table: "DailySalesDepositRecords");

            migrationBuilder.CreateIndex(
                name: "IX_DailySalesDepositRecords_BranchId",
                table: "DailySalesDepositRecords",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailySalesDepositRecords_Branches_BranchId",
                table: "DailySalesDepositRecords",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
