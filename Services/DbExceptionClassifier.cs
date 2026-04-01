using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.Sync;

/// <summary>
/// Classifies exceptions that should be treated as database connectivity or database availability problems.
/// It intentionally combines type-based checks with a conservative message fallback because provider stacks
/// sometimes wrap the original exception inconsistently.
/// </summary>
public sealed class DbExceptionClassifier
{
    private static readonly string[] ConnectivityFragments =
    {
        "connection",
        "timeout",
        "timed out",
        "could not connect",
        "failed to connect",
        "connection reset",
        "connection aborted",
        "connection refused",
        "broken pipe",
        "transport-level error",
        "network-related",
        "network name is no longer available",
        "terminating connection",
        "the connection is closed",
        "connection pool",
        "remaining connection slots",
        "too many clients",
        "server closed the connection unexpectedly",
        "exception while reading from stream",
        "exception while writing to stream",
        "57p01", // admin_shutdown
        "57p02", // crash_shutdown
        "57p03", // cannot_connect_now
        "53300", // too_many_connections
        "08000",
        "08001",
        "08003",
        "08006"
    };

    public bool IsConnectivityException(Exception exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is TimeoutException or IOException or SocketException)
        {
            return true;
        }

        if (exception is DbUpdateException or InvalidOperationException or ObjectDisposedException)
        {
            // Continue with inner-exception and message analysis below.
        }

        var current = exception;
        while (current is not null)
        {
            var typeName = current.GetType().FullName ?? current.GetType().Name;
            if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Postgres", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("DbException", StringComparison.OrdinalIgnoreCase))
            {
                var message = current.Message ?? string.Empty;
                if (ConnectivityFragments.Any(fragment => message.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            current = current.InnerException;
        }

        return FlattenMessages(exception)
            .Any(message => ConnectivityFragments.Any(fragment => message.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> FlattenMessages(Exception exception)
    {
        var current = exception;
        while (current is not null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                yield return current.Message;
            }

            current = current.InnerException;
        }
    }
}
