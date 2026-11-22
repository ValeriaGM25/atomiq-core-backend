namespace Atomiq.Application.Tutor;

internal static class ReactionExplanationValidator
{
    private const int MaxTextLength = 256;
    private const int MaxEquationLength = 512;

    public static void Validate(ReactionExplanationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireText(input.ReactantA, nameof(input.ReactantA), MaxTextLength);
        RequireText(input.ReactantB, nameof(input.ReactantB), MaxTextLength);
        RequireText(input.ProductFormula, nameof(input.ProductFormula), MaxTextLength);
        RequireText(input.ProductDisplayName, nameof(input.ProductDisplayName), MaxTextLength);
        RequireText(input.ReactionEquation, nameof(input.ReactionEquation), MaxEquationLength);
        RequireText(input.ThermodynamicClassification, nameof(input.ThermodynamicClassification), MaxTextLength);

        if (input.DeltaEnthalpyKjMol is double value && (double.IsNaN(value) || double.IsInfinity(value)))
        {
            throw new ApplicationException("deltaEnthalpyKjMol must be finite.");
        }

        if (input.ElectronsTransferred < 0)
        {
            throw new ApplicationException("electronsTransferred cannot be negative.");
        }

        if (input.Timestamp.Offset != TimeSpan.Zero)
        {
            throw new ApplicationException("The reaction timestamp must be UTC.");
        }
    }

    private static void RequireText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            throw new ApplicationException($"{name} is required and has an invalid length.");
        }
    }
}
