using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;

namespace Text_Grab.Utilities;

public static class ExternalLinkUtilities
{
    public static bool TryCreate(string? text, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string target = text.Trim();
        if (target.Any(char.IsControl)
            || !Uri.TryCreate(target, UriKind.Absolute, out Uri? candidate)
            || candidate.IsFile
            || candidate.IsUnc)
        {
            return false;
        }

        bool allowed = candidate.Scheme switch
        {
            "http" or "https" => !string.IsNullOrEmpty(candidate.Host),
            "mailto" => IsValidMailto(candidate),
            "tel" => IsValidTelephone(candidate),
            _ => false
        };
        if (!allowed)
            return false;

        uri = candidate;
        return true;
    }

    public static bool RequiresConfirmation(Uri uri) => uri.Scheme is "mailto" or "tel";

    private static bool IsValidMailto(Uri uri)
    {
        if (uri.Fragment.Length > 0 || uri.AbsolutePath.Length > 0)
            return false;

        string recipient = Uri.UnescapeDataString(uri.AbsoluteUri["mailto:".Length..].Split('?')[0]);
        if (recipient.Any(char.IsControl)
            || !MailAddress.TryCreate(recipient, out MailAddress? address)
            || address.DisplayName.Length > 0)
        {
            return false;
        }

        if (uri.Query.Length == 0)
            return true;

        HashSet<string> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (string field in uri.Query[1..].Split('&'))
        {
            string[] parts = field.Split('=', 2);
            string name = Uri.UnescapeDataString(parts[0]);
            if (parts.Length != 2
                || (!name.Equals("subject", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("body", StringComparison.OrdinalIgnoreCase))
                || !fields.Add(name))
            {
                return false;
            }

            string value = Uri.UnescapeDataString(parts[1]);
            bool isBody = name.Equals("body", StringComparison.OrdinalIgnoreCase);
            if (value.Any(c => char.IsControl(c) && !(isBody && c is '\r' or '\n' or '\t')))
                return false;
        }

        return true;
    }

    private static bool IsValidTelephone(Uri uri)
    {
        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return false;

        string[] parts = uri.AbsolutePath.Split(';');
        string number = parts[0];
        if (number.StartsWith('+'))
            number = number[1..];

        if (!IsTelephoneNumber(number))
            return false;

        return parts.Length == 1
            || (parts.Length == 2
                && parts[1].StartsWith("ext=", StringComparison.OrdinalIgnoreCase)
                && IsTelephoneNumber(parts[1]["ext=".Length..]));
    }

    private static bool IsTelephoneNumber(string number) =>
        number.Any(char.IsAsciiDigit)
        && number.All(c => char.IsAsciiDigit(c) || c is '-' or '.' or '(' or ')');
}
