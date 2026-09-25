using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReschedulesAndAdminAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refunds_status_Enum",
                table: "refunds");

            migrationBuilder.AddColumn<int>(
                name: "capture_attempts",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "capture_failed_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reschedule_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    original_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    proposed_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    proposed_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<string>(type: "text", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reschedule_requests", x => x.id);
                    table.CheckConstraint("CK_reschedule_requests_decided_by_Enum", "decided_by IN ('patient', 'doctor', 'admin', 'system')");
                    table.CheckConstraint("ck_reschedule_requests_original_range", "original_end_at > original_start_at");
                    table.CheckConstraint("ck_reschedule_requests_proposed_range", "proposed_end_at > proposed_start_at");
                    table.CheckConstraint("CK_reschedule_requests_status_Enum", "status IN ('pending', 'accepted', 'declined', 'expired')");
                    table.ForeignKey(
                        name: "fk_reschedule_requests_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reschedule_requests_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reschedule_requests_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reschedule_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds",
                sql: "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment', 'reschedule_declined')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_status_Enum",
                table: "refunds",
                sql: "status IN ('requested', 'approved', 'processing', 'succeeded', 'failed', 'manual_required')");

            migrationBuilder.CreateIndex(
                name: "ix_reschedule_requests_appointment_id",
                table: "reschedule_requests",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_reschedule_requests_doctor_id_proposed_start_at",
                table: "reschedule_requests",
                columns: new[] { "doctor_id", "proposed_start_at" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_reschedule_requests_original_start_at",
                table: "reschedule_requests",
                column: "original_start_at",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_reschedule_requests_patient_id",
                table: "reschedule_requests",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_reschedule_requests_requested_by_user_id",
                table: "reschedule_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reschedule_requests_status_created_at",
                table: "reschedule_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_reschedule_requests_appointment_pending",
                table: "reschedule_requests",
                column: "appointment_id",
                unique: true,
                filter: "status = 'pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reschedule_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refunds_status_Enum",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "capture_attempts",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "capture_failed_at",
                table: "payments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds",
                sql: "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_status_Enum",
                table: "refunds",
                sql: "status IN ('requested', 'approved', 'processing', 'succeeded', 'failed')");
        }
    }
}
