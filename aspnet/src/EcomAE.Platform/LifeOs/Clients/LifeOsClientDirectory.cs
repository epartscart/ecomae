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
        "results-no-login",
    ];

    private readonly ConcurrentDictionary<string, LifeOsClientProfile> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LifeOsActivityEvent>> _activities = new(StringComparer.OrdinalIgnoreCase);

    public LifeOsClientDirectory()
    {
        var test = BuildProfile(
            TestClientId,
            "Amina",
            "amina@lifeos.test",
            TestJoinToken,
            isTest: true,
            status: "active-test",
            country: "United Arab Emirates",
            countryCode: "AE",
            city: "Dubai",
            timeZone: "Asia/Dubai",
            locale: "en-AE",
            userAgent: "lifeos-seed",
            platform: "seed",
            referrer: "",
            joinSource: "test",
            ipCountryHint: "AE");
        _clients[test.ClientId] = test;
        _activities[test.ClientId] = new ConcurrentQueue<LifeOsActivityEvent>();
        Append(test.ClientId, "join", "Joined (seeded test client)", "Seeded Amina ↔ Amina for mobile trials", "join", null, null, null);
    }

    public LifeOsClientProfile TestClient => RefreshCounts(_clients[TestClientId]);

    public IReadOnlyList<LifeOsClientProfile> List() =>
        _clients.Values
            .Select(RefreshCounts)
            .OrderByDescending(c => c.IsTest)
            .ThenByDescending(c => c.JoinedAtUtc)
            .ToArray();

    public LifeOsClientProfile? Find(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? null : RefreshCounts(_clients.GetValueOrDefault(clientId.Trim()));

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

    public LifeOsJoinResult OpenTestClient() => ToJoinResult(TestClient, "Test client ready — open companion or view your results (no login).");

    public LifeOsJoinResult Join(LifeOsJoinRequest request)
    {
        if (request.UseTestClient == true)
        {
            return OpenTestClient();
        }

        var name = Clip((request.DisplayName ?? "").Trim(), 64);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Amina";
        }

        var email = (request.Email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            email = $"{Slug(name)}@lifeos.client";
        }

        var id = $"client-{Slug(name)}-{Guid.NewGuid().ToString("N")[..8]}";
        var token = $"join-{Guid.NewGuid():N}";
        var country = Clip(request.Country, 80);
        var countryCode = Clip(request.CountryCode ?? request.IpCountryHint, 8)?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(countryCode))
        {
            country = CountryName(countryCode);
        }

        var profile = BuildProfile(
            id,
            name,
            email,
            token,
            isTest: false,
            status: "active-scaffold",
            country,
            countryCode,
            Clip(request.City, 80),
            Clip(request.TimeZone, 80),
            Clip(request.Locale, 32),
            Clip(request.UserAgent, 400),
            Clip(request.Platform, 80),
            Clip(request.Referrer, 400),
            Clip(request.JoinSource, 40) ?? "web",
            Clip(request.IpCountryHint, 8)?.ToUpperInvariant());

        _clients[id] = profile;
        _activities[id] = new ConcurrentQueue<LifeOsActivityEvent>();
        Append(
            id,
            "join",
            "Joined LifeOS",
            $"Source={profile.JoinSource}; country={profile.Country ?? profile.CountryCode ?? "n/a"}; tz={profile.TimeZone ?? "n/a"}",
            "join",
            null,
            null,
            null);

        return ToJoinResult(RefreshCounts(profile), $"Welcome, {name}. Your clone {name} is ready beside you — no login required.");
    }

    public object DirectoryDigest() => new
    {
        ok = true,
        scaffold = true,
        title = "LifeOS client join directory",
        note = "No login required. Clients use clientId + joinToken. In-memory scaffold until durable IAM.",
        testClient = Public(TestClient),
        clients = List().Select(Public).ToArray(),
        join = "/lifeos/join",
        joinApi = "POST /lifeos/join",
        results = "/lifeos/results",
        resultsApi = "GET /lifeos/results/json",
        companion = "/lifeos/mobile",
        controlPanel = "/cp/lifeos-clients-app",
        manifest = LifeOsPwaAssets.ManifestPath,
        capabilities = DefaultCapabilities,
    };

    public object ControlPanelDigest() => new
    {
        ok = true,
        scaffold = true,
        title = "LifeOS joined clients — Control Panel",
        authNote = "Login not required for client join/results yet; CP console shows operator view of in-memory joins.",
        totalClients = List().Count,
        countries = List()
            .GroupBy(c => c.CountryCode ?? c.Country ?? "unknown")
            .Select(g => new { key = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToArray(),
        clients = List().Select(c => new
        {
            profile = Public(c),
            recent = Activities(c.ClientId).TakeLast(8).Reverse().ToArray(),
            resultsUrl = ResultsUrl(c),
            companionUrl = CompanionUrl(c),
        }).ToArray(),
    };

    public LifeOsCompanionSession CompanionSession(string clientId, string? joinToken)
    {
        var client = Authenticate(clientId, joinToken) ?? TestClient;
        var all = Activities(client.ClientId);
        return new LifeOsCompanionSession(
            client.ClientId,
            client.DisplayName,
            client.CloneName,
            client.IsTest,
            client.Country,
            client.CountryCode,
            client.TimeZone,
            all.Count(a => a.Kind == "track"),
            all.Count(a => a.Kind == "talk"),
            all.Count,
            all.Where(a => a.Kind == "track").TakeLast(12).Reverse().ToArray(),
            all.Where(a => a.Kind is "talk" or "guide" or "listen").TakeLast(12).Reverse().ToArray(),
            LifeOsCompanionGuide.Beats(client.DisplayName, client.CloneName),
            new
            {
                track = true,
                talk = true,
                listen = true,
                guide = true,
                results = true,
                loginRequired = false,
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
        var clone = $"{client.CloneName}: Logged {label}" + (evt.Value is null ? "." : $" ({evt.Value}). Keep going, {client.DisplayName}.");
        var row = Append(client.ClientId, "track", label, evt.Note, kind, evt.Value, null, clone);
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
        var kind = mode is "listen" or "guide" or "talk" ? mode : "talk";
        var activity = Append(client.ClientId, kind, step, reply, mode, null, heard, reply);
        return new LifeOsTalkReply(true, client.DisplayName, client.CloneName, heard, reply, step, mode, activity.Id, activity.AtUtc, actions);
    }

    public LifeOsClientResults Results(
        string? clientId,
        string? joinToken,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? kind)
    {
        var client = Authenticate(clientId, joinToken);
        if (client is null)
        {
            return new LifeOsClientResults(
                false,
                TestClient with { Status = "unauthorized" },
                fromUtc,
                toUtc,
                0,
                [],
                new { error = "invalid-client-or-token", loginRequired = false });
        }

        var q = Activities(client.ClientId).AsEnumerable();
        if (fromUtc is not null)
        {
            q = q.Where(a => a.AtUtc >= fromUtc);
        }

        if (toUtc is not null)
        {
            q = q.Where(a => a.AtUtc <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(kind) && !string.Equals(kind, "all", StringComparison.OrdinalIgnoreCase))
        {
            var k = kind.Trim().ToLowerInvariant();
            q = q.Where(a => string.Equals(a.Kind, k, StringComparison.OrdinalIgnoreCase));
        }

        var list = q.OrderByDescending(a => a.AtUtc).ToArray();
        var all = Activities(client.ClientId);
        return new LifeOsClientResults(
            true,
            client,
            fromUtc,
            toUtc,
            list.Length,
            list,
            new
            {
                tracks = all.Count(a => a.Kind == "track"),
                talks = all.Count(a => a.Kind == "talk"),
                guides = all.Count(a => a.Kind == "guide"),
                listens = all.Count(a => a.Kind == "listen"),
                joins = all.Count(a => a.Kind == "join"),
                firstAt = all.Count == 0 ? (DateTimeOffset?)null : all.Min(a => a.AtUtc),
                lastAt = all.Count == 0 ? (DateTimeOffset?)null : all.Max(a => a.AtUtc),
                loginRequired = false,
            });
    }

    public object CompanionDigest() => new
    {
        ok = true,
        scaffold = true,
        title = "LifeOS mobile companion",
        ui = "/lifeos/mobile",
        join = "/lifeos/join",
        results = "/lifeos/results",
        apis = new
        {
            session = "GET /lifeos/companion?clientId=&token=",
            track = "POST /lifeos/companion/track",
            talk = "POST /lifeos/companion/talk",
            results = "GET /lifeos/results/json?clientId=&token=&from=&to=&kind=",
            directory = "GET /lifeos/directory",
            controlPanel = "GET /lifeos/clients/cp",
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
        loginRequired = false,
    };

    private LifeOsActivityEvent Append(
        string clientId,
        string kind,
        string label,
        string? detail,
        string? mode,
        double? value,
        string? human,
        string? clone)
    {
        var evt = new LifeOsActivityEvent(
            $"act-{Guid.NewGuid():N}"[..20],
            DateTimeOffset.UtcNow,
            kind,
            label,
            detail,
            mode,
            value,
            human,
            clone);
        var q = _activities.GetOrAdd(clientId, _ => new ConcurrentQueue<LifeOsActivityEvent>());
        q.Enqueue(evt);
        while (q.Count > 500 && q.TryDequeue(out _)) { }
        return evt;
    }

    private IReadOnlyList<LifeOsActivityEvent> Activities(string clientId) =>
        _activities.TryGetValue(clientId, out var q) ? q.ToArray() : [];

    private LifeOsClientProfile RefreshCounts(LifeOsClientProfile? client)
    {
        if (client is null)
        {
            return null!;
        }

        var all = Activities(client.ClientId);
        var updated = client with
        {
            TrackCount = all.Count(a => a.Kind == "track"),
            TalkCount = all.Count(a => a.Kind is "talk" or "guide" or "listen"),
            ActivityCount = all.Count,
        };
        _clients[client.ClientId] = updated;
        return updated;
    }

    private static LifeOsClientProfile BuildProfile(
        string id,
        string name,
        string email,
        string token,
        bool isTest,
        string status,
        string? country,
        string? countryCode,
        string? city,
        string? timeZone,
        string? locale,
        string? userAgent,
        string? platform,
        string? referrer,
        string? joinSource,
        string? ipCountryHint) =>
        new(
            id,
            name,
            CloneName: name,
            email,
            token,
            isTest,
            DateTimeOffset.UtcNow,
            status,
            DefaultCapabilities,
            country,
            countryCode,
            city,
            timeZone,
            locale,
            userAgent,
            platform,
            referrer,
            joinSource,
            ipCountryHint,
            TrackCount: 0,
            TalkCount: 0,
            ActivityCount: 0);

    private static LifeOsJoinResult ToJoinResult(LifeOsClientProfile client, string message) =>
        new(
            true,
            message,
            client,
            CompanionUrl(client),
            ResultsUrl(client),
            CompanionUrl(client),
            LifeOsPwaAssets.ManifestPath,
            [
                "No login required — keep your join link or token",
                "Open the mobile companion to track / talk / listen / guide",
                "Open My results anytime to review discussions and tracking",
                "Add to Home Screen for an app-like experience",
            ]);

    private static string CompanionUrl(LifeOsClientProfile c) =>
        $"/lifeos/mobile?clientId={Uri.EscapeDataString(c.ClientId)}&token={Uri.EscapeDataString(c.JoinToken)}";

    private static string ResultsUrl(LifeOsClientProfile c) =>
        $"/lifeos/results?clientId={Uri.EscapeDataString(c.ClientId)}&token={Uri.EscapeDataString(c.JoinToken)}";

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
        c.Country,
        c.CountryCode,
        c.City,
        c.TimeZone,
        c.Locale,
        c.Platform,
        c.JoinSource,
        c.IpCountryHint,
        c.UserAgent,
        c.Referrer,
        c.TrackCount,
        c.TalkCount,
        c.ActivityCount,
        companionUrl = CompanionUrl(c),
        resultsUrl = ResultsUrl(c),
        joinToken = c.IsTest ? c.JoinToken : c.JoinToken, // issued at join; required for no-login access
    };

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }

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

    private static string CountryName(string code) => code.ToUpperInvariant() switch
    {
        "AE" => "United Arab Emirates",
        "SA" => "Saudi Arabia",
        "IN" => "India",
        "PK" => "Pakistan",
        "US" => "United States",
        "GB" => "United Kingdom",
        "DE" => "Germany",
        "FR" => "France",
        "EG" => "Egypt",
        "QA" => "Qatar",
        "KW" => "Kuwait",
        "BH" => "Bahrain",
        "OM" => "Oman",
        _ => code.ToUpperInvariant(),
    };
}
