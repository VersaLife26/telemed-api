using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Clinical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinical_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subjective = table.Column<string>(type: "text", nullable: false),
                    objective = table.Column<string>(type: "text", nullable: false),
                    assessment = table.Column<string>(type: "text", nullable: false),
                    plan = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    finalised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_notes", x => x.id);
                    table.CheckConstraint("ck_clinical_notes_finalised", "(status = 'draft' AND finalised_at IS NULL AND revision = 0) OR (status = 'finalised' AND finalised_at IS NOT NULL AND revision >= 1)");
                    table.CheckConstraint("ck_clinical_notes_sections", "char_length(subjective) <= 20000 AND char_length(objective) <= 20000 AND char_length(assessment) <= 20000 AND char_length(plan) <= 20000");
                    table.CheckConstraint("CK_clinical_notes_status_Enum", "status IN ('draft', 'finalised')");
                    table.ForeignKey(
                        name: "fk_clinical_notes_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clinical_notes_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clinical_notes_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prescriptions",
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
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescriptions", x => x.id);
                    table.CheckConstraint("ck_prescriptions_cancelled", "(status = 'cancelled') = (cancelled_at IS NOT NULL)");
                    table.CheckConstraint("CK_prescriptions_status_Enum", "status IN ('issued', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_prescriptions_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescriptions_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescriptions_users_patient_id",
                        column: x => x.patient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "record_access_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    resource_type = table.Column<string>(type: "text", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_role = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_record_access_logs", x => x.id);
                    table.CheckConstraint("CK_record_access_logs_action_Enum", "action IN ('view', 'download', 'list', 'upload', 'delete', 'verify', 'finalise', 'amend')");
                    table.CheckConstraint("CK_record_access_logs_actor_role_Enum", "actor_role IN ('patient', 'doctor', 'admin', 'anonymous')");
                    table.CheckConstraint("CK_record_access_logs_resource_type_Enum", "resource_type IN ('vault_document', 'prescription', 'clinical_note', 'doctor_document')");
                });

            migrationBuilder.CreateTable(
                name: "vault_folders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vault_folders", x => x.id);
                    table.CheckConstraint("ck_vault_folders_name", "char_length(btrim(name)) BETWEEN 1 AND 120");
                    table.ForeignKey(
                        name: "fk_vault_folders_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_folders_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_folders_vault_folders_parent_id",
                        column: x => x.parent_id,
                        principalTable: "vault_folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clinical_note_diagnoses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    display = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_note_diagnoses", x => x.id);
                    table.ForeignKey(
                        name: "fk_clinical_note_diagnoses_clinical_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "clinical_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_clinical_note_diagnoses_icd10_codes_code",
                        column: x => x.code,
                        principalTable: "icd10_codes",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clinical_note_revisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    subjective = table.Column<string>(type: "text", nullable: false),
                    objective = table.Column<string>(type: "text", nullable: false),
                    assessment = table.Column<string>(type: "text", nullable: false),
                    plan = table.Column<string>(type: "text", nullable: false),
                    diagnoses = table.Column<string>(type: "jsonb", nullable: false),
                    change_type = table.Column<string>(type: "text", nullable: false),
                    amendment_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_note_revisions", x => x.id);
                    table.CheckConstraint("CK_clinical_note_revisions_change_type_Enum", "change_type IN ('finalise', 'amend')");
                    table.CheckConstraint("ck_clinical_note_revisions_reason", "(change_type = 'finalise' AND amendment_reason IS NULL) OR (change_type = 'amend' AND char_length(btrim(amendment_reason)) > 0)");
                    table.CheckConstraint("ck_clinical_note_revisions_revision", "revision >= 1");
                    table.ForeignKey(
                        name: "fk_clinical_note_revisions_clinical_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "clinical_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clinical_note_revisions_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prescription_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    drug_id = table.Column<Guid>(type: "uuid", nullable: true),
                    drug_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    strength = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    form = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dosage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    frequency = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_generic = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescription_items", x => x.id);
                    table.CheckConstraint("ck_prescription_items_duration_days", "duration_days > 0");
                    table.CheckConstraint("ck_prescription_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_prescription_items_drugs_drug_id",
                        column: x => x.drug_id,
                        principalTable: "drugs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescription_items_prescriptions_prescription_id",
                        column: x => x.prescription_id,
                        principalTable: "prescriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vault_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vault_documents", x => x.id);
                    table.CheckConstraint("CK_vault_documents_document_type_Enum", "document_type IN ('report', 'scan', 'prescription', 'other')");
                    table.CheckConstraint("ck_vault_documents_size_bytes", "size_bytes BETWEEN 1 AND 10485760");
                    table.ForeignKey(
                        name: "fk_vault_documents_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_documents_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_documents_vault_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "vault_folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clinical_note_diagnoses_code",
                table: "clinical_note_diagnoses",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_note_diagnoses_note_id_code",
                table: "clinical_note_diagnoses",
                columns: new[] { "note_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_note_revisions_changed_by",
                table: "clinical_note_revisions",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_note_revisions_note_id_revision",
                table: "clinical_note_revisions",
                columns: new[] { "note_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_notes_doctor_id_updated_at",
                table: "clinical_notes",
                columns: new[] { "doctor_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_clinical_notes_patient_id",
                table: "clinical_notes",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ux_clinical_notes_appointment_id",
                table: "clinical_notes",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prescription_items_drug_id",
                table: "prescription_items",
                column: "drug_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescription_items_prescription_id_sort_order",
                table: "prescription_items",
                columns: new[] { "prescription_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prescriptions_doctor_id_issued_at",
                table: "prescriptions",
                columns: new[] { "doctor_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_prescriptions_patient_id_issued_at",
                table: "prescriptions",
                columns: new[] { "patient_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ux_prescriptions_appointment_id",
                table: "prescriptions",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_record_access_logs_actor_id_created_at",
                table: "record_access_logs",
                columns: new[] { "actor_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_record_access_logs_created_at",
                table: "record_access_logs",
                column: "created_at")
                .Annotation("Npgsql:IndexMethod", "brin");

            migrationBuilder.CreateIndex(
                name: "ix_record_access_logs_owner_id_created_at",
                table: "record_access_logs",
                columns: new[] { "owner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_record_access_logs_resource_type_resource_id",
                table: "record_access_logs",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_folder_id",
                table: "vault_documents",
                column: "folder_id",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_owner_id_created_at",
                table: "vault_documents",
                columns: new[] { "owner_id", "created_at" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_uploaded_by",
                table: "vault_documents",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_vault_folders_created_by",
                table: "vault_folders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_vault_folders_owner_id",
                table: "vault_folders",
                column: "owner_id",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vault_folders_parent_id",
                table: "vault_folders",
                column: "parent_id");

            migrationBuilder.Sql("""
                ALTER TABLE clinical_note_diagnoses ADD CONSTRAINT ex_clinical_note_diagnoses_one_primary
                    EXCLUDE USING gist (note_id WITH =) WHERE (is_primary) DEFERRABLE INITIALLY DEFERRED;

                CREATE UNIQUE INDEX ux_vault_folders_sibling_name_live ON vault_folders (owner_id, parent_id, lower(name))
                    NULLS NOT DISTINCT WHERE deleted_at IS NULL;

                CREATE FUNCTION record_rows_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION '% is append-only (% rejected)', TG_TABLE_NAME, TG_OP USING ERRCODE = 'insufficient_privilege';
                END;
                $$;
                CREATE TRIGGER clinical_note_revisions_no_update_delete BEFORE UPDATE OR DELETE ON clinical_note_revisions
                    FOR EACH ROW EXECUTE FUNCTION record_rows_append_only();
                CREATE TRIGGER clinical_note_revisions_no_truncate BEFORE TRUNCATE ON clinical_note_revisions
                    FOR EACH STATEMENT EXECUTE FUNCTION record_rows_append_only();
                CREATE TRIGGER record_access_logs_no_update_delete BEFORE UPDATE OR DELETE ON record_access_logs
                    FOR EACH ROW EXECUTE FUNCTION record_rows_append_only();
                CREATE TRIGGER record_access_logs_no_truncate BEFORE TRUNCATE ON record_access_logs
                    FOR EACH STATEMENT EXECUTE FUNCTION record_rows_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinical_note_diagnoses");

            migrationBuilder.DropTable(
                name: "clinical_note_revisions");

            migrationBuilder.DropTable(
                name: "prescription_items");

            migrationBuilder.DropTable(
                name: "record_access_logs");

            migrationBuilder.DropTable(
                name: "vault_documents");

            migrationBuilder.DropTable(
                name: "clinical_notes");

            migrationBuilder.DropTable(
                name: "prescriptions");

            migrationBuilder.DropTable(
                name: "vault_folders");

            migrationBuilder.Sql("DROP FUNCTION record_rows_append_only();");
        }
    }
}
