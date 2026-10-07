using Itmas3.Domain.Common;

namespace Itmas3.Domain.Authorization;

/// <summary>
/// Smallest unit of authorization: an action on a resource (e.g. <c>customers.delete</c>).
/// Declared in code and synchronised to the database; only its presentation metadata is editable.
/// </summary>
public sealed class PermissionDefinition : Entity<string>
{
    public const int MaxDisplayNameLength = 100;
    public const int MaxDescriptionLength = 500;
    public const int MaxCategoryLength = 100;

    private PermissionDefinition(string id, string displayName, string description, string category, bool isGrantable)
        : base(id)
    {
        DisplayName = displayName;
        Description = description;
        Category = category;
        IsGrantable = isGrantable;
    }

    public string Name => Id;

    public string DisplayName { get; private set; }

    public string Description { get; private set; }

    public string Category { get; private set; }

    /// <summary>Non-grantable permissions can never be part of a policy; only Root exercises them.</summary>
    public bool IsGrantable { get; private set; }

    /// <summary>No longer declared in code: grants nothing and cannot be added to policies. Kept for history.</summary>
    public bool IsDeprecated { get; private set; }

    /// <summary>Whether a policy may contain this permission.</summary>
    public bool IsAssignable => IsGrantable && !IsDeprecated;

    public static PermissionDefinition Declare(string name, string displayName, string description, string category, bool isGrantable)
    {
        var permission = new PermissionDefinition(name, string.Empty, string.Empty, string.Empty, isGrantable);
        permission.UpdateMetadata(displayName, description, category);
        return permission;
    }

    public void UpdateMetadata(string displayName, string description, string category)
    {
        DisplayName = Require(displayName, MaxDisplayNameLength, "El nombre visible del permiso");
        Description = Optional(description, MaxDescriptionLength, "La descripción del permiso");
        Category = Require(category, MaxCategoryLength, "La categoría del permiso");
    }

    /// <summary>Applies the code declaration. Returns true when something relevant to authorization changed.</summary>
    public bool Synchronise(bool isGrantable)
    {
        var changed = IsDeprecated || IsGrantable != isGrantable;
        IsDeprecated = false;
        IsGrantable = isGrantable;
        return changed;
    }

    public bool Deprecate()
    {
        if (IsDeprecated)
        {
            return false;
        }

        IsDeprecated = true;
        return true;
    }

    internal static string Require(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException($"{field} es obligatorio.");
        }

        return trimmed.Length <= maxLength ? trimmed : throw new DomainException($"{field} admite como máximo {maxLength} caracteres.");
    }

    internal static string Optional(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maxLength ? trimmed : throw new DomainException($"{field} admite como máximo {maxLength} caracteres.");
    }
}
