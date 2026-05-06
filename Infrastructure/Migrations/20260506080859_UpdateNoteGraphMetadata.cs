using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace graphnotelm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNoteGraphMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoteGraphId",
                table: "NoteGraphMetadata");

            migrationBuilder.RenameColumn(
                name: "isPublic",
                table: "NoteGraphMetadata",
                newName: "IsPublic");

            migrationBuilder.RenameColumn(
                name: "isDeleted",
                table: "NoteGraphMetadata",
                newName: "IsDeleted");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "NoteGraphMetadata",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "NoteGraphMetadata",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "NoteGraphMetadata");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "NoteGraphMetadata");

            migrationBuilder.RenameColumn(
                name: "IsPublic",
                table: "NoteGraphMetadata",
                newName: "isPublic");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "NoteGraphMetadata",
                newName: "isDeleted");

            migrationBuilder.AddColumn<Guid>(
                name: "NoteGraphId",
                table: "NoteGraphMetadata",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
