using System.Text;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The agreement module and the auth card layout against goldens recorded from the PHP includes by
/// <c>Fixtures/AuthPartials/harness.py</c>.
/// </summary>
public sealed class StorefrontAuthPartialsTests
{
    private static readonly Lazy<JsonElement> Goldens = new(() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "AuthPartials", "goldens.json"))).RootElement);

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in Goldens.Value.EnumerateObject())
        {
            data.Add(testCase.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_php(string name)
    {
        var golden = Goldens.Value.GetProperty(name);
        string html;
        if (golden.GetProperty("case").GetString() == "agreement")
        {
            html = StorefrontAuthPartials.UsersAgreementModule(golden.GetProperty("lang").GetString()!, id => "T" + id + "<b>\"'&amp;");
        }
        else
        {
            var layout = new StorefrontAuthPartials.AuthLayout();
            var output = new StringBuilder();
            foreach (var step in golden.GetProperty("steps").GetString()!.Split(','))
            {
                output.Append(step switch
                {
                    "css" => layout.Css(),
                    "close" => StorefrontAuthPartials.AuthLayout.Close(),
                    "open" => layout.Open(),
                    _ => layout.Open(step[5..]),
                }).Append('|');
            }

            html = output.ToString();
        }

        Assert.Equal(golden.GetProperty("html").GetString(), html);
    }
}
