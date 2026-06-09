using System.Text.Json;
using MockApi.Models;

namespace MockApi;

/// <summary>
/// Loads the Trading Desk / Capital Markets fixtures (<c>Data/td_*.json</c>) into typed,
/// in-memory lists and exposes the query helpers the trading cockpit needs, including the
/// cross-dataset aggregates the demo questions imply (client activity, "who is interested
/// in a security"). All data is fictional. This real dataset replaces the synthetic
/// municipal fixtures; it is additive at the store level and loaded once at startup.
/// </summary>
public sealed class TdDataStore
{
    private readonly List<TdClient> _clients;
    private readonly List<TdSecurity> _securities;
    private readonly List<TdTrade> _trades;
    private readonly List<TdMarketPrint> _prints;
    private readonly List<TdRfq> _rfqs;
    private readonly List<TdCrm> _crm;
    private readonly List<TdHolding> _holdings;
    private readonly List<TdInventory> _inventory;
    private readonly List<TdInquiry> _inquiries;
    private readonly List<TdNews> _news;
    private readonly List<TdResearch> _research;
    private readonly List<TdNarrativeTheme> _themes;

    public TdDataStore(string dataDirectory)
    {
        _clients = Load<TdClient>(dataDirectory, "td_clients.json");
        _securities = Load<TdSecurity>(dataDirectory, "td_securities.json");
        _trades = Load<TdTrade>(dataDirectory, "td_trades.json");
        _prints = Load<TdMarketPrint>(dataDirectory, "td_marketprints.json");
        _rfqs = Load<TdRfq>(dataDirectory, "td_rfqs.json");
        _crm = Load<TdCrm>(dataDirectory, "td_crm.json");
        _holdings = Load<TdHolding>(dataDirectory, "td_holdings.json");
        _inventory = Load<TdInventory>(dataDirectory, "td_inventory.json");
        _inquiries = Load<TdInquiry>(dataDirectory, "td_inquiries.json");
        _news = Load<TdNews>(dataDirectory, "td_news.json");
        _research = Load<TdResearch>(dataDirectory, "td_research.json");
        _themes = Load<TdNarrativeTheme>(dataDirectory, "td_narrative_themes.json");
    }

    public bool IsReady => _clients.Count > 0 && _securities.Count > 0;

    // ---------------------------------------------------------------- Clients

    public IEnumerable<TdClient> Clients(string? type = null, string? region = null, string? salesperson = null, string? assetClass = null)
    {
        IEnumerable<TdClient> q = _clients;
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(c => Eq(c.ClientType, type));
        if (!string.IsNullOrWhiteSpace(region)) q = q.Where(c => Eq(c.ClientRegion, region));
        if (!string.IsNullOrWhiteSpace(salesperson)) q = q.Where(c => Eq(c.CoverageSalesperson, salesperson));
        if (!string.IsNullOrWhiteSpace(assetClass)) q = q.Where(c => Eq(c.PreferredAssetClass, assetClass));
        return q;
    }

    public TdClient? Client(string clientId) => _clients.FirstOrDefault(c => Eq(c.ClientId, clientId));

    // ---------------------------------------------------------------- Securities

    public IEnumerable<TdSecurity> Securities(string? assetClass = null, string? sector = null, string? issuer = null, string? region = null)
    {
        IEnumerable<TdSecurity> q = _securities;
        if (!string.IsNullOrWhiteSpace(assetClass)) q = q.Where(s => Eq(s.AssetClass, assetClass));
        if (!string.IsNullOrWhiteSpace(sector)) q = q.Where(s => Eq(s.Sector, sector));
        if (!string.IsNullOrWhiteSpace(issuer)) q = q.Where(s => Contains(s.Issuer, issuer));
        if (!string.IsNullOrWhiteSpace(region)) q = q.Where(s => Eq(s.Region, region));
        return q;
    }

    public TdSecurity? Security(string securityId) => _securities.FirstOrDefault(s => Eq(s.SecurityId, securityId));

    // ---------------------------------------------------------------- Transactional resources

    public IEnumerable<TdTrade> Trades(string? clientId = null, string? securityId = null, string? direction = null, DateOnly? since = null)
    {
        IEnumerable<TdTrade> q = _trades;
        if (!string.IsNullOrWhiteSpace(clientId)) q = q.Where(t => Eq(t.ClientId, clientId));
        if (!string.IsNullOrWhiteSpace(securityId)) q = q.Where(t => Eq(t.SecurityId, securityId));
        if (!string.IsNullOrWhiteSpace(direction)) q = q.Where(t => Eq(t.Direction, direction));
        if (since is DateOnly s) q = q.Where(t => OnOrAfter(t.TradeDate, s));
        return q;
    }

