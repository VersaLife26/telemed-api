using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaitingRoomContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "waiting_room_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    link_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    video_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    image_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waiting_room_items", x => x.id);
                    table.CheckConstraint("CK_waiting_room_items_kind_Enum", "kind IN ('article', 'ad')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_waiting_room_items_display_order",
                table: "waiting_room_items",
                column: "display_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waiting_room_items");
        }
    }
}
