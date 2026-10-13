using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwMetalStockComboTests
{
    [Fact]
    public void MetalStockItemCodeIsOneComboBox()
    {
        var stock = Read("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryStockVerificationApp.razor");
        Assert.Contains("AllowNew=\"true\"", stock, StringComparison.Ordinal);
        Assert.Contains("FieldName=\"item_code\"", stock, StringComparison.Ordinal);
        Assert.Contains("Type new item code or pick existing", stock, StringComparison.Ordinal);
        Assert.Contains("name=\"item_id\"", stock, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", stock, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", stock, StringComparison.Ordinal);

        var picker = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpMasterPicker.razor");
        Assert.Contains("data-erp-picker-combo", picker, StringComparison.Ordinal);
        Assert.Contains("<datalist id=\"@ListId\">", picker, StringComparison.Ordinal);
        Assert.Contains("One box: type a new", picker, StringComparison.Ordinal);
        Assert.Contains("Duplicates are not saved.", picker, StringComparison.Ordinal);

        var js = Read("aspnet/src/EcomAE.Platform/wwwroot/erp/erp-master-picker.js");
        Assert.Contains("data-erp-picker-combo", js, StringComparison.Ordinal);
        Assert.Contains("findComboOption", js, StringComparison.Ordinal);
        Assert.Contains("upsertDatalist", js, StringComparison.Ordinal);
    }

    [Fact]
    public void MetalStockSaveRejectsDuplicateUnlessOpened()
    {
        var writes = Read("aspnet/src/EcomAE.Platform/Erp/ErpJwMetalStockWriteService.cs");
        Assert.Contains("already exists. Open that row to change it", writes, StringComparison.Ordinal);
        Assert.Contains("\"duplicate\"", writes, StringComparison.Ordinal);
        Assert.DoesNotContain("ON DUPLICATE KEY UPDATE", writes, StringComparison.Ordinal);
        Assert.Contains("ItemId", writes, StringComparison.Ordinal);
        Assert.Contains("LookupKaratPurityAsync", writes, StringComparison.Ordinal);

        var module = Read("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("itemId = LiveWriteFormBinder.Long(form, \"item_id\", \"itemId\")", module, StringComparison.Ordinal);
        Assert.Contains("ItemId: itemId", module, StringComparison.Ordinal);
    }

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
