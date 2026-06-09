namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic priority-call scoring for the RM Daily Briefing. The weights were
/// derived from (and reproduce the exact ranking of) the client ground-truth sample
/// <c>assets/rm_daily_briefing_2026-05-14.html</c> and confirmed with the user:
///
///   escalated complaint    +60     follow-up overdue      +50
///   in-progress complaint  +30     follow-up due today    +45
///   each stuck opportunity  +25     follow-up due ≤2 days  +40
///   opportunity closing ≤14d +40
///
/// A customer's score sums every active signal (complaints and stuck/closing
/// opportunities accumulate; the single most-urgent follow-up is scored). The top 8 by
/// score drive the call list. Pure and side-effect free so DEMO runs are byte-identical.
/// </summary>
public static class RmCallScorer
{
    public const int EscalatedComplaint = 60;
    public const int InProgressComplaint = 30;
    public const int StuckOpportunity = 25;
    public const int ClosingOpportunity = 40;

    public const int FollowUpOverdue = 50;
    public const int FollowUpToday = 45;
    public const int FollowUpSoon = 40;

    /// <summary>An open opportunity is "stuck" once it has aged past this many days without closing.</summary>
    public const int StuckMinDays = 40;

    /// <summary>Opportunities expected to close within this window are "closing soon".</summary>
    public const int ClosingWindowDays = 14;

    /// <summary>A follow-up due within this many days ahead counts as "due soon".</summary>
    public const int FollowUpSoonDays = 2;

    /// <summary>Only surface overdue follow-ups from within this trailing window (older ones are stale).</summary>
    public const int OverdueWindowDays = 14;

    public enum FollowUpUrgency
    {
        None = 0,
        Soon,
        Today,
        Overdue,
    }

    public static FollowUpUrgency ClassifyFollowUp(DateOnly followUp, DateOnly asOf)
    {
        if (followUp < asOf)
        {
            // Only recent overdue items are actionable for today's briefing.
            return followUp >= asOf.AddDays(-OverdueWindowDays)
                ? FollowUpUrgency.Overdue
                : FollowUpUrgency.None;
        }
        if (followUp == asOf) return FollowUpUrgency.Today;
        if (followUp <= asOf.AddDays(FollowUpSoonDays)) return FollowUpUrgency.Soon;
        return FollowUpUrgency.None;
    }

    public static int FollowUpScore(FollowUpUrgency urgency) => urgency switch
    {
        FollowUpUrgency.Overdue => FollowUpOverdue,
        FollowUpUrgency.Today => FollowUpToday,
        FollowUpUrgency.Soon => FollowUpSoon,
        _ => 0,
    };

    public static int Score(
        int escalatedComplaints,
        int inProgressComplaints,
        FollowUpUrgency followUp,
        int stuckOpportunities,
        int closingOpportunities) =>
        (escalatedComplaints * EscalatedComplaint)
        + (inProgressComplaints * InProgressComplaint)
        + FollowUpScore(followUp)
        + (stuckOpportunities * StuckOpportunity)
        + (closingOpportunities * ClosingOpportunity);

    /// <summary>UI colour band 1..4 (red→green): ranks 1-2→1, 3-4→2, 5-6→3, 7-8→4.</summary>
    public static int PriorityBand(int rank) => Math.Min(4, (rank + 1) / 2);
}
