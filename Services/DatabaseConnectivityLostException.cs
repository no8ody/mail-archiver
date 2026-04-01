using System;

namespace MailArchiver.Services.Sync;

/// <summary>
/// Raised when the sync layer has enough evidence that database connectivity is no longer healthy
/// and the current folder or account sync should stop instead of continuing to burn CPU and API calls.
/// </summary>
public sealed class DatabaseConnectivityLostException : Exception
{
    public DatabaseConnectivityLostException(string message)
        : base(message)
    {
    }

    public DatabaseConnectivityLostException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
