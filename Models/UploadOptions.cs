namespace MailArchiver.Models
{
    public class UploadOptions
    {
        public const string Upload = "Upload";

        public int MaxFileSizeGB { get; set; } = 2;
        public int KeepAliveTimeoutMinutes { get; set; } = 10;
        public int RequestHeadersTimeoutSeconds { get; set; } = 30;
        public int MemoryBufferThresholdMB { get; set; } = 1;
        public int MaxArchiveEntries { get; set; } = 10000;
        public int MaxArchiveEntrySizeMB { get; set; } = 50;
        public int MaxArchiveExpandedSizeGB { get; set; } = 2;
        public int MaxArchiveCompressionRatio { get; set; } = 100;
        public string Notes { get; set; } = string.Empty;

        public long MaxFileSizeBytes => MaxFileSizeGB * 1024L * 1024L * 1024L;
        public int MemoryBufferThresholdBytes => checked(MemoryBufferThresholdMB * 1024 * 1024);
        public long MaxArchiveEntrySizeBytes => MaxArchiveEntrySizeMB * 1024L * 1024L;
        public long MaxArchiveExpandedSizeBytes => MaxArchiveExpandedSizeGB * 1024L * 1024L * 1024L;
        public string MaxFileSizeFormatted => $"{MaxFileSizeGB} GB";
    }
}
