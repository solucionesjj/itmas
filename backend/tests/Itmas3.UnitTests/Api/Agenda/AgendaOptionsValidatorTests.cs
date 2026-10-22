using Itmas3.Api.Agenda;
using Microsoft.Extensions.Options;

namespace Itmas3.UnitTests.Api.Agenda;

/// <summary>SPEC-0006 CA-18, P-26, P-27 and P-28: start-up validation of the agenda configuration.</summary>
public sealed class AgendaOptionsValidatorTests
{
    private static readonly AgendaOptionsValidator Validator = new();

    private static AgendaOptions Complete() => new()
    {
        TimeZone = "America/Bogota",
        PublicUrl = "https://itmas.example.com",
        Outlook = new AgendaOutlookOptions
        {
            Enabled = true,
            TenantId = "tenant-placeholder",
            ClientId = "client-placeholder",
            ClientSecret = "placeholder-not-a-secret",
            Mailbox = "agenda@example.com",
            TeamAddress = "equipo@example.com",
        },
    };

    private static string[] Failures(AgendaOptions options) => [.. Validator.Validate(null, options).Failures ?? []];

    [Fact]
    public void The_defaults_are_valid_and_leave_outlook_off()
    {
        var options = new AgendaOptions();

        Assert.True(Validator.Validate(null, options).Succeeded);
        Assert.False(options.Outlook.Enabled);
        Assert.Equal("America/Bogota", options.TimeZone);
        Assert.Equal(60, options.PublishPermitLimitPerHour);
    }

    [Fact]
    public void CA18_a_complete_outlook_configuration_is_valid() => Assert.True(Validator.Validate(null, Complete()).Succeeded);

    [Theory]
    [InlineData("TenantId")]
    [InlineData("ClientId")]
    [InlineData("ClientSecret")]
    [InlineData("Mailbox")]
    public void CA18_with_outlook_enabled_each_missing_setting_is_reported_by_name(string missing)
    {
        var options = Complete();
        switch (missing)
        {
            case "TenantId": options.Outlook.TenantId = null; break;
            case "ClientId": options.Outlook.ClientId = " "; break;
            case "ClientSecret": options.Outlook.ClientSecret = string.Empty; break;
            default: options.Outlook.Mailbox = null; break;
        }

        var failures = Failures(options);

        Assert.Contains(failures, failure => failure.Contains($"Agenda:Outlook:{missing}", StringComparison.Ordinal));
    }

