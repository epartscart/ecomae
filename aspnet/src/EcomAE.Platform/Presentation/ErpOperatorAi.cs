using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

public static class ErpOperatorAiNames
{
    public static bool TryNormalize(string? raw, out string provider)
    {
        var compact = Regex.Replace(raw ?? string.Empty, @"[\s.]+", "", RegexOptions.CultureInvariant).ToLowerInvariant();
        provider = compact switch
        {
            "chatgpt" or "openai" => "chatgpt",
            "cursor" => "cursor",
            "devin" or "devinai" => "devin",
            _ => string.Empty,
        };
        return provider.Length > 0;
    }

    public static string Label(string provider)
        => provider switch
        {
            "chatgpt" => "ChatGPT",
            "cursor" => "Cursor",
            "devin" => "Devin",
            _ => "The tool",
        };
}

public sealed record ErpOperatorAiReply(
    bool Ok,
    string Say,
    string Href,
    bool Ready,
    string ExternalUrl,
    IReadOnlyList<string> Connected);

public sealed record ErpAiHttpCall(string Method, string Url, string Bearer, string? JsonBody);

public sealed record ErpAiHttpResult(int Status, string Body);

public interface IErpOperatorAiGateway
{
    Task<ErpAiHttpResult> SendAsync(ErpAiHttpCall call, CancellationToken cancellationToken);
}

public interface IErpOperatorAiDispatcher
{
    Task<ErpOperatorAiReply> StatusAsync(int userId, CancellationToken cancellationToken);

    Task<ErpOperatorAiReply> ConnectAsync(
        int userId,
        string? provider,
        string? token,
        string? org,
        string? repo,
        string? model,
        CancellationToken cancellationToken);

    Task<ErpOperatorAiReply> DisconnectAsync(int userId, string? provider, CancellationToken cancellationToken);

    Task<ErpOperatorAiReply> AskAsync(
        int userId,
        string provider,
        string task,
        DateOnly today,
        CancellationToken cancellationToken);
}

public sealed class ErpOperatorAiGateway : IErpOperatorAiGateway
{
    public const string HttpClientName = "erp-operator-ai";

    private readonly IHttpClientFactory _clients;

    public ErpOperatorAiGateway(IHttpClientFactory clients)
    {
        _clients = clients;
    }

