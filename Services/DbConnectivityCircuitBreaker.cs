using System;
using System.Threading;
using System.Threading.Tasks;
using MailArchiver.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MailArchiver.Services.Sync;

/// <summary>
/// Tracks repeated database availability failures during sync and escalates from message-level errors
/// to folder-level aborts and finally to account-level aborts.
/// </summary>
public sealed class DbConnectivityCircuitBreaker
{
    private readonly ILogger _logger;
    private readonly DbExceptionClassifier _classifier;
    private readonly int _maxConsecutiveMessageDbFailures;
    private readonly int _maxDbFailedFolders;

    private int _consecutiveMessageDbFailures;
    private int _failedFolders;

    public DbConnectivityCircuitBreaker(
        ILogger logger,
        DbExceptionClassifier classifier,
        int maxConsecutiveMessageDbFailures = 5,
        int maxDbFailedFolders = 2)
    {
        _logger = logger;
        _classifier = classifier;
        _maxConsecutiveMessageDbFailures = maxConsecutiveMessageDbFailures;
        _maxDbFailedFolders = maxDbFailedFolders;
    }

    public void ResetMessageFailures()
    {
        _consecutiveMessageDbFailures = 0;
    }

    public void RecordSuccessfulFolder()
    {
        _failedFolders = 0;
        _consecutiveMessageDbFailures = 0;
    }

    public void RecordFailedFolder(string accountName, string folderName)
    {
        _failedFolders++;
        _consecutiveMessageDbFailures = 0;

        _logger.LogWarning(
            "Database connectivity failure propagated from folder {FolderName} for account {AccountName}. Failed folders in a row: {FailedFolders}/{Threshold}",
            folderName,
            accountName,
            _failedFolders,
            _maxDbFailedFolders);

        if (_failedFolders >= _maxDbFailedFolders)
        {
            throw new DatabaseConnectivityLostException(
                $"Database still unavailable after {_failedFolders} consecutive folder aborts for account '{accountName}'.");
        }
    }

    public bool TryHandleMessageException(Exception exception, string accountName, string folderName, out DatabaseConnectivityLostException? abortException)
    {
        abortException = null;

        if (!_classifier.IsConnectivityException(exception))
        {
            return false;
        }

        _consecutiveMessageDbFailures++;

        _logger.LogWarning(
            exception,
            "Detected database connectivity failure while syncing folder {FolderName} for account {AccountName}. Consecutive DB message failures: {Count}/{Threshold}",
            folderName,
            accountName,
            _consecutiveMessageDbFailures,
            _maxConsecutiveMessageDbFailures);

        if (_consecutiveMessageDbFailures < _maxConsecutiveMessageDbFailures)
        {
            return true;
        }

        abortException = new DatabaseConnectivityLostException(
            $"Database connectivity lost while syncing folder '{folderName}' for account '{accountName}' after {_consecutiveMessageDbFailures} consecutive DB failures.",
            exception);

        return true;
    }

    public async Task EnsureDatabaseHealthyAsync(
        MailArchiverDbContext context,
        string accountName,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await context.Database.CanConnectAsync(cancellationToken);
            if (!isHealthy)
            {
                throw new DatabaseConnectivityLostException(
                    $"Database health check failed while syncing folder '{folderName}' for account '{accountName}'.");
            }
        }
        catch (DatabaseConnectivityLostException)
        {
            throw;
        }
        catch (Exception ex) when (_classifier.IsConnectivityException(ex))
        {
            throw new DatabaseConnectivityLostException(
                $"Database health check threw a connectivity exception while syncing folder '{folderName}' for account '{accountName}'.",
                ex);
        }
    }
}
