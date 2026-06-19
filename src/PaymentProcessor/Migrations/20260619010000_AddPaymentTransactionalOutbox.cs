using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eShop.PaymentProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactionalOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OutboxTransactionId",
                schema: "payment",
                table: "PaymentTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntegrationEventLog",
                schema: "payment",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTypeName = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    TimesSent = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationEventLog", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_OutboxTransactionId",
                schema: "payment",
                table: "PaymentTransactions",
                column: "OutboxTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationEventLog",
                schema: "payment");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_OutboxTransactionId",
                schema: "payment",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "OutboxTransactionId",
                schema: "payment",
                table: "PaymentTransactions");
        }
    }
}
