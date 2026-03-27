using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Security.Cryptography;
using System.Text;

namespace MailArchiver.Utilities
{
    public static class EmailEncryption
    {
        private const string CurrentStringPrefix = "enc:v2:";
        private const string LegacyStringPrefix = "enc:v1:";
        private static readonly byte[] CurrentBytesPrefix = Encoding.ASCII.GetBytes("ENC2");
        private static readonly byte[] LegacyBytesPrefix = Encoding.ASCII.GetBytes("ENC1");
        private static readonly byte[] PayloadMagic = Encoding.ASCII.GetBytes("MAE1");
        private const int NonceLength = 12;
        private const int TagLength = 16;
        private const int MinimumLegacyPayloadLength = NonceLength + TagLength;

        private static string? _configuredKey;

        public static bool IsEnabled => TryGetKey(out _);

        public static void Configure(IConfiguration configuration)
        {
            _configuredKey = FirstNonEmpty(
                configuration["Encryption:Key"],
                configuration["MailArchiver:EncryptionKey"],
                configuration["MAILARCHIVER_ENCRYPTION_KEY"],
                configuration["MailArchiver__EncryptionKey"],
                configuration["Encryption__Key"]);
        }

        public static void EnsureConfigured()
        {
            if (!TryGetKey(out _))
            {
                throw new InvalidOperationException(
                    "Encryption key is required. Configure Encryption:Key, MAILARCHIVER_ENCRYPTION_KEY, MailArchiver__EncryptionKey or Encryption__Key before starting MailArchiver.");
            }
        }

        public static bool HasCurrentStringMarker(string? value) =>
            !string.IsNullOrEmpty(value) && value.StartsWith(CurrentStringPrefix, StringComparison.Ordinal);

        public static bool HasLegacyStringMarker(string? value) =>
            !string.IsNullOrEmpty(value) && value.StartsWith(LegacyStringPrefix, StringComparison.Ordinal);

        public static bool HasCurrentBytesMarker(byte[]? value) =>
            StartsWithPrefix(value, CurrentBytesPrefix);

        public static bool HasLegacyBytesMarker(byte[]? value) =>
            StartsWithPrefix(value, LegacyBytesPrefix);

        public static string? EncryptString(string? value)
        {
            if (value == null || HasCurrentStringMarker(value))
                return value;

            var key = GetRequiredKey();
            var plaintext = Encoding.UTF8.GetBytes(value);
            var encrypted = EncryptBytesCore(plaintext, key);
            return CurrentStringPrefix + Convert.ToBase64String(encrypted);
        }

        public static string? DecryptString(string? value)
        {
            if (value == null)
                return null;

            if (HasCurrentStringMarker(value))
            {
                var key = GetRequiredKey();
                var encrypted = DecodeCurrentStringPayload(value);
                var plaintext = DecryptBytesCore(encrypted, key, PayloadFormat.Structured);
                return Encoding.UTF8.GetString(plaintext);
            }

            if (!TryDecodeLegacyStringPayload(value, out var legacyPayload))
                return value;

            try
            {
                var key = GetRequiredKey();
                var plaintext = DecryptBytesCore(legacyPayload, key, PayloadFormat.Legacy);
                return Encoding.UTF8.GetString(plaintext);
            }
            catch (CryptographicException)
            {
                return value;
            }
            catch (InvalidOperationException)
            {
                return value;
            }
        }

        public static byte[]? EncryptBytes(byte[]? value)
        {
            if (value == null || HasCurrentBytesMarker(value))
                return value;

            var key = GetRequiredKey();
            var encrypted = EncryptBytesCore(value, key);
            var result = new byte[CurrentBytesPrefix.Length + encrypted.Length];
            Buffer.BlockCopy(CurrentBytesPrefix, 0, result, 0, CurrentBytesPrefix.Length);
            Buffer.BlockCopy(encrypted, 0, result, CurrentBytesPrefix.Length, encrypted.Length);
            return result;
        }

