using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DoctorsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doctor_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slmc_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    specialty_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    languages = table.Column<string[]>(type: "text[]", nullable: false),
                    language_other = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    experience_years = table.Column<int>(type: "integer", nullable: false),
                    fee_cents = table.Column<long>(type: "bigint", nullable: false),
                    bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    pgim_board_certified = table.Column<bool>(type: "boolean", nullable: false),
                    is_general_practitioner = table.Column<bool>(type: "boolean", nullable: false),
                    medical_school = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    qualifications_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    availability_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    practicing_locations = table.Column<List<string>>(type: "text[]", nullable: false),
                    terms_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bank_branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bank_account_encrypted = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    upload_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    checklist = table.Column<string>(type: "jsonb", nullable: false),
                    review_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_applications", x => x.id);
                    table.CheckConstraint("ck_doctor_applications_email_lower", "email = lower(email)");
                    table.CheckConstraint("ck_doctor_applications_experience_years", "experience_years >= 0");
                    table.CheckConstraint("ck_doctor_applications_fee_cents", "fee_cents BETWEEN 50000 AND 5000000");
                    table.CheckConstraint("ck_doctor_applications_languages", "cardinality(languages) > 0 AND languages <@ ARRAY['en', 'si', 'ta', 'other']::text[]");
                    table.CheckConstraint("CK_doctor_applications_status_Enum", "status IN ('pending', 'under_review', 'approved', 'rejected')");
                    table.ForeignKey(
                        name: "fk_doctor_applications_specialties_specialty_code",
                        column: x => x.specialty_code,
                        principalTable: "specialties",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doctors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    slmc_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    specialty_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sub_specialties = table.Column<List<string>>(type: "text[]", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    languages = table.Column<string[]>(type: "text[]", nullable: false),
                    language_other = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    qualifications = table.Column<string>(type: "jsonb", nullable: false),
                    experience_years = table.Column<int>(type: "integer", nullable: false),
                    fee_cents = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    accepts_new_patients = table.Column<bool>(type: "boolean", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bank_branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bank_account_encrypted = table.Column<string>(type: "text", nullable: false),
                    photo_storage_key = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    suspended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    suspended_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    suspended_by = table.Column<Guid>(type: "uuid", nullable: true),
                    slot_duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    buffer_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    max_per_day = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    advance_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Asia/Colombo"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(to_tsvector('english', display_name), 'A') || setweight(to_tsvector('english', replace(specialty_code, '_', ' ')), 'B') || setweight(to_tsvector('english', bio), 'C')", stored: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctors", x => x.id);
                    table.CheckConstraint("ck_doctors_experience_years", "experience_years >= 0");
                    table.CheckConstraint("ck_doctors_fee_cents", "fee_cents BETWEEN 50000 AND 5000000");
                    table.CheckConstraint("ck_doctors_languages", "cardinality(languages) > 0 AND languages <@ ARRAY['en', 'si', 'ta', 'other']::text[]");
                    table.CheckConstraint("ck_doctors_schedule", "slot_duration_minutes > 0 AND buffer_minutes >= 0 AND max_per_day > 0 AND advance_days > 0");
                    table.CheckConstraint("CK_doctors_status_Enum", "status IN ('active', 'suspended')");
                    table.ForeignKey(
                        name: "fk_doctors_doctor_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "doctor_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doctors_specialties_specialty_code",
                        column: x => x.specialty_code,
                        principalTable: "specialties",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doctors_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doctor_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_documents", x => x.id);
                    table.CheckConstraint("ck_doctor_documents_owner", "application_id IS NOT NULL OR doctor_id IS NOT NULL");
                    table.CheckConstraint("CK_doctor_documents_type_Enum", "type IN ('slmc_certificate', 'nic', 'degree_certificate', 'specialty_board_certificate', 'signature', 'seal', 'other')");
                    table.ForeignKey(
                        name: "fk_doctor_documents_doctor_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "doctor_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doctor_documents_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_doctor_applications_doctor_id",
                table: "doctor_applications",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "ix_doctor_applications_specialty_code",
                table: "doctor_applications",
                column: "specialty_code");

            migrationBuilder.CreateIndex(
                name: "ix_doctor_applications_status_created_at",
                table: "doctor_applications",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_doctor_applications_phone_open",
                table: "doctor_applications",
                column: "phone",
                unique: true,
                filter: "status IN ('pending', 'under_review')");

            migrationBuilder.CreateIndex(
                name: "ux_doctor_applications_slmc_number_open",
                table: "doctor_applications",
                column: "slmc_number",
                unique: true,
                filter: "status IN ('pending', 'under_review')");

            migrationBuilder.CreateIndex(
                name: "ix_doctor_documents_doctor_id",
                table: "doctor_documents",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "ux_doctor_documents_application_type_live",
                table: "doctor_documents",
                columns: new[] { "application_id", "type" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_doctor_documents_doctor_stamp_live",
                table: "doctor_documents",
                columns: new[] { "doctor_id", "type" },
                unique: true,
                filter: "deleted_at IS NULL AND type IN ('signature', 'seal')");

            migrationBuilder.CreateIndex(
                name: "ix_doctors_languages",
                table: "doctors",
                column: "languages")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_doctors_search_vector",
                table: "doctors",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_doctors_specialty_code",
                table: "doctors",
                column: "specialty_code");

            migrationBuilder.CreateIndex(
                name: "ix_doctors_status_specialty_code",
                table: "doctors",
                columns: new[] { "status", "specialty_code" });

            migrationBuilder.CreateIndex(
                name: "ux_doctors_application_id",
                table: "doctors",
                column: "application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_doctors_slmc_number",
                table: "doctors",
                column: "slmc_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_doctors_user_id",
                table: "doctors",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_doctor_applications_doctors_doctor_id",
                table: "doctor_applications",
                column: "doctor_id",
                principalTable: "doctors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_doctor_applications_doctors_doctor_id",
                table: "doctor_applications");

            migrationBuilder.DropTable(
                name: "doctor_documents");

            migrationBuilder.DropTable(
                name: "doctors");

            migrationBuilder.DropTable(
                name: "doctor_applications");
        }
    }
}
