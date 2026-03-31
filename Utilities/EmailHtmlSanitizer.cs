using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MailArchiver.Utilities
{
    public static class EmailHtmlSanitizer
    {
        private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "abbr", "b", "blockquote", "br", "caption", "code", "col", "colgroup", "dd", "div",
            "dl", "dt", "em", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "img", "li",
            "ol", "p", "pre", "s", "small", "span", "strong", "sub", "sup", "table", "tbody",
            "td", "tfoot", "th", "thead", "tr", "u", "ul"
        };

        private static readonly Dictionary<string, HashSet<string>> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "title" },
            ["img"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "src", "alt", "title", "width", "height" },
            ["td"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan", "align", "valign" },
            ["th"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan", "align", "valign", "scope" },
            ["table"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "border", "cellpadding", "cellspacing", "summary" },
            ["col"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "span", "width" },
            ["colgroup"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "span", "width" },
            ["ol"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "start", "type" },
            ["li"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "value" }
        };

        private static readonly HashSet<string> GlobalAllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            "dir", "lang", "title", "aria-label"
        };

        private static readonly string[] DangerousElementNames =
        {
            "script", "style", "iframe", "frame", "frameset", "object", "embed", "applet", "meta", "base",
            "link", "form", "input", "button", "textarea", "select", "option", "svg", "math", "canvas",
            "template", "video", "audio", "source", "track", "noscript"
        };

        public static string Sanitize(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var sanitized = html.Replace("\0", string.Empty);
            sanitized = Regex.Replace(sanitized, @"<!--[\s\S]*?-->", string.Empty, RegexOptions.Singleline);
            sanitized = Regex.Replace(sanitized, @"<\?.*?\?>", string.Empty, RegexOptions.Singleline);
            sanitized = Regex.Replace(sanitized, @"<!DOCTYPE.*?>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (var element in DangerousElementNames)
            {
                sanitized = Regex.Replace(
                    sanitized,
                    $@"<\s*{element}\b[^>]*>[\s\S]*?<\s*/\s*{element}\s*>",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                sanitized = Regex.Replace(
                    sanitized,
                    $@"<\s*{element}\b[^>]*?/\s*>",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                sanitized = Regex.Replace(
                    sanitized,
                    $@"<\s*{element}\b[^>]*>",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            sanitized = Regex.Replace(
                sanitized,
                @"<(?<closing>/)?\s*(?<tag>[a-zA-Z0-9]+)(?<attrs>[^>]*)>",
                match => SanitizeTag(match),
                RegexOptions.Singleline);

            sanitized = Regex.Replace(sanitized, @"\n{3,}", "\n\n");
            return sanitized.Trim();
        }

        public static string WrapDocument(string bodyHtml, bool plainText)
        {
            var bodyClass = plainText ? "plain-text-body" : "html-body";
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        :root {{ color-scheme: light; }}
        html, body {{ margin: 0; padding: 0; background: #f8f9fa; color: #212529; }}
        body {{ font-family: Arial, Helvetica, sans-serif; line-height: 1.5; }}
        .mail-wrapper {{ max-width: 100%; box-sizing: border-box; padding: 16px; }}
        .mail-content {{ background: #fff; border: 1px solid #dee2e6; border-radius: 4px; padding: 20px; overflow-wrap: anywhere; }}
        .mail-content img {{ max-width: 100%; height: auto; }}
        .mail-content table {{ border-collapse: collapse; width: auto; max-width: 100%; display: block; overflow-x: auto; }}
        .mail-content th, .mail-content td {{ border: 1px solid #dee2e6; padding: 6px 8px; vertical-align: top; }}
        .mail-content pre {{ white-space: pre-wrap; word-break: break-word; margin: 0; font-family: ui-monospace, SFMono-Regular, SFMono-Regular, Consolas, monospace; }}
        .mail-content blockquote {{ margin: 0 0 0 1rem; padding-left: 1rem; border-left: 4px solid #ced4da; color: #495057; }}
        .mail-content a {{ color: #0d6efd; text-decoration: underline; }}
        .plain-text-body .mail-content {{ white-space: normal; }}
    </style>
</head>
<body class=""{bodyClass}"">
    <div class=""mail-wrapper"">
        <div class=""mail-content"">
            {bodyHtml}
        </div>
    </div>
</body>
</html>";
        }

        private static string SanitizeTag(Match match)
        {
            var tag = match.Groups["tag"].Value.ToLowerInvariant();
            var isClosingTag = match.Groups["closing"].Success;

            if (!AllowedTags.Contains(tag))
            {
                return string.Empty;
            }

            if (isClosingTag)
            {
                return $"</{tag}>";
            }

            var attrs = match.Groups["attrs"].Value;
            var sanitizedAttributes = SanitizeAttributes(tag, attrs);
            var selfClosing = match.Value.EndsWith("/>", StringComparison.Ordinal) || tag is "br" or "hr" or "img" or "col";

            return selfClosing
                ? $"<{tag}{sanitizedAttributes} />"
                : $"<{tag}{sanitizedAttributes}>";
        }

        private static string SanitizeAttributes(string tag, string rawAttributes)
        {
            if (string.IsNullOrWhiteSpace(rawAttributes))
            {
                return tag.Equals("a", StringComparison.OrdinalIgnoreCase)
                    ? " target=\"_blank\" rel=\"noopener noreferrer\""
                    : string.Empty;
            }

            var builder = new StringBuilder();
            var attributePattern = new Regex(
                @"(?<name>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*(=\s*(?<value>""[^""]*""|'[^']*'|[^\s""'=<>`]+))?",
                RegexOptions.Singleline);

            foreach (Match attrMatch in attributePattern.Matches(rawAttributes))
            {
                var attributeName = attrMatch.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(attributeName))
                {
                    continue;
                }

                if (attributeName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (attributeName.Equals("style", StringComparison.OrdinalIgnoreCase) ||
                    attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase) ||
                    attributeName.Equals("srcdoc", StringComparison.OrdinalIgnoreCase) ||
                    attributeName.Equals("formaction", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsAttributeAllowed(tag, attributeName))
                {
                    continue;
                }

                var rawValue = attrMatch.Groups["value"].Value;
                var normalizedValue = NormalizeAttributeValue(rawValue);

                if (!TrySanitizeAttributeValue(tag, attributeName, normalizedValue, out var safeValue))
                {
                    continue;
                }

                if (safeValue == null)
                {
                    builder.Append(' ').Append(attributeName);
                }
                else
                {
                    builder.Append(' ')
                        .Append(attributeName)
                        .Append("=\"")
                        .Append(WebUtility.HtmlEncode(safeValue))
                        .Append('"');
                }
            }

            if (tag.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
            }

            return builder.ToString();
        }

        private static bool IsAttributeAllowed(string tag, string attributeName)
        {
            return GlobalAllowedAttributes.Contains(attributeName)
                   || (AllowedAttributes.TryGetValue(tag, out var attributes) && attributes.Contains(attributeName));
        }

        private static string NormalizeAttributeValue(string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue))
            {
                return string.Empty;
            }

            var trimmed = rawValue.Trim();
            if ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
                (trimmed.StartsWith('\'') && trimmed.EndsWith('\'')))
            {
                trimmed = trimmed[1..^1];
            }

            trimmed = WebUtility.HtmlDecode(trimmed);
            trimmed = Regex.Replace(trimmed, "[\\u0000-\\u001F\\u007F]+", string.Empty);
            return trimmed.Trim();
        }

        private static bool TrySanitizeAttributeValue(string tag, string attributeName, string value, out string? sanitizedValue)
        {
            sanitizedValue = value;

            if (string.IsNullOrEmpty(value))
            {
                return attributeName is "alt" or "title" or "width" or "height" or "border" or "cellpadding" or "cellspacing" or "summary";
            }

            if (attributeName.Equals("href", StringComparison.OrdinalIgnoreCase))
            {
                return TrySanitizeHref(value, out sanitizedValue);
            }

            if (attributeName.Equals("src", StringComparison.OrdinalIgnoreCase))
            {
                return TrySanitizeImageSource(value, out sanitizedValue);
            }

            if (attributeName is "width" or "height" or "border" or "cellpadding" or "cellspacing" or "colspan" or "rowspan" or "span" or "start" or "value")
            {
                if (!Regex.IsMatch(value, @"^\d{1,5}$"))
                {
                    sanitizedValue = null;
                    return false;
                }

                return true;
            }

            if (attributeName.Equals("align", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Equals("left", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("right", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("center", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("justify", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                sanitizedValue = null;
                return false;
            }

            if (attributeName.Equals("valign", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Equals("top", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("middle", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("bottom", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("baseline", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                sanitizedValue = null;
                return false;
            }

            return true;
        }

        private static bool TrySanitizeHref(string value, out string? sanitizedValue)
        {
            sanitizedValue = null;
            if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
            {
                if (absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                    absoluteUri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase) ||
                    absoluteUri.Scheme.Equals("tel", StringComparison.OrdinalIgnoreCase))
                {
                    sanitizedValue = absoluteUri.ToString();
                    return true;
                }

                return false;
            }

            if (value.StartsWith('#'))
            {
                sanitizedValue = value;
                return true;
            }

            return false;
        }

        private static bool TrySanitizeImageSource(string value, out string? sanitizedValue)
        {
            sanitizedValue = null;

            if (value.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
            {
                sanitizedValue = value;
                return true;
            }

            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                sanitizedValue = value;
                return true;
            }

            return false;
        }
    }
}
