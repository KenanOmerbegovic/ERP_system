namespace Erp.Modules.Identity.Domain;

/// <summary>Normalizes text fields. Input is validated before it reaches the domain, so blanks here are bugs.</summary>
internal static class Text
{
    public static string Required(string value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}
