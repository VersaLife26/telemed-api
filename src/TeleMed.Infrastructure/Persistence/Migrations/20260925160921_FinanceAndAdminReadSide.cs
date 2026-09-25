using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceAndAdminReadSide : Migration
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

            migrationBuilder.AddColumn<Guid>(
                name: "dispute_id",
                table: "refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "refunds",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "refunds",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "requested_by_admin_id",
                table: "refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                table: "refunds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_admin_id",
                table: "refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "payout_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    opened_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disputes", x => x.id);
                    table.CheckConstraint("CK_disputes_status_Enum", "status IN ('open', 'investigating', 'resolved', 'closed')");
                    table.ForeignKey(
                        name: "fk_disputes_admin_users_assigned_admin_id",
                        column: x => x.assigned_admin_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_admin_users_opened_by_admin_id",
                        column: x => x.opened_by_admin_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_admin_users_resolved_by_admin_id",
                        column: x => x.resolved_by_admin_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disputes_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payout_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payout_batches", x => x.id);
                    table.CheckConstraint("ck_payout_batches_period", "period_start <= period_end");
                    table.CheckConstraint("CK_payout_batches_status_Enum", "status IN ('pending', 'processing', 'paid', 'failed')");
                    table.ForeignKey(
                        name: "fk_payout_batches_admin_users_created_by_admin_id",
                        column: x => x.created_by_admin_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispute_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispute_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_dispute_comments_admin_users_author_admin_id",
                        column: x => x.author_admin_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispute_comments_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<DateOnly>(type: "date", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    payment_count = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    transfer_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    marked_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payouts", x => x.id);
                    table.CheckConstraint("ck_payouts_amounts", "amount_cents >= 0 AND payment_count > 0");
                    table.CheckConstraint("ck_payouts_paid", "status <> 'paid' OR (transfer_reference IS NOT NULL AND paid_at IS NOT NULL)");
                    table.CheckConstraint("CK_payouts_status_Enum", "status IN ('pending', 'paid', 'failed', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_payouts_admin_users_marked_by_admin_id",
                        column: x => x.marked_by_admin_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payouts_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payouts_payout_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "payout_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_refunds_dispute_id",
                table: "refunds",
                column: "dispute_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_requested_by_admin_id",
                table: "refunds",
                column: "requested_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_reviewed_by_admin_id",
                table: "refunds",
                column: "reviewed_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_status_created_at",
                table: "refunds",
                columns: new[] { "status", "created_at" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds",
                sql: "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment', 'reschedule_declined', 'doctor_no_show', 'admin_request', 'dispute')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_status_Enum",
                table: "refunds",
                sql: "status IN ('requested', 'approved', 'processing', 'succeeded', 'failed', 'manual_required', 'rejected')");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payout_id",
                table: "payments",
                column: "payout_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_succeeded_at",
                table: "payments",
                column: "succeeded_at");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_comments_author_admin_id",
                table: "dispute_comments",
                column: "author_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_comments_dispute_id_created_at",
                table: "dispute_comments",
                columns: new[] { "dispute_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_disputes_appointment_id",
                table: "disputes",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_assigned_admin_id",
                table: "disputes",
                column: "assigned_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_doctor_id",
                table: "disputes",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_opened_by_admin_id",
                table: "disputes",
                column: "opened_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_patient_id",
                table: "disputes",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_resolved_by_admin_id",
                table: "disputes",
                column: "resolved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_status_created_at",
                table: "disputes",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payout_batches_created_by_admin_id",
                table: "payout_batches",
                column: "created_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ux_payout_batches_period_end",
                table: "payout_batches",
                column: "period_end",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payouts_batch_id",
                table: "payouts",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_marked_by_admin_id",
                table: "payouts",
                column: "marked_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ux_payouts_doctor_id_period",
                table: "payouts",
                columns: new[] { "doctor_id", "period" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_payments_payouts_payout_id",
                table: "payments",
                column: "payout_id",
                principalTable: "payouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_refunds_admin_users_requested_by_admin_id",
                table: "refunds",
                column: "requested_by_admin_id",
                principalTable: "admin_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_refunds_admin_users_reviewed_by_admin_id",
                table: "refunds",
                column: "reviewed_by_admin_id",
                principalTable: "admin_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_refunds_disputes_dispute_id",
                table: "refunds",
                column: "dispute_id",
                principalTable: "disputes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_payouts_payout_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "fk_refunds_admin_users_requested_by_admin_id",
                table: "refunds");

            migrationBuilder.DropForeignKey(
                name: "fk_refunds_admin_users_reviewed_by_admin_id",
                table: "refunds");

            migrationBuilder.DropForeignKey(
                name: "fk_refunds_disputes_dispute_id",
                table: "refunds");

            migrationBuilder.DropTable(
                name: "dispute_comments");

            migrationBuilder.DropTable(
                name: "payouts");

            migrationBuilder.DropTable(
                name: "disputes");

            migrationBuilder.DropTable(
                name: "payout_batches");

            migrationBuilder.DropIndex(
                name: "ix_refunds_dispute_id",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_refunds_requested_by_admin_id",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_refunds_reviewed_by_admin_id",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_refunds_status_created_at",
                table: "refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_refunds_status_Enum",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_payments_payout_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_succeeded_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "dispute_id",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "note",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "requested_by_admin_id",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "reviewed_by_admin_id",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "payout_id",
                table: "payments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_reason_Enum",
                table: "refunds",
                sql: "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment', 'reschedule_declined', 'doctor_no_show')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_refunds_status_Enum",
                table: "refunds",
                sql: "status IN ('requested', 'approved', 'processing', 'succeeded', 'failed', 'manual_required')");
        }
    }
}
