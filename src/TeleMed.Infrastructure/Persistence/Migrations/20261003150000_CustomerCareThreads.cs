using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerCareThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_admin_notifications_kind_Enum",
                table: "admin_notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "patient_id",
                table: "disputes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "opened_by_admin_id",
                table: "disputes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "doctor_id",
                table: "disputes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "appointment_id",
                table: "disputes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "disputes",
                type: "text",
                nullable: false,
                defaultValue: "refund");

            migrationBuilder.AddColumn<Guid>(
                name: "opened_by_user_id",
                table: "disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "author_admin_id",
                table: "dispute_comments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "author_user_id",
                table: "dispute_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_disputes_opened_by_user_id",
                table: "disputes",
                column: "opened_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_disputes_category_Enum",
                table: "disputes",
                sql: "category IN ('refund', 'appointment', 'consultation', 'prescription', 'account', 'technical')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_disputes_opener",
                table: "disputes",
                sql: "(opened_by_admin_id IS NULL) <> (opened_by_user_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_disputes_party",
                table: "disputes",
                sql: "patient_id IS NOT NULL OR doctor_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_comments_author_user_id",
                table: "dispute_comments",
                column: "author_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispute_comments_author",
                table: "dispute_comments",
                sql: "(author_admin_id IS NULL) <> (author_user_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_admin_notifications_kind_Enum",
                table: "admin_notifications",
                sql: "kind IN ('doctor_application_submitted', 'doctor_no_show', 'refund_manual_required', 'payment_capture_failed', 'customer_care')");

            migrationBuilder.AddForeignKey(
                name: "fk_dispute_comments_users_author_user_id",
                table: "dispute_comments",
                column: "author_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_disputes_users_opened_by_user_id",
                table: "disputes",
                column: "opened_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dispute_comments_users_author_user_id",
                table: "dispute_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_disputes_users_opened_by_user_id",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "ix_disputes_opened_by_user_id",
                table: "disputes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_disputes_category_Enum",
                table: "disputes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_disputes_opener",
                table: "disputes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_disputes_party",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "ix_dispute_comments_author_user_id",
                table: "dispute_comments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispute_comments_author",
                table: "dispute_comments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_admin_notifications_kind_Enum",
                table: "admin_notifications");

            migrationBuilder.DropColumn(
                name: "category",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "opened_by_user_id",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "author_user_id",
                table: "dispute_comments");

            migrationBuilder.AlterColumn<Guid>(
                name: "patient_id",
                table: "disputes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "opened_by_admin_id",
                table: "disputes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "doctor_id",
                table: "disputes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "appointment_id",
                table: "disputes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "author_admin_id",
                table: "dispute_comments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_admin_notifications_kind_Enum",
                table: "admin_notifications",
                sql: "kind IN ('doctor_application_submitted', 'doctor_no_show', 'refund_manual_required', 'payment_capture_failed')");
        }
    }
}
