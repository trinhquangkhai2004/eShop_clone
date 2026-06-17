using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eShop.PaymentProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReconciledAt",
                schema: "payment",
                table: "PaymentTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReconciliationAttempts",
                schema: "payment",
                table: "PaymentTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ResultEventPublished",
                schema: "payment",
                table: "PaymentTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE payment.\"PaymentTransactions\" SET \"ResultEventPublished\" = TRUE WHERE \"Status\" IN ('Succeeded', 'Failed');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReconciledAt",
                schema: "payment",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ReconciliationAttempts",
                schema: "payment",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ResultEventPublished",
                schema: "payment",
                table: "PaymentTransactions");
        }
    }
}
