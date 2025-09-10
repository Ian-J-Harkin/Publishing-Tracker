using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublishingTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "PlatformRequests");

            migrationBuilder.AlterColumn<decimal>(
                name: "CommissionRate",
                table: "PlatformRequests",
                type: "decimal(5,2)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CommissionRate",
                table: "PlatformRequests",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "PlatformRequests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
