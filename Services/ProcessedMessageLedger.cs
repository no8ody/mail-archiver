using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MailArchiver.Data;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.Sync;

/// <summary>
/// Durable provider-agnostic ledger for messages that were already processed once.
/// This survives later deletion of the archived email row itself.
/// </summary>
public sealed class ProcessedMessageLedger : IProcessedMessageLedger
{
    private readonly MailArchiverDbContext _context;

    public ProcessedMessageLedger(MailArchiverDbContext context)
    {
        _context = context;
    }

    public string? BuildNormalizedKey(string? messageId, string? contentHash)
    {
        if (!string.IsNullOrWhiteSpace(messageId))
        {
            var normalizedMessageId = messageId.Trim();
            if (normalizedMessageId.StartsWith("<", StringComparison.Ordinal) && normalizedMessageId.EndsWith(">", StringComparison.Ordinal))
            {
                normalizedMessageId = normalizedMessageId[1..^1];
            }

            normalizedMessageId = normalizedMessageId.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalizedMessageId))
            {
                return $"msg:{normalizedMessageId}";
            }
        }

        if (!string.IsNullOrWhiteSpace(contentHash))
        {
            return $"hash:{contentHash.Trim().ToLowerInvariant()}";
        }

        return null;
    }

    public async Task<bool> HasSeenMessageAsync(
        int mailAccountId,
        string? messageId,
        string? contentHash,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = BuildNormalizedKey(messageId, contentHash);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return false;
        }

        await using var command = await CreateCommandAsync(cancellationToken);
        command.CommandText = @"
SELECT EXISTS (
    SELECT 1
    FROM ""ProcessedMessageLedger""
    WHERE ""MailAccountId"" = @mailAccountId
      AND ""NormalizedMessageKey"" = @normalizedMessageKey
);";

        AddParameter(command, "mailAccountId", DbType.Int32, mailAccountId);
        AddParameter(command, "normalizedMessageKey", DbType.String, normalizedKey);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool b && b;
    }

    public async Task RememberMessageAsync(
        int mailAccountId,
        string? messageId,
        string? contentHash,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = BuildNormalizedKey(messageId, contentHash);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return;
        }

        await using var command = await CreateCommandAsync(cancellationToken);
        command.CommandText = @"
INSERT INTO ""ProcessedMessageLedger""
    (""MailAccountId"", ""NormalizedMessageKey"", ""OriginalMessageId"", ""ContentHash"", ""FirstSeenAtUtc"")
VALUES
    (@mailAccountId, @normalizedMessageKey, @originalMessageId, @contentHash, @firstSeenAtUtc)
ON CONFLICT (""MailAccountId"", ""NormalizedMessageKey"") DO NOTHING;";

        AddParameter(command, "mailAccountId", DbType.Int32, mailAccountId);
        AddParameter(command, "normalizedMessageKey", DbType.String, normalizedKey);
        AddParameter(command, "originalMessageId", DbType.String, (object?)messageId ?? DBNull.Value);
        AddParameter(command, "contentHash", DbType.String, (object?)contentHash ?? DBNull.Value);
        AddParameter(command, "firstSeenAtUtc", DbType.DateTime2, DateTime.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DbCommand> CreateCommandAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var command = connection.CreateCommand();
        var currentTransaction = _context.Database.CurrentTransaction;
        if (currentTransaction is not null)
        {
            command.Transaction = currentTransaction.GetDbTransaction();
        }

        return command;
    }

    private static void AddParameter(DbCommand command, string name, DbType dbType, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
