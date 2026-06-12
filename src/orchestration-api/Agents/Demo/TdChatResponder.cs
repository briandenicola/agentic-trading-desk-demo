using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic, offline DEMO responder for the grounded Institutional Sales &amp; Trading
/// assistant (the trading-desk "Open Chat" surface). It classifies the salesperson's question
/// by intent and answers from the same trading-desk mock systems-of-record the morning briefing
/// uses — over HTTP only (constitution Principle II / FR-002) — so the chat works with no model
/// or credentials and returns the identical <see cref="ChatReply"/> shape the LIVE Foundry chat
/// agent returns (Principle III).
///
/// Intents: client profile (CL-id), security interest (SEC-id), today's priority calls /
/// who-to-call, inventory axes, current events/news, and a capabilities/help fallback. All data
/// is fictional and grounded in <c>/mock/td/*</c>.
/// </summary>
public sealed partial class TdChatResponder(MockApiClient mockApi, EventTools eventTools, TdBriefingComposer composer)
{
    public async Task<ChatReply> RespondAsync(ChatRequest request, CancellationToken ct = default)
    {
        var lastUser = request.Messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content?.Trim() ?? "";
        var q = lastUser.ToLowerInvariant();
        var salesperson = string.IsNullOrWhiteSpace(request.SalespersonId) ? TdBriefingComposer.DefaultSalespersonId : request.SalespersonId!;

        var secMatch = SecurityIdRegex().Match(lastUser);
        if (secMatch.Success)
        {
            return await SecurityReplyAsync(secMatch.Value.ToUpperInvariant(), ct);
        }

        var clientMatch = ClientIdRegex().Match(lastUser);
        if (clientMatch.Success)
        {
            return await ClientReplyAsync(clientMatch.Value.ToUpperInvariant(), ct);
        }

        if (ContainsAny(q, "axe", "inventory", "position", "offer", "bid", "we own", "we're long", "we are long", "to work"))
        {
            return await AxesReplyAsync(salesperson, ct);
        }
        if (ContainsAny(q, "event", "news", "market", "happen", "overnight", "intraday", "alert", "macro", "moved", "tape"))
        {
            return await EventsReplyAsync(ct);
        }
        if (ContainsAny(q, "who", "call", "priorit", "today", "focus", "first", "attention", "book", "outreach", "plan"))
        {
            return await PriorityReplyAsync(salesperson, ct);
        }

        return Help();
    }

    // ------------------------------------------------------------------ intents

    private async Task<ChatReply> ClientReplyAsync(string clientId, CancellationToken ct)
    {
        var node = await SafeJsonAsync($"/mock/td/clients/{Uri.EscapeDataString(clientId)}/activity", ct);
        var client = node?["client"];
        if (client is null)
        {
            return Reply($"I couldn't find a client with id **{clientId}**. Try a known id like CL-2006, or ask \"who should I call first this morning?\".");
        }

        var name = Str(client, "clientName") ?? clientId;
        var type = Str(client, "clientType");
        var region = Str(client, "clientRegion");
        var coverage = Str(client, "coverageSalesperson");
        var asset = Str(client, "preferredAssetClass");
        var risk = Str(client, "riskStyle");

        var holdings = CountArray(node, "holdings");
        var trades = CountArray(node, "trades");
        var rfqs = CountArray(node, "rfqs");
        var inquiries = CountArray(node, "inquiries");
        var crm = CountArray(node, "crm");

        var sb = new StringBuilder();
        sb.Append($"**{name}** ({clientId})");
        var bits = new[] { type, region, asset }.Where(s => !string.IsNullOrWhiteSpace(s));
        if (bits.Any()) sb.Append($" — {string.Join(" · ", bits)}");
        sb.AppendLine(".");
        if (!string.IsNullOrWhiteSpace(coverage)) sb.AppendLine($"- Coverage: {coverage}{(string.IsNullOrWhiteSpace(risk) ? "" : $" · {risk} style")}");
        sb.AppendLine($"- Recent activity: {holdings} holding(s), {trades} trade(s), {rfqs} RFQ(s), {inquiries} inquiry(ies), {crm} CRM note(s).");

        var lastCrm = LastNote(node, "crm", "note") ?? LastNote(node, "crm", "summary");
        if (!string.IsNullOrWhiteSpace(lastCrm)) sb.AppendLine($"- Latest CRM colour: _{lastCrm}_");
        var lastInq = LastNote(node, "inquiries", "text") ?? LastNote(node, "inquiries", "summary");
        if (!string.IsNullOrWhiteSpace(lastInq)) sb.AppendLine($"- Latest inquiry: _{lastInq}_");

        return Reply(sb.ToString().TrimEnd(),
            $"What securities is {name} active in?", "Who should I call first this morning?", "What moved in the market overnight?");
    }

