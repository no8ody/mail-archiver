# 📧 Mail-Archiver - Email Archiving System

**A comprehensive solution for archiving, searching, and exporting emails**

<div style="display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 20px;">
  <a href="#"><img src="https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white" alt="Docker"></a>
  <a href="#"><img src="https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET"></a>
  <a href="#"><img src="https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL"></a>
  <a href="#"><img src="https://img.shields.io/badge/Bootstrap-563D7C?style=for-the-badge&logo=bootstrap&logoColor=white" alt="Bootstrap"></a>
  <a href="https://github.com/s1t5/mail-archiver"><img src="https://img.shields.io/github/stars/s1t5/mail-archiver?style=for-the-badge&logo=github" alt="GitHub Stars"></a>
  <a href="https://www.buymeacoffee.com/s1t5" target="_blank"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-s1t5-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee"></a>
  <a href="https://www.paypal.com/ncp/payment/E4HP9BVRYN54N" target="_blank"><img src="https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white" alt="PayPal Donate"></a>
</div>

## ✨ Key Features

### 📌 Core Features
- Automated archiving of incoming and outgoing emails from multiple accounts
- Storage of email content and attachments with scheduled synchronization
- Mobile and desktop optimized, multilingual responsive UI with dark mode
- OpenID Connect (OIDC) for integration of external authentication services ([OIDC Implementation Guide](doc/OIDC_Implementation.md))

### 🔍 Search & Access
- Advanced search across all archived emails with filtering options
- Preview emails with attachment list
- Export entire mail accounts as mbox files or zipped EML archives
- Export selected individual emails or email batches

### 👥 User Management
- Multi-user support with account-specific permissions
- Dashboard with statistics, storage monitoring, and sender analysis
- Comprehensive access logging with detailed activity tracking of user activities (Access, Export, Deletion, Restore and many more) - see [Access Logging Guide](doc/Logs.md) for details

### 🧩 Email Provider Support
- **IMAP**: Traditional IMAP accounts with full synchronization capabilities
- **M365**: Microsoft 365 mail accounts via Microsoft Graph API ([Setup Guide](doc/AZURE_APP_REGISTRATION_M365.md))
- **IMPORT**: Import-only accounts for migrating existing email archives

### 📥 Import & Restore Functions
- MBox Import and EML Import (ZIP files with folder structure support)
- Restore selected emails or entire mailboxes to destination mailboxes

### 🗑️ Retention Policies
- Configure automatic deletion of archived emails from mailserver after specified days ([Retention Policies Documentation](doc/RetentionPolicies.md))
- Set retention period per email account (e.g., 30, 90, or 365 days)
- **Local Archive Retention**: Configure separate retention period for local archive

## 📚 Documentation

For detailed documentation on installation, configuration, and usage, please refer to the [Documentation Index](doc/Index.md). Please note that the documentation is still fresh and is continuously being expanded.

## 🖼️ Screenshots

