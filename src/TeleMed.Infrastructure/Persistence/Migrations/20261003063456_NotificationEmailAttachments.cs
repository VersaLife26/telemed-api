using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationEmailAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "attachment_content",
                table: "notifications",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachment_file_name",
                table: "notifications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attachment_content",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "attachment_file_name",
                table: "notifications");
        }
    }
}