    private async Task<ChatReply> SecurityReplyAsync(string securityId, CancellationToken ct)
    {
        var node = await SafeJsonAsync($"/mock/td/securities/{Uri.EscapeDataString(securityId)}/interest", ct);
        var security = node?["security"];
        if (security is null)
        {
            return Reply($"I couldn't find a security with id **{securityId}**. Try a known id, or ask about a client (e.g. CL-2006) or the morning call list.");
        }

        var name = Str(security, "securityName") ?? Str(security, "name") ?? securityId;
        var assetClass = Str(security, "assetClass");
        var sector = Str(security, "sector");
        var issuer = Str(security, "issuer");

        var holders = CountArray(node, "holders");
        var trades = CountArray(node, "trades");
        var rfqs = CountArray(node, "rfqs");
        var inquiries = CountArray(node, "inquiries");
        var newsCount = CountArray(node, "news");
        var inventory = CountArray(node, "inventory");

        var sb = new StringBuilder();
        sb.Append($"**{name}** ({securityId})");
        var bits = new[] { assetClass, sector, issuer }.Where(s => !string.IsNullOrWhiteSpace(s));
        if (bits.Any()) sb.Append($" — {string.Join(" · ", bits)}");
        sb.AppendLine(".");
        sb.AppendLine($"- Interest: {holders} holder(s), {trades} recent trade(s), {rfqs} RFQ(s), {inquiries} inquiry(ies).");
        if (inventory > 0) sb.AppendLine($"- We are showing an axe in this name ({inventory} inventory line(s)).");
        if (newsCount > 0) sb.AppendLine($"- {newsCount} news/research item(s) touch this security.");

        var headline = LastNote(node, "news", "headline");
        if (!string.IsNullOrWhiteSpace(headline)) sb.AppendLine($"- Latest: _{headline}_");

        return Reply(sb.ToString().TrimEnd(),
            "Who should I call first this morning?", "What are our axes this morning?", "What moved in the market overnight?");
    }