    public static bool IsAllowed(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) || uri.UserInfo.Length > 0)
        {
            return false;
        }

        var path = uri.AbsolutePath;
        if (uri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase))
        {
            return path is "/v1/models" or "/v1/chat/completions";
        }

        if (uri.Host.Equals("api.cursor.com", StringComparison.OrdinalIgnoreCase))
        {
            return path is "/v1/me" or "/v1/agents";
        }

        if (uri.Host.Equals("api.devin.ai", StringComparison.OrdinalIgnoreCase))
        {
            if (path is "/v3/self" or "/v3/enterprise/organizations")
            {
                return true;
            }

            return Regex.IsMatch(path, @"^/v3/organizations/[A-Za-z0-9_-]{1,80}/sessions$", RegexOptions.CultureInvariant);
        }

        return false;
    }

    public async Task<ErpAiHttpResult> SendAsync(ErpAiHttpCall call, CancellationToken cancellationToken)
    {
        if (call.Method is not ("GET" or "POST") || !IsAllowed(call.Url) || string.IsNullOrWhiteSpace(call.Bearer))
        {
            return new ErpAiHttpResult(0, string.Empty);
        }

        try
        {
            var client = _clients.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(new HttpMethod(call.Method), call.Url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", call.Bearer);
            request.Headers.Accept.ParseAdd("application/json");
            if (call.JsonBody is not null)
            {
                request.Content = new StringContent(call.JsonBody, Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (body.Length > 16000)
            {
                body = body[..16000];
            }

            return new ErpAiHttpResult((int)response.StatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new ErpAiHttpResult(0, string.Empty);
        }
    }
}

public sealed class ErpOperatorAiDispatcher : IErpOperatorAiDispatcher
{
    public const string DefaultModel = "gpt-6-astra";

    private const string ChatSystem =
        "You help an ERP operator. Answer in short plain English. "
        + "If the request can be prepared by the ERP, add one line that starts with ERP: and then a command such as: "
        + "create a receipt voucher for customer NAME for AMOUNT from the ACCOUNT account. "
        + "Or: payment voucher for supplier NAME amount AMOUNT account ACCOUNT. "
        + "Or: COUNTRY sales report for last month by customer. "
        + "Or: open purchase orders. "
        + "Or: what do I need for a receipt voucher. "
        + "The ERP fills the form. It does not post money. Never say a voucher was posted. "
        + "If the request is not one of those, answer it and do not include an ERP line.";

    private readonly IErpOperatorAiSecretStore _store;
    private readonly IErpOperatorAiGateway _gateway;
    private readonly IErpMasterLookupReadService _lookups;

    public ErpOperatorAiDispatcher(
        IErpOperatorAiSecretStore store,
        IErpOperatorAiGateway gateway,
        IErpMasterLookupReadService lookups)
    {
        _store = store;
        _gateway = gateway;
        _lookups = lookups;
    }

    public async Task<ErpOperatorAiReply> StatusAsync(int userId, CancellationToken cancellationToken)
    {
        var connected = await ConnectedAsync(userId, cancellationToken).ConfigureAwait(false);
        return new ErpOperatorAiReply(true, StatusSay(connected), string.Empty, false, string.Empty, connected);
    }

    public async Task<ErpOperatorAiReply> ConnectAsync(
        int userId,
        string? providerRaw,
        string? tokenRaw,
        string? orgRaw,
        string? repoRaw,
        string? modelRaw,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return Fail("Sign in to connect a tool.");
        }

        if (!ErpOperatorAiNames.TryNormalize(providerRaw, out var provider))
        {
            return Fail("Choose ChatGPT, Cursor, or Devin.");
        }

        var token = CleanToken(tokenRaw);
        if (token.Length == 0)
        {
            return Fail("Paste the key from the account you log in to.");
        }

        var label = ErpOperatorAiNames.Label(provider);
        var model = CleanModel(modelRaw);
        var repo = (repoRaw ?? string.Empty).Trim();
        var org = (orgRaw ?? string.Empty).Trim();
        if (provider == "cursor" && repo.Length > 0 && !ValidRepo(repo))
        {
            return Fail("The repository address must be an https address without a password in it.");
        }

        if (provider == "devin" && org.Length > 0 && !ValidOrg(org))
        {
            return Fail("The organisation id should be the value shown in your Devin settings.");
        }

        if (provider == "chatgpt" && (modelRaw ?? string.Empty).Trim().Length > 0 && model.Length == 0)
        {
            return Fail("The model id can use letters, numbers, dots, and hyphens.");
        }

        var probe = provider switch
        {
            "cursor" => await SendAsync("GET", "https://api.cursor.com/v1/me", token, null, cancellationToken).ConfigureAwait(false),
            "devin" => await SendAsync("GET", "https://api.devin.ai/v3/self", token, null, cancellationToken).ConfigureAwait(false),
            _ => await SendAsync("GET", "https://api.openai.com/v1/models", token, null, cancellationToken).ConfigureAwait(false),
        };
        if (probe.Status == 0)
        {
            return Fail(label + " did not answer. Try again in a moment.");
        }

        if (probe.Status is 401 or 403)
        {
            return Fail(label + " refused that key. Check the key from your account.");
        }

        if (probe.Status != 200)
        {
            return Fail(label + " could not confirm the key. " + PublicError(probe.Body, token));
        }

        if (provider == "devin")
        {
            org = await ResolveDevinOrgAsync(token, org, probe.Body, cancellationToken).ConfigureAwait(false);
            if (!ValidOrg(org))
            {
                return Fail("Devin accepted the key. Paste the organisation id from Devin settings so a session can start.");
            }
        }

        var extras = JsonSerializer.Serialize(new { org = provider == "devin" ? org : string.Empty, repo = provider == "cursor" ? repo : string.Empty, model = provider == "chatgpt" ? model : string.Empty });
        var saved = await _store.SaveAsync(userId, provider, token, extras, cancellationToken).ConfigureAwait(false);
        if (!saved)
        {
            return Fail("The company database is not available, so the key was not saved.");
        }

        var connected = await ConnectedAsync(userId, cancellationToken).ConfigureAwait(false);
        var say = label + " is connected on your account. Say: ask " + label + ", then the work you want. A voucher is posted only when you press Validate and post.";
        if (provider == "cursor" && repo.Length > 0)
        {
            say += " Cursor will start in the repository you saved.";
        }

        return new ErpOperatorAiReply(true, say, string.Empty, false, string.Empty, connected);
    }

    public async Task<ErpOperatorAiReply> DisconnectAsync(int userId, string? providerRaw, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return Fail("Sign in to remove a tool.");
        }

        if (!ErpOperatorAiNames.TryNormalize(providerRaw, out var provider))
        {
            return Fail("Choose ChatGPT, Cursor, or Devin.");
        }

        await _store.RemoveAsync(userId, provider, cancellationToken).ConfigureAwait(false);
        var connected = await ConnectedAsync(userId, cancellationToken).ConfigureAwait(false);
        return new ErpOperatorAiReply(true, ErpOperatorAiNames.Label(provider) + " is removed from this sign-in.", string.Empty, false, string.Empty, connected);
    }

    public async Task<ErpOperatorAiReply> AskAsync(
        int userId,
        string provider,
        string task,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (!ErpOperatorAiNames.TryNormalize(provider, out provider))
        {
            return Fail("Choose ChatGPT, Cursor, or Devin.");
        }

        var label = ErpOperatorAiNames.Label(provider);
        if (userId <= 0)
        {
            return Fail("Sign in to ask " + label + ".");
        }

        task = Regex.Replace(task ?? string.Empty, @"\s+", " ").Trim();
        if (task.Length == 0)
        {
            return Fail("Say what " + label + " should do.");
        }

        if (task.Length > 2000)
        {
            task = task[..2000];
        }

        var secret = await _store.ReadAsync(userId, provider, cancellationToken).ConfigureAwait(false);
        if (secret.Found && !secret.Readable)
        {
            return Fail(label + " needs connecting again. Open Voice and paste the key from your account.");
        }

        if (!secret.Found || secret.Token.Length == 0)
        {
            return Fail(label + " is not connected. Open Voice, paste the key from your " + label + " account, and save it. Then ask again.");
        }

        var (org, repo, model) = ReadExtras(secret.Extras);
        return provider switch
        {
            "cursor" => await AskCursorAsync(secret.Token, task, repo, cancellationToken).ConfigureAwait(false),
            "devin" => await AskDevinAsync(userId, secret.Token, task, org, cancellationToken).ConfigureAwait(false),
            _ => await AskChatAsync(secret.Token, task, model, today, cancellationToken).ConfigureAwait(false),
        };
    }

    private async Task<ErpOperatorAiReply> AskChatAsync(string token, string task, string model, DateOnly today, CancellationToken cancellationToken)
    {
        if (model.Length == 0)
        {
            model = DefaultModel;
        }

        var payload = JsonSerializer.Serialize(new
        {
            model,
            store = false,
            messages = new object[]
            {
                new { role = "system", content = ChatSystem },
                new { role = "user", content = task },
            },
        });
        var result = await SendAsync("POST", "https://api.openai.com/v1/chat/completions", token, payload, cancellationToken).ConfigureAwait(false);
        if (result.Status == 0)
        {
            return Fail("ChatGPT did not answer. Try again in a moment.");
        }

        if (result.Status is 401 or 403)
        {
            return Fail("ChatGPT refused that key. Open Voice and paste the key again.");
        }

        if (result.Status != 200)
        {
            return Fail("ChatGPT could not do that. " + PublicError(result.Body, token));
        }

        var content = Scrub(ReadChatContent(result.Body), token);
        if (content.Length == 0)
        {
            return Fail("ChatGPT returned no answer.");
        }

        var command = TakeErpLine(content, out var rest);
        if (command.Length == 0)
        {
            return new ErpOperatorAiReply(true, Cap("ChatGPT says: " + content), string.Empty, false, string.Empty, []);
        }

        var prepared = await new ErpVoicePrepareService(_lookups).PrepareAsync(command, today, cancellationToken).ConfigureAwait(false);
        if (prepared.Intent is not ("receipt" or "payment" or "open" or "report" or "requirements" or "guide"))
        {
            return new ErpOperatorAiReply(true, Cap("ChatGPT says: " + (rest.Length > 0 ? rest : content)), string.Empty, false, string.Empty, []);
        }

        var say = prepared.Say;
        if (rest.Length > 0)
        {
            say = Cap(rest, 280) + " " + prepared.Say;
        }

        var href = prepared.Href.StartsWith("/erp", StringComparison.Ordinal) ? prepared.Href : string.Empty;
        var ready = prepared.Ready && href.Length > 0;
        return new ErpOperatorAiReply(true, Cap("ChatGPT answered. " + say), href, ready, string.Empty, []);
    }

    private async Task<ErpOperatorAiReply> AskCursorAsync(string token, string task, string repo, CancellationToken cancellationToken)
    {
        var prompt = "The ERP operator asked you to do this work: " + task
            + " Use the operator's Cursor account and do the work they asked for."
            + " Do not post vouchers, payments, or journals. The operator confirms those in the ERP.";
        var name = task.Length > 80 ? task[..80] : task;
        var pullRequest = task.Contains("pull request", StringComparison.OrdinalIgnoreCase)
            || task.Contains("merge request", StringComparison.OrdinalIgnoreCase);
        string payload;
        if (ValidRepo(repo))
        {
            payload = JsonSerializer.Serialize(new
            {
                prompt = new { text = prompt },
                name,
                repos = new[] { new { url = repo } },
                autoCreatePR = pullRequest,
            });
        }
        else
        {
            payload = JsonSerializer.Serialize(new
            {
                prompt = new { text = prompt },
                name,
                autoCreatePR = pullRequest,
            });
        }

        var result = await SendAsync("POST", "https://api.cursor.com/v1/agents", token, payload, cancellationToken).ConfigureAwait(false);
        if (result.Status == 0)
        {
            return Fail("Cursor did not answer. Try again in a moment.");
        }

        if (result.Status is 401 or 403)
        {
            return Fail("Cursor refused that key. Open Voice and paste the key again.");
        }

        if (result.Status is not (200 or 201))
        {
            return Fail("Cursor could not start that work. " + PublicError(result.Body, token));
        }

        var url = ReadCursorUrl(result.Body);
        var say = url.Length > 0
            ? "Cursor started that work on your account. Open the agent to watch it. Nothing is posted in the ERP until you press Validate and post."
            : "Cursor accepted the work. Open Cursor to watch the agent. Nothing is posted in the ERP until you press Validate and post.";
        return new ErpOperatorAiReply(true, say, string.Empty, false, url, []);
    }

    private async Task<ErpOperatorAiReply> AskDevinAsync(int userId, string token, string task, string org, CancellationToken cancellationToken)
    {
        if (!ValidOrg(org))
        {
            var self = await SendAsync("GET", "https://api.devin.ai/v3/self", token, null, cancellationToken).ConfigureAwait(false);
            org = self.Status == 200
                ? await ResolveDevinOrgAsync(token, string.Empty, self.Body, cancellationToken).ConfigureAwait(false)
                : string.Empty;
            if (ValidOrg(org))
            {
                var extras = JsonSerializer.Serialize(new { org, repo = string.Empty, model = string.Empty });
                await _store.SaveAsync(userId, "devin", token, extras, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!ValidOrg(org))
        {
            return Fail("Devin is connected. Paste the organisation id from Devin settings, then ask again.");
        }

        var title = task.Length > 80 ? task[..80] : task;
        var prompt = "The ERP operator asked you to do this work: " + task
            + " Do the work they asked for on this Devin account."
            + " Do not post vouchers, payments, or journals. The operator confirms those in the ERP.";
        var payload = JsonSerializer.Serialize(new { prompt, title });
        var result = await SendAsync("POST", "https://api.devin.ai/v3/organizations/" + org + "/sessions", token, payload, cancellationToken).ConfigureAwait(false);
        if (result.Status == 0)
        {
            return Fail("Devin did not answer. Try again in a moment.");
        }

        if (result.Status is 401 or 403)
        {
            return Fail("Devin refused that key. Open Voice and paste the key again.");
        }

        if (result.Status is not (200 or 201))
        {
            return Fail("Devin could not start that work. " + PublicError(result.Body, token));
        }

        var url = ReadDevinUrl(result.Body);
        var say = url.Length > 0
            ? "Devin started that work on your account. Open the session to watch it. Nothing is posted in the ERP until you press Validate and post."
            : "Devin accepted the work. Open Devin to watch the session. Nothing is posted in the ERP until you press Validate and post.";
        return new ErpOperatorAiReply(true, say, string.Empty, false, url, []);
    }

    private async Task<string> ResolveDevinOrgAsync(string token, string supplied, string selfBody, CancellationToken cancellationToken)
    {
        if (ValidOrg(supplied))
        {
            return supplied;
        }

        var fromSelf = OrgFromJson(selfBody, "org_id");
        if (ValidOrg(fromSelf))
        {
            return fromSelf;
        }

        var list = await SendAsync("GET", "https://api.devin.ai/v3/enterprise/organizations", token, null, cancellationToken).ConfigureAwait(false);
        if (list.Status != 200)
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(list.Body);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() != 1)
            {
                return string.Empty;
            }

            return ValidOrg(OrgFromElement(items[0])) ? OrgFromElement(items[0]) : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private async Task<IReadOnlyList<string>> ConnectedAsync(int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return [];
        }

        var rows = await _store.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        return new[] { "chatgpt", "cursor", "devin" }
            .Where(name => rows.Any(row => string.Equals(row, name, StringComparison.Ordinal)))
            .ToArray();
    }

    private async Task<ErpAiHttpResult> SendAsync(string method, string url, string token, string? json, CancellationToken cancellationToken)
        => await _gateway.SendAsync(new ErpAiHttpCall(method, url, token, json), cancellationToken).ConfigureAwait(false);

    private static string StatusSay(IReadOnlyList<string> connected)
    {
        if (connected.Count == 0)
        {
            return "No tool is connected yet. Open Voice and paste the key from your own ChatGPT, Cursor, or Devin account.";
        }

        return string.Join(" and ", connected.Select(ErpOperatorAiNames.Label)) + " connected. Say ask, then the tool name, then the work.";
    }

    private static string CleanToken(string? raw)
    {
        var token = (raw ?? string.Empty).Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token[7..].Trim();
        }

        if (token.Length < 16 || token.Length > 512 || token.Any(char.IsWhiteSpace))
        {
            return string.Empty;
        }

        return token;
    }

    private static string CleanModel(string? raw)
    {
        var model = (raw ?? string.Empty).Trim();
        if (model.Length == 0)
        {
            return string.Empty;
        }

        return Regex.IsMatch(model, @"^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant) ? model : string.Empty;
    }

    private static bool ValidOrg(string org)
        => Regex.IsMatch(org, @"^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant);

    private static bool ValidRepo(string repo)
    {
        if (repo.Length is 0 or > 300 || !Uri.TryCreate(repo, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && uri.UserInfo.Length == 0
            && uri.Host.Length > 0;
    }

    public static bool IsOperatorUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) || uri.UserInfo.Length > 0)
        {
            return false;
        }

        if (uri.Host.Equals("cursor.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.StartsWith("/agents/", StringComparison.Ordinal);
        }

        return uri.Host.Equals("app.devin.ai", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/sessions/", StringComparison.Ordinal);
    }

    private static (string Org, string Repo, string Model) ReadExtras(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return (Text(doc.RootElement, "org"), Text(doc.RootElement, "repo"), Text(doc.RootElement, "model"));
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty, string.Empty);
        }
    }

    private static string ReadChatContent(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            if (!choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
            {
                return string.Empty;
            }

            return content.ValueKind == JsonValueKind.String ? content.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string TakeErpLine(string content, out string rest)
    {
        var kept = new List<string>();
        var command = string.Empty;
        foreach (var line in content.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = line.Trim().Trim('`').Trim();
            if (command.Length == 0 && trimmed.StartsWith("ERP:", StringComparison.OrdinalIgnoreCase))
            {
                command = trimmed[4..].Trim();
                continue;
            }

            if (trimmed.Length > 0)
            {
                kept.Add(trimmed);
            }
        }

        rest = string.Join(' ', kept).Trim();
        if (command.Length > 0)
        {
            return command;
        }

        var inline = Regex.Match(content, @"ERP:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!inline.Success)
        {
            return string.Empty;
        }

        command = inline.Groups[1].Value.Trim();
        rest = (content.Remove(inline.Index, inline.Length) + " " + rest).Trim();
        return command;
    }

    private static string ReadCursorUrl(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("agent", out var agent))
            {
                return string.Empty;
            }

            var url = Text(agent, "url");
            return IsOperatorUrl(url) ? url : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string ReadDevinUrl(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var url = Text(doc.RootElement, "url");
            if (IsOperatorUrl(url))
            {
                return url;
            }

            var id = Text(doc.RootElement, "session_id");
            if (Regex.IsMatch(id, @"^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant))
            {
                return "https://app.devin.ai/sessions/" + id;
            }

            return string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string OrgFromJson(string body, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            return Text(doc.RootElement, name);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string OrgFromElement(JsonElement element)
        => Text(element, "org_id");

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return value.GetString() ?? string.Empty;
    }

    private static string PublicError(string body, string token)
    {
        var message = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                message = error.ValueKind == JsonValueKind.String ? error.GetString() ?? string.Empty : Text(error, "message");
            }
            else
            {
                message = Text(doc.RootElement, "message");
            }
        }
        catch (JsonException)
        {
            message = string.Empty;
        }

        message = Scrub(message, token).Trim();
        if (message.Length == 0
            || message.Contains("sk-", StringComparison.OrdinalIgnoreCase)
            || message.Contains("crsr_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cog_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return Cap(message, 160);
    }

    private static string Scrub(string text, string token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token) || token.Length < 8)
        {
            return text ?? string.Empty;
        }

        return text.Replace(token, "…", StringComparison.Ordinal);
    }

    private static string Cap(string text, int limit = 700)
    {
        var value = (text ?? string.Empty).Trim();
        return value.Length <= limit ? value : value[..limit].Trim();
    }

    private static ErpOperatorAiReply Fail(string say)
        => new(false, say, string.Empty, false, string.Empty, []);
}
