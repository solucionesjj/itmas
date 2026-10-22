using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>SPEC-0006 RN-10 and CA-9: evidence links.</summary>
public sealed class EvidenceLinkTests
{
    [Theory]
    [InlineData("https://evidencia.example.com/informe.pdf")]
    [InlineData("http://wiki.example.com/pagina?x=1#a")]
    [InlineData("  https://example.com/x  ")]
    public void Http_and_https_links_are_accepted(string url)
    {
        var link = EvidenceLink.Create("Informe", url);

        Assert.Equal(url.Trim(), link.Url);
    }

    [Theory]
    [InlineData("ftp://example.com/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hola")]
    [InlineData("//example.com/x")]
    [InlineData("example.com/x")]
    [InlineData("https://")]
    [InlineData("https://exa mple.com")]
    [InlineData("https://example.com/\u0007")]
    public void Other_schemes_and_malformed_urls_are_rejected(string url) =>
        Assert.Throws<DomainException>(() => EvidenceLink.Create("Informe", url));

    [Theory]
    [InlineData("https://usuario:clave@example.com/x")]
    [InlineData("https://usuario@example.com/x")]
    [InlineData("https://@example.com/x")]
    [InlineData("http://a:b@example.com")]
    public void Embedded_credentials_are_rejected(string url) =>
        Assert.Throws<DomainException>(() => EvidenceLink.Create("Informe", url));

    [Fact]
    public void An_arroba_after_the_authority_is_not_a_credential() =>
        Assert.Equal("https://example.com/@usuario", EvidenceLink.Create("Perfil", "https://example.com/@usuario").Url);

    [Fact]
    public void Urls_admit_up_to_500_characters()
    {
        var prefix = "https://example.com/";
        Assert.Equal(500, EvidenceLink.Create("a", prefix + new string('a', 500 - prefix.Length)).Url.Length);
        Assert.Throws<DomainException>(() => EvidenceLink.Create("a", prefix + new string('a', 501 - prefix.Length)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void The_label_and_the_url_are_required(string? value)
    {
        Assert.Throws<DomainException>(() => EvidenceLink.Create(value, "https://example.com"));
        Assert.Throws<DomainException>(() => EvidenceLink.Create("Informe", value));
    }

    [Fact]
    public void Labels_admit_up_to_100_characters()
    {
        Assert.Equal(100, EvidenceLink.Create(new string('a', 100), "https://example.com").Label.Length);
        Assert.Throws<DomainException>(() => EvidenceLink.Create(new string('a', 101), "https://example.com"));
    }

    [Fact]
    public void An_event_admits_ten_links_but_not_eleven()
    {
        IEnumerable<(string?, string?)> Links(int count) => Enumerable.Range(1, count).Select(i => ((string?)$"E{i}", (string?)$"https://example.com/{i}"));

        Assert.Equal(10, EvidenceLink.CreateSet(Links(10)).Count);
        Assert.Throws<DomainException>(() => EvidenceLink.CreateSet(Links(11)));
    }
}
