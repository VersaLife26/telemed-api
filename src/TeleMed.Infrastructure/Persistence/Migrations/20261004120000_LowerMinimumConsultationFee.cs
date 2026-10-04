using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TeleMed.Infrastructure.Persistence;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261004120000_LowerMinimumConsultationFee")]
    public partial class LowerMinimumConsultationFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_doctors_fee_cents", table: "doctors");
            migrationBuilder.AddCheckConstraint(
                name: "ck_doctors_fee_cents",
                table: "doctors",
                sql: "fee_cents BETWEEN 10000 AND 5000000");

            migrationBuilder.DropCheckConstraint(name: "ck_doctor_applications_fee_cents", table: "doctor_applications");
            migrationBuilder.AddCheckConstraint(
                name: "ck_doctor_applications_fee_cents",
                table: "doctor_applications",
                sql: "fee_cents BETWEEN 10000 AND 5000000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_doctors_fee_cents", table: "doctors");
            migrationBuilder.AddCheckConstraint(
                name: "ck_doctors_fee_cents",
                table: "doctors",
                sql: "fee_cents BETWEEN 50000 AND 5000000");

            migrationBuilder.DropCheckConstraint(name: "ck_doctor_applications_fee_cents", table: "doctor_applications");
            migrationBuilder.AddCheckConstraint(
                name: "ck_doctor_applications_fee_cents",
                table: "doctor_applications",
                sql: "fee_cents BETWEEN 50000 AND 5000000");
        }
    }
}
