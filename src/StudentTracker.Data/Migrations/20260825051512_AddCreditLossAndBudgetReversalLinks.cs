using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditLossAndBudgetReversalLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCreditLoss",
                table: "CertificateCreditTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesTransactionId",
                table: "BudgetTransactions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCreditLoss",
                table: "CertificateCreditTransactions");

            migrationBuilder.DropColumn(
                name: "ReversesTransactionId",
                table: "BudgetTransactions");
        }
    }
}
