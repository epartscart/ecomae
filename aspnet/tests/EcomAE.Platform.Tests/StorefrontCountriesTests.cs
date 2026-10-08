using System.Text.Json.Nodes;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <see cref="EpcCountries"/>. <c>Fixtures/Countries/goldens.json</c> is what the real <c>content/users/epc_countries.php</c>
/// returned (run by <c>harness.py</c>): the full lists, and the dial prefix, address rules and normalised code for each input.
/// </summary>
public sealed class StorefrontCountriesTests
{
    private static readonly JsonNode Golden = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Countries", "goldens.json")))!;

    private static JsonArray Pairs(IEnumerable<KeyValuePair<string, string>> pairs)
        => new(pairs.Select(p => (JsonNode)new JsonArray(p.Key, p.Value)).ToArray());

    private static void Same(JsonNode? php, JsonNode? actual, string what)
        => Assert.True(JsonNode.DeepEquals(php, actual), $"{what}: PHP {php?.ToJsonString()} vs {actual?.ToJsonString()}");

    [Fact]
    public void Lists_MatchPhp()
    {
        Same(Golden["iso3166"], Pairs(EpcCountries.Iso3166Alpha2), "iso3166");
        Same(Golden["registration"], Pairs(EpcCountries.RegistrationOptions), "registration");
        Same(Golden["dial_codes"], Pairs(EpcCountries.DialCodes), "dial_codes");
        Same(Golden["emirates"], new JsonArray(EpcCountries.UaeEmirates.Select(e => (JsonNode)e).ToArray()), "emirates");
    }

    [Fact]
    public void DialPrefix_AddressMeta_AndNormalize_MatchPhp()
    {
        foreach (var row in Golden["inputs"]!.AsArray())
        {
            var input = row!["in"]!.GetValue<string>();
            Assert.Equal(row["dial"]!.GetValue<string>(), EpcCountries.DialPrefix(input));
            Assert.Equal(row["normalize"]!.GetValue<string>(), EpcCountries.NormalizeCode(input));
            var meta = EpcCountries.AddressMeta(input);
            Same(row["meta"], new JsonObject
            {
                ["state_label"] = meta.StateLabel,
                ["postal_label"] = meta.PostalLabel,
                ["postal_required"] = meta.PostalRequired,
                ["use_emirate_select"] = meta.UseEmirateSelect,
                ["emirates"] = new JsonArray(meta.Emirates.Select(e => (JsonNode)e).ToArray()),
            }, "meta " + input);
        }
    }
}
