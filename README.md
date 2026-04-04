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
The tables below list the preferred environment variables and their shipped defaults.

Legacy ASP.NET Core names such as `Authentication__Username` are still supported, but they are intentionally omitted here to keep the README readable. For detailed behavior and extra examples, see the [Setup Guide](doc/Setup.md).

### Runtime & Access

| Variable | Default |
|---|---|
| ASPNETCORE_URLS | http://0.0.0.0:5000 |
| DATABASE_URL | Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey |
| MAILARCHIVE_URL | empty |
| ALLOWED_HOSTS | localhost;127.0.0.1;[::1] |
| APPLICATION_PUBLIC_ORIGIN | empty |
| AUTHENTICATION_ENABLED | true |
| AUTHENTICATION_USERNAME | admin |
| AUTHENTICATION_PASSWORD | secure123! |
| AUTHENTICATION_SESSION_TIMEOUT_MINUTES | 60 |
| AUTHENTICATION_COOKIE_NAME | MailArchiverAuth |
| AUTHENTICATION_COOKIE_SAME_SITE | Strict |
| AUTHENTICATION_COOKIE_SECURE_POLICY | SameAsRequest |
| AUTHENTICATION_PUBLIC_ORIGIN | empty |

### OAuth

| Variable | Default |
|---|---|
| OAUTH_ENABLED | false |
| OAUTH_AUTHORITY | empty |
| OAUTH_CLIENT_ID | empty |
| OAUTH_CLIENT_SECRET | empty |
| OAUTH_CLIENT_SCOPES_0 | openid |
| OAUTH_CLIENT_SCOPES_1 | profile |
| OAUTH_CLIENT_SCOPES_2 | email |
| OAUTH_DISABLE_PASSWORD_LOGIN | false |
| OAUTH_AUTO_REDIRECT | false |
| OAUTH_AUTO_APPROVE_USERS | false |
| OAUTH_ADMIN_EMAILS_N | empty |

### Mail Sync

| Variable | Default |
|---|---|
| MAIL_SYNC_INTERVAL_MINUTES | 15 |
| MAIL_SYNC_TIMEOUT_MINUTES | 120 |
| MAIL_SYNC_CONNECTION_TIMEOUT_SECONDS | 300 |
| MAIL_SYNC_COMMAND_TIMEOUT_SECONDS | 600 |
| MAIL_SYNC_ALWAYS_FORCE_FULL_SYNC | false |
| MAIL_SYNC_IGNORE_SELF_SIGNED_CERT | false |

### Batch Operations & Selection

| Variable | Default |
|---|---|
| BATCH_RESTORE_ASYNC_THRESHOLD | 50 |
| BATCH_RESTORE_MAX_SYNC_EMAILS | 150 |
| BATCH_RESTORE_MAX_ASYNC_EMAILS | 50000 |
| BATCH_RESTORE_SESSION_TIMEOUT_MINUTES | 30 |
| BATCH_RESTORE_DEFAULT_BATCH_SIZE | 50 |
| BATCH_OPERATION_BATCH_SIZE | 50 |
| BATCH_OPERATION_PAUSE_BETWEEN_EMAILS_MS | 50 |
| BATCH_OPERATION_PAUSE_BETWEEN_BATCHES_MS | 200 |
| SELECTION_MAX_SELECTABLE_EMAILS | 250 |

### UI & Refresh

| Variable | Default |
|---|---|
| VIEW_DEFAULT_TO_PLAIN_TEXT | true |
| VIEW_BLOCK_EXTERNAL_RESOURCES | true |
| TZ | Etc/UCT |
| REFRESH_INTERVAL_MINUTES | 5 |

### Bandwidth & Maintenance

| Variable | Default |
|---|---|
| BANDWIDTH_TRACKING_ENABLED | false |
| BANDWIDTH_TRACKING_DAILY_LIMIT_MB | 25000 |
| BANDWIDTH_TRACKING_WARNING_THRESHOLD_PERCENT | 80 |
| BANDWIDTH_TRACKING_PAUSE_HOURS_ON_LIMIT | 24 |
| BANDWIDTH_TRACKING_TRACK_UPLOAD_BYTES | false |
| DATABASE_MAINTENANCE_ENABLED | false |
| DATABASE_MAINTENANCE_DAILY_EXECUTION_TIME | 02:00 |
| DATABASE_MAINTENANCE_TIMEOUT_MINUTES | 30 |

### Upload Limits

| Variable | Default |
|---|---|
| UPLOAD_MAX_FILE_SIZE_GB | 2 |
| UPLOAD_KEEP_ALIVE_TIMEOUT_MINUTES | 10 |
| UPLOAD_REQUEST_HEADERS_TIMEOUT_SECONDS | 30 |
| UPLOAD_MEMORY_BUFFER_THRESHOLD_MB | 1 |
| UPLOAD_MAX_ARCHIVE_ENTRIES | 10000 |
| UPLOAD_MAX_ARCHIVE_ENTRY_SIZE_MB | 50 |
| UPLOAD_MAX_ARCHIVE_EXPANDED_SIZE_GB | 2 |
| UPLOAD_MAX_ARCHIVE_COMPRESSION_RATIO | 100 |
| UPLOAD_NOTES | Security-hardened upload defaults with bounded ZIP processing |

### Reverse Proxy & Host Filtering

| Variable | Default |
|---|---|
| REVERSE_PROXY_FORWARD_LIMIT | 1 |
| REVERSE_PROXY_REQUIRE_HEADER_SYMMETRY | true |
| REVERSE_PROXY_KNOWN_PROXIES_N | empty |
| REVERSE_PROXY_KNOWN_NETWORKS_N | empty |
| HOST_FILTERING_ALLOWED_HOSTS | empty |
| HOST_FILTERING_ADDITIONAL_ALLOWED_HOSTS | empty |

### Storage, Encryption & Logging

| Variable | Default |
|---|---|
| DATA_PROTECTION_KEY_PATH | /app/DataProtection-Keys |
| ENCRYPTION_KEY | empty |
| NPGSQL_COMMAND_TIMEOUT | 900 |
| LOGGING_LOG_LEVEL_DEFAULT | Information |
| LOGGING_LOG_LEVEL_MICROSOFT_ASP_NET_CORE | Warning |
| LOGGING_LOG_LEVEL_MICROSOFT_ENTITY_FRAMEWORK_CORE_DATABASE_COMMAND | Warning |


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
