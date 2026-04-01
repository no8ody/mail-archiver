using System.Threading;
using System.Threading.Tasks;

namespace MailArchiver.Services.Sync;

public interface IProcessedMessageLedger
{
    Task<bool> HasSeenMessageAsync(
        int mailAccountId,
        string? messageId,
        string? contentHash,
        CancellationToken cancellationToken = default);

    Task RememberMessageAsync(
        int mailAccountId,
        string? messageId,
        string? contentHash,
        CancellationToken cancellationToken = default);

    string? BuildNormalizedKey(string? messageId, string? contentHash);
}
