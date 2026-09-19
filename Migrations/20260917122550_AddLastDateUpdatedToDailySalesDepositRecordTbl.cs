using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustFlip.Migrations
{
    /// <inheritdoc />
    public partial class AddLastDateUpdatedToDailySalesDepositRecordTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDateUpdated",
                table: "DailySalesDepositRecords",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDateUpdated",
                table: "DailySalesDepositRecords");
        }
    }
}
