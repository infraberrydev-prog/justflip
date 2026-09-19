using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchCodeAndParentRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailySalesDepositRecordRows_DailySalesDepositRecords_DailyS~",
                table: "DailySalesDepositRecordRows");

            migrationBuilder.AlterColumn<int>(
                name: "DailySalesDepositRecordId",
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
                column: "DailySalesDepositRecordId",
                principalTable: "DailySalesDepositRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailySalesDepositRecordRows_DailySalesDepositRecords_DailyS~",
                table: "DailySalesDepositRecordRows");

            migrationBuilder.AlterColumn<int>(
                name: "DailySalesDepositRecordId",
                table: "DailySalesDepositRecordRows",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_DailySalesDepositRecordRows_DailySalesDepositRecords_DailyS~",
                table: "DailySalesDepositRecordRows",
                column: "DailySalesDepositRecordId",
                principalTable: "DailySalesDepositRecords",
                principalColumn: "Id");
        }
    }
}
