namespace Atomiq.Application.Validation;

public static class InputValidation
{
    public static bool IsValidDeviceId(string? value) => IsValidIdentifier(value, 64);

    public static bool IsValidEventId(string? value) => IsValidIdentifier(value, 128);

    private static bool IsValidIdentifier(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            return false;
        }

        if (!char.IsLetterOrDigit(value[0]))
        {
            return false;
        }

        return value.All(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':');
    }
}
