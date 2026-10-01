using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PatientNoShowRefundReason : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_refunds_reason_Enum",
            table: "refunds");

        migrationBuilder.AddCheckConstraint(
            name: "CK_refunds_reason_Enum",
            table: "refunds",
            sql: "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment', 'reschedule_declined', 'doctor_no_show', 'patient_no_show', 'admin_request', 'dispute')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_refunds_reason_Enum",
            table: "refunds");

        migrationBuilder.AddCheckConstraint(
            name: "CK_refunds_reason_Enum",
            table: "refunds",
            sql: "reason IN ('patient_cancellation', 'doctor_cancellation', 'admin_cancellation', 'system_cancellation', 'late_payment', 'reschedule_declined', 'doctor_no_show', 'admin_request', 'dispute')");
    }
}
