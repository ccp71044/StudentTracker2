using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Allocations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Allocations");
        }
    }
}
