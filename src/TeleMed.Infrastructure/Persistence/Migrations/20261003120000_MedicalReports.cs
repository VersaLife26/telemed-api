using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MedicalReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_record_access_logs_resource_type_Enum",
                table: "record_access_logs");

            migrationBuilder.CreateTable(
                name: "medical_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    doctor_slmc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doctor_qualifications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verification_hmac = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_test = table.Column<bool>(type: "boolean", nullable: false),
                    addressee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    clinical_impression = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    findings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    advice = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fitness = table.Column<string>(type: "text", nullable: false),
                    leave_from = table.Column<DateOnly>(type: "date", nullable: true),
                    leave_until = table.Column<DateOnly>(type: "date", nullable: true),
                    return_to_work_on = table.Column<DateOnly>(type: "date", nullable: true),
                    fitness_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medical_reports", x => x.id);
                    table.CheckConstraint("ck_medical_reports_cancelled", "(status = 'cancelled') = (cancelled_at IS NOT NULL)");
                    table.CheckConstraint("CK_medical_reports_fitness_Enum", "fitness IN ('not_assessed', 'fit', 'unfit', 'restricted')");
                    table.CheckConstraint("CK_medical_reports_status_Enum", "status IN ('issued', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_medical_reports_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_reports_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_reports_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_record_access_logs_resource_type_Enum",
                table: "record_access_logs",
                sql: "resource_type IN ('vault_document', 'prescription', 'clinical_note', 'doctor_document', 'medical_report')");

            migrationBuilder.CreateIndex(
                name: "ix_medical_reports_doctor_id_issued_at",
                table: "medical_reports",
                columns: new[] { "doctor_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_medical_reports_patient_id_issued_at",
                table: "medical_reports",
                columns: new[] { "patient_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ux_medical_reports_appointment_id",
                table: "medical_reports",
                column: "appointment_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medical_reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_record_access_logs_resource_type_Enum",
                table: "record_access_logs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_record_access_logs_resource_type_Enum",
                table: "record_access_logs",
                sql: "resource_type IN ('vault_document', 'prescription', 'clinical_note', 'doctor_document')");
        }
    }
}
