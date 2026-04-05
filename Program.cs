using MailArchiver.Auth.Extensions;
using MailArchiver.Auth.Options;
using MailArchiver.Auth.Services;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Services.Providers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Threading.RateLimiting;
using System.Net;
using IPNetwork = System.Net.IPNetwork;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Helper method to parse SameSite mode from string
static SameSiteMode ParseSameSiteMode(string? value)
{
    return value?.ToLowerInvariant() switch
    {
        "strict" => SameSiteMode.Strict,
        "none" => SameSiteMode.None,
        _ => SameSiteMode.Lax // Default to Lax for better cross-site navigation support
    };
}

static CookieSecurePolicy ParseCookieSecurePolicy(string? value, CookieSecurePolicy defaultPolicy = CookieSecurePolicy.SameAsRequest)
{
    return value?.ToLowerInvariant() switch
    {
        "always" => CookieSecurePolicy.Always,
        "none" => CookieSecurePolicy.None,
        "sameasrequest" => CookieSecurePolicy.SameAsRequest,
        _ => defaultPolicy
    };
}

static void ApplyFriendlyEnvironmentAliases(ConfigurationManager configuration, string contentRootPath)
{
    var aliasMap = BuildFriendlyEnvironmentAliasMap(contentRootPath);
    if (aliasMap.Count == 0)
    {
        return;
    }

    var environmentVariables = Environment.GetEnvironmentVariables();
    if (environmentVariables.Count == 0)
    {
        return;
    }

    var aliasesByLength = aliasMap.Keys
        .OrderByDescending(alias => alias.Length)
        .ToArray();

    var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    foreach (DictionaryEntry entry in environmentVariables)
    {
        var environmentKey = entry.Key?.ToString();
        var environmentValue = entry.Value?.ToString();

        if (string.IsNullOrWhiteSpace(environmentKey) || environmentValue is null)
        {
            continue;
        }

        if (environmentKey.Contains("__", StringComparison.Ordinal))
        {
            continue;
        }

        if (aliasMap.TryGetValue(environmentKey, out var directTargets))
        {
            foreach (var target in directTargets)
            {
                if (!HasCanonicalEnvironmentOverride(environmentVariables, target))
                {
                    overrides[target] = environmentValue;
                }
            }

            continue;
        }

        foreach (var alias in aliasesByLength)
        {
            if (!environmentKey.StartsWith(alias + "_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!aliasMap.TryGetValue(alias, out var prefixTargets))
            {
                continue;
            }

            var remainder = environmentKey[(alias.Length + 1)..];
            if (string.IsNullOrWhiteSpace(remainder))
            {
                continue;
            }

            var normalizedRemainder = NormalizeAliasRemainder(remainder);
            foreach (var target in prefixTargets)
            {
                var expandedTarget = $"{target}:{normalizedRemainder}";
                if (!HasCanonicalEnvironmentOverride(environmentVariables, expandedTarget))
                {
                    overrides[expandedTarget] = environmentValue;
                }
            }

            break;
        }
    }

    if (overrides.Count > 0)
    {
        configuration.AddInMemoryCollection(overrides);
    }
}

static Dictionary<string, List<string>> BuildFriendlyEnvironmentAliasMap(string contentRootPath)
{
    var canonicalPaths = GetKnownConfigurationPaths(contentRootPath);
    var aliases = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    foreach (var canonicalPath in canonicalPaths)
    {
        AddFriendlyAlias(aliases, BuildCompactAlias(canonicalPath), canonicalPath);
        AddFriendlyAlias(aliases, BuildReadableAlias(canonicalPath), canonicalPath);
        AddFriendlyAlias(aliases, BuildHybridAlias(canonicalPath), canonicalPath);
    }

    AddFriendlyAlias(aliases, "TZ", "TimeZone:DisplayTimeZoneId");
    AddFriendlyAlias(aliases, "ENCRYPTION_KEY", "Encryption:Key");
    AddFriendlyAlias(aliases, "DATABASE_URL", "ConnectionStrings:DefaultConnection");
    AddFriendlyAlias(aliases, "MAILARCHIVE_URL", "Authentication:PublicOrigin");
    AddFriendlyAlias(aliases, "MAILARCHIVE_URL", "Application:PublicOrigin");
    AddFriendlyAlias(aliases, "MAILARCHIVE_URL", "HostFiltering:AdditionalAllowedHosts");

    return aliases.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
        StringComparer.OrdinalIgnoreCase);
}

static HashSet<string> GetKnownConfigurationPaths(string contentRootPath)
{
    var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AllowedHosts",
        "Application:PublicOrigin",
        "Authentication:PublicOrigin",
        "Authentication:CookieSecurePolicy",
        "ConnectionStrings:DefaultConnection",
        "DataProtection:KeyPath",
        "Encryption:Key",
        "HostFiltering:AdditionalAllowedHosts",
        "HostFiltering:AllowedHosts",
        "Npgsql:CommandTimeout",
        "Refresh:IntervalMinutes",
        "ReverseProxy:ForwardLimit",
        "ReverseProxy:KnownNetworks",
        "ReverseProxy:KnownProxies",
        "ReverseProxy:RequireHeaderSymmetry"
    };

    AddOptionPaths<AuthenticationOptions>(knownPaths, AuthenticationOptions.Authentication);
    AddOptionPaths<OAuthOptions>(knownPaths, OAuthOptions.OAuth);
    AddOptionPaths<MailSyncOptions>(knownPaths, MailSyncOptions.MailSync);
    AddOptionPaths<BatchRestoreOptions>(knownPaths, BatchRestoreOptions.BatchRestore);
    AddOptionPaths<BatchOperationOptions>(knownPaths, BatchOperationOptions.BatchOperation);
    AddOptionPaths<SelectionOptions>(knownPaths, "Selection");
    AddOptionPaths<ViewOptions>(knownPaths, "View");
    AddOptionPaths<TimeZoneOptions>(knownPaths, "TimeZone");
    AddOptionPaths<RefreshOptions>(knownPaths, RefreshOptions.Refresh);
    AddOptionPaths<UploadOptions>(knownPaths, UploadOptions.Upload);
    AddOptionPaths<BandwidthTrackingOptions>(knownPaths, BandwidthTrackingOptions.BandwidthTracking);

    try
    {
        var appSettingsPath = Path.Combine(contentRootPath, "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            return knownPaths;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        CollectConfigurationPaths(document.RootElement, null, knownPaths);
    }
    catch
    {
        // Ignore malformed or unavailable appsettings.json and fall back to the built-in path list.
    }

    return knownPaths;
}

static void CollectConfigurationPaths(JsonElement element, string? currentPath, ISet<string> collector)
{
    if (!string.IsNullOrWhiteSpace(currentPath))
    {
        collector.Add(currentPath);
    }

    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            foreach (var property in element.EnumerateObject())
            {
                var nextPath = string.IsNullOrWhiteSpace(currentPath)
                    ? property.Name
                    : $"{currentPath}:{property.Name}";

                CollectConfigurationPaths(property.Value, nextPath, collector);
            }
            break;

        case JsonValueKind.Array:
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var nextPath = string.IsNullOrWhiteSpace(currentPath)
                    ? index.ToString()
                    : $"{currentPath}:{index}";

                CollectConfigurationPaths(item, nextPath, collector);
                index++;
            }
            break;
    }
}