        public static byte[]? DecryptBytes(byte[]? value)
        {
            if (value == null)
                return null;

            if (HasCurrentBytesMarker(value))
            {
                if (value.Length <= CurrentBytesPrefix.Length)
                {
                    throw new InvalidOperationException("Encrypted byte payload is incomplete.");
                }

                var key = GetRequiredKey();
                var encrypted = new byte[value.Length - CurrentBytesPrefix.Length];
                Buffer.BlockCopy(value, CurrentBytesPrefix.Length, encrypted, 0, encrypted.Length);
                return DecryptBytesCore(encrypted, key, PayloadFormat.Structured);
            }

            if (!TryExtractLegacyBytesPayload(value, out var legacyPayload))
                return value;

            try
            {
                var key = GetRequiredKey();
                return DecryptBytesCore(legacyPayload, key, PayloadFormat.Legacy);
            }
            catch (CryptographicException)
            {
                return value;
            }
            catch (InvalidOperationException)
            {
                return value;
            }
        }

        public static bool IsEncryptedString(string? value) =>
            HasCurrentStringMarker(value) || LooksLikeLegacyEncryptedString(value);

        public static bool IsEncryptedBytes(byte[]? value) =>
            HasCurrentBytesMarker(value);

        public static string BuildBodySearchText(string? body, string? htmlBody)
        {
            var source = !string.IsNullOrWhiteSpace(body) ? body : HtmlToPlainText(htmlBody);
            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

            var normalized = source.Replace("\0", string.Empty);
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        public static string BuildFallbackMessageId(MimeMessage message, DateTimeOffset emailDate)
        {
            var from = message.From?.ToString() ?? string.Empty;
            var to = message.To?.ToString() ?? string.Empty;
            var subject = message.Subject ?? string.Empty;
            var basis = $"{from}|{to}|{subject}|{emailDate.UtcDateTime.Ticks}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(basis));
            return $"generated-{Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        public static bool TryGetKey(out byte[] key)
        {
            var configured = FirstNonEmpty(
                Environment.GetEnvironmentVariable("MAILARCHIVER_ENCRYPTION_KEY"),
                Environment.GetEnvironmentVariable("MailArchiver__EncryptionKey"),
                Environment.GetEnvironmentVariable("Encryption__Key"),
                _configuredKey);

            if (string.IsNullOrWhiteSpace(configured))
            {
                key = Array.Empty<byte>();
                return false;
            }

            key = NormalizeKey(configured.Trim());
            return true;
        }

        private static byte[] GetRequiredKey()
        {
            if (!TryGetKey(out var key))
            {
                throw new InvalidOperationException(
                    "Encryption key is required. Configure Encryption:Key, MAILARCHIVER_ENCRYPTION_KEY, MailArchiver__EncryptionKey or Encryption__Key before accessing encrypted data.");
            }

            return key;
        }

        private static string? FirstNonEmpty(params string?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate;
            }

            return null;
        }

        private static bool LooksLikeLegacyEncryptedString(string? value)
            => TryDecodeLegacyStringPayload(value, out _);

        private static byte[] DecodeCurrentStringPayload(string value)
        {
            try
            {
                var payload = Convert.FromBase64String(value.Substring(CurrentStringPrefix.Length));
                if (!LooksLikeStructuredPayload(payload))
                {
                    throw new InvalidOperationException("Encrypted text payload is malformed.");
                }

                return payload;
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Encrypted text payload is not valid Base64.", ex);
            }
        }

        private static bool TryDecodeLegacyStringPayload(string? value, out byte[] payload)
        {
            payload = Array.Empty<byte>();

            if (!HasLegacyStringMarker(value))
                return false;

            try
            {
                payload = Convert.FromBase64String(value![LegacyStringPrefix.Length..]);
                return LooksLikeLegacyPayload(payload);
            }
            catch (FormatException)
            {
                payload = Array.Empty<byte>();
                return false;
            }
        }

        private static bool TryExtractLegacyBytesPayload(byte[]? value, out byte[] payload)
        {
            payload = Array.Empty<byte>();

            if (!HasLegacyBytesMarker(value) || value!.Length <= LegacyBytesPrefix.Length)
                return false;

            payload = new byte[value.Length - LegacyBytesPrefix.Length];
            Buffer.BlockCopy(value, LegacyBytesPrefix.Length, payload, 0, payload.Length);
            return LooksLikeLegacyPayload(payload);
        }

        private static bool StartsWithPrefix(byte[]? value, byte[] prefix)
        {
            if (value == null || value.Length < prefix.Length)
                return false;

            for (var i = 0; i < prefix.Length; i++)
            {
                if (value[i] != prefix[i])
                    return false;
            }

            return true;
        }

        private static bool LooksLikeStructuredPayload(byte[] payload)
        {
            if (payload.Length < PayloadMagic.Length + MinimumLegacyPayloadLength)
                return false;

            for (var i = 0; i < PayloadMagic.Length; i++)
            {
                if (payload[i] != PayloadMagic[i])
                    return false;
            }

            return true;
        }

        private static bool LooksLikeLegacyPayload(byte[] payload)
            => payload.Length >= MinimumLegacyPayloadLength;

        private static byte[] NormalizeKey(string configured)
        {
            try
            {
                var maybeBase64 = Convert.FromBase64String(configured);
                if (maybeBase64.Length is 16 or 24 or 32)
                    return maybeBase64.Length == 32 ? maybeBase64 : SHA256.HashData(maybeBase64);
            }
            catch
            {
            }

            return SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        }

        private static byte[] EncryptBytesCore(byte[] plaintext, byte[] key)
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceLength);
            var cipher = new byte[plaintext.Length];
            var tag = new byte[TagLength];
            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(nonce, plaintext, cipher, tag);

