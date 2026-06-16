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
    // News / research relevance — the CATALYST that explains "why now", deliberately weighted
    // BELOW client-engagement signals so the briefing leads with actionable, engaged clients
    // rather than the loudest headline.
    public const int NewsItem = 7;
    public const int ResearchAction = 10;
    public const int SentimentBoost = 4;          // strong (Positive/Negative) vs Mixed/Neutral
    public const int NewsResearchPair = 10;       // public news paired with our own internal research on the same name

    // Open RFQ follow-up — direct, two-sided client engagement.
    public const int RfqQuoted = 24;              // quoted, awaiting client decision
    public const int RfqNoFollowUp = 16;          // received, never closed out
    public const int RfqPassed = 10;              // we passed; revisit appetite

    // Inquiries — the client raised it / mentioned it in chat.
    public const int InquiryUrgent = 28;
    public const int InquiryRecent = 14;
    public const int InquiryAmbiguous = 10;       // missing inferred security/direction → clarify

    // Engaged-but-didn't-trade — the hottest unconverted lead: the client signalled intent via an
    // RFQ or inquiry on a security but has NO matching executed trade on it. Added once per name.
    public const int EngagedNoTrade = 26;

    // Inventory axe match (our axe ↔ a security the client trades)
    public const int AxeMatch = 22;

    // CRM urgency
    public const int CrmHigh = 22;
    public const int CrmMedium = 10;

    /// <summary>UI colour band 1..4 (red→green) from the composite score.</summary>
    public static int PriorityBand(int score) =>
        score >= 80 ? 1 : score >= 50 ? 2 : score >= 25 ? 3 : 4;

    /// <summary>Clamp a raw component contribution to a 0..100 display intensity.</summary>
    public static int Intensity(int raw) => Math.Clamp(raw, 0, 100);
}
