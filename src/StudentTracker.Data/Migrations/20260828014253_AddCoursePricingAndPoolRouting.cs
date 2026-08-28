using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursePricingAndPoolRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatchKey",
                table: "CourseDefinitions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CoursePrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CourseDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletionPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoursePrices_CourseDefinitions_CourseDefinitionId",
                        column: x => x.CourseDefinitionId,
                        principalTable: "CourseDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseDefinitions_MatchKey",
                table: "CourseDefinitions",
                column: "MatchKey");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateCreditTransactions_ExternalTransactionId",
                table: "CertificateCreditTransactions",
                column: "ExternalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CoursePrices_CourseDefinitionId_EffectiveFrom",
                table: "CoursePrices",
                columns: new[] { "CourseDefinitionId", "EffectiveFrom" });

            migrationBuilder.Sql("UPDATE FundingSources SET Type = 'OwnerPersonal' WHERE Type = 'AlexPersonal';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoursePrices");

            migrationBuilder.DropIndex(
                name: "IX_CourseDefinitions_MatchKey",
                table: "CourseDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CertificateCreditTransactions_ExternalTransactionId",
                table: "CertificateCreditTransactions");

            migrationBuilder.DropColumn(
                name: "MatchKey",
                table: "CourseDefinitions");
        }
    }
}
