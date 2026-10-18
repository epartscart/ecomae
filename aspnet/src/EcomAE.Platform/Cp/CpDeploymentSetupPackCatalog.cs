using System.IO.Compression;
using System.Text;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Allowlisted on-premises and hybrid setup files served from Super
/// <c>/cp/deployment-models-app</c>. Resolver checks publish output
/// <c>setup-packs/{kind}</c> then walks to repo <c>deploy/{kind}</c>.
/// </summary>
public static class CpDeploymentSetupPackCatalog
{
    public const string OnPremises = "on-premises";
    public const string OnPremisesAspNet = "on-premises-aspnet";
    public const string Hybrid = "hybrid";

    public sealed record PackFile(string Kind, string RelativePath, string Title, string ContentType);

    public static readonly IReadOnlyList<PackFile> Files =
    [
        new(OnPremises, "README.md", "On-premises install guide", "text/markdown; charset=utf-8"),
        new(OnPremises, "DESKTOP_QUICKSTART.md", "Desktop trial guide", "text/markdown; charset=utf-8"),
        new(OnPremises, "install.sh", "Linux server installer", "text/x-shellscript; charset=utf-8"),
        new(OnPremises, "docker-compose.yml", "Docker Compose stack", "text/yaml; charset=utf-8"),
        new(OnPremises, "Dockerfile", "PHP application image", "text/plain; charset=utf-8"),
        new(OnPremises, ".env.example", "Environment template (no secrets)", "text/plain; charset=utf-8"),
        new(OnPremises, "setup-wizard.php", "Schema setup wizard", "text/plain; charset=utf-8"),
        new(OnPremises, "activate-license.php", "License activation CLI", "text/plain; charset=utf-8"),
        new(OnPremises, "backup.php", "Backup job", "text/plain; charset=utf-8"),
        new(OnPremises, "health-check.php", "Health check + BOS report", "text/plain; charset=utf-8"),
        new(OnPremises, "epc_license_manager.php", "License manager", "text/plain; charset=utf-8"),
        new(OnPremises, "license_public_key.pem", "License public key (verify only)", "application/x-pem-file"),
        new(OnPremisesAspNet, "README.md", "ASP.NET-only on-premises guide", "text/markdown; charset=utf-8"),
        new(OnPremisesAspNet, "FUTURE_ASPNET_ONLY.md", "No future PHP in this pack", "text/markdown; charset=utf-8"),
        new(OnPremisesAspNet, "install.sh", "Linux installer script", "text/x-shellscript; charset=utf-8"),
        new(OnPremisesAspNet, "ecomae-setup", "Linux setup file", "text/x-shellscript; charset=utf-8"),
        new(OnPremisesAspNet, "setup.exe", "Windows setup.exe", "application/vnd.microsoft.portable-executable"),
        new(OnPremisesAspNet, "setup.cmd", "Windows setup (double-click)", "text/plain; charset=utf-8"),
        new(OnPremisesAspNet, "setup.ps1", "Windows PowerShell setup", "text/plain; charset=utf-8"),
        new(OnPremisesAspNet, "docker-compose.yml", "ASP.NET + MariaDB compose", "text/yaml; charset=utf-8"),
        new(OnPremisesAspNet, "Dockerfile", "ASP.NET platform image", "text/plain; charset=utf-8"),
        new(OnPremisesAspNet, ".env.example", "Environment template (no secrets)", "text/plain; charset=utf-8"),
        new(OnPremisesAspNet, "setup/EcomAE.OnPremSetup.csproj", "setup.exe project", "text/xml; charset=utf-8"),
        new(OnPremisesAspNet, "setup/Program.cs", "setup.exe source", "text/plain; charset=utf-8"),
        new(Hybrid, "README.md", "Hybrid install + sync guide", "text/markdown; charset=utf-8"),
        new(Hybrid, "install.sh", "Hybrid installer (ERP + sync agent)", "text/x-shellscript; charset=utf-8"),
        new(Hybrid, "ecomae-setup", "Linux hybrid setup file", "text/x-shellscript; charset=utf-8"),
        new(Hybrid, "setup.exe", "Windows hybrid setup.exe", "application/vnd.microsoft.portable-executable"),
        new(Hybrid, "setup.cmd", "Windows hybrid setup (double-click)", "text/plain; charset=utf-8"),
        new(Hybrid, "setup.ps1", "Windows hybrid PowerShell setup", "text/plain; charset=utf-8"),
        new(Hybrid, ".env.example", "Hybrid environment template", "text/plain; charset=utf-8"),
        new(Hybrid, "docker-compose.hybrid.yml", "Sync-agent Compose overlay", "text/yaml; charset=utf-8"),
        new(Hybrid, "sync-agent/sync-agent.py", "Outbound HTTPS sync agent", "text/x-python; charset=utf-8"),
        new(Hybrid, "sync-agent/ownership.json", "Master / transaction ownership", "application/json; charset=utf-8"),
        new(Hybrid, "sync-agent/local-schema.sql", "Site inbox / outbox tables", "application/sql; charset=utf-8"),
        new(Hybrid, "systemd/ecomae-hybrid-sync.service", "systemd unit for the agent", "text/plain; charset=utf-8"),
    ];

