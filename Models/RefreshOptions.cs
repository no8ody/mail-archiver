namespace MailArchiver.Models
{
    public class RefreshOptions
    {
        public const string Refresh = "Refresh";

        /// <summary>
        /// Global auto-refresh interval in minutes. Set to 0 to disable automatic background refresh.
        /// Fractional values such as 0.5 (30 seconds) are supported.
        /// </summary>
        public double IntervalMinutes { get; set; } = 5;

        public int IntervalMilliseconds => (int)Math.Max(0, Math.Round(TimeSpan.FromMinutes(Math.Max(0, IntervalMinutes)).TotalMilliseconds));
    }
}
