using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudentTracker.Data;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StudentTrackerDbContext))]
    [Migration("20260829030000_AddBudgetPoolCategoryAndClientMetadata")]
    public partial class AddBudgetPoolCategoryAndClientMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "BudgetPools",
                type: "TEXT",
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "BudgetPools",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "BudgetPools");

            migrationBuilder.DropColumn(
                name: "ClientName",
                table: "BudgetPools");
        }
    }
}