    [Fact]
    public void CA18_the_messages_name_the_settings_and_never_include_their_values()
    {
        var options = Complete();
        options.Outlook.TenantId = null;
        options.Outlook.ClientSecret = "valor-secreto-que-no-debe-salir";
        options.Outlook.Mailbox = "no-es-un-correo";
        options.AllowedAttendeeDomains = "dominio invalido!";
        options.PublicUrl = "ftp://valor-privado";

        var text = string.Join('\n', Failures(options));

        Assert.NotEmpty(text);
        Assert.DoesNotContain("valor-secreto", text, StringComparison.Ordinal);
        Assert.DoesNotContain("no-es-un-correo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dominio invalido", text, StringComparison.Ordinal);
        Assert.DoesNotContain("valor-privado", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CA18_with_outlook_off_the_outlook_settings_are_not_required()
    {
        var options = new AgendaOptions { Outlook = new AgendaOutlookOptions { Enabled = false } };

        Assert.True(Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://itmas.example.com")]
    [InlineData("itmas.example.com")]
    [InlineData("ftp://itmas.example.com")]
    public void P27_the_public_url_is_required_and_https_when_outlook_is_enabled(string? url)
    {
        var options = Complete();
        options.PublicUrl = url;

        Assert.Contains(Failures(options), failure => failure.Contains("Agenda:PublicUrl", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://localhost:8088")]
    [InlineData("https://itmas.example.com")]
    public void P27_without_outlook_the_public_url_is_optional_but_must_be_http_when_given(string? url)
    {
        var options = new AgendaOptions { PublicUrl = url };

        Assert.True(Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("ftp://itmas.example.com")]
    [InlineData("no es una url")]
    public void P27_without_outlook_a_public_url_that_is_not_http_is_rejected(string url) =>
        Assert.Contains(Failures(new AgendaOptions { PublicUrl = url }), failure => failure.Contains("Agenda:PublicUrl", StringComparison.Ordinal));

    [Theory]
    [InlineData("Marte/Olimpo")]
    [InlineData("")]
    [InlineData("  ")]
    public void P28_an_unknown_time_zone_is_rejected(string zone) =>
        Assert.Contains(Failures(new AgendaOptions { TimeZone = zone }), failure => failure.Contains("Agenda:TimeZone", StringComparison.Ordinal));

    [Theory]
    [InlineData("America/Bogota")]
    [InlineData("America/New_York")]
    [InlineData("UTC")]
    public void P28_known_iana_zones_are_accepted(string zone) => Assert.True(Validator.Validate(null, new AgendaOptions { TimeZone = zone }).Succeeded);

    [Theory]
    [InlineData("example.com", true)]
    [InlineData("example.com, ecs-la.com", true)]
    [InlineData("example.com;otro.org", true)]
    [InlineData("", true)]
    [InlineData("no es un dominio", false)]
    [InlineData("@example.com", false)]
    [InlineData("example", false)]
    [InlineData("-malo.com", false)]
    public void The_allowed_attendee_domains_must_be_domain_names(string domains, bool valid) =>
        Assert.Equal(valid, Validator.Validate(null, new AgendaOptions { AllowedAttendeeDomains = domains }).Succeeded);

    [Theory]
    [InlineData("equipo@example.com", true)]
    [InlineData("equipo@EXAMPLE.com", true)]
    [InlineData("equipo@otro-dominio.com", false)]
    [InlineData("sin-arroba", false)]
    public void The_team_address_must_belong_to_an_allowed_domain_the_one_of_the_mailbox_by_default(string address, bool valid)
    {
        var options = Complete();
        options.Outlook.TeamAddress = address;

        Assert.Equal(valid, Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void The_team_address_is_checked_against_the_configured_domains_when_there_are_some()
    {
        var options = Complete();
        options.AllowedAttendeeDomains = "ecs-la.com";
        options.Outlook.TeamAddress = "equipo@ecs-la.com";

        Assert.True(Validator.Validate(null, options).Succeeded);

        options.Outlook.TeamAddress = "equipo@example.com";
        Assert.False(Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void The_team_address_is_optional()
    {
        var options = Complete();
        options.Outlook.TeamAddress = null;

        Assert.True(Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("example.com, Otro.ORG ,example.com", new[] { "example.com", "otro.org" })]
    [InlineData("", new string[0])]
    [InlineData("  ;  ", new string[0])]
    public void The_domain_list_is_lowercase_unique_and_accepts_commas_semicolons_and_spaces(string value, string[] expected) =>
        Assert.Equal(expected, AgendaOptionsValidator.ParseDomains(value));

    [Theory]
    [InlineData("agenda@example.com", "example.com")]
    [InlineData("Agenda@EXAMPLE.com", "example.com")]
    [InlineData("sin-arroba", null)]
    [InlineData("@example.com", null)]
    [InlineData("a@", null)]
    [InlineData(null, null)]
    public void The_domain_of_an_address_is_its_part_after_the_last_arroba(string? address, string? expected) =>
        Assert.Equal(expected, AgendaOptionsValidator.DomainOf(address));

    [Fact]
    public void The_validator_plugs_into_the_options_pipeline()
    {
        IValidateOptions<AgendaOptions> validator = Validator;

        var result = validator.Validate(null, new AgendaOptions { TimeZone = "Marte/Olimpo" });

        Assert.True(result.Failed);
    }
}
