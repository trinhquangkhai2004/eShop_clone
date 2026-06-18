using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eShop.PaymentProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFailureReasonAndReconciliationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "payment",
                table: "PaymentTransactions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Status_UpdatedAt",
                schema: "payment",
                table: "PaymentTransactions",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Status_ResultEventPublished",
                schema: "payment",
                table: "PaymentTransactions",
                columns: new[] { "Status", "ResultEventPublished" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_Status_UpdatedAt",
                schema: "payment",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_Status_ResultEventPublished",
                schema: "payment",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "payment",
                table: "PaymentTransactions");
        }
    }
}
