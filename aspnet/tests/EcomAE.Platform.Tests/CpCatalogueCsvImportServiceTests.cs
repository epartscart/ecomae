using System.Collections.Generic;
using System.Linq;
using System.Text;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCatalogueCsvImportServiceTests
{
    private static Dictionary<string, string> BaseForm() => new()
    {
        ["category_id"] = "12",
        ["storages"] = "4",
        ["strings_to_left"] = "2",
        ["col_name"] = "1",
        ["col_name_url_check"] = "1",
        ["col_text"] = "0",
        ["col_img"] = "5",
        ["col_price"] = "2",
        ["col_exist"] = "3",
        ["encoding"] = "windows-1251"
    };

    [Fact]
    public void Options_follow_the_php_import_options_field_names()
    {
        var form = BaseForm();
        form["col_71"] = "6";
        form["col_71_url_check"] = "1";
        form["col_9"] = "0";
        form["delete_products_data"] = "1";

        var options = CpCatalogueCsvImportService.ParseOptions(form);

        Assert.Equal(12, options.CategoryId);
        Assert.Equal(4, options.StorageId);
        Assert.Equal(2, options.StringsToSkip);
        Assert.True(options.ColNameUrlCheck);
        Assert.Equal(5, options.ColImage);
        Assert.Equal("windows-1251", options.Encoding);
        Assert.True(options.DeleteProductsData);
        Assert.False(options.DeleteStorageData);
        Assert.Equal([9L, 71L], options.Properties.Select(p => p.PropertyId).ToArray());
        Assert.True(options.Properties.Single(p => p.PropertyId == 71).UrlCheck);
        Assert.False(options.Properties.Single(p => p.PropertyId == 9).UrlCheck);
    }

    [Fact]
    public void Validation_repeats_the_php_page_gate()
    {
        Assert.Null(CpCatalogueCsvImportService.Validate(CpCatalogueCsvImportService.ParseOptions(BaseForm())));

        var noCategory = BaseForm();
        noCategory["category_id"] = "0";
        Assert.Contains("category", CpCatalogueCsvImportService.Validate(CpCatalogueCsvImportService.ParseOptions(noCategory)) ?? "");

        var noStorage = BaseForm();
        noStorage["storages"] = "0";
        Assert.Contains("warehouse", CpCatalogueCsvImportService.Validate(CpCatalogueCsvImportService.ParseOptions(noStorage)) ?? "");

        var noName = BaseForm();
        noName["col_name"] = "0";
        Assert.Contains("product name", CpCatalogueCsvImportService.Validate(CpCatalogueCsvImportService.ParseOptions(noName)) ?? "");

        var negative = BaseForm();
        negative["col_img"] = "-1";
        Assert.Contains("negative", CpCatalogueCsvImportService.Validate(CpCatalogueCsvImportService.ParseOptions(negative)) ?? "");
    }

    [Fact]
    public void Windows_1251_and_utf8_uploads_decode_to_the_same_text()
    {
        var ansi = new byte[] { 0xCC, 0xE0, 0xF1, 0xEB, 0xEE }; // Масло
        Assert.Equal("Масло", CpCatalogueCsvImportService.Decode(ansi, "windows-1251"));
        Assert.Equal("Масло", CpCatalogueCsvImportService.Decode(Encoding.UTF8.GetBytes("Масло"), "UTF-8"));
        Assert.Equal("a;b", CpCatalogueCsvImportService.Decode(Encoding.UTF8.GetBytes("\uFEFFa;b"), "UTF-8"));
    }

    [Fact]
    public void Csv_rows_are_split_on_the_detected_delimiter_with_quoted_fields()
    {
        var rows = CpCatalogueCsvImportService.ParseCsv("name;price;qty\r\n\"Brake pad; front\";10,50;4\r\n\r\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal(["name", "price", "qty"], rows[0]);
        Assert.Equal(["Brake pad; front", "10,50", "4"], rows[1]);

        var commas = CpCatalogueCsvImportService.ParseCsv("a,b,c\n1,2,3");
        Assert.Equal(["1", "2", "3"], commas[1]);
    }

    [Fact]
    public void Product_url_is_slugified_from_the_caption()
    {
        Assert.Equal("brake-pad-front-2024", CpCatalogueCsvImportService.Alias("  Brake Pad (front) 2024 "));
        Assert.Equal("Масло", CpCatalogueCsvImportService.Alias("Масло"));
    }
}
