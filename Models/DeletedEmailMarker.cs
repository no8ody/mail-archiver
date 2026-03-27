namespace MailArchiver.Models
{
    public class DeletedEmailMarker
    {
        public int Id { get; set; }
        public int MailAccountId { get; set; }
        public string MessageId { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime SentDate { get; set; }
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    }
}