            var result = new byte[PayloadMagic.Length + nonce.Length + tag.Length + cipher.Length];
            Buffer.BlockCopy(PayloadMagic, 0, result, 0, PayloadMagic.Length);
            Buffer.BlockCopy(nonce, 0, result, PayloadMagic.Length, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, PayloadMagic.Length + nonce.Length, tag.Length);
            Buffer.BlockCopy(cipher, 0, result, PayloadMagic.Length + nonce.Length + tag.Length, cipher.Length);
            return result;
        }

        private static byte[] DecryptBytesCore(byte[] encrypted, byte[] key, PayloadFormat format)
        {
            byte[] nonce;
            byte[] tag;
            byte[] cipher;

            if (format == PayloadFormat.Structured)
            {
                if (!LooksLikeStructuredPayload(encrypted))
                    throw new InvalidOperationException("Encrypted payload is malformed.");

                var nonceOffset = PayloadMagic.Length;
                var tagOffset = nonceOffset + NonceLength;
                var cipherOffset = tagOffset + TagLength;

                nonce = new byte[NonceLength];
                tag = new byte[TagLength];
                cipher = new byte[encrypted.Length - cipherOffset];

                Buffer.BlockCopy(encrypted, nonceOffset, nonce, 0, NonceLength);
                Buffer.BlockCopy(encrypted, tagOffset, tag, 0, TagLength);
                Buffer.BlockCopy(encrypted, cipherOffset, cipher, 0, cipher.Length);
            }
            else
            {
                if (!LooksLikeLegacyPayload(encrypted))
                    throw new InvalidOperationException("Legacy encrypted payload is malformed.");

                nonce = encrypted[..NonceLength];
                tag = encrypted[NonceLength..(NonceLength + TagLength)];
                cipher = encrypted[(NonceLength + TagLength)..];
            }

            var plaintext = new byte[cipher.Length];
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, cipher, tag, plaintext);
            return plaintext;
        }

        private static string HtmlToPlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var withoutTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            return System.Net.WebUtility.HtmlDecode(withoutTags);
        }

        private enum PayloadFormat
        {
            Structured,
            Legacy
        }
    }
}