    private async Task<ChatReply> PriorityReplyAsync(string salesperson, CancellationToken ct)
    {
        var brief = await composer.ComposeAsync(salesperson, null, ct);
        var top = brief.PriorityCallList.Take(3).ToList();
        if (top.Count == 0)
        {
            return Reply($"{brief.Salesperson.Name}'s book has no clients flagged for outreach right now — no open RFQs, fresh inquiries or axe matches.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Top {top.Count} calls for **{brief.Salesperson.Name}** this morning:");
        foreach (var c in top)
        {
            sb.AppendLine();
            sb.AppendLine($"**{c.Rank}. {c.ClientName}** ({c.ClientId})" + (string.IsNullOrWhiteSpace(c.ClientType) ? "" : $" · {c.ClientType}"));
            if (c.WhyNow.Count > 0) sb.AppendLine($"   {c.WhyNow[0].Label}");
            sb.AppendLine($"   → {c.SuggestedAction}");
        }
        return Reply(sb.ToString().TrimEnd(),
            $"Tell me about {top[0].ClientId}", "What are our axes this morning?", "What moved in the market overnight?");
    }

    private async Task<ChatReply> AxesReplyAsync(string salesperson, CancellationToken ct)
    {
        var brief = await composer.ComposeAsync(salesperson, null, ct);
        if (brief.InventoryAxes.Count == 0)
        {
            return Reply($"The desk has no inventory axes flagged for {brief.Salesperson.Name}'s book right now.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"**{brief.InventoryAxes.Count} inventory axe(s)** the desk wants to work:");
        foreach (var a in brief.InventoryAxes.Take(6))
        {
            var matched = a.MatchedClients.Count == 0 ? "" : $" — matches {a.MatchedClients.Count} client(s) in book";
            sb.AppendLine($"- **{a.SecurityName}** ({a.SecurityId}) · {a.AxeSide.ToUpperInvariant()}{(string.IsNullOrWhiteSpace(a.Sector) ? "" : " · " + a.Sector)}{matched}.");
        }
        return Reply(sb.ToString().TrimEnd(), "Who should I call first this morning?", "What moved in the market overnight?");
    }

    private async Task<ChatReply> EventsReplyAsync(CancellationToken ct)
    {
        IReadOnlyList<MarketEvent> events;
        try { events = await eventTools.ListEventsAsync(null, ct); }
        catch { events = []; }

        if (events.Count == 0)
        {
            return Reply("There are no current market or news events in the system. Inject one from the News Desk to see the desk react.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"**{events.Count} current event(s)** the desk is weighing:");
        foreach (var e in events.Take(6))
        {
            var scope = string.IsNullOrWhiteSpace(e.Scope) ? "" : $"[{e.Scope}] ";
            sb.AppendLine($"- {scope}**{e.Headline}** · {e.Severity}{(string.IsNullOrWhiteSpace(e.Source) ? "" : " · " + e.Source)}");
        }
        return Reply(sb.ToString().TrimEnd(), "Who should I call first this morning?", "What are our axes this morning?");
    }

    private static ChatReply Help() => new()
    {
        Mode = "DEMO",
        Message =
            "I'm your Trading Desk assistant. I'm grounded in your book, our axes and the live tape. Try:\n" +
            "- **Who should I call first this morning?**\n" +
            "- **Tell me about CL-2006**\n" +
            "- **What's the interest in SEC-3601?**\n" +
            "- **What are our axes this morning?**\n" +
            "- **What moved in the market overnight?**",
        Suggestions = ["Who should I call first this morning?", "What are our axes this morning?", "What moved in the market overnight?"],
    };

    // ------------------------------------------------------------------ helpers

    private async Task<JsonNode?> SafeJsonAsync(string path, CancellationToken ct)
    {
        try { return await mockApi.GetJsonAsync(path, ct); }
        catch { return null; }
    }

    private static int CountArray(JsonNode? node, string key) =>
        node?[key] is JsonArray arr ? arr.Count : 0;

    private static string? LastNote(JsonNode? node, string arrayKey, string field)
    {
        if (node?[arrayKey] is not JsonArray arr || arr.Count == 0) return null;
        var last = arr[arr.Count - 1];
        var v = last?[field];
        var text = v?.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static ChatReply Reply(string message, params string[] suggestions) => new()
    {
        Mode = "DEMO",
        Message = message,
        Suggestions = suggestions.Length == 0 ? null : suggestions,
    };

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(haystack.Contains);

    private static string? Str(JsonNode node, string key)
    {
        var v = node[key];
        return v is null ? null : v.GetValue<object>()?.ToString();
    }

    [GeneratedRegex(@"CL-\d{3,5}", RegexOptions.IgnoreCase)]
    private static partial Regex ClientIdRegex();

    [GeneratedRegex(@"SEC-\d{3,6}", RegexOptions.IgnoreCase)]
    private static partial Regex SecurityIdRegex();
}
