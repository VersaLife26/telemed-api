using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TeleMed.Infrastructure.Persistence;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261011133000_DefaultMaxPerDay200")]
    public partial class DefaultMaxPerDay200 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "max_per_day",
                table: "doctors",
                type: "integer",
                nullable: false,
                defaultValue: 200,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 24);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "max_per_day",
                table: "doctors",
                type: "integer",
                nullable: false,
                defaultValue: 24,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 200);
        }
    }
}
