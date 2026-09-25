using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    is_test = table.Column<bool>(type: "boolean", nullable: false),
                    fee_cents = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    intake = table.Column<string>(type: "jsonb", nullable: false),
                    visit_patient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    visit_patient_date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    visit_patient_sex = table.Column<string>(type: "text", nullable: true),
                    visit_patient_weight_kg = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    visit_patient_allergies = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    payment_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    no_show_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "text", nullable: true),
                    cancelled_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    refund_percent = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.CheckConstraint("CK_appointments_cancelled_by_Enum", "cancelled_by IN ('patient', 'doctor', 'admin', 'system')");
                    table.CheckConstraint("ck_appointments_fee_cents", "fee_cents >= 0");
                    table.CheckConstraint("ck_appointments_range", "end_at > start_at");
                    table.CheckConstraint("ck_appointments_refund_percent", "refund_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_appointments_status_Enum", "status IN ('pending_payment', 'confirmed', 'completed', 'no_show', 'cancelled')");
                    table.CheckConstraint("CK_appointments_visit_patient_sex_Enum", "visit_patient_sex IN ('female', 'male', 'other')");
                    table.CheckConstraint("ck_appointments_visit_patient_weight_kg", "visit_patient_weight_kg > 0");
                    table.ForeignKey(
                        name: "fk_appointments_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promo_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    discount_type = table.Column<string>(type: "text", nullable: false),
                    percent_bps = table.Column<int>(type: "integer", nullable: true),
                    amount_off_cents = table.Column<long>(type: "bigint", nullable: true),
                    max_discount_cents = table.Column<long>(type: "bigint", nullable: true),
                    min_amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    max_redemptions = table.Column<int>(type: "integer", nullable: true),
                    max_per_user = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promo_codes", x => x.id);
                    table.CheckConstraint("ck_promo_codes_code_upper", "code = upper(code)");
                    table.CheckConstraint("ck_promo_codes_discount", "(discount_type = 'percent' AND percent_bps BETWEEN 1 AND 10000 AND amount_off_cents IS NULL) OR (discount_type = 'fixed' AND amount_off_cents > 0 AND percent_bps IS NULL AND max_discount_cents IS NULL)");
                    table.CheckConstraint("CK_promo_codes_discount_type_Enum", "discount_type IN ('percent', 'fixed')");
                    table.CheckConstraint("ck_promo_codes_limits", "min_amount_cents >= 0 AND (max_discount_cents IS NULL OR max_discount_cents > 0) AND (max_redemptions IS NULL OR max_redemptions > 0) AND max_per_user > 0");
                    table.CheckConstraint("ck_promo_codes_validity", "valid_until IS NULL OR valid_until > valid_from");
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    gross_cents = table.Column<long>(type: "bigint", nullable: false),
                    discount_cents = table.Column<long>(type: "bigint", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    captured_cents = table.Column<long>(type: "bigint", nullable: true),
                    commission_cents = table.Column<long>(type: "bigint", nullable: false),
                    provider_fee_cents = table.Column<long>(type: "bigint", nullable: false),
                    payout_cents = table.Column<long>(type: "bigint", nullable: false),
                    refunded_cents = table.Column<long>(type: "bigint", nullable: false),
                    refunded_commission_cents = table.Column<long>(type: "bigint", nullable: false),
                    refunded_provider_fee_cents = table.Column<long>(type: "bigint", nullable: false),
                    refunded_payout_cents = table.Column<long>(type: "bigint", nullable: false),
                    promo_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    authorize_only = table.Column<bool>(type: "boolean", nullable: false),
                    intent_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_payment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    authorization_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    authorized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    succeeded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amounts", "gross_cents > 0 AND discount_cents >= 0 AND amount_cents > 0 AND amount_cents = gross_cents - discount_cents");
                    table.CheckConstraint("ck_payments_captured", "captured_cents IS NULL OR (captured_cents > 0 AND captured_cents <= amount_cents)");
                    table.CheckConstraint("CK_payments_provider_Enum", "provider IN ('payhere', 'mock')");
                    table.CheckConstraint("ck_payments_refunds", "refunded_commission_cents >= 0 AND refunded_provider_fee_cents >= 0 AND refunded_payout_cents >= 0 AND refunded_commission_cents + refunded_provider_fee_cents + refunded_payout_cents = refunded_cents AND refunded_cents <= COALESCE(captured_cents, 0)");
                    table.CheckConstraint("ck_payments_split", "commission_cents >= 0 AND provider_fee_cents >= 0 AND payout_cents >= 0 AND commission_cents + provider_fee_cents + payout_cents = COALESCE(captured_cents, amount_cents)");
                    table.CheckConstraint("CK_payments_status_Enum", "status IN ('pending', 'authorized', 'succeeded', 'failed', 'voided', 'partially_refunded', 'refunded')");
                    table.ForeignKey(
                        name: "fk_payments_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_webhook_events", x => x.id);
                    table.CheckConstraint("CK_payment_webhook_events_provider_Enum", "provider IN ('payhere', 'mock')");
                    table.ForeignKey(
                        name: "fk_payment_webhook_events_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promo_redemptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    promo_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discount_cents = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    release_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promo_redemptions", x => x.id);
                    table.CheckConstraint("ck_promo_redemptions_discount_cents", "discount_cents > 0");
                    table.CheckConstraint("CK_promo_redemptions_status_Enum", "status IN ('reserved', 'consumed', 'released')");
                    table.ForeignKey(
                        name: "fk_promo_redemptions_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_promo_redemptions_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_promo_redemptions_promo_codes_promo_code_id",
                        column: x => x.promo_code_id,
                        principalTable: "promo_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_promo_redemptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    commission_cents = table.Column<long>(type: "bigint", nullable: false),
                    provider_fee_cents = table.Column<long>(type: "bigint", nullable: false),
                    payout_cents = table.Column<long>(type: "bigint", nullable: false),
                    percent = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    provider_refund_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refunds", x => x.id);
                    table.CheckConstraint("ck_refunds_amounts", "amount_cents > 0 AND commission_cents >= 0 AND provider_fee_cents >= 0 AND payout_cents >= 0 AND commission_cents + provider_fee_cents + payout_cents = amount_cents");
                    table.CheckConstraint("ck_refunds_percent", "percent BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_refunds_reason_Enum", "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment')");
                    table.CheckConstraint("CK_refunds_status_Enum", "status IN ('requested', 'approved', 'processing', 'succeeded', 'failed')");
                    table.ForeignKey(
                        name: "fk_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_doctor_id_start_at",
                table: "appointments",
                columns: new[] { "doctor_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_id_start_at",
                table: "appointments",
                columns: new[] { "patient_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_payment_due_at",
                table: "appointments",
                column: "payment_due_at",
                filter: "status = 'pending_payment'");

            migrationBuilder.CreateIndex(
                name: "ix_payment_webhook_events_payment_id",
                table: "payment_webhook_events",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ux_payment_webhook_events_provider_event_id",
                table: "payment_webhook_events",
                columns: new[] { "provider", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_doctor_id",
                table: "payments",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_patient_id_created_at",
                table: "payments",
                columns: new[] { "patient_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_status",
                table: "payments",
                column: "status",
                filter: "status = 'authorized'");

            migrationBuilder.CreateIndex(
                name: "ux_payments_appointment_id",
                table: "payments",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_promo_codes_code",
                table: "promo_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promo_redemptions_appointment_id",
                table: "promo_redemptions",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_promo_redemptions_expires_at",
                table: "promo_redemptions",
                column: "expires_at",
                filter: "status = 'reserved'");

            migrationBuilder.CreateIndex(
                name: "ix_promo_redemptions_promo_code_id_user_id_status",
                table: "promo_redemptions",
                columns: new[] { "promo_code_id", "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_promo_redemptions_user_id",
                table: "promo_redemptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_promo_redemptions_payment_live",
                table: "promo_redemptions",
                column: "payment_id",
                unique: true,
                filter: "status IN ('reserved', 'consumed')");

            // Doctor overlap backs up the per-doctor calendar lock; patient overlap is the only guard across different doctors.
            migrationBuilder.Sql("""
                ALTER TABLE appointments ADD CONSTRAINT ex_appointments_doctor_overlap
                    EXCLUDE USING gist (doctor_id WITH =, tstzrange(start_at, end_at) WITH &&)
                    WHERE (status <> 'cancelled' AND NOT is_test);
                ALTER TABLE appointments ADD CONSTRAINT ex_appointments_patient_overlap
                    EXCLUDE USING gist (patient_id WITH =, tstzrange(start_at, end_at) WITH &&)
                    WHERE (status <> 'cancelled' AND NOT is_test);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_refunds_payment_id",
                table: "refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_status",
                table: "refunds",
                column: "status",
                filter: "status = 'processing'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_webhook_events");

            migrationBuilder.DropTable(
                name: "promo_redemptions");

            migrationBuilder.DropTable(
                name: "refunds");

            migrationBuilder.DropTable(
                name: "promo_codes");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "appointments");
        }
    }
}