static void AddOptionPaths<TOptions>(ISet<string> collector, string sectionName)
{
    CollectTypePaths(typeof(TOptions), sectionName, collector);
}

static void CollectTypePaths(Type type, string currentPath, ISet<string> collector)
{
    if (string.IsNullOrWhiteSpace(currentPath))
    {
        return;
    }

    collector.Add(currentPath);

    foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
    {
        if (property.GetMethod is null || property.GetIndexParameters().Length > 0)
        {
            continue;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var propertyPath = $"{currentPath}:{property.Name}";
        collector.Add(propertyPath);

        if (propertyType == typeof(string) ||
            propertyType.IsPrimitive ||
            propertyType.IsEnum ||
            propertyType == typeof(decimal) ||
            propertyType == typeof(DateTime) ||
            propertyType == typeof(DateTimeOffset) ||
            propertyType == typeof(TimeSpan) ||
            propertyType == typeof(Guid) ||
            propertyType.IsArray)
        {
            continue;
        }

        CollectTypePaths(propertyType, propertyPath, collector);
    }
}

static void AddFriendlyAlias(IDictionary<string, HashSet<string>> aliases, string alias, string canonicalPath)
{
    if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(canonicalPath))
    {
        return;
    }

    if (!aliases.TryGetValue(alias, out var values))
    {
        values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        aliases[alias] = values;
    }

    values.Add(canonicalPath);
}

