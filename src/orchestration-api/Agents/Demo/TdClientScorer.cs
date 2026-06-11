namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic priority scoring for the Trading Desk morning briefing. A coverage
/// salesperson's clients are ranked by the strength of the reasons to call them today,
/// summed across five signal families derived from the fictional trading-desk dataset:
///
///   news / research touching their book   per item  (sentiment-weighted)
///   open RFQ awaiting follow-up            per RFQ   (Quoted highest)
///   recent / urgent / ambiguous inquiry    per item  (Urgent highest)
///   dealer inventory axe matching demand    per match
///   CRM urgency (High / Medium)             once
///
/// Each component is reported as a 0..100 intensity; the composite (used for ranking) is
/// the raw sum plus any live-event delta and can exceed 100. Pure and side-effect free so
/// DEMO runs are byte-identical.
/// </summary>
public static class TdClientScorer
{
    // News / research relevance
    public const int NewsItem = 12;
    public const int ResearchAction = 16;
    public const int SentimentBoost = 6;          // strong (Positive/Negative) vs Mixed/Neutral

    // Open RFQ follow-up
    public const int RfqQuoted = 22;              // quoted, awaiting client decision
    public const int RfqNoFollowUp = 12;          // received, never closed out
    public const int RfqPassed = 8;               // we passed; revisit appetite

    // Inquiries
    public const int InquiryUrgent = 26;
    public const int InquiryRecent = 12;
    public const int InquiryAmbiguous = 8;        // missing inferred security/direction → clarify

    // Inventory axe match (our axe ↔ a security the client trades)
    public const int AxeMatch = 18;

    // CRM urgency
    public const int CrmHigh = 22;
    public const int CrmMedium = 10;

    /// <summary>UI colour band 1..4 (red→green) from the composite score.</summary>
    public static int PriorityBand(int score) =>
        score >= 80 ? 1 : score >= 50 ? 2 : score >= 25 ? 3 : 4;

    /// <summary>Clamp a raw component contribution to a 0..100 display intensity.</summary>
    public static int Intensity(int raw) => Math.Clamp(raw, 0, 100);
}
