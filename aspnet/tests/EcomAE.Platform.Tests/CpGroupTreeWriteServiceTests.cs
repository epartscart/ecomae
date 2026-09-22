using EcomAE.Platform.Cp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpGroupTreeWriteServiceTests
{
    private static IFormCollection Form(params (string Key, string[] Values)[] fields)
        => new FormCollection(fields.ToDictionary(f => f.Key, f => new StringValues(f.Values)));

    private static CpGroupDraft Row(long id, string value, long parent = 0, int unblocked = 1, int guests = 0, int reg = 0, int backend = 0)
        => new(id, value, "", "", "", parent, unblocked, guests, reg, backend, 0);

    [Fact]
    public void ParseForm_ReadsRowsRadiosAndCheckboxes()
    {
        var form = Form(
            ("g_idx", ["0", "1", "zz", "2"]),
            ("g_0_id", ["1"]), ("g_0_value", ["Guests"]), ("g_0_value_lang_str_id", ["k1"]), ("g_0_parent", ["0"]), ("g_0_unblocked", ["1"]),
            ("g_1_id", ["2"]), ("g_1_value", ["Admins"]), ("g_1_parent", ["0"]), ("g_1_unblocked", ["on"]), ("g_1_for_percentage", ["1"]),
            ("g_2_id", ["0"]), ("g_2_value", ["   "]),
            ("g_for_guests", ["0"]), ("g_for_registrated", ["0"]), ("g_for_backend", ["1"]));

        var (rows, error) = CpGroupTreeWriteService.ParseForm(form);
        Assert.Null(error);
        Assert.Equal(2, rows.Count);
        Assert.Equal("k1", rows[0].ValueLangStrId);
        Assert.Equal(1, rows[0].ForGuests);
        Assert.Equal(1, rows[0].ForRegistrated);
        Assert.Equal(0, rows[0].ForBackend);
        Assert.Equal(1, rows[1].ForBackend);
        Assert.Equal(1, rows[1].Unblocked);
        Assert.Equal(1, rows[1].ForPercentage);
        Assert.Equal(0, rows[1].ForGuests);
    }

    [Fact]
    public void ParseForm_RequiresIndexList()
    {
        Assert.NotNull(CpGroupTreeWriteService.ParseForm(Form(("g_0_value", ["x"]))).Error);
    }

    [Fact]
    public void Validate_RequiresExactlyOneOfEachRole()
    {
        Assert.Contains("guests", CpGroupTreeWriteService.Validate([Row(1, "A", reg: 1, backend: 1)]).Error, StringComparison.Ordinal);
        Assert.Contains("registration", CpGroupTreeWriteService.Validate([Row(1, "A", guests: 1), Row(2, "B", backend: 1)]).Error, StringComparison.Ordinal);
        Assert.Contains("backend", CpGroupTreeWriteService.Validate([Row(1, "A", guests: 1, reg: 1)]).Error, StringComparison.Ordinal);
        Assert.Contains("cannot also", CpGroupTreeWriteService.Validate([Row(1, "A", guests: 1, reg: 1, backend: 1)]).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsBlockedRolesBackendAncestryAndBadParents()
    {
        Assert.Contains("unblocked", CpGroupTreeWriteService.Validate([Row(1, "G", guests: 1, reg: 1, unblocked: 0), Row(2, "B", backend: 1)]).Error, StringComparison.Ordinal);
        Assert.Contains("under the backend", CpGroupTreeWriteService.Validate([Row(2, "B", backend: 1), Row(1, "G", parent: 2, guests: 1, reg: 1)]).Error, StringComparison.Ordinal);
        Assert.Contains("missing parent", CpGroupTreeWriteService.Validate([Row(2, "B", backend: 1), Row(1, "G", parent: 9, guests: 1, reg: 1)]).Error, StringComparison.Ordinal);
        Assert.Contains("duplicated", CpGroupTreeWriteService.Validate([Row(2, "B", backend: 1), Row(2, "G", guests: 1, reg: 1)]).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OrdersParentFirstAndComputesLevels()
    {
        var (ordered, levels, error) = CpGroupTreeWriteService.Validate(
        [
            Row(3, "Child", parent: 1),
            Row(1, "Guests", guests: 1, reg: 1),
            Row(2, "Admins", backend: 1),
            Row(0, "New under admins", parent: 2),
            Row(4, "Grandchild", parent: 3),
        ]);

        Assert.Null(error);
        Assert.Equal([1L, 3L, 4L, 2L, 0L], ordered.Select(r => r.Id).ToArray());
        Assert.Equal(1, levels[1]);
        Assert.Equal(2, levels[3]);
        Assert.Equal(3, levels[4]);
        Assert.Equal(1, levels[2]);
    }

    [Fact]
    public void Endpoint_IsRegisteredWithCpGateAndDryRun()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.CpGroupsWrite", text, StringComparison.Ordinal);
        Assert.Contains("CpGroupTreeWriteService.ParseForm(form)", text, StringComparison.Ordinal);
        Assert.Contains("Admin CP capability required for groups.", text, StringComparison.Ordinal);
        Assert.Contains("Set confirmWrites=true to save groups on ASP.NET.", text, StringComparison.Ordinal);
        Assert.Contains("ICpGroupTreeWriteService, EcomAE.Platform.Cp.CpGroupTreeWriteService", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        return dir is null ? throw new FileNotFoundException(relative) : Path.Combine(dir.FullName, relative);
    }
}
