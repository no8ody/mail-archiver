using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    public partial class MigrateV2603_3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodySearchText",
                schema: "mail_archiver",
                table: "ArchivedEmails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE mail_archiver.""ArchivedEmails""
SET ""BodySearchText"" = COALESCE(NULLIF(""Body"", ''), regexp_replace(COALESCE(""HtmlBody"", ''), '<[^>]+>', ' ', 'g'), '');");

            migrationBuilder.Sql(@"
DELETE FROM mail_archiver.""ArchivedEmails"" a
USING mail_archiver.""ArchivedEmails"" b
WHERE a.""MailAccountId"" = b.""MailAccountId""
  AND a.""MessageId"" = b.""MessageId""
  AND a.""Id"" < b.""Id"";");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedEmails_MailAccountId_MessageId",
                schema: "mail_archiver",
                table: "ArchivedEmails",
                columns: new[] { "MailAccountId", "MessageId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArchivedEmails_MailAccountId_MessageId",
                schema: "mail_archiver",
                table: "ArchivedEmails");

            migrationBuilder.DropColumn(
                name: "BodySearchText",
                schema: "mail_archiver",
                table: "ArchivedEmails");
        }
    }
}
