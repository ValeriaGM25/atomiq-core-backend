using System.Globalization;
using System.Text;

namespace Atomiq.Application.Tutor;

/// <summary>Offline answers for the most common questions in the AR lab, matched by keywords.</summary>
internal static class FallbackTutorChat
{
    // A keyword matches a word that starts with it; "=word" must match the whole word.
    private static readonly (string[] Keywords, string Answer)[] Answers =
    [
        (["agua", "h2o", "explot", "fuego", "calor", "exoterm"],
            "El sodio reacciona con el agua: forma hidróxido de sodio (NaOH) y gas hidrógeno (H₂), y libera mucho calor. " +
            "Por eso es peligroso en la vida real; aquí puedes verlo de forma segura en el simulador."),
        (["ionic", "enlace", "cristal"],
            "En un enlace iónico un átomo cede electrones y otro los recibe. Quedan iones con carga opuesta (Na⁺ y Cl⁻) " +
            "que se atraen y se ordenan en una red cristalina, como la sal de mesa."),
        (["octeto", "valencia", "capa"],
            "La regla del octeto dice que muchos átomos son más estables con 8 electrones en su capa externa. " +
            "El cloro tiene 7 y busca ganar 1; el sodio tiene 1 y tiende a perderlo."),
        (["electron", "atra", "carga", "unen"],
            "Los átomos se atraen por sus cargas eléctricas. Cuando el sodio cede su electrón al cloro, " +
            "el sodio queda positivo (Na⁺) y el cloro negativo (Cl⁻), y las cargas opuestas se atraen."),
        (["=sal", "nacl", "cloruro"],
            "La sal de mesa es cloruro de sodio (NaCl). Se forma cuando el sodio le da un electrón al cloro " +
            "y los iones resultantes se unen en una red cristalina."),
        (["sodio", "=na"],
            "El sodio (Na) es un metal alcalino con número atómico 11. Tiene un solo electrón externo " +
            "y lo cede con facilidad, por eso reacciona con el cloro y, con fuerza, con el agua."),
        (["cloro", "=cl"],
            "El cloro (Cl) es un halógeno con número atómico 17. Tiene 7 electrones externos " +
            "y le falta 1 para completar su capa, por eso lo toma de átomos como el sodio.")
    ];

    public static TutorChatReply Reply(TutorChatInput input)
    {
        var words = Tokenize(input.Question);
        foreach (var (keywords, answer) in Answers)
        {
            if (keywords.Any(keyword => words.Any(word => Matches(word, keyword))))
            {
                return new TutorChatReply(answer, "OfflineFallback");
            }
        }

        return new TutorChatReply(
            "Ahora estoy en modo local y solo puedo responder sobre el sodio, el cloro, el agua, la sal y los enlaces iónicos. " +
            "Prueba preguntar, por ejemplo, por qué se atraen el sodio y el cloro.",
            "OfflineFallback");
    }

    private static bool Matches(string word, string keyword)
    {
        return keyword.StartsWith('=')
            ? string.Equals(word, keyword[1..], StringComparison.Ordinal)
            : word.StartsWith(keyword, StringComparison.Ordinal);
    }

    private static List<string> Tokenize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var normalized = character == '₂' ? '2' : character;
            if (char.IsLetterOrDigit(normalized))
            {
                current.Append(normalized);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }
}