static string BuildCompactAlias(string canonicalPath)
{
    var parts = canonicalPath
        .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(NormalizeAliasSegmentCompact)
        .Where(part => !string.IsNullOrWhiteSpace(part));

    return string.Join("_", parts);
}

static string BuildReadableAlias(string canonicalPath)
{
    var parts = canonicalPath
        .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(NormalizeAliasSegmentReadable)
        .Where(part => !string.IsNullOrWhiteSpace(part));

    return string.Join("_", parts);
}

static string BuildHybridAlias(string canonicalPath)
{
    var parts = canonicalPath
        .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(NormalizeAliasSegmentHybrid)
        .Where(part => !string.IsNullOrWhiteSpace(part));

    return string.Join("_", parts);
}

static string NormalizeAliasSegmentCompact(string segment)
{
    return string.Concat(segment.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}

static string NormalizeAliasSegmentReadable(string segment)
{
    var sanitized = Regex.Replace(segment, @"[^A-Za-z0-9]+", " ");
    if (string.IsNullOrWhiteSpace(sanitized))
    {
        return string.Empty;
    }

    var readableTokens = new List<string>();
    foreach (var token in sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        foreach (Match match in Regex.Matches(token, @"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+"))
        {
            readableTokens.Add(match.Value.ToUpperInvariant());
        }
    }

    return readableTokens.Count == 0
        ? NormalizeAliasSegmentCompact(segment)
        : string.Join("_", readableTokens);
}

static string NormalizeAliasSegmentHybrid(string segment)
{
    if (segment.Any(character => !char.IsLetterOrDigit(character)))
    {
        var punctuationTokens = Regex.Split(segment, @"[^A-Za-z0-9]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.ToUpperInvariant());

        return string.Join("_", punctuationTokens);
    }

    return NormalizeAliasSegmentReadable(segment);
}

static string NormalizeAliasRemainder(string remainder)
{
    return string.Join(":", remainder
        .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

static bool HasCanonicalEnvironmentOverride(IDictionary environmentVariables, string canonicalPath)
{
    var environmentKey = canonicalPath.Replace(':', '_').Replace("_", "__");
    foreach (DictionaryEntry entry in environmentVariables)
    {
        var existingKey = entry.Key?.ToString();
        if (string.IsNullOrWhiteSpace(existingKey))
        {
            continue;
        }

        if (string.Equals(existingKey, environmentKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static IReadOnlyCollection<string> BuildAllowedHosts(IConfiguration configuration)
{
    var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "[::1]"
    };

    AddConfiguredHosts(hosts, configuration["AllowedHosts"]);
    AddConfiguredHosts(hosts, configuration["HostFiltering:AllowedHosts"]);
    AddOriginHost(hosts, configuration["Authentication:PublicOrigin"]);
    AddOriginHost(hosts, configuration["Application:PublicOrigin"]);
    AddConfiguredHosts(hosts, configuration["HostFiltering:AdditionalAllowedHosts"]);

    return hosts.ToArray();
}

static void AddOriginHost(ISet<string> hosts, string? originValue)
{
    if (string.IsNullOrWhiteSpace(originValue))
    {
        return;
    }

    if (!Uri.TryCreate(originValue, UriKind.Absolute, out var originUri) ||
        string.IsNullOrWhiteSpace(originUri.Host))
    {
        return;
    }

    AddNormalizedHost(hosts, originUri.Host);
}

static void AddConfiguredHosts(ISet<string> hosts, string? configuredHosts)
{
    if (string.IsNullOrWhiteSpace(configuredHosts))
    {
        return;
    }

    foreach (var rawHost in configuredHosts.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        AddNormalizedHost(hosts, rawHost);
    }
}

static void AddNormalizedHost(ISet<string> hosts, string? hostValue)
{
    if (string.IsNullOrWhiteSpace(hostValue))
    {
        return;
    }

    var host = hostValue.Trim();
    if (host == "*")
    {
        return;
    }

    if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        if (!Uri.TryCreate(host, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return;
        }

        host = uri.Host;
    }

    if (host.Length >= 2 && host[0] == '[' && host[^1] == ']')
    {
        hosts.Add(host);
        return;
    }

    if (IPAddress.TryParse(host, out var parsedAddress) && parsedAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
    {
        hosts.Add($"[{parsedAddress}]");
        return;
    }

    hosts.Add(host);
}

static void ConfigureAllowedHosts(HostFilteringOptions options, IConfiguration configuration)
{
    options.AllowedHosts = BuildAllowedHosts(configuration).ToList();
}

static string ResolveWritableDirectoryPath(string preferredPath, string fallbackPath)
{
    if (TryEnsureWritableDirectory(preferredPath, out var resolvedPreferredPath))
    {
        return resolvedPreferredPath;
    }

    if (TryEnsureWritableDirectory(fallbackPath, out var resolvedFallbackPath))
    {
        return resolvedFallbackPath;
    }

    return resolvedPreferredPath ?? resolvedFallbackPath ?? preferredPath;
}

static bool TryEnsureWritableDirectory(string? path, out string? normalizedPath)
{
    normalizedPath = null;
    if (string.IsNullOrWhiteSpace(path))
    {
        return false;
    }

    try
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);

        var probePath = Path.Combine(fullPath, $".write-test-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probePath, "ok");
        File.Delete(probePath);

        normalizedPath = fullPath;
        return true;
    }
    catch
    {
        normalizedPath = path;
        return false;
    }
}

static void AddDefaultTrustedProxyNetworks(ICollection<IPNetwork> knownNetworks)
{
    foreach (var value in new[]
    {
        "127.0.0.0/8",
        "::1/128",
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "169.254.0.0/16",
        "100.64.0.0/10",
        "fc00::/7",
        "fe80::/10"
    })
    {
        if (TryParseIpNetwork(value, out var network) && !knownNetworks.Contains(network))
        {
            knownNetworks.Add(network);
        }
    }
}

static void ConfigureTrustedForwarders(ForwardedHeadersOptions options, IConfiguration configuration)
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedHost |
                               ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = Math.Max(1, configuration.GetValue<int?>("ReverseProxy:ForwardLimit") ?? 1);
    options.RequireHeaderSymmetry = configuration.GetValue("ReverseProxy:RequireHeaderSymmetry", true);

    var configuredProxyCount = 0;
    var proxyValues = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>();
    foreach (var proxyValue in proxyValues)
    {
        if (IPAddress.TryParse(proxyValue, out var proxyAddress))
        {
            options.KnownProxies.Add(proxyAddress);
            configuredProxyCount++;
        }
    }

    var configuredNetworkCount = 0;
    var networkValues = configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>();
    foreach (var networkValue in networkValues)
    {
        if (!TryParseIpNetwork(networkValue, out var network))
        {
            continue;
        }

        options.KnownIPNetworks.Add(network);
        configuredNetworkCount++;
    }

    if (configuredProxyCount == 0 && configuredNetworkCount == 0)
    {
        AddDefaultTrustedProxyNetworks(options.KnownIPNetworks);
    }
}

static bool TryParseIpNetwork(string? value, out IPNetwork network)
{
    network = default!;
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 1 && IPAddress.TryParse(parts[0], out var singleAddress))
    {
        var prefixLength = singleAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        network = new IPNetwork(singleAddress, prefixLength);
        return true;
    }

    if (parts.Length == 2 &&
        IPAddress.TryParse(parts[0], out var address) &&
        int.TryParse(parts[1], out var parsedPrefix))
    {
        var maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (parsedPrefix >= 0 && parsedPrefix <= maxPrefix)
        {
            network = new IPNetwork(address, parsedPrefix);
            return true;
        }
    }

    return false;
}

// Helper method to ensure __EFMigrationsHistory table exists
async static Task EnsureMigrationsHistoryTableExists(MailArchiverDbContext context, IServiceProvider services)
{
    var connection = context.Database.GetDbConnection();
    
    // Check if connection is already open
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }
    
    var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT EXISTS (
            SELECT 1 
            FROM information_schema.tables 
            WHERE table_name = '__EFMigrationsHistory'
        );";
    
    var result = await command.ExecuteScalarAsync();
    var tableExists = result != null && (bool)result;
    
    if (!tableExists)
    {
        // Create the migrations history table if it doesn't exist
        var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" character varying(150) NOT NULL,
                ""ProductVersion"" character varying(32) NOT NULL,
                CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
            );";
        await createTableCommand.ExecuteNonQueryAsync();
        
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("__EFMigrationsHistory table created");
    }
}

var builder = WebApplication.CreateBuilder(args);
ApplyFriendlyEnvironmentAliases(builder.Configuration, builder.Environment.ContentRootPath);

MailArchiver.Utilities.EmailEncryption.Configure(builder.Configuration);

try
{
    MailArchiver.Utilities.EmailEncryption.EnsureConfigured();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[Startup] Encryption configuration error: {ex.Message}");
    Environment.Exit(1);
}

// Configure Forwarded Headers for reverse proxy support
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    ConfigureTrustedForwarders(options, builder.Configuration);
});

builder.Services.PostConfigure<HostFilteringOptions>(options =>
{
    ConfigureAllowedHosts(options, builder.Configuration);
});

// Check if authentication is explicitly disabled in appsettings.json
var authEnabled = builder.Configuration.GetSection("Authentication:Enabled").Value;
if (authEnabled != null && authEnabled.Equals("false", StringComparison.OrdinalIgnoreCase))
{
    // Create a logger to log the error message
    var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    logger.LogError("Authentication is now mandatory and must be enabled. Please remove the 'Enabled' property from the 'Authentication' section in appsettings.json or set it to 'true' and define admin credentials to access the application.");
    logger.LogError("For more information, please refer to the documentation ( https://github.com/s1t5/mail-archiver/blob/main/doc/Setup.md ) on how to set up username and password using environment variables.");
    Environment.Exit(1);
}

// Check if authentication password is set and not empty
var authPassword = builder.Configuration.GetSection("Authentication:Password").Value;
if (string.IsNullOrWhiteSpace(authPassword))
{
    // Create a logger to log the error message
    var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    logger.LogError("Authentication password must be set and cannot be empty. Please define a valid password in the 'Authentication' section in appsettings.json or using environment variables.");
    logger.LogError("For more information, please refer to the documentation ( https://github.com/s1t5/mail-archiver/blob/main/doc/Setup.md ) on how to set up username and password using environment variables.");
    Environment.Exit(1);
}

// Add Authentication Options
builder.Services.Configure<AuthenticationOptions>(
    builder.Configuration.GetSection(AuthenticationOptions.Authentication));

// Add OAuth Options
builder.Services.Configure<OAuthOptions>(
    builder.Configuration.GetSection(OAuthOptions.OAuth));

// Add Batch Restore Options
builder.Services.Configure<BatchRestoreOptions>(
    builder.Configuration.GetSection(BatchRestoreOptions.BatchRestore));

// Add Batch Operation Options
builder.Services.Configure<BatchOperationOptions>(
    builder.Configuration.GetSection(BatchOperationOptions.BatchOperation));

// Add Mail Sync Options
builder.Services.Configure<MailSyncOptions>(
    builder.Configuration.GetSection(MailSyncOptions.MailSync));

// Add Upload Options
builder.Services.Configure<UploadOptions>(
    builder.Configuration.GetSection(UploadOptions.Upload));

// Add Selection Options
builder.Services.Configure<SelectionOptions>(
    builder.Configuration.GetSection("Selection"));

// Add View Options
builder.Services.Configure<ViewOptions>(
    builder.Configuration.GetSection("View"));

// Add TimeZone Options
builder.Services.Configure<TimeZoneOptions>(
    builder.Configuration.GetSection("TimeZone"));

// Add Bandwidth Tracking Options
builder.Services.Configure<BandwidthTrackingOptions>(
    builder.Configuration.GetSection(BandwidthTrackingOptions.BandwidthTracking));

// Add Refresh Options
builder.Services.Configure<RefreshOptions>(
    builder.Configuration.GetSection(RefreshOptions.Refresh));

// Add DateTimeHelper
builder.Services.AddScoped<MailArchiver.Utilities.DateTimeHelper>();

// Add Session support
builder.Services.AddDistributedMemoryCache();

// Get authentication options for SameSite and secure-cookie configuration
var authOptionsConfig = builder.Configuration.GetSection(AuthenticationOptions.Authentication).Get<AuthenticationOptions>() ?? new AuthenticationOptions();
var cookieSameSiteMode = ParseSameSiteMode(authOptionsConfig.CookieSameSite);
var cookieSecurePolicy = ParseCookieSecurePolicy(builder.Configuration["Authentication:CookieSecurePolicy"], CookieSecurePolicy.SameAsRequest);

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = cookieSameSiteMode;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

// Configure Anti-forgery (CSRF) cookies with same SameSite policy
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = cookieSameSiteMode;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

// Add Data Protection with persistent key storage
var configuredDataProtectionPath = builder.Configuration.GetValue<string>("DataProtection:KeyPath") ?? "/app/DataProtection-Keys";
var dataProtectionPath = ResolveWritableDirectoryPath(configuredDataProtectionPath, "/tmp/MailArchiver-DataProtection-Keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("MailArchiver");

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Login Attempt Rate Limiting: 5 attempts per 10 minutes per IP
    options.AddPolicy("LoginAttempts", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var username = string.Empty;

        if (HttpMethods.IsPost(httpContext.Request.Method) && httpContext.Request.HasFormContentType)
        {
            try
            {
                username = (httpContext.Request.Form["Username"].FirstOrDefault() ??
                            httpContext.Request.Form["username"].FirstOrDefault() ??
                            string.Empty)
                    .Trim()
                    .ToLowerInvariant();
            }
            catch
            {
                username = string.Empty;
            }
        }

        var partitionKey = string.IsNullOrEmpty(username)
            ? $"login-{clientIp}"
            : $"login-{clientIp}-{username}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
    
    // 2FA Verification Rate Limiting: 5 attempts per 15 minutes per IP/User
    options.AddPolicy("TwoFactorVerify", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var username = httpContext.Session.GetString("TwoFactorUsername") ?? "anonymous";
        var partitionKey = $"2fa-{clientIp}-{username}";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
    
    // Global Rate Limiting: 100 requests per minute per IP for other endpoints
    options.AddPolicy("Global", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            clientIp,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
    
    // Rejection response
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        
        if (context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
        {
            var retryAfterSeconds = retryAfter is TimeSpan ts ? ts.TotalSeconds : 0;
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        }
        
        // Redirect to blocked page for login and 2FA endpoints
        var path = context.HttpContext.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.Contains("/auth/login") || path.Contains("/twofactor/verify"))
        {
            context.HttpContext.Response.Redirect("/Auth/Blocked");
        }
        else
        {
            // Get localizer for rate limit message
            var serviceProvider = context.HttpContext.RequestServices;
            var localizer = serviceProvider.GetService<Microsoft.Extensions.Localization.IStringLocalizer<MailArchiver.SharedResource>>();
            var message = localizer?["RateLimitExceeded"] ?? "Rate limit exceeded. Please try again later.";
            
            await context.HttpContext.Response.WriteAsync(message, cancellationToken: token);
        }
    };
});

// Add Authentication
builder.AddAuth();

// Set global encoding to UTF-8
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// PostgreSQL-Datenbankkontext hinzufügen
builder.Services.AddDbContext<MailArchiverDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    options.UseNpgsql(
        connectionString,
        npgsqlOptions => {
            npgsqlOptions.CommandTimeout(
                builder.Configuration.GetValue<int>("Npgsql:CommandTimeout", 600) // 10 Minuten Standardwert
            );
        }
    )
    .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    
    // Enable sensitive data logging for debugging (remove in production)
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});

