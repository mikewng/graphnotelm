using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace graphnotelm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionToNoteGraphMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "NoteGraphMetadata",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "NoteGraphMetadata");
        }
    }
}
