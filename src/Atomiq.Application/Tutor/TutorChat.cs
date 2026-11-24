namespace Atomiq.Application.Tutor;

/// <summary>One previous message in the student ↔ tutor conversation.</summary>
public sealed record TutorChatTurn(TutorChatRole Role, string Text);

public enum TutorChatRole
{
    Student = 0,
    Tutor = 1
}

/// <summary>
/// A student question plus optional context from the AR lab (current challenge and last reaction).
/// Everything here comes from the device and is treated as untrusted data, never as instructions.
/// </summary>
public sealed record TutorChatInput(
    string Question,
    IReadOnlyList<TutorChatTurn> History,
    string? Challenge,
    string? LastReaction);

public sealed record TutorChatReply(string Reply, string Source);

public interface ITutorChatService
{
    Task<TutorChatReply> ReplyAsync(TutorChatInput input, CancellationToken cancellationToken = default);
}

public static class TutorChatLimits
{
    public const int MaxQuestionLength = 400;
    public const int MaxHistoryTurns = 8;
    public const int MaxTurnLength = 600;
    public const int MaxContextLength = 200;
    public const int MaxReplyWords = 90;
    public const int MaxOutputTokens = 400;
}