// Services hinzufügen
builder.Services.AddScoped<IGraphEmailService, GraphEmailService>(provider =>
    new GraphEmailService(
        provider.GetRequiredService<MailArchiverDbContext>(),
        provider.GetRequiredService<ILogger<GraphEmailService>>(),
        provider.GetRequiredService<ISyncJobService>(),
        provider.GetRequiredService<IOptions<BatchOperationOptions>>(),
        provider.GetRequiredService<IOptions<MailSyncOptions>>(),
        provider.GetRequiredService<MailArchiver.Utilities.DateTimeHelper>(),
        provider.GetRequiredService<MailArchiver.Services.Core.EmailCoreService>()
    ));
// Register GraphEmailService also for IProviderEmailService
builder.Services.AddScoped<MailArchiver.Services.Providers.IProviderEmailService>(provider => 
    provider.GetRequiredService<IGraphEmailService>() as MailArchiver.Services.Providers.IProviderEmailService);
builder.Services.AddScoped<IAuthenticationService, CookieAuthenticationService>();
builder.Services.AddScoped<OAuthAuthenticationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<MailArchiver.Services.Sync.DbExceptionClassifier>();
builder.Services.AddScoped<MailArchiver.Services.Sync.DbConnectivityCircuitBreaker>();
builder.Services.AddScoped<MailArchiver.Services.Sync.IProcessedMessageLedger, MailArchiver.Services.Sync.ProcessedMessageLedger>();
builder.Services.AddSingleton<ISyncJobService, SyncJobService>(); // NEUE SERVICE

