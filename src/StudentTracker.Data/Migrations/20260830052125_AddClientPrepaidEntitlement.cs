using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPrepaidEntitlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientPrepaidPools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Client = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    FinancialPeriod = table.Column<string>(type: "TEXT", nullable: true),
                    RestrictedToCourseDefinitionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RestrictedToCourseCategory = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPrepaidPools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidPools_CourseDefinitions_RestrictedToCourseDefinitionId",
                        column: x => x.RestrictedToCourseDefinitionId,
                        principalTable: "CourseDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClientPrepaidEntitlementTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<string>(type: "TEXT", nullable: true),
                    PoolId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LinkedTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TransactionType = table.Column<string>(type: "TEXT", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    MonetaryReferenceValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPrepaidEntitlementTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidEntitlementTransactions_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientPrepaidEntitlementTransactions_ClientPrepaidEntitlementTransactions_LinkedTransactionId",
                        column: x => x.LinkedTransactionId,
                        principalTable: "ClientPrepaidEntitlementTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientPrepaidEntitlementTransactions_ClientPrepaidPools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "ClientPrepaidPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientPrepaidEntitlementTransactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidEntitlementTransactions_AllocationId",
                table: "ClientPrepaidEntitlementTransactions",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidEntitlementTransactions_InvoiceId",
                table: "ClientPrepaidEntitlementTransactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidEntitlementTransactions_LinkedTransactionId",
                table: "ClientPrepaidEntitlementTransactions",
                column: "LinkedTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidEntitlementTransactions_PoolId",
                table: "ClientPrepaidEntitlementTransactions",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidPools_RestrictedToCourseDefinitionId",
                table: "ClientPrepaidPools",
                column: "RestrictedToCourseDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientPrepaidEntitlementTransactions");

            migrationBuilder.DropTable(
                name: "ClientPrepaidPools");
        }
    }
}
