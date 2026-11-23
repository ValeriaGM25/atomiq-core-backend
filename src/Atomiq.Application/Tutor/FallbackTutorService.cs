namespace Atomiq.Application.Tutor;

internal static class FallbackTutorService
{
    public static TutorExplanation Explain(ReactionExplanationInput input)
    {
        if (!input.IsValid)
        {
            return new TutorExplanation(
                "Unity marcó el resultado como no válido; observa la simulación sin asumir productos no confirmados.",
                "OfflineFallback");
        }

        var reactants = new[] { Normalize(input.ReactantA), Normalize(input.ReactantB) };
        if (reactants.Contains("na", StringComparer.OrdinalIgnoreCase)
            && reactants.Contains("cl", StringComparer.OrdinalIgnoreCase)
            && Normalize(input.ProductFormula).Equals("nacl", StringComparison.OrdinalIgnoreCase))
        {
            return new TutorExplanation(
                "El sodio transfiere un electrón al cloro; se forman iones Na⁺ y Cl⁻ que se ordenan en una red iónica estable.",
                "OfflineFallback");
        }

        if (reactants.Contains("na", StringComparer.OrdinalIgnoreCase)
            && reactants.Contains("h2o", StringComparer.OrdinalIgnoreCase))
        {
            return new TutorExplanation(
                "El sodio reacciona con agua, forma NaOH y libera H₂; la energía liberada puede calentar la mezcla.",
                "OfflineFallback");
        }

        return new TutorExplanation(
            "No hay una explicación específica confirmada; observa el resultado mostrado sin inventar productos ni datos termodinámicos.",
            "OfflineFallback");
    }

    private static string Normalize(string value)
    {
        return string.Concat(value.Normalize().Where(character => !char.IsWhiteSpace(character)));
    }
}