// Register BatchRestoreService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<BatchRestoreService>();
builder.Services.AddSingleton<IBatchRestoreService>(provider => provider.GetRequiredService<BatchRestoreService>());
builder.Services.AddHostedService<BatchRestoreService>(provider => provider.GetRequiredService<BatchRestoreService>());

// Register MBoxImportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<MBoxImportService>();
builder.Services.AddSingleton<IMBoxImportService>(provider => provider.GetRequiredService<MBoxImportService>());
builder.Services.AddHostedService<MBoxImportService>(provider => provider.GetRequiredService<MBoxImportService>());

// Register EmlImportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<EmlImportService>();
builder.Services.AddSingleton<IEmlImportService>(provider => provider.GetRequiredService<EmlImportService>());
builder.Services.AddHostedService<EmlImportService>(provider => provider.GetRequiredService<EmlImportService>());

// Register ExportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<ExportService>();
builder.Services.AddSingleton<IExportService>(provider => provider.GetRequiredService<ExportService>());
builder.Services.AddHostedService<ExportService>(provider => provider.GetRequiredService<ExportService>());

// Register SelectedEmailsExportService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<SelectedEmailsExportService>();
builder.Services.AddSingleton<ISelectedEmailsExportService>(provider => provider.GetRequiredService<SelectedEmailsExportService>());
builder.Services.AddHostedService<SelectedEmailsExportService>(provider => provider.GetRequiredService<SelectedEmailsExportService>());

