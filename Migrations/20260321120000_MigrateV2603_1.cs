using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2603_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM mail_archiver.""ArchivedEmails"" a
                USING mail_archiver.""ArchivedEmails"" b
                WHERE a.""Id"" > b.""Id""
                  AND a.""MailAccountId"" = b.""MailAccountId""
                  AND a.""MessageId"" = b.""MessageId"";
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ArchivedEmails_MailAccountId_MessageId_Unique""
                ON mail_archiver.""ArchivedEmails"" (""MailAccountId"", ""MessageId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS mail_archiver.""IX_ArchivedEmails_MailAccountId_MessageId_Unique"";
            ");
        }
    }
}
