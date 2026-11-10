using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;
using static Itmas3.UnitTests.Domain.Agenda.AgendaTestData;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>SPEC-0008 RF-2b, CA-8: the strict address rule of the agenda, at save time and as the writer applies it.</summary>
public sealed class AgendaEmailTests
{
    [Theory]
    [InlineData("ana.perez@example.com", "ana.perez@example.com")]
    [InlineData("  Ana.Perez@Example.COM ", "ana.perez@example.com")]
    [InlineData("soporte+agenda@mail.example.com", "soporte+agenda@mail.example.com")]
    [InlineData("a_b-c@example-1.co", "a_b-c@example-1.co")]
    public void A_valid_address_is_trimmed_and_lowercased(string raw, string expected) =>
        Assert.Equal(expected, AgendaEmail.Normalize(raw));

    [Theory]
    [InlineData("ana;evil@example.com")]
    [InlineData("ana:x@example.com")]
    [InlineData("ana,x@example.com")]
    [InlineData("\"ana\"@example.com")]
    [InlineData("<ana>@example.com")]
    [InlineData("ana?subject=x@example.com")]
    [InlineData("ana%40x@example.com")]
    [InlineData("ana&x@example.com")]
    [InlineData("ana#x@example.com")]
    [InlineData("josé@example.com")]
    [InlineData(".ana@example.com")]
    [InlineData("ana.@example.com")]
    [InlineData("ana..perez@example.com")]
    [InlineData("ana@example")]
    [InlineData("ana@-example.com")]
    [InlineData("ana@exa_mple.com")]
    [InlineData("ana@@example.com")]
    [InlineData("ana@exa@mple.com")]
    [InlineData("ana@example.com\r\nBcc: x@evil.example")]
    [InlineData("ana\n@example.com")]
    [InlineData("ana@example.com\n.org")]
    [InlineData("ana@example\n.com")]
    [InlineData("ana @example.com")]
    [InlineData("@example.com")]
    [InlineData("")]
    [InlineData(null)]
    public void An_address_that_breaks_the_rule_is_refused(string? raw)
    {
        Assert.Null(AgendaEmail.Normalize(raw));
        Assert.False(AgendaEmail.IsValid(raw));
    }

    [Fact]
    public void The_whole_address_has_at_most_254_characters_and_the_local_part_64()
    {
        var domain = string.Join('.', Enumerable.Repeat(new string('d', 60), 3)) + ".example.com"; // 194 characters
        var local = new string('a', 254 - domain.Length - 1);
        Assert.Equal(59, local.Length);
        Assert.NotNull(AgendaEmail.Normalize($"{local}@{domain}"));
        Assert.Null(AgendaEmail.Normalize($"{local}a@{domain}"));
        Assert.Null(AgendaEmail.Normalize($"{new string('a', 65)}@example.com"));
    }

    [Fact]
    public void Saving_a_participant_that_breaks_the_rule_is_refused_with_a_message_that_names_it()
    {
        var exception = Assert.Throws<DomainException>(() => Details(participants: ["luis;evil@example.com"]));

        Assert.Contains("«luis;evil@example.com»", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rejected_value_is_quoted_without_control_characters_and_cut()
    {
        var exception = Assert.Throws<DomainException>(() => Details(participants: ["x\r\ny" + new string('z', 200) + "@example.com"]));

        Assert.DoesNotContain('\r', exception.Message);
        Assert.DoesNotContain('\n', exception.Message);
        Assert.Contains("…", exception.Message, StringComparison.Ordinal);
    }
}