// Register MailAccountDeletionService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<MailAccountDeletionService>();
builder.Services.AddSingleton<IMailAccountDeletionService>(provider => provider.GetRequiredService<MailAccountDeletionService>());
builder.Services.AddHostedService<MailAccountDeletionService>(provider => provider.GetRequiredService<MailAccountDeletionService>());

// Register EmailDeletionService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<EmailDeletionService>();
builder.Services.AddSingleton<IEmailDeletionService>(provider => provider.GetRequiredService<EmailDeletionService>());
builder.Services.AddHostedService<EmailDeletionService>(provider => provider.GetRequiredService<EmailDeletionService>());

builder.Services.AddHostedService<EncryptionMigrationHostedService>();
builder.Services.AddHostedService<MailSyncBackgroundService>();

// Register DatabaseMaintenanceService as singleton and hosted service - MUST be the same instance
builder.Services.AddSingleton<DatabaseMaintenanceService>();
builder.Services.AddSingleton<IDatabaseMaintenanceService>(provider => provider.GetRequiredService<DatabaseMaintenanceService>());
builder.Services.AddHostedService<DatabaseMaintenanceService>(provider => provider.GetRequiredService<DatabaseMaintenanceService>());

// Register AccessLogService
builder.Services.AddScoped<IAccessLogService, AccessLogService>();

