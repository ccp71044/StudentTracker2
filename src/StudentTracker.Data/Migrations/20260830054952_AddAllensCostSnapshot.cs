using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllensCostSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientPrepaidEntitlementTransactions_Allocations_AllocationId",
                table: "ClientPrepaidEntitlementTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ClientPrepaidEntitlementTransactions_AllocationId",
                table: "ClientPrepaidEntitlementTransactions");

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultAllensCost",
                table: "CourseDefinitions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualAllensCost",
                table: "Allocations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AllensCostAtAllocation",
                table: "Allocations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientPrepaidEntitlementTransactionId",
                table: "Allocations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientPrepaidPoolId",
                table: "Allocations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_ClientPrepaidEntitlementTransactionId",
                table: "Allocations",
                column: "ClientPrepaidEntitlementTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_ClientPrepaidPoolId",
                table: "Allocations",
                column: "ClientPrepaidPoolId");

            migrationBuilder.AddForeignKey(
                name: "FK_Allocations_ClientPrepaidEntitlementTransactions_ClientPrepaidEntitlementTransactionId",
                table: "Allocations",
                column: "ClientPrepaidEntitlementTransactionId",
                principalTable: "ClientPrepaidEntitlementTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Allocations_ClientPrepaidPools_ClientPrepaidPoolId",
                table: "Allocations",
                column: "ClientPrepaidPoolId",
                principalTable: "ClientPrepaidPools",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Allocations_ClientPrepaidEntitlementTransactions_ClientPrepaidEntitlementTransactionId",
                table: "Allocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Allocations_ClientPrepaidPools_ClientPrepaidPoolId",
                table: "Allocations");

            migrationBuilder.DropIndex(
                name: "IX_Allocations_ClientPrepaidEntitlementTransactionId",
                table: "Allocations");

            migrationBuilder.DropIndex(
                name: "IX_Allocations_ClientPrepaidPoolId",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "DefaultAllensCost",
                table: "CourseDefinitions");

            migrationBuilder.DropColumn(
                name: "ActualAllensCost",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "AllensCostAtAllocation",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "ClientPrepaidEntitlementTransactionId",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "ClientPrepaidPoolId",
                table: "Allocations");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPrepaidEntitlementTransactions_AllocationId",
                table: "ClientPrepaidEntitlementTransactions",
                column: "AllocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPrepaidEntitlementTransactions_Allocations_AllocationId",
                table: "ClientPrepaidEntitlementTransactions",
                column: "AllocationId",
                principalTable: "Allocations",
                principalColumn: "Id");
        }
    }
}
