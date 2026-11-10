using System.Net.Security;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Infrastructure.Agenda.Smtp;
using Itmas3.UnitTests.Application.Agenda;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Itmas3.UnitTests.Infrastructure.Agenda.Smtp;

/// <summary>
/// SPEC-0008 CA-8 ("adaptador"): the SMTP adapter over a double of the connection (ADR-0023 decisions 1, 6 to 8 and 11). Nothing
/// connects to a server; the integration test with a real SMTP server is in the integration suite.
/// </summary>
public sealed class SmtpOutlookCalendarGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventId = Guid.Parse("0196b7a0-0000-7000-8000-0000000000e2");
    private static readonly string Uid = InvitationWriter.UidOf(EventId);

    private readonly AdjustableClock _clock = new(Now);
    private readonly FakeTransportFactory _factory = new();
    private readonly SmtpThrottleGate _gate;

    public SmtpOutlookCalendarGatewayTests() => _gate = new SmtpThrottleGate(_clock);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AgendaSettings Settings(DateTimeOffset? acceptedUntil = null) => new()
    {
        TimeZone = TimeZoneInfo.Utc,
        AllowedAttendeeDomains = ["example.com"],
        OutlookEnabled = true,
        Transport = OutlookTransport.Smtp,
        SmtpAcceptedUntil = acceptedUntil,
        PublicUrl = "https://itmas.example.com",
    };

    private SmtpOutlookCalendarGateway Gateway(AgendaSettings? settings = null, TimeProvider? clock = null, TimeSpan? messageTimeout = null) =>
        new(new SmtpOutlookSettings("Agenda@Example.com", "placeholder-not-a-secret"), settings ?? Settings(), _gate, _factory, clock ?? _clock)
        {
            MessageTimeout = messageTimeout ?? TimeSpan.FromSeconds(15),
        };

    private static OutlookEventData Data(
        IReadOnlyList<string>? required = null, IReadOnlyList<string>? optional = null, IReadOnlyList<string>? removed = null, DateTimeOffset? deadline = null) =>
        new(EventId, "[Mantenimiento] Parches", "Aplicar parches", new DateTime(2026, 11, 20, 9, 0, 0), new DateTime(2026, 11, 20, 11, 0, 0),
            "America/Bogota", false, "Remoto", 60, "Mantenimiento", required ?? ["ana@example.com"], optional ?? ["equipo@example.com"])
        {
            Sequence = 9,
            StartUtc = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 11, 20, 16, 0, 0, TimeSpan.Zero),
            RemovedRecipients = removed ?? [],
            Deadline = deadline ?? Now + TimeSpan.FromSeconds(45),
        };

    private static TextPart Calendar(MimeMessage message) =>
        Assert.IsType<MultipartAlternative>(message.Body).OfType<TextPart>().Single(part => part.ContentType.MediaSubtype == "calendar");

    private static string CalendarText(MimeMessage message) => Calendar(message).Text.Replace("\r\n ", string.Empty, StringComparison.Ordinal);

    [Fact]
    public async Task An_update_sends_one_cancellation_to_the_removed_and_then_the_request_over_one_connection()
    {
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(removed: ["Luis@example.com"]), null, Ct);

        Assert.True(result.Succeeded);
        Assert.Equal(InvitationWriter.UidOf(EventId), result.OutlookEventId);
        Assert.Equal(["luis@example.com"], result.ConfirmedRemovals);
        Assert.Equal(3, result.RecipientCount);
        Assert.Equal(1, _factory.Connects);
        Assert.Equal(2, _factory.Sent.Count);

        var cancel = _factory.Sent[0];
        Assert.Equal("CANCEL", Calendar(cancel).ContentType.Parameters["method"]);
        Assert.Equal(["luis@example.com"], cancel.To.Mailboxes.Select(m => m.Address));
        Assert.Contains("SEQUENCE:8", CalendarText(cancel), StringComparison.Ordinal);
        // SPEC-0008 RF-3b (security review): the person removed gets a neutral withdrawal, nothing of the activity as it is now.
        Assert.Equal(InvitationWriter.WithdrawalSubject, cancel.Subject);
        Assert.Equal(InvitationWriter.WithdrawalText, cancel.TextBody);
        Assert.DoesNotContain("Parches", CalendarText(cancel), StringComparison.Ordinal);
        Assert.DoesNotContain("Aplicar parches", CalendarText(cancel), StringComparison.Ordinal);
        Assert.DoesNotContain("Remoto", CalendarText(cancel), StringComparison.Ordinal);
        Assert.DoesNotContain("DESCRIPTION:", CalendarText(cancel), StringComparison.Ordinal);
        Assert.DoesNotContain("URL:", CalendarText(cancel), StringComparison.Ordinal);
        Assert.DoesNotContain("CATEGORIES:", CalendarText(cancel), StringComparison.Ordinal);
        Assert.Contains("STATUS:CANCELLED", CalendarText(cancel), StringComparison.Ordinal);

        var request = _factory.Sent[1];
        Assert.Equal("REQUEST", Calendar(request).ContentType.Parameters["method"]);
        Assert.Equal(["ana@example.com"], request.To.Mailboxes.Select(m => m.Address));
        Assert.Equal(["equipo@example.com"], request.Cc.Mailboxes.Select(m => m.Address));
        Assert.Contains("SEQUENCE:9", CalendarText(request), StringComparison.Ordinal);
        Assert.Equal("agenda@example.com", request.From.Mailboxes.Single().Address);
        Assert.IsType<MultipartAlternative>(request.Body);
        Assert.Equal("utf-8", Calendar(request).ContentType.Charset);
        Assert.Equal(ContentEncoding.Base64, Calendar(request).ContentTransferEncoding);
    }

    [Fact]
    public async Task A_batch_authenticates_once_and_the_connection_is_closed_at_the_end()
    {
        var gateway = Gateway();
        await gateway.PublishAsync(Data(), null, Ct);
        await gateway.PublishAsync(Data(), null, Ct);
        await gateway.CancelAsync(Uid, "motivo", Data(required: ["ana@example.com"], optional: []) with { Sequence = 11 }, Ct);

        Assert.Equal(1, _factory.Connects);
        Assert.Equal(3, _factory.Sent.Count);

        await gateway.DisposeAsync();
        Assert.True(_factory.Created.All(transport => transport.Disposed));
        Assert.True(await _gate.TryEnterConnectionAsync(TimeSpan.Zero, Ct));
        Assert.True(await _gate.TryEnterConnectionAsync(TimeSpan.Zero, Ct));
    }

    [Theory]
    [InlineData("535 5.7.3 Authentication unsuccessful", OutlookErrorCodes.SmtpAuthFailed)]
    [InlineData("5.7.139 Authentication unsuccessful, SmtpClientAuthentication is disabled for the Tenant", OutlookErrorCodes.SmtpBasicAuthDisabled)]
    [InlineData("550 5.7.30 Basic authentication is not supported for Client Submission", OutlookErrorCodes.SmtpBasicAuthDisabled)]
    public async Task A_refused_authentication_stops_the_batch_pauses_the_process_and_is_not_retried(string serverText, string expected)
    {
        _factory.ConnectFailure = new AuthenticationException(serverText);
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(), null, Ct);
        var next = await gateway.PublishAsync(Data(), null, Ct);

        Assert.Equal(expected, result.ErrorCode);
        Assert.True(result.StopsBatch);
        Assert.True(_gate.IsPaused);
        Assert.True(next.NotAttempted);
        Assert.Equal(1, _factory.Connects);
        Assert.DoesNotContain("unsuccessful", result.ToString(), StringComparison.OrdinalIgnoreCase);

        await using var another = Gateway();
        Assert.True((await another.PublishAsync(Data(), null, Ct)).NotAttempted);
        Assert.Equal(1, _factory.Connects);
    }

    [Theory]
    [InlineData(SmtpErrorCode.SenderNotAccepted, SmtpStatusCode.MailboxUnavailable, "550 5.7.708 Access denied", OutlookErrorCodes.SmtpSenderRejected, true)]
    [InlineData(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxUnavailable, "554 5.2.252 SendAsDenied", OutlookErrorCodes.SmtpSenderRejected, true)]
    [InlineData(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxUnavailable, "550 5.1.8 Access denied, bad outbound sender", OutlookErrorCodes.SmtpSenderRejected, true)]
    [InlineData(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "554 5.2.0 STOREDRV.Submission.Exception:SubmissionQuotaExceededException", OutlookErrorCodes.SmtpThrottled, true)]
    [InlineData(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxUnavailable, "550 5.1.90 Your message can't be sent because you've reached your daily limit", OutlookErrorCodes.SmtpThrottled, true)]
    [InlineData(SmtpErrorCode.UnexpectedStatusCode, (SmtpStatusCode)432, "432 4.3.2 STOREDRV.ClientSubmit; sender thread limit exceeded", OutlookErrorCodes.SmtpThrottled, true)]
    [InlineData(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.ServiceNotAvailable, "421 4.4.2 Connection dropped due to SocketError", OutlookErrorCodes.SmtpThrottled, true)]
    [InlineData(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "554 5.6.0 Invalid message content", OutlookErrorCodes.SmtpRejected, false)]
    public async Task Each_answer_of_the_server_maps_by_its_enhanced_code(
        SmtpErrorCode errorCode, SmtpStatusCode statusCode, string text, string expected, bool stops)
    {
        _factory.SendScript.Enqueue(new SmtpCommandException(errorCode, statusCode, text));
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(), null, Ct);

        Assert.Equal(expected, result.ErrorCode);
        Assert.Equal(stops, result.StopsBatch);
        Assert.False(_gate.IsPaused);
    }

    [Theory]
    [MemberData(nameof(ConnectionFailures))]
    public async Task No_connection_or_no_valid_tls_is_unavailable(Exception failure)
    {
        _factory.ConnectFailure = failure;
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(), null, Ct);

        Assert.Equal(OutlookErrorCodes.SmtpUnavailable, result.ErrorCode);
        Assert.False(_gate.IsPaused);
    }

    public static TheoryData<Exception> ConnectionFailures() =>
    [
        new IOException("connection reset"),
        new System.Net.Sockets.SocketException(),
        new SslHandshakeException("certificate rejected"),
        new NotSupportedException("The SMTP server does not support the STARTTLS extension."),
        new SmtpProtocolException("unexpected"),
    ];

    [Fact]
    public async Task A_message_that_takes_too_long_is_a_timeout_and_the_connection_is_dropped()
    {
        _factory.SendDelay = Timeout.InfiniteTimeSpan;
        await using var gateway = Gateway(clock: TimeProvider.System, messageTimeout: TimeSpan.FromMilliseconds(50));

        var result = await gateway.PublishAsync(Data(deadline: DateTimeOffset.UtcNow.AddMinutes(1)), null, Ct);

        Assert.Equal(OutlookErrorCodes.SmtpTimeout, result.ErrorCode);
        Assert.True(_factory.Created.Single().Disposed);
    }

    [Fact]
    public async Task Without_room_before_the_deadline_nothing_starts_and_the_event_stays_pending()
    {
        await using var gateway = Gateway();

        // A new connection needs 15 s, the message 15 s and the margin 5 s: 34 s are not enough.
        var result = await gateway.PublishAsync(Data(deadline: Now + TimeSpan.FromSeconds(34)), null, Ct);

        Assert.True(result.NotAttempted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(0, _factory.Connects);
    }

    [Fact]
    public async Task With_a_connection_open_the_next_message_only_needs_the_message_and_the_margin()
    {
        await using var gateway = Gateway();
        await gateway.PublishAsync(Data(), null, Ct);

        var result = await gateway.PublishAsync(Data(deadline: Now + TimeSpan.FromSeconds(20)), null, Ct);

        Assert.True(result.Succeeded);
        Assert.Equal(2, _factory.Sent.Count);
    }

    [Fact]
    public async Task The_cancellation_to_the_removed_can_go_out_and_the_update_wait_for_the_next_sync()
    {
        _factory.OnSend = () => _clock.Set(_clock.GetUtcNow() + TimeSpan.FromSeconds(30));
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(removed: ["luis@example.com"]), null, Ct);

        Assert.True(result.NotAttempted);
        Assert.Equal(["luis@example.com"], result.ConfirmedRemovals);
        Assert.Single(_factory.Sent);
    }

    [Fact]
    public async Task Past_the_pace_of_25_messages_a_minute_a_message_that_does_not_fit_waits_for_the_next_sync()
    {
        for (var i = 0; i < SmtpThrottleGate.MessagesPerMinute; i++)
        {
            _gate.RecordMessage();
        }

        await using var gateway = Gateway();
        var result = await gateway.PublishAsync(Data(), null, Ct);

        Assert.True(result.NotAttempted);
        Assert.True(result.StopsBatch);
        Assert.Equal(0, _factory.Connects);
    }

    [Fact]
    public async Task A_recipient_refused_for_good_does_not_stop_the_message_for_the_others()
    {
        _factory.SendScript.Enqueue(new[] { "equipo@example.com" });
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(), null, Ct);

        Assert.True(result.Succeeded);
        Assert.Equal(["equipo@example.com"], result.RejectedRecipients);
    }

    [Fact]
    public async Task A_cancellation_goes_to_whoever_may_hold_the_meeting_with_the_reason()
    {
        await using var gateway = Gateway();

        var result = await gateway.CancelAsync(Uid, "Se movió a enero", Data(required: ["ana@example.com", "luis@example.com"], optional: []) with { Sequence = 13 }, Ct);

        var message = Assert.Single(_factory.Sent);
        Assert.Equal("CANCEL", Calendar(message).ContentType.Parameters["method"]);
        Assert.Contains("SEQUENCE:13", CalendarText(message), StringComparison.Ordinal);
        Assert.Contains("COMMENT:Se movió a enero", CalendarText(message), StringComparison.Ordinal);
        Assert.Contains("Motivo: Se movió a enero", message.TextBody, StringComparison.Ordinal);
        Assert.Equal(["ana@example.com", "luis@example.com"], result.ConfirmedRemovals);
    }

    [Fact]
    public async Task A_cancellation_with_nobody_to_tell_is_done_without_connecting()
    {
        await using var gateway = Gateway();

        var result = await gateway.CancelAsync(Uid, "motivo", Data(required: [], optional: []), Ct);

        Assert.True(result.Succeeded);
        Assert.Equal(0, _factory.Connects);
    }

    [Fact]
    public async Task Addresses_that_break_the_rule_are_left_out_and_counted()
    {
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(required: ["ana@example.com", "x;y@example.com"], optional: []), null, Ct);

        Assert.Equal(1, result.OmittedCount);
        Assert.Equal(["ana@example.com"], _factory.Sent.Single().To.Mailboxes.Select(m => m.Address));
    }

    [Fact]
    public async Task After_the_expiry_of_the_acceptance_it_answers_not_configured_and_connects_to_nothing()
    {
        await using var gateway = Gateway(Settings(acceptedUntil: Now));

        Assert.Equal(OutlookErrorCodes.NotConfigured, (await gateway.PublishAsync(Data(), null, Ct)).ErrorCode);
        Assert.Equal(OutlookErrorCodes.NotConfigured, (await gateway.CancelAsync(Uid, "motivo", Data(), Ct)).ErrorCode);
        Assert.Equal(0, _factory.Connects);
    }

    [Fact]
    public void The_settings_never_print_the_password_and_the_host_is_fixed()
    {
        var settings = new SmtpOutlookSettings("agenda@example.com", "placeholder-not-a-secret");

        Assert.DoesNotContain("placeholder", settings.ToString(), StringComparison.Ordinal);
        Assert.Equal("smtp.office365.com", settings.Host);
        Assert.Equal(587, settings.Port);
        Assert.False(typeof(SmtpOutlookSettings).GetProperty(nameof(SmtpOutlookSettings.Host))!.CanWrite);
    }

    [Fact]
    public void The_production_factory_validates_strictly_and_only_tests_can_trust_another_ca()
    {
        Assert.False(new MailKitSmtpTransportFactory().UsesTestTrust);
        Assert.True(MailKitSmtpTransportFactory.Strict(this, null, null, SslPolicyErrors.None));
        Assert.False(MailKitSmtpTransportFactory.Strict(this, null, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(MailKitSmtpTransportFactory.Strict(this, null, null, SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void The_adapter_has_no_logger_so_nothing_of_the_server_can_be_logged()
    {
        var parameters = typeof(SmtpOutlookCalendarGateway).GetConstructors().Single().GetParameters();

        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType.Name.Contains("Logger", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Several_people_removed_get_one_single_cancellation()
    {
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(removed: ["luis@example.com", "marta@example.com"]), null, Ct);

        Assert.Equal(2, _factory.Sent.Count);
        var cancel = _factory.Sent[0];
        Assert.Equal("CANCEL", Calendar(cancel).ContentType.Parameters["method"]);
        Assert.Equal(["luis@example.com", "marta@example.com"], cancel.To.Mailboxes.Select(m => m.Address));
        Assert.Equal(2, CalendarText(cancel).Split('\n').Count(line => line.StartsWith("ATTENDEE", StringComparison.Ordinal)));
        Assert.Equal(["luis@example.com", "marta@example.com"], result.ConfirmedRemovals);
    }

    [Fact]
    public async Task A_cancellation_refused_by_the_server_does_not_block_the_update_of_the_others()
    {
        _factory.SendScript.Enqueue(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "554 5.6.0 Invalid content"));
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(removed: ["luis@example.com"]), null, Ct);

        Assert.Equal(OutlookErrorCodes.SmtpRejected, result.ErrorCode);
        Assert.Empty(result.ConfirmedRemovals);
        var request = Assert.Single(_factory.Sent);
        Assert.Equal("REQUEST", Calendar(request).ContentType.Parameters["method"]);
        Assert.Equal(1, _factory.Connects);
    }

    [Fact]
    public async Task Removed_addresses_the_server_refuses_for_good_are_forgotten_so_they_never_block_again()
    {
        _factory.SendScript.Enqueue(new[] { "luis@example.com" });
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(removed: ["luis@example.com"]), null, Ct);

        Assert.True(result.Succeeded);
        Assert.Equal(["luis@example.com"], result.ConfirmedRemovals);
        Assert.Equal(2, _factory.Sent.Count);
    }

    [Fact]
    public async Task An_update_that_reaches_nobody_is_rejected_and_reports_the_refused_addresses()
    {
        _factory.SendScript.Enqueue(new[] { "ana@example.com", "equipo@example.com" });
        await using var gateway = Gateway();

        var result = await gateway.PublishAsync(Data(), null, Ct);

        Assert.Equal(OutlookErrorCodes.SmtpRejected, result.ErrorCode);
        Assert.Equal(["ana@example.com", "equipo@example.com"], result.RejectedRecipients);
    }

    [Fact]
    public async Task A_cancellation_the_server_refuses_for_every_address_is_done_because_nobody_can_hold_the_meeting()
    {
        _factory.SendScript.Enqueue(new[] { "ana@example.com" });
        await using var gateway = Gateway();

        var result = await gateway.CancelAsync(Uid, "motivo", Data(required: ["ana@example.com"], optional: []), Ct);

        Assert.True(result.Succeeded);
        Assert.Equal(["ana@example.com"], result.ConfirmedRemovals);
    }

    [Fact]
    public async Task A_refusal_of_the_server_keeps_the_connection_and_counts_in_the_pace()
    {
        _factory.SendScript.Enqueue(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "554 5.6.0 Invalid content"));
        await using var gateway = Gateway();

        await gateway.PublishAsync(Data(), null, Ct);
        await gateway.PublishAsync(Data(), null, Ct);

        Assert.Equal(1, _factory.Connects);
        for (var i = 0; i < SmtpThrottleGate.MessagesPerMinute - 2; i++)
        {
            _gate.RecordMessage();
        }

        Assert.NotEqual(TimeSpan.Zero, _gate.DelayBeforeNextMessage());
    }

    [Fact]
    public async Task A_timeout_of_the_socket_is_smtp_timeout()
    {
        _factory.SendScript.Enqueue(new TimeoutException("socket"));
        await using var gateway = Gateway();

        Assert.Equal(OutlookErrorCodes.SmtpTimeout, (await gateway.PublishAsync(Data(), null, Ct)).ErrorCode);
    }

    [Fact]
    public async Task An_event_published_with_graph_is_rejected_and_never_sent_by_e_mail()
    {
        await using var gateway = Gateway();

        var update = await gateway.PublishAsync(Data(), "AAMkGraphId", Ct);
        var cancel = await gateway.CancelAsync("AAMkGraphId", "motivo", Data(), Ct);

        Assert.Equal(OutlookErrorCodes.OutlookRejected, update.ErrorCode);
        Assert.Equal(OutlookErrorCodes.OutlookRejected, cancel.ErrorCode);
        Assert.Equal(0, _factory.Connects);
    }

    [Fact]
    public async Task Text_and_addresses_cannot_inject_mime_headers()
    {
        await using var gateway = Gateway();
        var data = Data(required: ["ana@example.com", "x@example.com\r\nBcc: evil@example.org"], optional: ["equipo@example.com\r\nBcc: evil@example.org"]) with
        {
            Subject = "Parches\r\nBcc: evil@example.org",
            BodyText = "Cuerpo\r\nBcc: evil@example.org",
        };

        await gateway.PublishAsync(data, null, Ct);

        var message = Assert.Single(_factory.Sent);
        using var raw = new MemoryStream();
        await message.WriteToAsync(raw, Ct);
        // The header block ends at the first empty line, whatever line end MimeKit writes.
        var text = System.Text.Encoding.ASCII.GetString(raw.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
        var headers = text[..text.IndexOf("\n\n", StringComparison.Ordinal)].Split('\n');
        Assert.DoesNotContain(headers, line => line.StartsWith("Bcc:", StringComparison.OrdinalIgnoreCase));
        Assert.Single(headers, line => line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["ana@example.com"], message.To.Mailboxes.Select(m => m.Address));
        Assert.Empty(message.Cc.Mailboxes);
        Assert.Empty(message.Bcc.Mailboxes);
    }

    /// <summary>Connections that answer from a script and record every message. They never open a socket.</summary>
    private sealed class FakeTransportFactory : ISmtpTransportFactory
    {
        public List<FakeTransport> Created { get; } = [];

        public List<MimeMessage> Sent { get; } = [];

        /// <summary>Each item is an exception to throw or the recipients refused for good.</summary>
        public Queue<object> SendScript { get; } = new();

        public Exception? ConnectFailure { get; set; }

        public TimeSpan SendDelay { get; set; }

        public Action? OnSend { get; set; }

        public int Connects { get; set; }

        public ISmtpTransport Create()
        {
            var transport = new FakeTransport(this);
            Created.Add(transport);
            return transport;
        }
    }

    private sealed class FakeTransport(FakeTransportFactory factory) : ISmtpTransport
    {
        public bool IsConnected { get; private set; }

        public bool Disposed { get; private set; }

        public Task ConnectAsync(SmtpOutlookSettings settings, CancellationToken cancellationToken)
        {
            factory.Connects++;
            if (factory.ConnectFailure is { } failure)
            {
                throw failure;
            }

            IsConnected = true;
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<string>> SendAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            if (factory.SendDelay != TimeSpan.Zero)
            {
                await Task.Delay(factory.SendDelay, cancellationToken);
            }

            factory.OnSend?.Invoke();
            var next = factory.SendScript.Count > 0 ? factory.SendScript.Dequeue() : Array.Empty<string>();
            if (next is Exception exception)
            {
                throw exception;
            }

            factory.Sent.Add(message);
            return (string[])next;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
