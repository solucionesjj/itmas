using System.Text.RegularExpressions;

namespace Itmas3.Application.Authorization;

/// <summary>
/// Permission names follow the <c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c> convention
/// (at least two lowercase segments), e.g. <c>users.read</c> or <c>billing.invoices.approve</c>.
/// </summary>
public static partial class PermissionName
{
    public static bool IsValid(string? permission) =>
        !string.IsNullOrWhiteSpace(permission) && PermissionPattern().IsMatch(permission);

    public static string EnsureValid(string? permission) =>
        IsValid(permission)
            ? permission!
            : throw new ArgumentException(
                $"Invalid permission name '{permission}'. Expected '<module>.<resource>.<action>' in lowercase.",
                nameof(permission));

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PermissionPattern();
}
