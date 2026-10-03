using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InternationalPatientPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_sri_lankan_citizen",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "national_id_encrypted",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_country",
                table: "users",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "foreign_multiplier",
                table: "doctors",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_billing_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lkr_per_usd = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_billing_settings", x => x.id);
                    table.CheckConstraint("ck_platform_billing_settings_lkr_per_usd", "lkr_per_usd IS NULL OR (lkr_per_usd >= 1 AND lkr_per_usd <= 100000)");
                });

            migrationBuilder.InsertData(
                table: "platform_billing_settings",
                columns: new[] { "id", "created_at", "lkr_per_usd", "updated_at" },
                values: new object[] { new Guid("0199a000-0000-7000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateTimeOffset(new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.AddCheckConstraint(
                name: "ck_doctors_foreign_multiplier",
                table: "doctors",
                sql: "foreign_multiplier IS NULL OR (foreign_multiplier >= 1 AND foreign_multiplier <= 100)");

            migrationBuilder.DropIndex(
                name: "ux_payouts_doctor_id_period",
                table: "payouts");

            migrationBuilder.CreateIndex(
                name: "ux_payouts_doctor_id_period",
                table: "payouts",
                columns: new[] { "doctor_id", "period", "currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_billing_settings");

            migrationBuilder.DropIndex(
                name: "ux_payouts_doctor_id_period",
                table: "payouts");

            migrationBuilder.CreateIndex(
                name: "ux_payouts_doctor_id_period",
                table: "payouts",
                columns: new[] { "doctor_id", "period" },
                unique: true);

            migrationBuilder.DropCheckConstraint(
                name: "ck_doctors_foreign_multiplier",
                table: "doctors");

            migrationBuilder.DropColumn(
                name: "is_sri_lankan_citizen",
                table: "users");

            migrationBuilder.DropColumn(
                name: "national_id_encrypted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "registration_country",
                table: "users");

            migrationBuilder.DropColumn(
                name: "foreign_multiplier",
                table: "doctors");
        }
    }
}
