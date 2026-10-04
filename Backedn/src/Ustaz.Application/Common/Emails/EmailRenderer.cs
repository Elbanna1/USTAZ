using System.Net;
using System.Text;
using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Common.Emails;

/// <summary>Everything the renderer needs that is not part of the template itself.</summary>
public sealed record EmailBranding
{
    public string ProductName { get; init; } = "Ustaz";

    /// <summary>Base URL of the client. CTA paths are resolved against it.</summary>
    public string ClientBaseUrl { get; init; } = "http://localhost:5173";

    public string SupportEmail { get; init; } = "support.webcraft.eg@gmail.com";
}

/// <summary>
/// Turns a template plus its values into a finished message.
///
/// One layout serves both languages: Arabic renders <c>dir="rtl" lang="ar"</c> and English
/// <c>dir="ltr" lang="en"</c>, and the CSS is inline because email clients discard style
/// sheets. Every interpolated value is HTML-encoded — a student's display name is untrusted
/// input, and it ends up inside an email that other people read.
/// </summary>
public static class EmailRenderer
{
    private const string BrandColor = "#0F766E";
    private const string TextColor = "#1F2933";
    private const string MutedColor = "#6B7280";
    private const string BorderColor = "#E5E7EB";

    public static EmailMessage Render(
        EmailTemplate template,
        EmailLanguage language,
        string toEmail,
        string? toDisplayName,
        IReadOnlyDictionary<string, string>? values,
        EmailBranding branding)
    {
        var isArabic = language != EmailLanguage.English;

        var subject = EmailTemplate.Render(template.Subject(language), values);
        var heading = EmailTemplate.Render(template.Heading(language), values);

        var paragraphs = template.Body(language)
            .Select(paragraph => EmailTemplate.Render(paragraph, values))
            .ToList();

        var ctaLabel = template.CtaLabel(language) is { } label
            ? EmailTemplate.Render(label, values)
            : null;

        var ctaUrl = BuildCtaUrl(template.CtaPath, values, branding.ClientBaseUrl);
        var footnote = template.Footnote(language) is { } note
            ? EmailTemplate.Render(note, values)
            : null;

        return new EmailMessage
        {
            To = toEmail,
            ToDisplayName = toDisplayName,
            Subject = subject,
            HtmlBody = BuildHtml(isArabic, heading, paragraphs, ctaLabel, ctaUrl, footnote, branding),
            PlainTextBody = BuildText(heading, paragraphs, ctaLabel, ctaUrl, footnote, branding)
        };
    }

    /// <summary>
    /// Resolves a CTA path against the client base URL. An absolute URL — a verification or
    /// reset link built elsewhere — is passed through untouched.
    /// </summary>
    public static string? BuildCtaUrl(
        string? ctaPath,
        IReadOnlyDictionary<string, string>? values,
        string clientBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(ctaPath))
        {
            return null;
        }

        var resolved = EmailTemplate.Render(ctaPath, values);

        if (resolved.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || resolved.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return resolved;
        }

