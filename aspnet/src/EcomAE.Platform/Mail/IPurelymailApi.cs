using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Mail;

public interface IPurelymailApi
{
    Task<PurelymailCallResult> AddDomainAsync(string domain, CancellationToken cancellationToken);
    Task<PurelymailCallResult> GetOwnershipCodeAsync(CancellationToken cancellationToken);
    Task<PurelymailCallResult> ListDomainsAsync(CancellationToken cancellationToken);
    Task<PurelymailCallResult> RecheckDnsAsync(string domain, CancellationToken cancellationToken);
    Task<PurelymailCallResult> CreateUserAsync(string address, string password, CancellationToken cancellationToken);
    Task<PurelymailCallResult> ListUsersAsync(CancellationToken cancellationToken);
    Task<PurelymailCallResult> DeleteUserAsync(string address, CancellationToken cancellationToken);
    Task<PurelymailCallResult> CreateRoutingRuleAsync(string domain, string matchUser, string target, CancellationToken cancellationToken);
}

public sealed class PurelymailHttpApi : IPurelymailApi
{
    public const string HttpClientName = "purelymail";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _http;
    private readonly IPlatformMailDirectory _directory;
    private readonly PurelymailOptions _options;

    public PurelymailHttpApi(
        IHttpClientFactory http,
        IPlatformMailDirectory directory,
        IOptions<PurelymailOptions> options)
    {
        _http = http;
        _directory = directory;
        _options = options.Value;
    }

    public Task<PurelymailCallResult> AddDomainAsync(string domain, CancellationToken cancellationToken)
        => PostAsync("addDomain", new { domainName = PlatformMailNormalize.Domain(domain) }, cancellationToken);

    public Task<PurelymailCallResult> GetOwnershipCodeAsync(CancellationToken cancellationToken)
        => PostAsync("getOwnershipCode", new { }, cancellationToken);

    public Task<PurelymailCallResult> ListDomainsAsync(CancellationToken cancellationToken)
        => PostAsync("listDomains", new { }, cancellationToken);

    public Task<PurelymailCallResult> RecheckDnsAsync(string domain, CancellationToken cancellationToken)
        => PostAsync("updateDomainSettings", new { name = PlatformMailNormalize.Domain(domain), recheckDns = true }, cancellationToken);

    public Task<PurelymailCallResult> CreateUserAsync(string address, string password, CancellationToken cancellationToken)
    {
        var (local, domain) = PlatformMailNormalize.SplitAddress(address);
        return PostAsync("createUser", new { userName = local, domainName = domain, password }, cancellationToken);
    }

    public Task<PurelymailCallResult> ListUsersAsync(CancellationToken cancellationToken)
        => PostAsync("listUser", new { }, cancellationToken);

    public Task<PurelymailCallResult> DeleteUserAsync(string address, CancellationToken cancellationToken)
        => PostAsync("deleteUser", new { userName = address.Trim().ToLowerInvariant() }, cancellationToken);

    public Task<PurelymailCallResult> CreateRoutingRuleAsync(string domain, string matchUser, string target, CancellationToken cancellationToken)
        => PostAsync("createRoutingRule", new
        {
            domainName = PlatformMailNormalize.Domain(domain),
            prefix = false,
            matchUser = (matchUser ?? string.Empty).Trim().ToLowerInvariant(),
            targetAddresses = new[] { target.Trim().ToLowerInvariant() }
        }, cancellationToken);

    private async Task<PurelymailCallResult> PostAsync(string endpoint, object body, CancellationToken cancellationToken)
    {
        var token = _directory.ResolveApiToken(_options);
        if (string.IsNullOrWhiteSpace(token))
        {
            return PurelymailCallResult.Fail("token_missing", "Set ECOMAE_PURELYMAIL_API_TOKEN or save the API token on Super CP platform mail.");
        }

        try
        {
            var client = _http.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.TryAddWithoutValidation(PurelymailDns.ApiTokenHeader, token);
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Parse(response.IsSuccessStatusCode, raw);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return PurelymailCallResult.Fail("transport", ex.Message);
        }
    }

    public static PurelymailCallResult Parse(bool httpOk, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return httpOk
                ? PurelymailCallResult.Success("Purelymail accepted the request.")
                : PurelymailCallResult.Fail("empty", "Purelymail returned an empty body.");
        }

