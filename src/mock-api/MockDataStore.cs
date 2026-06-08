using System.Text.Json;
using System.Text.Json.Nodes;

namespace MockApi;

/// <summary>
/// Loads the fictional source-of-record JSON from <c>Data/</c> into in-memory
/// <see cref="JsonNode"/> graphs and exposes simple query helpers used by the
/// mock tool endpoints (T008). All data is fictional.
/// </summary>
public sealed class MockDataStore
{
    private readonly JsonArray _clients;
    private readonly JsonArray _engagement;
    private readonly JsonArray _axes;
    private readonly JsonArray _holdings;
    private readonly JsonObject _marketData;
    private readonly JsonObject _relval;
    private readonly JsonObject _news;
    private readonly JsonArray _newIssues;
    private readonly JsonObject _coalition;

    public MockDataStore(string dataDirectory)
    {
        _clients = LoadArray(dataDirectory, "clients.json");
        _engagement = LoadArray(dataDirectory, "engagement.json");
        _axes = LoadArray(dataDirectory, "axes.json");
        _holdings = LoadArray(dataDirectory, "holdings.json");
        _marketData = LoadObject(dataDirectory, "marketdata.json");
        _relval = LoadObject(dataDirectory, "relval.json");
        _news = LoadObject(dataDirectory, "news.json");
        _newIssues = LoadArray(dataDirectory, "newissues.json");
        _coalition = LoadObject(dataDirectory, "coalition.json");
    }

    public bool IsReady => _clients.Count > 0;

    public JsonNode AllClients() => _clients.DeepClone();

    public JsonNode? Client(string cid) =>
        _clients.FirstOrDefault(c => Eq(c?["id"], cid))?.DeepClone();

    public JsonNode? Engagement(string cid) =>
        _engagement.FirstOrDefault(e => Eq(e?["cid"], cid))?.DeepClone();

    public JsonNode AllAxes() => _axes.DeepClone();

    public JsonNode Holdings(string? cusip, string? state, string? sector)
    {
        IEnumerable<JsonNode?> q = _holdings;
        if (!string.IsNullOrWhiteSpace(cusip)) q = q.Where(h => Eq(h?["cusip"], cusip));
        if (!string.IsNullOrWhiteSpace(state)) q = q.Where(h => Eq(h?["state"], state));
        if (!string.IsNullOrWhiteSpace(sector)) q = q.Where(h => Eq(h?["sector"], sector));
        return new JsonArray(q.Select(h => h?.DeepClone()).ToArray());
    }

    public JsonNode NewIssues() => _newIssues.DeepClone();

    public JsonNode MarketData() => _marketData.DeepClone();

    public JsonNode? RelativeValue(string eventId) => _relval[eventId]?.DeepClone();

    public JsonNode? News(string eventId) => _news[eventId]?.DeepClone();

    public JsonNode? Coalition(string cid) =>
        (_coalition["clients"] as JsonObject)?[cid]?.DeepClone();

    public JsonNode? CoalitionSector(string sector) =>
        (_coalition["sectors"] as JsonObject)?[sector]?.DeepClone();

    private static bool Eq(JsonNode? node, string value) =>
        node is not null && string.Equals(node.GetValue<string>(), value, StringComparison.OrdinalIgnoreCase);

    private static JsonArray LoadArray(string dir, string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(dir, file)))!.AsArray();

    private static JsonObject LoadObject(string dir, string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(dir, file)))!.AsObject();
}
