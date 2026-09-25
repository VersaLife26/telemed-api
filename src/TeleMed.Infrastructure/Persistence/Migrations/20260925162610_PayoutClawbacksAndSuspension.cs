using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayoutClawbacksAndSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "suspended_with_user",
                table: "doctors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "payout_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    applied_payout_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payout_adjustments", x => x.id);
                    table.CheckConstraint("ck_payout_adjustments_amount", "amount_cents < 0");
                    table.ForeignKey(
                        name: "fk_payout_adjustments_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payout_adjustments_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payout_adjustments_payouts_applied_payout_id",
                        column: x => x.applied_payout_id,
                        principalTable: "payouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payout_adjustments_refunds_refund_id",
                        column: x => x.refund_id,
                        principalTable: "refunds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payout_adjustments_applied_payout_id",
                table: "payout_adjustments",
                column: "applied_payout_id");

            migrationBuilder.CreateIndex(
                name: "ix_payout_adjustments_doctor_id",
                table: "payout_adjustments",
                column: "doctor_id",
                filter: "applied_payout_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payout_adjustments_payment_id",
                table: "payout_adjustments",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ux_payout_adjustments_refund_id",
                table: "payout_adjustments",
                column: "refund_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payout_adjustments");

            migrationBuilder.DropColumn(
                name: "suspended_with_user",
                table: "doctors");
        }
    }
}
