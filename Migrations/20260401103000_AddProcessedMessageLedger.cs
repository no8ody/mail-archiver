using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    public partial class AddProcessedMessageLedger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedMessageLedger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MailAccountId = table.Column<int>(type: "integer", nullable: false),
                    NormalizedMessageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalMessageId = table.Column<string>(type: "text", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessageLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessedMessageLedger_MailAccounts_MailAccountId",
                        column: x => x.MailAccountId,
                        principalTable: "MailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessageLedger_MailAccountId_NormalizedMessageKey",
                table: "ProcessedMessageLedger",
                columns: new[] { "MailAccountId", "NormalizedMessageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessageLedger_MailAccountId_FirstSeenAtUtc",
                table: "ProcessedMessageLedger",
                columns: new[] { "MailAccountId", "FirstSeenAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedMessageLedger");
        }
    }
}
