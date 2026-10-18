using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpApprovalLevelTests
{
    [Fact]
    public void StandardBandsSeparateSaveFromPost()
    {
        Assert.Equal(4, ErpApprovalLevelCatalog.Standard.Count);
        Assert.Equal("Supervisor", ErpApprovalLevelCatalog.Standard[0].Role);
        Assert.NotEqual(ErpApprovalLevelCatalog.Standard[0].SaveUpTo, ErpApprovalLevelCatalog.Standard[0].PostUpTo);
        Assert.Contains("cannot be the only approver", ErpApprovalLevelCatalog.Standard[3].Meaning, StringComparison.OrdinalIgnoreCase);

        var page = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpApprovalsApp.razor"));
        Assert.Contains("How approval works", page, StringComparison.Ordinal);
        Assert.Contains("Save levels", page, StringComparison.Ordinal);
        Assert.Contains("Post levels", page, StringComparison.Ordinal);
        Assert.Contains("name=\"step_gate\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"step_level\"", page, StringComparison.Ordinal);
        Assert.Contains("Save rule", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", page, StringComparison.Ordinal);
    }

    [Fact]
    public void SavedStepKeepsSaveOrPostLevel()
    {
        var save = ErpBosWfSaveRuleWriteService.NormalizeSteps([new ErpBosWfSaveStep("Supervisor", "Save", "save", 1, 5000)]);
        Assert.Equal("save", save[0].Gate);
        Assert.Equal(1, save[0].Level);

        var post = ErpBosWfSaveRuleWriteService.NormalizeSteps([new ErpBosWfSaveStep("Director", "Post", "POST", 9, 0)]);
        Assert.Equal("post", post[0].Gate);
        Assert.Equal(4, post[0].Level);

        var legacy = ErpBosWfSaveRuleWriteService.NormalizeSteps([]);
        Assert.Equal("Manager", legacy[0].Role);
        Assert.Equal("Approval", legacy[0].Label);
        Assert.Equal("", legacy[0].Gate);

        Assert.Equal("save", ErpBosWfRequestReadService.GateOf("""[{"role":"Supervisor","label":"Save","gate":"save","level":1}]"""));
        Assert.Equal(2, ErpBosWfRequestReadService.LevelOf("""[{"role":"Manager","label":"Post","gate":"post","level":2}]"""));
        Assert.Equal("post", ErpBosWfRequestReadService.GateOf("""[{"role":"Manager","label":"Approval"}]"""));
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, relative);
            if (File.Exists(path))
            {
                return path;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Missing " + relative);
    }
}
