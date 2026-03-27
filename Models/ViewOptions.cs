namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration options for email viewing behavior.
    /// </summary>
    public class ViewOptions
    {
        /// <summary>
        /// When true, emails default to plain text view.
        /// Users can still switch to a restricted HTML view.
        /// </summary>
        public bool DefaultToPlainText { get; set; } = true;

        /// <summary>
        /// When true, remote resources are blocked in HTML emails.
        /// </summary>
        public bool BlockExternalResources { get; set; } = true;
    }
}