// Register BandwidthService for rate limit management
builder.Services.AddScoped<IBandwidthService, BandwidthService>();

// ====================
// NEW: Provider-based Architecture Services
// ====================
builder.Services.AddScoped<MailArchiver.Services.Core.EmailCoreService>();
builder.Services.AddScoped<MailArchiver.Services.Providers.ImapEmailService>();
builder.Services.AddScoped<MailArchiver.Services.Providers.ImportEmailService>();
builder.Services.AddScoped<MailArchiver.Services.Factories.ProviderEmailServiceFactory>();

// Add Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
// Configure Form Options for large file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    var uploadOptions = builder.Configuration.GetSection(UploadOptions.Upload).Get<UploadOptions>() ?? new UploadOptions();
    
    options.MultipartBodyLengthLimit = uploadOptions.MaxFileSizeBytes;
    options.ValueLengthLimit = 1024 * 1024;
    options.MultipartHeadersLengthLimit = 64 * 1024;
    options.MemoryBufferThreshold = uploadOptions.MemoryBufferThresholdBytes;
    options.BufferBody = false; // Stream large files directly to disk
});

// MVC hinzufügen
builder.Services.AddControllersWithViews(options =>
{
    // Add global filter for password change requirement
    options.Filters.Add<MailArchiver.Attributes.PasswordChangeRequiredAttribute>();
})
    .AddViewLocalization();

