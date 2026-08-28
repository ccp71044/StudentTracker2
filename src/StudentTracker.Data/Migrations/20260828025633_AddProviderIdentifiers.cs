using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderStudentId",
                table: "Students",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCourseId",
                table: "CourseDeliveries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderStudentId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ProviderCourseId",
                table: "CourseDeliveries");
        }
    }
}