    public static bool IsKind(string? kind)
    {
        var key = NormalizeKind(kind);
        return key is OnPremises or OnPremisesAspNet or Hybrid;
    }

    public static string NormalizeKind(string? kind)
        => CpDeploymentModelsCatalog.NormalizeKind(kind) switch
        {
            "on-premises" => OnPremises,
            "on-premises-aspnet" or "aspnet" or "aspnet-only" => OnPremisesAspNet,
            "hybrid" => Hybrid,
            _ => string.Empty,
        };

    public static IReadOnlyList<PackFile> ForKind(string? kind)
    {
        var key = NormalizeKind(kind);
        return Files.Where(f => f.Kind == key).ToArray();
    }

    public static PackFile? Find(string? kind, string? relativePath)
    {
        var key = NormalizeKind(kind);
        var path = NormalizeRelativePath(relativePath);
        if (key.Length == 0 || path.Length == 0)
        {
            return null;
        }

        return Files.FirstOrDefault(f => f.Kind == key && string.Equals(f.RelativePath, path, StringComparison.Ordinal));
    }

    public static string NormalizeRelativePath(string? relativePath)
    {
        var raw = (relativePath ?? string.Empty).Trim().Replace('\\', '/').TrimStart('/');
        if (raw.StartsWith("./", StringComparison.Ordinal))
        {
            raw = raw[2..];
        }

        if (raw.Length == 0
            || raw.Contains("..", StringComparison.Ordinal)
            || raw.Contains(':', StringComparison.Ordinal)
            || Path.IsPathRooted(raw))
        {
            return string.Empty;
        }

        return raw;
    }

    public static string? ResolveDirectory(string kind, params string?[] hints)
    {
        var key = NormalizeKind(kind);
        if (key.Length == 0)
        {
            return null;
        }

        foreach (var hint in hints)
        {
            if (string.IsNullOrWhiteSpace(hint))
            {
                continue;
            }

            foreach (var start in RootsFrom(hint))
            {
                foreach (var candidate in new[]
                         {
                             Path.Combine(start, "setup-packs", key),
                             Path.Combine(start, "deploy", key),
                         })
                {
                    if (Directory.Exists(candidate) && HasAnyAllowlistedFile(key, candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    public static string? ResolveFile(string kind, string relativePath, params string?[] hints)
    {
        var file = Find(kind, relativePath);
        if (file is null)
        {
            return null;
        }

        var root = ResolveDirectory(kind, hints);
        if (root is null)
        {
            return null;
        }

        var physical = Path.GetFullPath(Path.Combine(root, file.RelativePath));
        var rootFull = Path.GetFullPath(root);
        if (!physical.StartsWith(rootFull, StringComparison.Ordinal)
            || !File.Exists(physical))
        {
            return null;
        }

        return physical;
    }

    public static byte[] ZipKind(string kind, params string?[] hints)
    {
        var key = NormalizeKind(kind);
        if (key.Length == 0)
        {
            throw new ArgumentException("Setup pack kind must be on-premises, on-premises-aspnet, or hybrid.", nameof(kind));
        }

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in ForKind(key))
            {
                var physical = ResolveFile(key, file.RelativePath, hints);
                if (physical is null)
                {
                    continue;
                }

                zip.CreateEntryFromFile(physical, key + "/" + file.RelativePath, CompressionLevel.Fastest);
            }
        }

        if (stream.Length == 0)
        {
            throw new FileNotFoundException("No allowlisted setup files were found for " + key + ".");
        }

        return stream.ToArray();
    }

    public static string ZipFileName(string kind)
        => NormalizeKind(kind) switch
        {
            OnPremises => "ecomae-on-premises-setup.zip",
            OnPremisesAspNet => "ecomae-on-premises-aspnet-setup.zip",
            Hybrid => "ecomae-hybrid-setup.zip",
            _ => "ecomae-setup.zip",
        };

    public static IReadOnlyList<object> CatalogPayload()
        => Files
            .GroupBy(f => f.Kind, StringComparer.Ordinal)
            .Select(g => (object)new
            {
                kind = g.Key,
                zip = "/cp/deployment-models/setup-pack?kind=" + g.Key,
                files = g.Select(f => new
                {
                    path = f.RelativePath,
                    title = f.Title,
                    href = "/cp/deployment-models/setup-file?kind=" + g.Key + "&file=" + Uri.EscapeDataString(f.RelativePath),
                }).ToArray(),
            })
            .ToArray();

    private static IEnumerable<string> RootsFrom(string hint)
    {
        var full = Path.GetFullPath(hint);
        var current = Directory.Exists(full)
            ? new DirectoryInfo(full)
            : File.Exists(full)
                ? new FileInfo(full).Directory
                : new DirectoryInfo(full);

        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static bool HasAnyAllowlistedFile(string kind, string directory)
        => ForKind(kind).Any(f => File.Exists(Path.Combine(directory, f.RelativePath)));

    public static string ReadText(string physicalPath)
        => File.ReadAllText(physicalPath, Encoding.UTF8);
}
