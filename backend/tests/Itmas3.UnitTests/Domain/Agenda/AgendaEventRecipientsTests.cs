using Itmas3.Domain.Agenda;
using static Itmas3.UnitTests.Domain.Agenda.AgendaTestData;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>
/// SPEC-0008 RF-4, CA-8 ("lista"): who may hold the meeting with the SMTP transport, and the answers of Outlook that never go back.
/// </summary>
public sealed class AgendaEventRecipientsTests
{
    private const string Uid = "uid@invite.agenda.itmas";

    private static AgendaEvent Scheduled()
    {
        var agendaEvent = Draft();
        agendaEvent.Schedule(Now, Bogota, Actor);
        return agendaEvent;
    }

    [Fact]
    public void The_write_ahead_fixes_the_uid_and_adds_normalized_sorted_recipients_and_returns_only_the_new_ones()
    {
        var agendaEvent = Scheduled();

        var first = agendaEvent.RecordInvitationRecipients(Uid, ["Luis@Example.com", "ana@example.com"]);
        var second = agendaEvent.RecordInvitationRecipients(Uid, ["ana@example.com", "marta@example.com"]);

        Assert.Equal(Uid, agendaEvent.OutlookEventId);
        Assert.Equal(["luis@example.com", "ana@example.com"], first);
        Assert.Equal(["marta@example.com"], second);
        Assert.Equal(["ana@example.com", "luis@example.com", "marta@example.com"], agendaEvent.OutlookSentRecipients);
    }

    [Fact]
    public void Nobody_to_write_ahead_keeps_the_list_null_but_fixes_the_uid()
    {
        var agendaEvent = Scheduled();

        agendaEvent.RecordInvitationRecipients(Uid, []);

        Assert.Equal(Uid, agendaEvent.OutlookEventId);
        Assert.Null(agendaEvent.OutlookSentRecipients);
    }

    [Fact]
    public void An_event_published_with_another_transport_is_not_written_ahead()
    {
        var agendaEvent = Scheduled();
        agendaEvent.MarkOutlookPublished("AAMkGraphId", null, agendaEvent.Revision, Now);

        Assert.Throws<InvalidOperationException>(() => agendaEvent.RecordInvitationRecipients(Uid, ["ana@example.com"]));
    }

    [Fact]
    public void The_second_write_removes_only_with_the_current_revision_and_never_leaves_an_empty_list()
    {
        var agendaEvent = Scheduled();
        agendaEvent.RecordInvitationRecipients(Uid, ["ana@example.com", "luis@example.com"]);

        agendaEvent.ForgetInvitationRecipients(agendaEvent.Revision - 1, ["ana@example.com"]);
        Assert.Equal(["ana@example.com", "luis@example.com"], agendaEvent.OutlookSentRecipients);

        agendaEvent.ForgetInvitationRecipients(agendaEvent.Revision, ["ANA@example.com"]);
        Assert.Equal(["luis@example.com"], agendaEvent.OutlookSentRecipients);

        agendaEvent.ForgetInvitationRecipients(agendaEvent.Revision, ["luis@example.com"]);
        Assert.Null(agendaEvent.OutlookSentRecipients);
        Assert.Equal(Uid, agendaEvent.OutlookEventId);
    }

    [Fact]
    public void A_confirmed_cancellation_forgets_the_recipients_and_a_cancelled_event_never_adds_again()
    {
        var agendaEvent = Scheduled();
        agendaEvent.RecordInvitationRecipients(Uid, ["ana@example.com"]);
        agendaEvent.Cancel("Ya no aplica", Now, Actor);

        Assert.Empty(agendaEvent.RecordInvitationRecipients(Uid, ["luis@example.com"]));
        Assert.Equal(["ana@example.com"], agendaEvent.OutlookSentRecipients);

        agendaEvent.MarkOutlookPublished(Uid, null, agendaEvent.Revision, Now);
        Assert.Null(agendaEvent.OutlookSentRecipients);
    }

    [Fact]
    public void Completing_the_activity_forgets_the_recipients_and_never_adds_again()
    {
        var agendaEvent = Scheduled();
        agendaEvent.RecordInvitationRecipients(Uid, ["ana@example.com"]);

        agendaEvent.Complete(CompletionResult.Succeeded, null, null, null, agendaEvent.StartsAtUtc.AddHours(1), Actor);

        Assert.Null(agendaEvent.OutlookSentRecipients);
        Assert.Empty(agendaEvent.RecordInvitationRecipients(Uid, ["ana@example.com"]));
        Assert.Null(agendaEvent.OutlookSentRecipients);
    }

    [Fact]
    public void A_late_answer_for_an_older_revision_never_undoes_a_confirmed_cancellation()
    {
        var agendaEvent = Scheduled();
        var published = agendaEvent.Revision;
        agendaEvent.RecordInvitationRecipients(Uid, ["ana@example.com"]);
        agendaEvent.Cancel("Ya no aplica", Now, Actor);
        agendaEvent.MarkOutlookPublished(Uid, null, agendaEvent.Revision, Now);

        // The answer of the REQUEST arrives after the cancellation was confirmed.
        agendaEvent.MarkOutlookPublished(Uid, null, published, Now.AddSeconds(1));
        agendaEvent.MarkOutlookFailed("smtp_timeout", Now.AddSeconds(2), published);
        agendaEvent.MarkOutlookPending(published);

        Assert.True(agendaEvent.OutlookIsCurrent);
        Assert.Equal(OutlookStatus.Published, agendaEvent.OutlookStatus);
        Assert.Equal(agendaEvent.Revision, agendaEvent.OutlookSyncedRevision);
        Assert.False(agendaEvent.NeedsOutlookCancellation);
        Assert.Null(agendaEvent.OutlookErrorCode);
    }

    [Fact]
    public void Without_a_revision_the_marks_behave_as_before()
    {
        var agendaEvent = Scheduled();
        agendaEvent.MarkOutlookPublished(Uid, null, agendaEvent.Revision, Now);

        agendaEvent.MarkOutlookFailed("smtp_unavailable", Now);

        Assert.Equal(OutlookStatus.Error, agendaEvent.OutlookStatus);
        Assert.Equal("smtp_unavailable", agendaEvent.OutlookErrorCode);
    }
}
