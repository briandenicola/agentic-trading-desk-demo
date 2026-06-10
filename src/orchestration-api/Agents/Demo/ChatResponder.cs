using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic, offline DEMO responder for the grounded Markets-Intelligence assistant
/// (the "AI Chat" surface). It classifies the user's question by intent and answers from the
/// same mock systems-of-record the briefings use — over HTTP only (constitution Principle II /
/// FR-002) — so the chat works with no model or credentials and returns the identical
/// <see cref="ChatReply"/> shape the LIVE Foundry chat agent returns (Principle III).
///
/// Intents: customer profile (CB-id), today's priority calls, current events/news, active
/// complaints, closing pipeline, and a capabilities/help fallback. All data is fictional.
/// </summary>
public sealed partial class ChatResponder(MockApiClient mockApi, EventTools eventTools, RmBriefingComposer composer)
{
    public async Task<ChatReply> RespondAsync(ChatRequest request, CancellationToken ct = default)
    {
        var lastUser = request.Messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content?.Trim() ?? "";
        var q = lastUser.ToLowerInvariant();
        var rmId = string.IsNullOrWhiteSpace(request.RmId) ? RmBriefingComposer.DefaultRmId : request.RmId!;

        var idMatch = CustomerIdRegex().Match(lastUser);
        if (idMatch.Success)
        {
            return await CustomerReplyAsync(idMatch.Value.ToUpperInvariant(), ct);
        }

        if (ContainsAny(q, "complaint", "escalat", "dissatisf"))
        {
            return await ComplaintsReplyAsync(rmId, ct);
        }
        if (ContainsAny(q, "pipeline", "opportunit", "deal", "closing", "close", "revenue"))
        {
            return await PipelineReplyAsync(rmId, ct);
        }
        if (ContainsAny(q, "event", "news", "market", "happen", "overnight", "intraday", "alert", "macro", "rate"))
        {
            return await EventsReplyAsync(ct);
        }
        if (ContainsAny(q, "who", "call", "priorit", "today", "focus", "first", "attention", "book"))
        {
            return await PriorityReplyAsync(rmId, ct);
        }

        return Help();
    }

    // ------------------------------------------------------------------ intents

    private async Task<ChatReply> CustomerReplyAsync(string customerId, CancellationToken ct)
    {
        var node = await SafeJsonAsync($"/mock/cb/customers/{Uri.EscapeDataString(customerId)}", ct);
        if (node is null)
        {
            return Reply($"I couldn't find a customer with id **{customerId}**. Try a known id like CB-10036, or ask \"who should I call today?\".");
        }

        var name = Str(node, "dba") ?? Str(node, "legalName") ?? customerId;
        var sector = Str(node, "industrySector");
        var city = Str(node, "hqCity");
        var state = Str(node, "state");
        var revenue = Num(node, "annualRevenueMm");
        var exposure = Num(node, "totalExposureMm");
        var deposits = Num(node, "depositBalanceMm");
        var rm = Str(node, "relationshipManager");
        var products = Str(node, "productsHeld");
        var risk = Str(node, "riskRating");

        var sb = new StringBuilder();
        sb.Append($"**{name}** ({customerId})");
        if (!string.IsNullOrWhiteSpace(sector)) sb.Append($" — {sector}");
        if (!string.IsNullOrWhiteSpace(city)) sb.Append($", {city}{(string.IsNullOrWhiteSpace(state) ? "" : ", " + state)}");
        sb.AppendLine(".");
        if (revenue is not null) sb.AppendLine($"- Annual revenue: ${revenue:0.#}M");
        if (exposure is not null || deposits is not null)
            sb.AppendLine($"- Exposure: ${exposure ?? 0:0.#}M · Deposits: ${deposits ?? 0:0.#}M" + (risk is null ? "" : $" · Risk rating {risk}"));
        if (!string.IsNullOrWhiteSpace(products)) sb.AppendLine($"- Products: {products}");
        if (!string.IsNullOrWhiteSpace(rm)) sb.AppendLine($"- Relationship manager: {rm}");

        // Reactive overlay: any current events naming this customer.
        var events = await SafeEventsByEntityAsync(customerId, "customer", ct);
        if (events.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"**{events.Count} current event(s) touch this customer:**");
            foreach (var e in events.Take(3)) sb.AppendLine($"- {e.Headline} ({e.Severity})");
        }

