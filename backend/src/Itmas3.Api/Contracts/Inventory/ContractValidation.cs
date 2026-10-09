using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Itmas3.Api.Contracts.Inventory;

/// <summary>
/// Text received from a device: untrusted. Rejects control characters and enforces a maximum length
/// (SPEC-0002 §12.2). Applies to a string or to every element of a string collection.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SafeTextAttribute(int maxLength) : ValidationAttribute
{
    public int MaxLength { get; } = maxLength;

    /// <summary>Maximum number of elements when applied to a collection.</summary>
    public int MaxCount { get; init; } = int.MaxValue;

    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string text => IsSafe(text),
        IEnumerable<string?> items => items.Count() <= MaxCount && items.All(item => item is not null && IsSafe(item)),
        _ => false,
    };

    public override string FormatErrorMessage(string name) =>
        $"{name} contiene caracteres no permitidos o supera {MaxLength} caracteres.";

    private bool IsSafe(string text) => text.Length <= MaxLength && !text.Any(char.IsControl);
}

/// <summary>IPv4 or IPv6 address in textual form (or a collection of them).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class IpAddressTextAttribute : ValidationAttribute
{
    public const int MaxLength = 45;

    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string text => IsIp(text),
        IEnumerable items => items.Cast<object?>().All(item => item is string text && IsIp(text)),
        _ => false,
    };

    public override string FormatErrorMessage(string name) => $"{name} no es una dirección IP válida.";

    private static bool IsIp(string text) => text.Length <= MaxLength && IPAddress.TryParse(text, out _);
}