builder.Services.Configure<BatchRestoreOptions>(
    builder.Configuration.GetSection(BatchRestoreOptions.BatchRestore));


// Kestrel-Server-Limits konfigurieren - using configuration values
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var uploadOptions = context.Configuration.GetSection(UploadOptions.Upload).Get<UploadOptions>() ?? new UploadOptions();
    
    options.Limits.MaxRequestBodySize = uploadOptions.MaxFileSizeBytes;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(uploadOptions.KeepAliveTimeoutMinutes);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(uploadOptions.RequestHeadersTimeoutSeconds);
});

var app = builder.Build();

// Datenbank initialisieren
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MailArchiverDbContext>();
        try
        {
            // Ensure __EFMigrationsHistory table exists before running migrations
            await EnsureMigrationsHistoryTableExists(context, services);
            
            // Now run migrations
            context.Database.Migrate();
        }
        catch (Exception ex)
        {
            // If migrations fail, it might be a completely new database
            // In this case, ensure the database exists and then try migrations again
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Migration failed, attempting to create database structure");
            
            // Ensure database exists
            context.Database.EnsureCreated();
            
            // Ensure __EFMigrationsHistory table exists before running migrations again
            await EnsureMigrationsHistoryTableExists(context, services);
            
            // Try migrations again
            context.Database.Migrate();
        }
        context.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS citext;");

        // Create admin user if it doesn't exist
        var authOptions = services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        if (authOptions.Enabled)
        {
            var userService = services.GetRequiredService<IUserService>();
            var adminUser = await userService.GetUserByUsernameAsync(authOptions.Username);
            if (adminUser == null)
            {
                var adminEmail = $"{authOptions.Username}@local";
                adminUser = await userService.CreateUserAsync(
                    authOptions.Username,
                    adminEmail,
                    authOptions.Password,
                    true);
                var userLogger = services.GetRequiredService<ILogger<Program>>();
                userLogger.LogInformation("Admin user created: {Username} with email {Email}", authOptions.Username, adminEmail);
            }
        }

        var initLogger = services.GetRequiredService<ILogger<Program>>();
        initLogger.LogInformation("Datenbank wurde initialisiert");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ein Fehler ist bei der Datenbankinitialisierung aufgetreten");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Use Forwarded Headers middleware for reverse proxy support
app.UseForwardedHeaders();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "en-GB", "de", "es", "fr", "it", "sl", "nl", "ru", "hu", "pl")
    .AddSupportedUICultures("en", "en-GB", "de", "es", "fr", "it", "sl", "nl", "ru", "hu", "pl"));
app.UseRouting();
app.UseSession();

// Add Rate Limiting Middleware
app.UseRateLimiter();

// Add our custom authentication middleware
app.UseAuth();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
