using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentVersionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesDocumentId",
                table: "Documents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupersedesDocumentId",
                table: "Documents");
        }
    }
}
