using System.Net.Sockets;
using System.Text.RegularExpressions;
using Itmas3.Application.Agenda;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Itmas3.Infrastructure.Agenda.Smtp;

/// <summary>What a failure of the SMTP server means for the batch (ADR-0023 decision 8).</summary>
internal sealed record SmtpFailure(string ErrorCode, bool StopBatch, bool Throttled, bool AuthenticationRefused);

/// <summary>
/// Turns an exception of MailKit into a code of the closed set, decided by the enhanced status code (RFC 3463). The text of the server is
/// read here, in memory, only to find that code and the quota marker: it is never logged, stored or returned (SPEC-0008 RNF-2).
/// </summary>
internal static partial class SmtpErrorMap
{
    public static SmtpFailure Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        switch (exception)
        {
            case AuthenticationException authentication:
                return Authentication(EnhancedCode(authentication.Message));

            case SmtpCommandException command:
                return Command(command);

            case TimeoutException:
                return new SmtpFailure(OutlookErrorCodes.SmtpTimeout, StopBatch: false, Throttled: false, AuthenticationRefused: false);

            case SslHandshakeException or SmtpProtocolException or ServiceNotConnectedException or ServiceNotAuthenticatedException
                or IOException or SocketException or NotSupportedException or System.Security.Authentication.AuthenticationException:
                return new SmtpFailure(OutlookErrorCodes.SmtpUnavailable, StopBatch: false, Throttled: false, AuthenticationRefused: false);

            default:
                return new SmtpFailure(OutlookErrorCodes.SmtpUnavailable, StopBatch: false, Throttled: false, AuthenticationRefused: false);
        }
    }

    private static SmtpFailure Command(SmtpCommandException command)
    {
        var status = (int)command.StatusCode;
        var enhanced = EnhancedCode(command.Message);
        if (status == 535 || enhanced is "5.7.139" or "5.7.30" or "5.7.3")
        {
            return Authentication(enhanced);
        }

        if (command.ErrorCode == SmtpErrorCode.SenderNotAccepted || enhanced is "5.1.8" or "5.2.252" || enhanced?.StartsWith("5.7.70", StringComparison.Ordinal) == true)
        {
            return new SmtpFailure(OutlookErrorCodes.SmtpSenderRejected, StopBatch: true, Throttled: false, AuthenticationRefused: false);
        }

        var quota = enhanced is "4.3.2" or "4.4.2" or "5.1.90"
            || (enhanced == "5.2.0" && command.Message.Contains("SubmissionQuotaExceeded", StringComparison.OrdinalIgnoreCase));
        if (quota || status is >= 400 and < 500)
        {
            return new SmtpFailure(OutlookErrorCodes.SmtpThrottled, StopBatch: true, Throttled: true, AuthenticationRefused: false);
        }

        return new SmtpFailure(OutlookErrorCodes.SmtpRejected, StopBatch: false, Throttled: false, AuthenticationRefused: false);
    }

    private static SmtpFailure Authentication(string? enhanced) =>
        enhanced is "5.7.30" or "5.7.139"
            ? new SmtpFailure(OutlookErrorCodes.SmtpBasicAuthDisabled, StopBatch: true, Throttled: false, AuthenticationRefused: true)
            : new SmtpFailure(OutlookErrorCodes.SmtpAuthFailed, StopBatch: true, Throttled: false, AuthenticationRefused: true);

    /// <summary>The first enhanced status code (<c>x.y.z</c>) of the text, or null.</summary>
    internal static string? EnhancedCode(string? text) =>
        text is null ? null : EnhancedPattern().Match(text) is { Success: true } match ? match.Value : null;

    [GeneratedRegex(@"\b[245]\.\d{1,3}\.\d{1,3}\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex EnhancedPattern();
}