        return clientBaseUrl.TrimEnd('/') + "/" + resolved.TrimStart('/');
    }

    private static string BuildHtml(
        bool isArabic,
        string heading,
        IReadOnlyList<string> paragraphs,
        string? ctaLabel,
        string? ctaUrl,
        string? footnote,
        EmailBranding branding)
    {
        var dir = isArabic ? "rtl" : "ltr";
        var lang = isArabic ? "ar" : "en";
        var align = isArabic ? "right" : "left";
        var fontStack = isArabic
            ? "'Segoe UI', Tahoma, 'Noto Naskh Arabic', Arial, sans-serif"
            : "'Segoe UI', Helvetica, Arial, sans-serif";

        var footerNote = isArabic
            ? "وصلتك الرسالة دي من منصة " + branding.ProductName + "."
            : "You received this message from " + branding.ProductName + ".";

        var supportNote = isArabic
            ? "لو محتاج مساعدة، ابعتلنا على"
            : "Need help? Write to us at";

        var builder = new StringBuilder();

        builder.Append("<!DOCTYPE html><html dir=\"").Append(dir).Append("\" lang=\"").Append(lang).Append("\">");
        builder.Append("<head><meta charset=\"utf-8\">");
        builder.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("<title>").Append(WebUtility.HtmlEncode(heading)).Append("</title></head>");

        builder.Append("<body style=\"margin:0;padding:0;background-color:#F3F4F6;\">");

        // A single centred table: the only layout every mail client agrees on.
        builder.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"background-color:#F3F4F6;padding:24px 12px;\"><tr><td align=\"center\">");

        builder.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" ")
            .Append("style=\"max-width:600px;background-color:#FFFFFF;border-radius:12px;overflow:hidden;")
            .Append("border:1px solid ").Append(BorderColor).Append(";\">");

        // Header
        builder.Append("<tr><td style=\"background-color:").Append(BrandColor)
            .Append(";padding:20px 24px;text-align:").Append(align).Append(";\">")
            .Append("<span style=\"color:#FFFFFF;font-family:").Append(fontStack)
            .Append(";font-size:20px;font-weight:700;letter-spacing:0.5px;\">")
            .Append(WebUtility.HtmlEncode(branding.ProductName))
            .Append("</span></td></tr>");

        // Body
        builder.Append("<tr><td style=\"padding:28px 24px;text-align:").Append(align)
            .Append(";font-family:").Append(fontStack).Append(";color:").Append(TextColor).Append(";\">");

        builder.Append("<h1 style=\"margin:0 0 16px;font-size:22px;line-height:1.4;font-weight:700;\">")
            .Append(WebUtility.HtmlEncode(heading)).Append("</h1>");

        foreach (var paragraph in paragraphs)
        {
            builder.Append("<p style=\"margin:0 0 14px;font-size:16px;line-height:1.7;\">")
                .Append(WebUtility.HtmlEncode(paragraph)).Append("</p>");
        }

        if (ctaLabel is not null && ctaUrl is not null)
        {
            builder.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:24px 0;\">")
                .Append("<tr><td style=\"border-radius:8px;background-color:").Append(BrandColor).Append(";\">")
                .Append("<a href=\"").Append(WebUtility.HtmlEncode(ctaUrl))
                .Append("\" style=\"display:inline-block;padding:13px 28px;font-family:").Append(fontStack)
                .Append(";font-size:16px;font-weight:600;color:#FFFFFF;text-decoration:none;border-radius:8px;\">")
                .Append(WebUtility.HtmlEncode(ctaLabel))
                .Append("</a></td></tr></table>");

            // Clients that strip buttons still need the destination.
            builder.Append("<p style=\"margin:0 0 14px;font-size:13px;line-height:1.7;color:").Append(MutedColor)
                .Append(";word-break:break-all;\">")
                .Append(WebUtility.HtmlEncode(ctaUrl)).Append("</p>");
        }

        if (footnote is not null)
        {
            builder.Append("<p style=\"margin:20px 0 0;font-size:13px;line-height:1.7;color:").Append(MutedColor)
                .Append(";\">").Append(WebUtility.HtmlEncode(footnote)).Append("</p>");
        }

        builder.Append("</td></tr>");

        // Footer
        builder.Append("<tr><td style=\"padding:18px 24px;background-color:#F9FAFB;border-top:1px solid ")
            .Append(BorderColor).Append(";text-align:").Append(align)
            .Append(";font-family:").Append(fontStack).Append(";font-size:12px;line-height:1.7;color:")
            .Append(MutedColor).Append(";\">")
            .Append(WebUtility.HtmlEncode(footerNote)).Append("<br>")
            .Append(WebUtility.HtmlEncode(supportNote)).Append(' ')
            .Append("<a href=\"mailto:").Append(WebUtility.HtmlEncode(branding.SupportEmail))
            .Append("\" style=\"color:").Append(BrandColor).Append(";text-decoration:none;\">")
            .Append(WebUtility.HtmlEncode(branding.SupportEmail)).Append("</a>")
            .Append("</td></tr>");

        builder.Append("</table></td></tr></table></body></html>");

        return builder.ToString();
    }

    private static string BuildText(
        string heading,
        IReadOnlyList<string> paragraphs,
        string? ctaLabel,
        string? ctaUrl,
        string? footnote,
        EmailBranding branding)
    {
        var builder = new StringBuilder();

        builder.AppendLine(heading).AppendLine();

        foreach (var paragraph in paragraphs)
        {
            builder.AppendLine(paragraph).AppendLine();
        }

        if (ctaLabel is not null && ctaUrl is not null)
        {
            builder.AppendLine(ctaLabel + ": " + ctaUrl).AppendLine();
        }

        if (footnote is not null)
        {
            builder.AppendLine(footnote).AppendLine();
        }

        builder.AppendLine(branding.ProductName + " — " + branding.SupportEmail);

        return builder.ToString();
    }
}
