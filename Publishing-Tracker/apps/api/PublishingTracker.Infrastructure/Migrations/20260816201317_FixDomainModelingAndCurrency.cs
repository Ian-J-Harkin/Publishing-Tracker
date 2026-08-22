using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PublishingTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixDomainModelingAndCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseCurrencyRevenue",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseCurrencyRoyalty",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Sales",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformCommissionRate",
                table: "Sales",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseCurrencyRevenue",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "BaseCurrencyRoyalty",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PlatformCommissionRate",
                table: "Sales");
        }
    }
}
