using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>SPEC-0006 RN-15 and CA-25: normalization of event tags.</summary>
public sealed class TagNameTests
{
    [Theory]
    [InlineData("windows", "windows")]
    [InlineData("  producción  ", "producción")]
    [InlineData("iso   27001", "iso 27001")]
    [InlineData("sql  server", "sql server")]
    public void Names_are_trimmed_and_repeated_spaces_collapse(string raw, string expected) =>
        Assert.Equal(expected, TagName.Normalize(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("a\tb")]
    [InlineData("a\u0007")]
    public void Names_are_required_and_free_of_control_characters(string? raw) =>
        Assert.Throws<DomainException>(() => TagName.Normalize(raw));

    [Fact]
    public void Names_admit_up_to_30_characters()
    {
        Assert.Equal(30, TagName.Normalize(new string('a', 30)).Length);
        Assert.Throws<DomainException>(() => TagName.Normalize(new string('a', 31)));
    }

    [Fact]
    public void The_length_is_measured_after_normalizing()
    {
        var raw = "  " + new string('a', 30) + "   ";

        Assert.Equal(30, TagName.Normalize(raw).Length);
    }

    [Fact]
    public void Tags_that_differ_only_in_case_are_one_and_the_first_spelling_wins()
    {
        var tags = TagName.NormalizeSet(["Windows", " windows ", "WINDOWS", "Producción"]);

        Assert.Equal(["Windows", "Producción"], tags);
    }

    [Fact]
    public void An_event_admits_ten_tags_but_not_eleven()
    {
        Assert.Equal(10, TagName.NormalizeSet(Enumerable.Range(1, 10).Select(i => $"t{i}")).Count);
        Assert.Throws<DomainException>(() => TagName.NormalizeSet(Enumerable.Range(1, 11).Select(i => $"t{i}")));
    }

    [Fact]
    public void Repeated_tags_do_not_count_towards_the_maximum()
    {
        var raw = Enumerable.Range(1, 10).Select(i => $"t{i}").Append("T1").Append("t2");

        Assert.Equal(10, TagName.NormalizeSet(raw).Count);
    }

    [Fact]
    public void No_tags_is_valid() => Assert.Empty(TagName.NormalizeSet(null));

    [Fact]
    public void The_key_ignores_case() => Assert.Equal(TagName.Key("SQL Server"), TagName.Key("sql server"));

    [Fact]
    public void The_key_treats_the_capital_dotted_i_like_the_database_does_so_both_spellings_are_one_tag()
    {
        Assert.Equal(TagName.Key("istanbul"), TagName.Key("\u0130stanbul"));
        Assert.Equal(["\u0130stanbul"], TagName.NormalizeSet(["\u0130stanbul", "istanbul"]));
    }
}