        return Reply(sb.ToString().TrimEnd(),
            $"What are {name}'s open opportunities?", "Who should I call today?", "What's happening in the market?");
    }

    private async Task<ChatReply> PriorityReplyAsync(string rmId, CancellationToken ct)
    {
        var brief = await composer.ComposeAsync(rmId, null, ct);
        var top = brief.PriorityCallList.Take(3).ToList();
        if (top.Count == 0)
        {
            return Reply($"{brief.Rm.Name}'s book has no customers needing attention today — no active complaints, due follow-ups or closing deals.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Top {top.Count} calls for **{brief.Rm.Name}** today:");
        foreach (var c in top)
        {
            var tags = c.Tags.Count == 0 ? "" : " — " + string.Join(", ", c.Tags.Select(t => t.Label));
            sb.AppendLine();
            sb.AppendLine($"**{c.Rank}. {c.CustomerName}** ({c.CustomerId}){tags}");
            if (c.Reasons.Count > 0) sb.AppendLine($"   {c.Reasons[0]}");
            sb.AppendLine($"   → {c.SuggestedAction}");
        }
        return Reply(sb.ToString().TrimEnd(),
            $"Tell me about {top[0].CustomerId}", "Which deals are closing soon?", "Any active complaints?");
    }

    private async Task<ChatReply> ComplaintsReplyAsync(string rmId, CancellationToken ct)
    {
        var brief = await composer.ComposeAsync(rmId, null, ct);
        if (brief.ComplaintsSnapshot.Count == 0)
        {
            return Reply($"No active complaints in {brief.Rm.Name}'s book right now. 👍");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"**{brief.ComplaintsSnapshot.Count} active complaint(s)** in {brief.Rm.Name}'s book:");
        foreach (var c in brief.ComplaintsSnapshot.Take(6))
        {
            sb.AppendLine($"- **{c.CustomerName}** · {c.Category} · {c.Severity} · _{c.Status}_ ({c.ComplaintId}, filed {c.DateFiled}).");
        }
        return Reply(sb.ToString().TrimEnd(), "Who should I call first?", "Which deals are closing soon?");
    }

    private async Task<ChatReply> PipelineReplyAsync(string rmId, CancellationToken ct)
    {
        var brief = await composer.ComposeAsync(rmId, null, ct);
        var closing = brief.PipelineClosing;
        var sb = new StringBuilder();
        sb.AppendLine($"Pipeline for **{brief.Rm.Name}**: {brief.Kpis.OpenPipelineCount} open opportunities worth ${brief.Kpis.OpenPipelineAmountMm:0.#}M; {brief.Kpis.ClosingWithin14Days} closing within 14 days.");
        if (closing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Closing soon:**");
            foreach (var o in closing.Take(6))
            {
                sb.AppendLine($"- **{o.CustomerName}** · {o.ProductType} · {o.Stage} · ${o.AmountMm:0.#}M (close {o.ExpectedCloseDate}, {o.OpportunityId}).");
            }
        }
        return Reply(sb.ToString().TrimEnd(), "Who should I call today?", "Any active complaints?");
    }

    private async Task<ChatReply> EventsReplyAsync(CancellationToken ct)
    {
        IReadOnlyList<MarketEvent> events;
        try { events = await eventTools.ListEventsAsync(null, ct); }
        catch { events = []; }

        if (events.Count == 0)
        {
            return Reply("There are no current market or news events in the system. Inject one from the News Desk to see the agents react.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"**{events.Count} current event(s)** the agents are weighing:");
        foreach (var e in events.Take(6))
        {
            var scope = string.IsNullOrWhiteSpace(e.Scope) ? "" : $"[{e.Scope}] ";
            sb.AppendLine($"- {scope}**{e.Headline}** · {e.Severity}{(string.IsNullOrWhiteSpace(e.Source) ? "" : " · " + e.Source)}");
        }
        return Reply(sb.ToString().TrimEnd(), "How do these affect my book?", "Who should I call today?");
    }

    private static ChatReply Help() => new()
    {
        Mode = "DEMO",
        Message =
            "I'm your Markets-Intelligence assistant. I'm grounded in your book and the current event feed. Try:\n" +
            "- **Who should I call today?**\n" +
            "- **Tell me about CB-10036**\n" +
            "- **What's happening in the market?**\n" +
            "- **Any active complaints?**\n" +
            "- **Which deals are closing soon?**",
        Suggestions = ["Who should I call today?", "What's happening in the market?", "Any active complaints?"],
    };

    // ------------------------------------------------------------------ helpers

    private async Task<System.Text.Json.Nodes.JsonNode?> SafeJsonAsync(string path, CancellationToken ct)
    {
        try { return await mockApi.GetJsonAsync(path, ct); }
        catch { return null; }
    }

    private async Task<IReadOnlyList<MarketEvent>> SafeEventsByEntityAsync(string value, string kind, CancellationToken ct)
    {
        try { return await eventTools.GetEventsByEntityAsync(value, kind, ct); }
        catch { return []; }
    }

    private static ChatReply Reply(string message, params string[] suggestions) => new()
    {
        Mode = "DEMO",
        Message = message,
        Suggestions = suggestions.Length == 0 ? null : suggestions,
    };

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(haystack.Contains);

    private static string? Str(System.Text.Json.Nodes.JsonNode node, string key)
    {
        var v = node[key];
        return v is null ? null : v.GetValue<object>()?.ToString();
    }

    private static double? Num(System.Text.Json.Nodes.JsonNode node, string key)
    {
        var v = node[key];
        if (v is null) return null;
        return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    [GeneratedRegex(@"CB-\d{4,6}", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerIdRegex();
}
