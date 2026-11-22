namespace Atomiq.Domain;

internal static class FormulaNormalizer
{
    public static string NormalizeRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var normalized = string.Concat(value.Where(character => !char.IsWhiteSpace(character))).Trim();
        return normalized.Length == 0
            ? throw new DomainException($"{name} is required.")
            : normalized;
    }
}
