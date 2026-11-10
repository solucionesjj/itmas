using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Outlook;
using MimeKit;
using MimeKit.Text;

namespace Itmas3.Infrastructure.Agenda.Smtp;

/// <summary>
/// The Outlook port over SMTP of Microsoft 365 (SPEC-0008, ADR-0023): iCalendar invitations sent as e-mail. Scoped: one instance per
/// request, so one batch opens one connection and authenticates once, lazily on the first message, and closes it when the scope ends.
/// <list type="bullet">
/// <item>An update is at most two messages: one <c>CANCEL</c> to whoever was removed (<c>SEQUENCE = 2·revision</c>) and the
/// <c>REQUEST</c> to the attendees (<c>2·revision + 1</c>).</item>
/// <item>A message starts only if the connection (when none is open), the message and a margin fit before the deadline of the batch, and
/// the pace of <see cref="SmtpThrottleGate"/> allows it; otherwise the answer is "not attempted" and the event stays Pending.</item>
/// <item>A refused authentication stops the batch and pauses the process; nothing of the server reaches a logger.</item>
/// <item>From the expiry of the accepted exception (SPEC-0008 RF-7) it answers <c>not_configured</c> and sends nothing.</item>
/// </list>
/// </summary>
internal sealed class SmtpOutlookCalendarGateway(
    SmtpOutlookSettings smtp,
    AgendaSettings agenda,
    SmtpThrottleGate gate,
    ISmtpTransportFactory factory,
    TimeProvider timeProvider) : IOutlookCalendarGateway, IAsyncDisposable, IDisposable
{
    /// <summary>Most time to connect, encrypt and authenticate (RF-6). Only the tests change it.</summary>
    internal TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Most time to send one message (RF-6). Only the tests change it.</summary>
    internal TimeSpan MessageTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Margin a message must leave before the deadline (RF-6).</summary>
    internal TimeSpan Margin { get; init; } = TimeSpan.FromSeconds(5);

    private ISmtpTransport? _transport;
    private bool _holdsConnection;
    private bool _authenticationRefused;

    public async Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        var uid = InvitationWriter.UidOf(data.EventId);
        if (!agenda.IsOutlookActive(timeProvider.GetUtcNow()))
        {
            return OutlookGatewayResult.Failure(OutlookErrorCodes.NotConfigured);
        }

        if (outlookEventId is not null && !string.Equals(outlookEventId, uid, StringComparison.Ordinal))
        {
            // Published with the other transport: it cannot be updated by e-mail (ADR-0023 decision 10).
            return OutlookGatewayResult.Failure(OutlookErrorCodes.OutlookRejected);
        }

        var (removed, omittedRemoved) = OutlookRecipients.Normalize(data.RemovedRecipients);
        var (required, omittedRequired) = OutlookRecipients.Normalize(data.RequiredAttendees);
        var (optionalAll, omittedOptional) = OutlookRecipients.Normalize(data.OptionalAttendees);
        var optional = optionalAll.Where(address => !required.Contains(address, StringComparer.Ordinal)).ToList();
        var omitted = omittedRemoved + omittedRequired + omittedOptional;
        var total = removed.Count + required.Count + optional.Count;

        // Whoever was removed gets a neutral cancellation first (RF-3b). Once the server took it, they leave the list: the ones it
        // refused for good have no mailbox that could hold the meeting. A failure that does not stop the batch does not block the
        // update of the others; the event ends in Error and "Sincronizar de nuevo" sends the cancellation again.
        IReadOnlyList<string> forgotten = [];
        SendOutcome? cancellationFailure = null;
        if (removed.Count > 0)
        {
            var cancellation = await SendAsync(InvitationMethod.Cancel, data, removed, [], data.Sequence - 1, withdrawal: true, cancellationToken);
            if (cancellation.Postponed || (!cancellation.Sent && cancellation.StopBatch))
            {
                return Outcome(uid, cancellation, [], total, omitted);
            }

            if (cancellation.Sent)
            {
                forgotten = removed;
            }
            else
            {
                cancellationFailure = cancellation;
            }
        }

        if (required.Count + optional.Count == 0)
        {
            // Every attendee was removed or left out: there is nobody to send the update to.
            return cancellationFailure is null
                ? OutlookGatewayResult.Success(uid, null) with { ConfirmedRemovals = forgotten, RecipientCount = total, OmittedCount = omitted }
                : Outcome(uid, cancellationFailure, [], total, omitted);
        }

        var request = await SendAsync(InvitationMethod.Request, data, required, optional, data.Sequence, withdrawal: false, cancellationToken);
        if (request.Sent && request.Rejected.Count >= required.Count + optional.Count)
        {
            // The server refused every attendee: nobody got the update.
            request = SendOutcome.Failed(OutlookErrorCodes.SmtpRejected, stopBatch: false, throttled: false) with { Rejected = request.Rejected };
        }

        if (cancellationFailure is not null && request.Sent)
        {
            return Outcome(uid, cancellationFailure, [], total, omitted) with { RejectedRecipients = request.Rejected };
        }

        return Outcome(uid, request, forgotten, total, omitted);
    }

    public async Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, OutlookEventData? data, CancellationToken cancellationToken)
    {
        if (data is null)
        {
            return OutlookGatewayResult.Failure(OutlookErrorCodes.OutlookRejected);
        }

        var uid = InvitationWriter.UidOf(data.EventId);
        if (!agenda.IsOutlookActive(timeProvider.GetUtcNow()))
        {
            return OutlookGatewayResult.Failure(OutlookErrorCodes.NotConfigured);
        }

        if (!string.Equals(outlookEventId, uid, StringComparison.Ordinal))
        {
            // Published with the other transport: an e-mail cancellation would reach nobody, so it is never reported as done.
            return OutlookGatewayResult.Failure(OutlookErrorCodes.OutlookRejected);
        }

        var (recipients, omitted) = OutlookRecipients.Normalize(data.RequiredAttendees);
        if (recipients.Count == 0)
        {
            // Nobody may hold the meeting: the cancellation is as done as it can be.
            return OutlookGatewayResult.Success(uid, null) with { OmittedCount = omitted };
        }

        var withReason = data with { CancelReason = string.IsNullOrWhiteSpace(reason) ? data.CancelReason : reason };
        var outcome = await SendAsync(InvitationMethod.Cancel, withReason, recipients, [], data.Sequence, withdrawal: false, cancellationToken);

        // Taken by the server: nobody may hold the meeting any more (the ones refused for good have no mailbox for it).
        return Outcome(uid, outcome, outcome.Sent ? recipients : [], recipients.Count, omitted);
    }

    /// <summary>For a scope disposed synchronously: closes the connection the same way, waiting at most the 5 s of the QUIT.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        if (_holdsConnection)
        {
            _holdsConnection = false;
            gate.ReleaseConnection();
        }
    }

    private static OutlookGatewayResult Outcome(string uid, SendOutcome outcome, IReadOnlyList<string> confirmed, int recipients, int omitted)
    {
        var result = outcome switch
        {
            { Sent: true } => OutlookGatewayResult.Success(uid, null) with { RejectedRecipients = outcome.Rejected },
            { Postponed: true } => OutlookGatewayResult.Postponed(uid, outcome.StopBatch),
            _ => new OutlookGatewayResult(uid, null, outcome.ErrorCode, outcome.Throttled) { StopBatch = outcome.StopBatch, RejectedRecipients = outcome.Rejected },
        };
        return result with { ConfirmedRemovals = confirmed, RecipientCount = recipients, OmittedCount = omitted };
    }

    /// <summary>One message, within the deadline and the pace, over the connection of this batch.</summary>
    private async Task<SendOutcome> SendAsync(
        InvitationMethod method,
        OutlookEventData data,
        IReadOnlyList<string> to,
        IReadOnlyList<string> cc,
        int sequence,
        bool withdrawal,
        CancellationToken cancellationToken)
    {
        if (_authenticationRefused || gate.IsPaused)
        {
            return SendOutcome.Later(stopBatch: true);
        }

        if (!Fits(data.Deadline, TimeSpan.Zero))
        {
            return SendOutcome.Later(stopBatch: false);
        }

        var delay = gate.DelayBeforeNextMessage();
        if (delay > TimeSpan.Zero)
        {
            if (!Fits(data.Deadline, delay))
            {
                return SendOutcome.Later(stopBatch: true);
            }

            await Task.Delay(delay, timeProvider, cancellationToken);
        }

        var submitting = false;
        try
        {
            if (_transport is not { IsConnected: true })
            {
                await ResetAsync();
                if (!_holdsConnection)
                {
                    var wait = data.Deadline is { } deadline ? deadline - timeProvider.GetUtcNow() - ConnectTimeout - MessageTimeout - Margin : ConnectTimeout;
                    if (!await gate.TryEnterConnectionAsync(wait, cancellationToken))
                    {
                        return SendOutcome.Later(stopBatch: true);
                    }

                    _holdsConnection = true;
                }

                _transport = factory.Create();
                using var connectTimeout = new CancellationTokenSource(ConnectTimeout, timeProvider);
                using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectTimeout.Token);
                await _transport.ConnectAsync(smtp, connect.Token);
            }

            var message = Build(method, data, to, cc, sequence, withdrawal);
            using var messageTimeout = new CancellationTokenSource(MessageTimeout, timeProvider);
            using var send = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, messageTimeout.Token);
            submitting = true;
            var rejected = await _transport.SendAsync(message, send.Token);
            gate.RecordMessage();
            return SendOutcome.Done(rejected);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await ResetAsync();
            return SendOutcome.Failed(OutlookErrorCodes.SmtpTimeout, stopBatch: false, throttled: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing of the exception is logged: its message carries the text of the server and addresses (RNF-2).
            var failure = SmtpErrorMap.Map(exception);
            if (submitting)
            {
                // The message reached the server, which counts it against the limits of the mailbox whatever it answered.
                gate.RecordMessage();
            }

            if (failure.AuthenticationRefused)
            {
                _authenticationRefused = true;
                gate.PauseAfterAuthenticationFailure();
            }

            // A refusal of the server leaves the session usable: the batch keeps its one connection and one authentication (RF-6).
            // Anything else (transport, TLS, authentication) drops it.
            if (exception is not MailKit.Net.Smtp.SmtpCommandException || failure.AuthenticationRefused || _transport is not { IsConnected: true })
            {
                await ResetAsync();
            }

            return SendOutcome.Failed(failure.ErrorCode, failure.StopBatch, failure.Throttled);
        }
    }

    /// <summary>Whether a message started after <paramref name="wait"/> still fits before the deadline (RF-6).</summary>
    private bool Fits(DateTimeOffset? deadline, TimeSpan wait)
    {
        if (deadline is not { } limit)
        {
            return true;
        }

        var needed = wait + (_transport is { IsConnected: true } ? TimeSpan.Zero : ConnectTimeout) + MessageTimeout + Margin;
        return timeProvider.GetUtcNow() + needed <= limit;
    }

    /// <summary>
    /// The message. A withdrawal (the cancellation to whoever was removed, RF-3b) is neutral: it says nothing of the activity as it
    /// is now, so the person removed never sees a title, a description or a place they were taken out of.
    /// </summary>
    private MimeMessage Build(InvitationMethod method, OutlookEventData data, IReadOnlyList<string> to, IReadOnlyList<string> cc, int sequence, bool withdrawal)
    {
        var now = timeProvider.GetUtcNow();
        var message = new MimeMessage { Date = now };
        message.From.Add(MailboxAddress.Parse(smtp.Username));
        message.To.AddRange(to.Select(MailboxAddress.Parse));
        message.Cc.AddRange(cc.Select(MailboxAddress.Parse));
        message.Subject = withdrawal
            ? InvitationWriter.WithdrawalSubject
            : method == InvitationMethod.Cancel ? $"Cancelada: {data.Subject}" : data.Subject;

        var text = withdrawal
            ? InvitationWriter.WithdrawalText
            : method == InvitationMethod.Cancel
                ? $"Se canceló esta actividad.{(string.IsNullOrWhiteSpace(data.CancelReason) ? string.Empty : $"\n\nMotivo: {data.CancelReason}")}\n\n{data.BodyText}"
                : data.BodyText;
        var plain = new TextPart(TextFormat.Plain) { Text = text };
        plain.ContentType.Charset = "utf-8";

        var calendar = new TextPart("calendar")
        {
            Text = withdrawal
                ? InvitationWriter.WriteWithdrawal(data, smtp.Username, to, sequence, now)
                : InvitationWriter.Write(method, data, smtp.Username, to, cc, sequence, now, agenda.PublicUrl),
            ContentTransferEncoding = ContentEncoding.Base64,
        };
        calendar.ContentType.Charset = "utf-8";
        calendar.ContentType.Parameters.Add("method", InvitationWriter.MethodName(method));

        message.Body = new MultipartAlternative { plain, calendar };
        return message;
    }

    private async Task ResetAsync()
    {
        if (_transport is { } transport)
        {
            _transport = null;
            await transport.DisposeAsync();
        }
    }

    private sealed record SendOutcome(bool Sent, bool Postponed, string? ErrorCode, bool StopBatch, bool Throttled, IReadOnlyList<string> Rejected)
    {
        public static SendOutcome Done(IReadOnlyList<string> rejected) => new(true, false, null, false, false, rejected);

        public static SendOutcome Later(bool stopBatch) => new(false, true, null, stopBatch, false, []);

        public static SendOutcome Failed(string errorCode, bool stopBatch, bool throttled) => new(false, false, errorCode, stopBatch, throttled, []);
    }
}
