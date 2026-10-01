using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeleMed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaitingRoomVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "video_storage_key",
                table: "waiting_room_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "video_storage_key",
                table: "waiting_room_items");
        }
    }
}
