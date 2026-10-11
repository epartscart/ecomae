namespace EcomAE.Platform.Mail;

/// <summary>Cloudflare records every shop domain needs — same shape as ecomae.com.</summary>
public static class CloudflareDnsPack
{
    public sealed record Row(string Type, string Name, string Content, string Hint);

    public static IReadOnlyList<Row> ForDomain(string domain, string ownershipCode)
    {
        var host = PlatformMailNormalize.Domain(domain);
        var ownership = string.IsNullOrWhiteSpace(ownershipCode)
            ? PurelymailDns.OwnershipTxtPrefix + "(from Super CP Refresh DNS)"
            : ownershipCode.StartsWith(PurelymailDns.OwnershipTxtPrefix, StringComparison.Ordinal)
                ? ownershipCode
                : PurelymailDns.OwnershipTxtPrefix + ownershipCode;
        return
        [
            new("MX", host, "10 " + PurelymailDns.MxHost, "Mail exchanger"),
            new("TXT", host, PurelymailDns.Spf, "SPF"),
            new("CNAME", "purelymail1._domainkey." + host, "key1.dkimroot.purelymail.com", "DKIM 1"),
            new("CNAME", "purelymail2._domainkey." + host, "key2.dkimroot.purelymail.com", "DKIM 2"),
            new("CNAME", "purelymail3._domainkey." + host, "key3.dkimroot.purelymail.com", "DKIM 3"),
            new("CNAME", "_dmarc." + host, PurelymailDns.DmarcCname, "DMARC"),
            new("TXT", host, ownership, "Purelymail ownership"),
        ];
    }

    public static IReadOnlyList<(string Domain, string SiteKey, IReadOnlyList<Row> Records)> Fleet(IReadOnlyDictionary<string, string>? ownershipByDomain = null)
    {
        var rows = new List<(string Domain, string SiteKey, IReadOnlyList<Row> Records)>
        {
            (PurelymailDns.FirstDomain, PurelymailDns.PlatformSiteKey, ForDomain(PurelymailDns.FirstDomain, Lookup(ownershipByDomain, PurelymailDns.FirstDomain))),
        };
        foreach (var shop in KnownShopMailTenants.Rows)
        {
            rows.Add((shop.Hostname, shop.SiteKey, ForDomain(shop.Hostname, Lookup(ownershipByDomain, shop.Hostname))));
        }

        return rows;
    }

    public static string ToCsv(IReadOnlyList<(string Domain, string SiteKey, IReadOnlyList<Row> Records)>? fleet = null)
    {
        var pack = fleet ?? Fleet();
        var lines = new List<string> { "domain,site_key,type,name,content,hint" };
        foreach (var zone in pack)
        {
            foreach (var row in zone.Records)
            {
                lines.Add(string.Join(',',
                    Csv(zone.Domain),
                    Csv(zone.SiteKey),
                    Csv(row.Type),
                    Csv(row.Name),
                    Csv(row.Content),
                    Csv(row.Hint)));
            }
        }

        return string.Join('\n', lines) + "\n";
    }

    private static string Lookup(IReadOnlyDictionary<string, string>? map, string domain)
        => map is not null && map.TryGetValue(domain, out var value) ? value : string.Empty;

    private static string Csv(string value)
        => "\"" + (value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
