using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAiAdvisorGuideTests
{
    [Fact]
    public void Guide_app_reads_the_ai_advisor_tab()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpGuideApp.razor"));
        Assert.Contains("@page \"/erp/guide-app\"", src);
        Assert.Contains("Name = \"tab\"", src);
        Assert.Contains("ai_advisor", src);
        Assert.Contains("ErpOperatorAiAdvisor", src);
        Assert.Contains("ErpGuideBooks", src);
        Assert.Contains("action=\"/erp/guide/articles/save\"", src);
        Assert.DoesNotContain("@onclick", src);
        Assert.DoesNotContain("@oninput", src);
    }

    [Fact]
    public void Advisor_tab_shows_chatgpt_cursor_and_devin_details()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpOperatorAiAdvisor.razor"));
        Assert.Contains("ChatGPT", src);
        Assert.Contains("Cursor", src);
        Assert.Contains("Devin", src);
        Assert.Contains("data-erp-ai-tool=\"chatgpt\"", src);
        Assert.Contains("data-erp-ai-tool=\"cursor\"", src);
        Assert.Contains("data-erp-ai-tool=\"devin\"", src);
        Assert.Contains("data-erp-ai-token", src);
        Assert.Contains("data-erp-ai-model", src);
        Assert.Contains("data-erp-ai-repo", src);
        Assert.Contains("data-erp-ai-org", src);
        Assert.Contains("/erp/ajax/ai-tools", src);
        Assert.Contains("action: \"connect\"", src);
        Assert.Contains("Validate and post", src);
        Assert.DoesNotContain("epc-voice__panel", src);
        Assert.DoesNotContain("@onclick", src);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
