using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommissionRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "commission_bps",
                table: "doctors",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_commission_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_commission_bps = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_commission_policies", x => x.id);
                    table.CheckConstraint("ck_platform_commission_policies_bps", "default_commission_bps BETWEEN 0 AND 9700");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_doctors_commission_bps",
                table: "doctors",
                sql: "commission_bps IS NULL OR (commission_bps BETWEEN 0 AND 9700)");

            migrationBuilder.InsertData(
                table: "platform_commission_policies",
                columns: new[] { "id", "default_commission_bps", "created_at", "updated_at" },
                values: new object[]
                {
                    new Guid("c0111551-0001-7000-8000-000000000001"),
                    2000,
                    new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero),
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_commission_policies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_doctors_commission_bps",
                table: "doctors");

            migrationBuilder.DropColumn(
                name: "commission_bps",
                table: "doctors");
        }
    }
}
