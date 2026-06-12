namespace OrchestrationApi.Models;

/// <summary>
/// Request body for the grounded Markets-Intelligence assistant (<c>POST /api/chat</c>). The
/// client replays the full conversation on every turn so the orchestration layer stays stateless
/// (matching the scene endpoints). The assistant answers using the same mock systems-of-record
/// the briefings use — over HTTP only (Principle II). All data is fictional.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>The conversation so far, oldest first; the last entry must be the user's question.</summary>
    public required IReadOnlyList<ChatTurn> Messages { get; init; }

    /// <summary>Optional RM context (e.g. <c>RM-104</c>) so "who should I call?" resolves a book.</summary>
    public string? RmId { get; init; }

    /// <summary>
    /// Optional trading-desk coverage salesperson (e.g. <c>Theo Wexler</c>). When present, the
    /// request is routed to the Institutional Sales &amp; Trading assistant grounded in
    /// <c>/mock/td/*</c> (clients, securities, RFQs, inventory axes, events) instead of the
    /// Commercial Banking RM assistant. Mutually exclusive with <see cref="RmId"/> in practice.
    /// </summary>
    public string? SalespersonId { get; init; }
}

/// <summary>One conversational turn. <c>Role</c> is <c>user</c> or <c>assistant</c>.</summary>
public sealed class ChatTurn
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}

/// <summary>
/// The assistant's reply. Identical shape in DEMO (deterministic intent responder) and LIVE
/// (Foundry chat agent with the mock-api tools bound) — the UI is mode-blind (Principle III).
/// </summary>
public sealed record ChatReply
{
    public required string Mode { get; init; }          // "DEMO" | "LIVE"
    public required string Message { get; init; }
    public IReadOnlyList<string>? Suggestions { get; init; }
    public IReadOnlyList<string>? ToolsUsed { get; init; }
}
