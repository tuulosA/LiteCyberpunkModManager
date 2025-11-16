using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Documents;

namespace LiteCyberpunkModManager.Helpers
{
    public static class TextFormatter
    {
        public static string ConvertHtmlToPlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            // decode HTML entities (e.g. &#92; -> \)
            string decoded = WebUtility.HtmlDecode(html);

            decoded = decoded
                .Replace("<br />", "\n")
                .Replace("<br/>", "\n")
                .Replace("<br>", "\n");

            decoded = Regex.Replace(decoded, @"\[img\](.*?)\[/img\]", "$1");
            decoded = Regex.Replace(decoded, @"\[(\/)?[^\]]+\]", string.Empty);

            return decoded;
        }

        public static List<Inline> ParseToInlines(string text)
        {
            var inlines = new List<Inline>();

            // Split by 2 or more newlines to separate paragraphs
            var paragraphs = Regex.Split(text, @"(\n\s*\n)+");

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para))
                    continue;

                // Split by single newline for line breaks inside the paragraph
                var lines = para.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var parts = Regex.Split(lines[i], @"(https?://[^\s]+)");

                    foreach (var part in parts)
                    {
                        if (Regex.IsMatch(part, @"^https?://"))
                        {
                            var hyperlink = new Hyperlink(new Run(part))
                            {
                                NavigateUri = new Uri(part)
                            };
                            hyperlink.RequestNavigate += (s, e) =>
                            {
                                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                                e.Handled = true;
                            };
                            inlines.Add(hyperlink);
                        }
                        else
                        {
                            inlines.Add(new Run(part));
                        }
                    }

                    // Add a line break between lines, but not after the last line
                    if (i < lines.Length - 1)
                        inlines.Add(new LineBreak());
                }

                // Add a little extra space after a paragraph (e.g., two line breaks)
                inlines.Add(new LineBreak());
            }

            return inlines;
        }
    }
}
