using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TeleMed.Infrastructure.Persistence;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261003160000_CardHoldSwitches")]
    public partial class CardHoldSwitches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hold_lkr_within_six_days",
                table: "platform_billing_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "hold_usd_within_six_days",
                table: "platform_billing_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hold_lkr_within_six_days",
                table: "platform_billing_settings");

            migrationBuilder.DropColumn(
                name: "hold_usd_within_six_days",
                table: "platform_billing_settings");
        }
    }
}
