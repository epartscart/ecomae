using System.Collections.Concurrent;

namespace EcomAE.Platform.LifeOs.Clients;

public sealed class LifeOsClientDirectory : ILifeOsClientDirectory
{
    public const string TestClientId = "test-amina";
    public const string TestJoinToken = "lifeos-test-amina-join";

    private static readonly string[] DefaultCapabilities =
    [
        "track",
        "talk",
        "listen",
        "guide",
        "mobile-browser-pwa",
    ];

    private readonly ConcurrentDictionary<string, LifeOsClientProfile> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<object>> _tracks = new(StringComparer.OrdinalIgnoreCase);

    public LifeOsClientDirectory()
    {
        var test = new LifeOsClientProfile(
            TestClientId,
            "Amina",
            "Amina",
            "amina@lifeos.test",
            TestJoinToken,
            IsTest: true,
            JoinedAtUtc: DateTimeOffset.UtcNow,
            Status: "active-test",
            Capabilities: DefaultCapabilities);
        _clients[test.ClientId] = test;
        _tracks[test.ClientId] = new ConcurrentQueue<object>();
    }

    public LifeOsClientProfile TestClient => _clients[TestClientId];

    public IReadOnlyList<LifeOsClientProfile> List() =>
        _clients.Values.OrderByDescending(c => c.IsTest).ThenBy(c => c.JoinedAtUtc).ToArray();