        try
        {
            var node = JsonNode.Parse(raw);
            var type = Read(node, "type");
            var code = Read(node, "code");
            var message = Read(node, "message");
            if (string.Equals(type, "error", StringComparison.OrdinalIgnoreCase)
                || (!httpOk && !string.Equals(type, "success", StringComparison.OrdinalIgnoreCase)))
            {
                return PurelymailCallResult.Fail(
                    string.IsNullOrWhiteSpace(code) ? "api_error" : code,
                    string.IsNullOrWhiteSpace(message) ? "Purelymail rejected the request." : message,
                    raw);
            }

            var ownership = Read(node?["result"], "code")
                            ?? Read(node?["result"], "ownershipCode")
                            ?? Read(node, "ownershipCode")
                            ?? Read(node, "code");
            if (!string.IsNullOrWhiteSpace(ownership)
                && ownership.Contains("purelymail", StringComparison.OrdinalIgnoreCase))
            {
                return PurelymailCallResult.Success(ownership, raw);
            }

            return PurelymailCallResult.Success(
                string.IsNullOrWhiteSpace(message) ? "Purelymail accepted the request." : message,
                raw);
        }
        catch (JsonException)
        {
            return httpOk
                ? PurelymailCallResult.Success("Purelymail accepted the request.", raw)
                : PurelymailCallResult.Fail("parse", raw.Length > 180 ? raw[..180] : raw, raw);
        }
    }

    public static IReadOnlyList<string> ParseDomainNames(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            var node = JsonNode.Parse(raw);
            var found = new List<string>();
            CollectDomains(node?["result"]?["domains"] ?? node?["result"] ?? node?["domains"], found);
            return found
                .Select(PlatformMailNormalize.Domain)
                .Where(d => d.Length > 0 && d.Contains('.', StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void CollectDomains(JsonNode? node, List<string> found)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                CollectDomains(item, found);
            }

            return;
        }

        var name = Read(node, "name") ?? Read(node, "domainName") ?? Read(node, "domain");
        if (string.IsNullOrWhiteSpace(name) && node is JsonValue)
        {
            name = node.GetValue<string>();
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            found.Add(name);
        }
    }

    private static string? Read(JsonNode? node, string name)
        => node?[name]?.GetValue<string>();
}

public sealed class InMemoryPurelymailApi : IPurelymailApi
{
    public List<string> Domains { get; } = [];
    public List<string> Users { get; } = [];
    public List<string> Calls { get; } = [];
    public string OwnershipCode { get; set; } = "purelymail-ownership-test";
    public bool FailNext { get; set; }

    public Task<PurelymailCallResult> AddDomainAsync(string domain, CancellationToken cancellationToken)
    {
        Calls.Add("addDomain:" + PlatformMailNormalize.Domain(domain));
        if (FailNext)
        {
            FailNext = false;
            return Task.FromResult(PurelymailCallResult.Fail("exists", "Domain already on Purelymail."));
        }

        var name = PlatformMailNormalize.Domain(domain);
        if (name.Length == 0)
        {
            return Task.FromResult(PurelymailCallResult.Fail("invalid_domain", "Domain is required."));
        }

        if (!Domains.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            Domains.Add(name);
        }

        return Task.FromResult(PurelymailCallResult.Success("Domain added."));
    }

    public Task<PurelymailCallResult> GetOwnershipCodeAsync(CancellationToken cancellationToken)
    {
        Calls.Add("getOwnershipCode");
        return Task.FromResult(PurelymailCallResult.Success(OwnershipCode));
    }

    public Task<PurelymailCallResult> ListDomainsAsync(CancellationToken cancellationToken)
    {
        Calls.Add("listDomains");
        var json = "{\"type\":\"success\",\"result\":{\"domains\":["
                   + string.Join(",", Domains.Select(d => "{\"name\":\"" + d + "\"}"))
                   + "]}}";
        return Task.FromResult(PurelymailCallResult.Success(string.Join(",", Domains), json));
    }

    public Task<PurelymailCallResult> RecheckDnsAsync(string domain, CancellationToken cancellationToken)
    {
        Calls.Add("recheckDns:" + PlatformMailNormalize.Domain(domain));
        return Task.FromResult(PurelymailCallResult.Success("DNS recheck queued."));
    }

    public Task<PurelymailCallResult> CreateUserAsync(string address, string password, CancellationToken cancellationToken)
    {
        Calls.Add("createUser:" + address.Trim().ToLowerInvariant());
        if (string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(PurelymailCallResult.Fail("password_required", "Mailbox password is required."));
        }

        var user = address.Trim().ToLowerInvariant();
        if (!Users.Contains(user, StringComparer.OrdinalIgnoreCase))
        {
            Users.Add(user);
        }

        return Task.FromResult(PurelymailCallResult.Success("Mailbox created."));
    }

    public Task<PurelymailCallResult> ListUsersAsync(CancellationToken cancellationToken)
    {
        Calls.Add("listUser");
        return Task.FromResult(PurelymailCallResult.Success(string.Join(",", Users)));
    }

    public Task<PurelymailCallResult> DeleteUserAsync(string address, CancellationToken cancellationToken)
    {
        var user = address.Trim().ToLowerInvariant();
        Calls.Add("deleteUser:" + user);
        Users.RemoveAll(u => string.Equals(u, user, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(PurelymailCallResult.Success("Mailbox deleted."));
    }

    public Task<PurelymailCallResult> CreateRoutingRuleAsync(string domain, string matchUser, string target, CancellationToken cancellationToken)
    {
        Calls.Add("createRoutingRule:" + PlatformMailNormalize.Domain(domain) + ":" + matchUser + "->" + target);
        return Task.FromResult(PurelymailCallResult.Success("Routing rule created."));
    }
}
