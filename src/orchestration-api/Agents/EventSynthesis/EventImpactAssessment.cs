namespace OrchestrationApi.Agents.EventSynthesis;

/// <summary>
/// One event specialist's structured output (002 US4, FR-018): a single market/news event
/// resolved to the typed portfolio selectors it touches, with a signed priority
/// <see cref="Contribution"/> the briefing synthesizer folds into the affected items' scores.
///
/// <para>The shape mirrors the deterministic DEMO <c>EventLinkage</c> so LIVE and DEMO produce
/// the same driver semantics (Principle III / SC-004): <see cref="Selectors"/> are flat
/// <c>kind:value</c> strings (e.g. <c>customerId:CB-10036</c>, <c>sector:Agriculture</c>),
/// contributions net (sum) when several events hit one entity, and every contributing event
/// stays listed.</para>
/// </summary>
public sealed record EventImpactAssessment
{
    public required string EventId { get; init; }
    public required string Headline { get; init; }
    public string? Severity { get; init; }
    public string? Direction { get; init; }

    /// <summary>Typed entity selectors this event touches, formatted <c>kind:value</c>.</summary>
    public IReadOnlyList<string> Selectors { get; init; } = [];

    /// <summary>Signed priority delta this event adds to the entities it touches.</summary>
    public double Contribution { get; init; }

    /// <summary>Human-readable lens (e.g. "downside risk to manage").</summary>
    public string? Lens { get; init; }

    /// <summary>One-line rationale shown to the user as the driver explanation.</summary>
    public string? Rationale { get; init; }
}