    public LifeOsClientProfile? Find(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? null : _clients.GetValueOrDefault(clientId.Trim());

    public LifeOsClientProfile? Authenticate(string? clientId, string? joinToken)
    {
        var client = Find(clientId);
        if (client is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(joinToken))
        {
            return client.IsTest ? client : null;
        }

        return string.Equals(client.JoinToken, joinToken.Trim(), StringComparison.Ordinal)
            ? client
            : null;
    }

    public LifeOsJoinResult OpenTestClient() => ToJoinResult(TestClient, "Test client ready — open the mobile companion.");

    public LifeOsJoinResult Join(LifeOsJoinRequest request)
    {
        if (request.UseTestClient == true)
        {
            return OpenTestClient();
        }

        var name = (request.DisplayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Amina";
        }

        if (name.Length > 64)
        {
            name = name[..64];
        }

        var email = (request.Email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            email = $"{Slug(name)}@lifeos.client";
        }

        var id = $"client-{Slug(name)}-{Guid.NewGuid().ToString("N")[..8]}";
        var token = $"join-{Guid.NewGuid():N}";
        var profile = new LifeOsClientProfile(
            id,
            name,
            CloneName: name,
            email,
            token,
            IsTest: false,
            JoinedAtUtc: DateTimeOffset.UtcNow,
            Status: "active-scaffold",
            Capabilities: DefaultCapabilities);

        _clients[id] = profile;
        _tracks[id] = new ConcurrentQueue<object>();
        return ToJoinResult(profile, $"Welcome, {name}. Your clone {name} is ready beside you.");
    }

    public object DirectoryDigest() => new
    {
        ok = true,
        scaffold = true,
        title = "LifeOS client join directory",
        note = "In-memory scaffold — not durable production IAM. Native store apps still roadmap; mobile browser PWA is live.",
        testClient = Public(TestClient),
        clients = List().Select(Public).ToArray(),
        join = "/lifeos/join",
        joinApi = "POST /lifeos/join",
        companion = "/lifeos/mobile",
        manifest = LifeOsPwaAssets.ManifestPath,
        capabilities = DefaultCapabilities,
    };

    public LifeOsCompanionSession CompanionSession(string clientId, string? joinToken)
    {
        var client = Authenticate(clientId, joinToken) ?? TestClient;
        var q = _tracks.GetOrAdd(client.ClientId, _ => new ConcurrentQueue<object>());
        return new LifeOsCompanionSession(
            client.ClientId,
            client.DisplayName,
            client.CloneName,
            client.IsTest,
            q.Count,
            q.Reverse().Take(12).ToArray(),
            LifeOsCompanionGuide.Beats(client.DisplayName, client.CloneName),
            new
            {
                track = true,
                talk = true,
                listen = true,
                guide = true,
                speechRecognition = "Web Speech API (browser)",
                speechSynthesis = "speechSynthesis TTS (browser)",
                install = "Add to Home Screen via PWA manifest",
                notClaimed = new[] { "Native App Store binary", "Always-on wake-word DSP", "Durable cloud sync" }
            });
    }

    public LifeOsTrackResult RecordTrack(LifeOsTrackEvent evt)
    {
        var client = Authenticate(evt.ClientId, evt.JoinToken) ?? TestClient;
        var kind = string.IsNullOrWhiteSpace(evt.Kind) ? "activity" : evt.Kind.Trim().ToLowerInvariant();
        var label = string.IsNullOrWhiteSpace(evt.Label) ? kind : evt.Label.Trim();
        var row = new
        {
            at = DateTimeOffset.UtcNow,
            kind,
            label,
            value = evt.Value,
            note = evt.Note ?? "",
            clientId = client.ClientId
        };
        var q = _tracks.GetOrAdd(client.ClientId, _ => new ConcurrentQueue<object>());
        q.Enqueue(row);
        while (q.Count > 100 && q.TryDequeue(out _)) { }

        var clone = $"{client.CloneName}: Logged {label}" + (evt.Value is null ? "." : $" ({evt.Value}). Keep going, {client.DisplayName}.");
        return new LifeOsTrackResult(true, true, row, clone, CompanionSession(client.ClientId, client.JoinToken));
    }

    public LifeOsTalkReply Talk(LifeOsTalkRequest request)
    {
        var client = Authenticate(request.ClientId, request.JoinToken) ?? TestClient;
        var heard = (request.Utterance ?? "").Trim();
        if (string.IsNullOrWhiteSpace(heard))
        {
            heard = $"{client.DisplayName}, walk me through today.";
        }

        var mode = string.IsNullOrWhiteSpace(request.Mode) ? "guide" : request.Mode.Trim().ToLowerInvariant();
        var (reply, step, actions) = LifeOsCompanionGuide.Reply(client.DisplayName, client.CloneName, heard, mode);
        return new LifeOsTalkReply(true, client.DisplayName, client.CloneName, heard, reply, step, mode, actions);
    }

    public object CompanionDigest() => new
    {
        ok = true,
        scaffold = true,
        title = "LifeOS mobile companion",
        ui = "/lifeos/mobile",
        join = "/lifeos/join",
        apis = new
        {
            session = "GET /lifeos/companion?clientId=&token=",
            track = "POST /lifeos/companion/track",
            talk = "POST /lifeos/companion/talk",
            directory = "GET /lifeos/directory",
        },
        pwa = new
        {
            manifest = LifeOsPwaAssets.ManifestPath,
            serviceWorker = LifeOsPwaAssets.ServiceWorkerPath,
            display = "standalone",
            startUrl = "/lifeos/mobile?clientId=test-amina",
        },
        testClient = Public(TestClient),
        modes = new[] { "track", "talk", "listen", "guide" },
    };

    private static LifeOsJoinResult ToJoinResult(LifeOsClientProfile client, string message)
    {
        var companion = $"/lifeos/mobile?clientId={Uri.EscapeDataString(client.ClientId)}&token={Uri.EscapeDataString(client.JoinToken)}";
        return new LifeOsJoinResult(
            true,
            message,
            client,
            companion,
            companion,
            LifeOsPwaAssets.ManifestPath,
            [
                "Open the mobile companion in your phone browser",
                "Tap Install / Add to Home Screen for app-like tracking",
                $"Talk to your clone as {client.CloneName} — same name as you",
                "Use Track · Talk · Listen · Guide tabs",
            ]);
    }

    private static object Public(LifeOsClientProfile c) => new
    {
        c.ClientId,
        c.DisplayName,
        c.CloneName,
        c.Email,
        c.IsTest,
        c.JoinedAtUtc,
        c.Status,
        c.Capabilities,
        companionUrl = $"/lifeos/mobile?clientId={Uri.EscapeDataString(c.ClientId)}&token={Uri.EscapeDataString(c.JoinToken)}",
        // Test token is intentionally public for the seeded demo client.
        joinToken = c.IsTest ? c.JoinToken : "(issued-at-join)",
    };

    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var s = new string(chars);
        while (s.Contains("--", StringComparison.Ordinal))
        {
            s = s.Replace("--", "-", StringComparison.Ordinal);
        }

        return s.Trim('-');
    }
}
