using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Consultations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds");

            migrationBuilder.CreateTable(
                name: "consultations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    patient_waiting_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    admitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_by_role = table.Column<string>(type: "text", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    doctor_joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    patient_joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    early_join_offered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    early_join_response = table.Column<string>(type: "text", nullable: true),
                    early_join_responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    running_late_notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultations", x => x.id);
                    table.CheckConstraint("ck_consultations_duration_seconds", "duration_seconds >= 0");
                    table.CheckConstraint("CK_consultations_early_join_response_Enum", "early_join_response IN ('accepted', 'declined')");
                    table.CheckConstraint("CK_consultations_ended_by_role_Enum", "ended_by_role IN ('patient', 'doctor')");
                    table.CheckConstraint("CK_consultations_status_Enum", "status IN ('scheduled', 'waiting', 'active', 'ended', 'abandoned')");
                    table.ForeignKey(
                        name: "fk_consultations_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consultation_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    consultation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    actor_role = table.Column<string>(type: "text", nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultation_events", x => x.id);
                    table.CheckConstraint("CK_consultation_events_actor_role_Enum", "actor_role IN ('patient', 'doctor')");
                    table.CheckConstraint("CK_consultation_events_kind_Enum", "kind IN ('joined', 'left', 'admitted', 'ended', 'quality')");
                    table.ForeignKey(
                        name: "fk_consultation_events_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "consultations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consultation_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consultation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_role = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultation_messages", x => x.id);
                    table.CheckConstraint("ck_consultation_messages_body", "char_length(body) BETWEEN 1 AND 4000");
                    table.CheckConstraint("CK_consultation_messages_sender_role_Enum", "sender_role IN ('patient', 'doctor')");
                    table.ForeignKey(
                        name: "fk_consultation_messages_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "consultations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consultation_messages_users_sender_user_id",
                        column: x => x.sender_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds",
                sql: "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment', 'reschedule_declined', 'doctor_no_show')");

            migrationBuilder.CreateIndex(
                name: "ix_consultation_events_consultation_id_kind_id",
                table: "consultation_events",
                columns: new[] { "consultation_id", "kind", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_consultation_messages_consultation_id_created_at",
                table: "consultation_messages",
                columns: new[] { "consultation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_consultation_messages_sender_user_id",
                table: "consultation_messages",
                column: "sender_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_consultations_appointment_id",
                table: "consultations",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consultations_status",
                table: "consultations",
                column: "status",
                filter: "status IN ('waiting', 'active')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultation_events");

            migrationBuilder.DropTable(
                name: "consultation_messages");

            migrationBuilder.DropTable(
                name: "consultations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds",
                sql: "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment', 'reschedule_declined')");
        }
    }
}