### Dashboard
![Mail-Archiver Dashboard](https://github.com/s1t5/mail-archiver/blob/main/Screenshots/dashboard.jpg?raw=true)

### Archive
![Mail-Archiver Archive](https://github.com/s1t5/mail-archiver/blob/main/Screenshots/archive.jpg?raw=true)

### Email Details
![Mail-Archiver Mail](https://github.com/s1t5/mail-archiver/blob/main/Screenshots/details.jpg?raw=true)

## 🚀 Quick Start

### Prerequisites
- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

### 🛠️ Installation

1. Install the prerequisites on your system

2. Create a `docker-compose.yml` file 
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Preferred aliases (legacy __ variables still work)
      - DATABASE_URL=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;
      - AUTHENTICATION_USERNAME=admin
      - AUTHENTICATION_PASSWORD=secure123!
      - TZ=Etc/UCT
      - REFRESH_INTERVAL_MINUTES=5
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy


  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. Edit the database configuration in the `docker-compose.yml` and set a secure password in the `POSTGRES_PASSWORD` variable and the `DATABASE_URL`.

4. Define `AUTHENTICATION_USERNAME` and `AUTHENTICATION_PASSWORD` for the initial admin user. Legacy names like `Authentication__Username` and `Authentication__Password` are still supported.

5. Adjust `TZ` to match your preferred timezone (default is "Etc/UCT"). You can use any IANA timezone identifier (e.g., "Europe/Berlin", "Asia/Tokyo").

6. Optionally set `REFRESH_INTERVAL_MINUTES` to control the global background refresh interval. The default is `5` minutes. Fractional values such as `0.5` (30 seconds) are supported, and `0` disables automatic refresh.

7. Configure a reverse proxy of your choice with https to secure access to the application. 

> ⚠️ **Attention**
> The application itself does not provide encrypted access via https! It must be set up via a reverse proxy! Moreover the application is not build for public internet access!

8. Initial start of the containers:
```bash
docker compose up -d
```

9. Restart containers:
```bash
docker compose restart
```

10. Access the application in your prefered browser.

11. Login with your defined credentials and add your first email account:
- Navigate to "Email Accounts" section
- Click "New Account"
- Enter your server details and credentials
- Save and start archiving!
- If you want, create other users and assign accounts.

## 🔐 Security Notes
- Use strong passwords and change default credentials
- Consider implementing HTTPS with a reverse proxy in production
- Regular backups of the PostgreSQL database recommended (see [Backup & Restore Guide](doc/BackupRestore.md) for detailed instructions)

## ⚙️ Advanced Setup
The table below lists the preferred environment variables, their legacy ASP.NET Core `Section__Key` equivalents, and the shipped defaults. Empty defaults mean the setting is unset unless you provide a value.

Shortcuts such as `DATABASE_URL`, `TZ`, and `MAILARCHIVE_URL` are preferred where available. Direct single-underscore aliases such as `CONNECTION_STRINGS_DEFAULT_CONNECTION` and `TIME_ZONE_DISPLAY_TIME_ZONE_ID` also continue to work, in addition to the legacy `__` syntax.

| Variable | Legacy `__` form | Default |
|---|---|---|
| `ASPNETCORE_URLS` | — | `http://0.0.0.0:5000` |
| `DATABASE_URL` | `ConnectionStrings__DefaultConnection` | `Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey` |
| `MAILARCHIVE_URL` | `Authentication__PublicOrigin` / `Application__PublicOrigin` / `HostFiltering__AdditionalAllowedHosts` | empty |
| `ALLOWED_HOSTS` | `AllowedHosts` | `localhost;127.0.0.1;[::1]` |
| `APPLICATION_PUBLIC_ORIGIN` | `Application__PublicOrigin` | empty |
| `AUTHENTICATION_ENABLED` | `Authentication__Enabled` | `true` |
| `AUTHENTICATION_USERNAME` | `Authentication__Username` | `admin` |
| `AUTHENTICATION_PASSWORD` | `Authentication__Password` | `secure123!` |
| `AUTHENTICATION_SESSION_TIMEOUT_MINUTES` | `Authentication__SessionTimeoutMinutes` | `60` |
| `AUTHENTICATION_COOKIE_NAME` | `Authentication__CookieName` | `MailArchiverAuth` |
| `AUTHENTICATION_COOKIE_SAME_SITE` | `Authentication__CookieSameSite` | `Strict` |
| `AUTHENTICATION_COOKIE_SECURE_POLICY` | `Authentication__CookieSecurePolicy` | `SameAsRequest` |
| `AUTHENTICATION_PUBLIC_ORIGIN` | `Authentication__PublicOrigin` | empty |
| `OAUTH_ENABLED` | `OAuth__Enabled` | `false` |
| `OAUTH_AUTHORITY` | `OAuth__Authority` | empty |
| `OAUTH_CLIENT_ID` | `OAuth__ClientId` | empty |
| `OAUTH_CLIENT_SECRET` | `OAuth__ClientSecret` | empty |
| `OAUTH_CLIENT_SCOPES_0` | `OAuth__ClientScopes__0` | `openid` |
| `OAUTH_CLIENT_SCOPES_1` | `OAuth__ClientScopes__1` | `profile` |
| `OAUTH_CLIENT_SCOPES_2` | `OAuth__ClientScopes__2` | `email` |
| `OAUTH_DISABLE_PASSWORD_LOGIN` | `OAuth__DisablePasswordLogin` | `false` |
| `OAUTH_AUTO_REDIRECT` | `OAuth__AutoRedirect` | `false` |
| `OAUTH_AUTO_APPROVE_USERS` | `OAuth__AutoApproveUsers` | `false` |
| `OAUTH_ADMIN_EMAILS_N` | `OAuth__AdminEmails__N` | empty |
| `MAIL_SYNC_INTERVAL_MINUTES` | `MailSync__IntervalMinutes` | `15` |
| `MAIL_SYNC_TIMEOUT_MINUTES` | `MailSync__TimeoutMinutes` | `120` |
| `MAIL_SYNC_CONNECTION_TIMEOUT_SECONDS` | `MailSync__ConnectionTimeoutSeconds` | `300` |
| `MAIL_SYNC_COMMAND_TIMEOUT_SECONDS` | `MailSync__CommandTimeoutSeconds` | `600` |
| `MAIL_SYNC_ALWAYS_FORCE_FULL_SYNC` | `MailSync__AlwaysForceFullSync` | `false` |
| `MAIL_SYNC_IGNORE_SELF_SIGNED_CERT` | `MailSync__IgnoreSelfSignedCert` | `false` |
| `BATCH_RESTORE_ASYNC_THRESHOLD` | `BatchRestore__AsyncThreshold` | `50` |
| `BATCH_RESTORE_MAX_SYNC_EMAILS` | `BatchRestore__MaxSyncEmails` | `150` |
| `BATCH_RESTORE_MAX_ASYNC_EMAILS` | `BatchRestore__MaxAsyncEmails` | `50000` |
| `BATCH_RESTORE_SESSION_TIMEOUT_MINUTES` | `BatchRestore__SessionTimeoutMinutes` | `30` |
| `BATCH_RESTORE_DEFAULT_BATCH_SIZE` | `BatchRestore__DefaultBatchSize` | `50` |
| `BATCH_OPERATION_BATCH_SIZE` | `BatchOperation__BatchSize` | `50` |
| `BATCH_OPERATION_PAUSE_BETWEEN_EMAILS_MS` | `BatchOperation__PauseBetweenEmailsMs` | `50` |
| `BATCH_OPERATION_PAUSE_BETWEEN_BATCHES_MS` | `BatchOperation__PauseBetweenBatchesMs` | `200` |
| `SELECTION_MAX_SELECTABLE_EMAILS` | `Selection__MaxSelectableEmails` | `250` |
| `VIEW_DEFAULT_TO_PLAIN_TEXT` | `View__DefaultToPlainText` | `true` |
| `VIEW_BLOCK_EXTERNAL_RESOURCES` | `View__BlockExternalResources` | `true` |
| `BANDWIDTH_TRACKING_ENABLED` | `BandwidthTracking__Enabled` | `false` |
| `BANDWIDTH_TRACKING_DAILY_LIMIT_MB` | `BandwidthTracking__DailyLimitMb` | `25000` |
| `BANDWIDTH_TRACKING_WARNING_THRESHOLD_PERCENT` | `BandwidthTracking__WarningThresholdPercent` | `80` |
| `BANDWIDTH_TRACKING_PAUSE_HOURS_ON_LIMIT` | `BandwidthTracking__PauseHoursOnLimit` | `24` |
| `BANDWIDTH_TRACKING_TRACK_UPLOAD_BYTES` | `BandwidthTracking__TrackUploadBytes` | `false` |
| `TZ` | `TimeZone__DisplayTimeZoneId` | `Etc/UCT` |
| `REFRESH_INTERVAL_MINUTES` | `Refresh__IntervalMinutes` | `5` |
| `DATABASE_MAINTENANCE_ENABLED` | `DatabaseMaintenance__Enabled` | `false` |
| `DATABASE_MAINTENANCE_DAILY_EXECUTION_TIME` | `DatabaseMaintenance__DailyExecutionTime` | `02:00` |
| `DATABASE_MAINTENANCE_TIMEOUT_MINUTES` | `DatabaseMaintenance__TimeoutMinutes` | `30` |
| `UPLOAD_MAX_FILE_SIZE_GB` | `Upload__MaxFileSizeGB` | `2` |
| `UPLOAD_KEEP_ALIVE_TIMEOUT_MINUTES` | `Upload__KeepAliveTimeoutMinutes` | `10` |
| `UPLOAD_REQUEST_HEADERS_TIMEOUT_SECONDS` | `Upload__RequestHeadersTimeoutSeconds` | `30` |
| `UPLOAD_MEMORY_BUFFER_THRESHOLD_MB` | `Upload__MemoryBufferThresholdMB` | `1` |
| `UPLOAD_MAX_ARCHIVE_ENTRIES` | `Upload__MaxArchiveEntries` | `10000` |
| `UPLOAD_MAX_ARCHIVE_ENTRY_SIZE_MB` | `Upload__MaxArchiveEntrySizeMB` | `50` |
| `UPLOAD_MAX_ARCHIVE_EXPANDED_SIZE_GB` | `Upload__MaxArchiveExpandedSizeGB` | `2` |
| `UPLOAD_MAX_ARCHIVE_COMPRESSION_RATIO` | `Upload__MaxArchiveCompressionRatio` | `100` |
| `UPLOAD_NOTES` | `Upload__Notes` | `Security-hardened upload defaults with bounded ZIP processing` |
| `REVERSE_PROXY_FORWARD_LIMIT` | `ReverseProxy__ForwardLimit` | `1` |
| `REVERSE_PROXY_REQUIRE_HEADER_SYMMETRY` | `ReverseProxy__RequireHeaderSymmetry` | `true` |
| `REVERSE_PROXY_KNOWN_PROXIES_N` | `ReverseProxy__KnownProxies__N` | empty |
| `REVERSE_PROXY_KNOWN_NETWORKS_N` | `ReverseProxy__KnownNetworks__N` | empty |
| `HOST_FILTERING_ALLOWED_HOSTS` | `HostFiltering__AllowedHosts` | empty |
| `HOST_FILTERING_ADDITIONAL_ALLOWED_HOSTS` | `HostFiltering__AdditionalAllowedHosts` | empty |
| `DATA_PROTECTION_KEY_PATH` | `DataProtection__KeyPath` | `/app/DataProtection-Keys` |
| `ENCRYPTION_KEY` | `Encryption__Key` | empty |
| `NPGSQL_COMMAND_TIMEOUT` | `Npgsql__CommandTimeout` | `900` |
| `LOGGING_LOG_LEVEL_DEFAULT` | `Logging__LogLevel__Default` | `Information` |
| `LOGGING_LOG_LEVEL_MICROSOFT_ASP_NET_CORE` | `Logging__LogLevel__Microsoft.AspNetCore` | `Warning` |
| `LOGGING_LOG_LEVEL_MICROSOFT_ENTITY_FRAMEWORK_CORE_DATABASE_COMMAND` | `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command` | `Warning` |

For detailed behavior and setup examples, please refer to the [Setup Guide](doc/Setup.md).


## 📋 Technical Details

### Architecture
- ASP.NET Core 10 MVC application
- PostgreSQL database for email storage
- MailKit library for IMAP communication
- Microsoft Graph API for M365 email access
- Background service for email synchronization
- Bootstrap 5 and Chart.js for frontend

## 🤝 Contributing

We welcome contributions from the community! Please read our [Contributing Guide](CONTRIBUTING.md) for detailed information about how to contribute to Mail Archiver.

For code changes by third parties, please coordinate with us via email at mail@s1t5.dev before making any changes.

You can also:
- Open an Issue for bug reports or feature requests
- Submit a Pull Request for improvements
- Help improve documentation

## 💖 Support the Project
If you find this project useful and would like to support its continued development, you can buy me a coffee! Your support helps me dedicate more time and resources to improving the application and adding new features. While financial support is not required, it is greatly appreciated and helps ensure the project's ongoing maintenance and enhancement.

<a href="https://www.buymeacoffee.com/s1t5" target="_blank"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-s1t5-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee"></a>
<a href="https://www.paypal.com/ncp/payment/E4HP9BVRYN54N" target="_blank"><img src="https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white" alt="PayPal Donate"></a>

---

📄 *License: GNU GENERAL PUBLIC LICENSE Version 3 (see LICENSE file)*
