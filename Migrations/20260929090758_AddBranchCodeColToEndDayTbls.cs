using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchCodeColToEndDayTbls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EndDayReports_Branches_BranchId",
                table: "EndDayReports");

            migrationBuilder.DropIndex(
                name: "IX_EndDayReports_BranchId",
                table: "EndDayReports");

            migrationBuilder.AddColumn<string>(
                name: "BranchCode",
                table: "EndDayReports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BranchCode",
                table: "EndDayReportRows",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchCode",
                table: "EndDayReports");

            migrationBuilder.DropColumn(
                name: "BranchCode",
                table: "EndDayReportRows");

            migrationBuilder.CreateIndex(
                name: "IX_EndDayReports_BranchId",
                table: "EndDayReports",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_EndDayReports_Branches_BranchId",
                table: "EndDayReports",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