    public IEnumerable<TdRfq> Rfqs(string? clientId = null, string? securityId = null, string? status = null, DateOnly? since = null)
    {
        IEnumerable<TdRfq> q = _rfqs;
        if (!string.IsNullOrWhiteSpace(clientId)) q = q.Where(r => Eq(r.ClientId, clientId));
        if (!string.IsNullOrWhiteSpace(securityId)) q = q.Where(r => Eq(r.SecurityId, securityId));
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => Eq(r.ResponseStatus, status));
        if (since is DateOnly s) q = q.Where(r => OnOrAfter(r.RfqDate, s));
        return q;
    }

    public IEnumerable<TdCrm> Crm(string? clientId = null, string? urgency = null, DateOnly? since = null)
    {
        IEnumerable<TdCrm> q = _crm;
        if (!string.IsNullOrWhiteSpace(clientId)) q = q.Where(c => Eq(c.ClientId, clientId));
        if (!string.IsNullOrWhiteSpace(urgency)) q = q.Where(c => Eq(c.Urgency, urgency));
        if (since is DateOnly s) q = q.Where(c => OnOrAfter(c.EntryDate, s));
        return q;
    }

    public IEnumerable<TdHolding> Holdings(string? clientId = null, string? securityId = null)
    {
        IEnumerable<TdHolding> q = _holdings;
        if (!string.IsNullOrWhiteSpace(clientId)) q = q.Where(h => Eq(h.ClientId, clientId));
        if (!string.IsNullOrWhiteSpace(securityId)) q = q.Where(h => Eq(h.SecurityId, securityId));
        return q;
    }

    /// <summary>Dealer inventory / axes — what each desk is long or short and showing.</summary>
    public IEnumerable<TdInventory> Inventory(string? securityId = null, string? desk = null)
    {
        IEnumerable<TdInventory> q = _inventory;
        if (!string.IsNullOrWhiteSpace(securityId)) q = q.Where(i => Eq(i.SecurityId, securityId));
        if (!string.IsNullOrWhiteSpace(desk)) q = q.Where(i => Eq(i.Desk, desk));
        return q;
    }

    public IEnumerable<TdInquiry> Inquiries(string? clientId = null, string? securityId = null, string? sentiment = null, DateOnly? since = null)
    {
        IEnumerable<TdInquiry> q = _inquiries;
        if (!string.IsNullOrWhiteSpace(clientId)) q = q.Where(i => Eq(i.ClientId, clientId));
        if (!string.IsNullOrWhiteSpace(securityId)) q = q.Where(i => Eq(i.InferredSecurity, securityId));
        if (!string.IsNullOrWhiteSpace(sentiment)) q = q.Where(i => Eq(i.Sentiment, sentiment));
        if (since is DateOnly s) q = q.Where(i => OnOrAfter(i.InquiryDate, s));
        return q;
    }

    public IEnumerable<TdNews> News(string? securityId = null, string? sector = null, string? macroTheme = null, DateOnly? since = null)
    {
        IEnumerable<TdNews> q = _news;
        if (!string.IsNullOrWhiteSpace(securityId)) q = q.Where(n => Eq(n.RelatedSecurityId, securityId));
        if (!string.IsNullOrWhiteSpace(sector)) q = q.Where(n => Eq(n.RelatedSector, sector));
        if (!string.IsNullOrWhiteSpace(macroTheme)) q = q.Where(n => Eq(n.MacroTheme, macroTheme));
        if (since is DateOnly s) q = q.Where(n => OnOrAfter(n.PublishTimestamp, s));
        return q;
    }

    public IEnumerable<TdResearch> Research(string? securityId = null, string? sector = null, string? ratingAction = null)
    {
        IEnumerable<TdResearch> q = _research;
        if (!string.IsNullOrWhiteSpace(securityId)) q = q.Where(r => Eq(r.RelatedSecurityId, securityId));
        if (!string.IsNullOrWhiteSpace(sector)) q = q.Where(r => Eq(r.Sector, sector));
        if (!string.IsNullOrWhiteSpace(ratingAction)) q = q.Where(r => Eq(r.RatingAction, ratingAction));
        return q;
    }

    public IReadOnlyList<TdNarrativeTheme> NarrativeThemes() => _themes;

    // ---------------------------------------------------------------- Cross-dataset aggregates

    /// <summary>
    /// A 360° activity summary for one client over a trailing window: holdings, trades, RFQs,
    /// inquiries and CRM touches. Backs prompts like "summarize recent activity for client X".
    /// </summary>
    public object? ClientActivity(string clientId, DateOnly? since = null)
    {
        var client = Client(clientId);
        if (client is null) return null;
        return new
        {
            client,
            since = since?.ToString("yyyy-MM-dd"),
            holdings = Holdings(clientId: clientId).ToList(),
            trades = Trades(clientId: clientId, since: since).ToList(),
            rfqs = Rfqs(clientId: clientId, since: since).ToList(),
            inquiries = Inquiries(clientId: clientId, since: since).ToList(),
            crm = Crm(clientId: clientId, since: since).ToList(),
        };
    }

    /// <summary>
    /// Everything pointing at one security: the security, dealer inventory/axes, plus the
    /// holders, traders, RFQs, inquiries, news and research touching it. Backs prompts like
    /// "which clients have shown interest in security X".
    /// </summary>
    public object? SecurityInterest(string securityId, DateOnly? since = null)
    {
        var security = Security(securityId);
        if (security is null) return null;
        return new
        {
            security,
            since = since?.ToString("yyyy-MM-dd"),
            inventory = Inventory(securityId: securityId).ToList(),
            holders = Holdings(securityId: securityId).ToList(),
            trades = Trades(securityId: securityId, since: since).ToList(),
            rfqs = Rfqs(securityId: securityId, since: since).ToList(),
            inquiries = Inquiries(securityId: securityId, since: since).ToList(),
            news = News(securityId: securityId, since: since).ToList(),
            research = Research(securityId: securityId).ToList(),
        };
    }

    // ---------------------------------------------------------------- helpers

    private static bool Eq(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? a, string? b) =>
        a is not null && b is not null && a.Contains(b, StringComparison.OrdinalIgnoreCase);

    private static bool OnOrAfter(string? value, DateOnly since)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var datePart = value.Length >= 10 ? value[..10] : value;
        return DateOnly.TryParse(datePart, out var d) && d >= since;
    }

    private static List<T> Load<T>(string dir, string file)
    {
        var path = Path.Combine(dir, file);
        if (!File.Exists(path)) return [];
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(json, CbJson.Options) ?? [];
    }
}
