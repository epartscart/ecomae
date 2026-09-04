using System.IO.Compression;
using System.Text;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontBulkUploadParityTests
{
    [Fact]
    public void Parser_ReadsPhpColumnOrderAndSkipsHeader()
    {
        var csv = StorefrontBulkUploadFileParser.SampleCsv();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var items = StorefrontBulkUploadFileParser.Read(stream, "sample.csv", out var error);

        Assert.Null(error);
        Assert.Equal(3, items.Count);
        Assert.Equal("BOSCH", items[0].Brand);
        Assert.Equal("0986424795", items[0].Article);
        Assert.Equal(2, items[0].Qty);
        Assert.Equal("45.00", items[0].TargetPrice);
        Assert.Equal("1", items[0].Delivery);
        Assert.Equal("Front brake pads", items[0].Comment);
        Assert.Equal("MANN", items[1].Brand);
        Assert.Equal(1, items[1].Qty);
    }

    [Fact]
    public void Parser_AcceptsSemicolonAndTab()
    {
        const string text = "Brand;Part Number;Qty\nNGK;BKR6E;4\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var items = StorefrontBulkUploadFileParser.Read(stream, "parts.csv", out var error);
        Assert.Null(error);
        Assert.Single(items);
        Assert.Equal("NGK", items[0].Brand);
        Assert.Equal("BKR6E", items[0].Article);
        Assert.Equal(4, items[0].Qty);
    }

    [Fact]
    public void Parser_ReadsXlsxFirstSheet()
    {
        var xlsx = MinimalXlsx(
            ("BOSCH", "0986424795", "2"),
            ("MANN", "W71275", "1"));
        using var stream = new MemoryStream(xlsx);
        var items = StorefrontBulkUploadFileParser.Read(stream, "list.xlsx", out var error);
        Assert.Null(error);
        Assert.Equal(2, items.Count);
        Assert.Equal("BOSCH", items[0].Brand);
        Assert.Equal("0986424795", items[0].Article);
        Assert.Equal(2, items[0].Qty);
    }

    [Fact]
    public void Parser_RejectsOleXlsAndEmpty()
    {
        var ole = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00 };
        using (var stream = new MemoryStream(ole))
        {
            var items = StorefrontBulkUploadFileParser.Read(stream, "old.xls", out var error);
            Assert.Empty(items);
            Assert.Contains("xlsx", error, StringComparison.OrdinalIgnoreCase);
        }

        using (var empty = new MemoryStream())
        {
            var items = StorefrontBulkUploadFileParser.Read(empty, "empty.csv", out var error);
            Assert.Empty(items);
            Assert.Equal("Upload file is required.", error);
        }
    }

    [Fact]
    public void Matcher_PicksBestPriceThenDelivery()
    {
        var cheapSlow = Offer("BOSCH", "A1", 90m, 5, 10);
        var dearFast = Offer("BOSCH", "A1", 120m, 1, 4);
        var rows = new[] { cheapSlow, dearFast };

        var byPrice = StorefrontBulkUploadMatcher.PickBest(rows, "price");
        Assert.Same(cheapSlow, byPrice);

        var byDelivery = StorefrontBulkUploadMatcher.PickBest(rows, "delivery");
        Assert.Same(dearFast, byDelivery);
    }

    [Fact]
    public void Matcher_BuildRowAndCsvMatchPhpColumns()
    {
        var line = new StorefrontBulkUploadLine("BOSCH", "0986424795", 4, "45", "1", "");
        var exact = StorefrontBulkUploadMatcher.ToOffer(Offer("BOSCH", "0986424795", 40m, 2, 2), 4, "exact", "Exact", true);
        var row = StorefrontBulkUploadMatcher.BuildRow(line, exact, null, false);
        Assert.True(row.Available);
        Assert.True(row.ShortQty);
        Assert.Equal("Available but short quantity", row.StatusLabel);

        var (summary, csv) = StorefrontBulkUploadMatcher.Summarize([row]);
        Assert.Equal(1, summary.Uploaded);
        Assert.Equal(1, summary.Available);
        Assert.Equal(1, summary.Short);
        Assert.Equal(0, summary.Notfound);
        Assert.StartsWith("Brand,Requested Article,Qty,", csv, StringComparison.Ordinal);
        Assert.Contains("0986424795", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void App_UsesPhpHeroFormAndAspNetCheck()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontBulkUploadApp.razor"));
        Assert.Contains("@page \"/storefront/bulk-upload-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/shop/bulk-upload\"", text, StringComparison.Ordinal);
        Assert.Contains("Bulk Spare Parts Upload", text, StringComparison.Ordinal);
        Assert.Contains("name=\"bulk_file\"", text, StringComparison.Ordinal);
        Assert.Contains("accept=\".xlsx,.xls,.csv,.txt\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"priority\"", text, StringComparison.Ordinal);
        Assert.Contains("Best price first", text, StringComparison.Ordinal);
        Assert.Contains("Upload and check prices", text, StringComparison.Ordinal);
        Assert.Contains("Fetch cross for all not found / short qty", text, StringComparison.Ordinal);
        Assert.Contains("Add selected to cart", text, StringComparison.Ordinal);
        Assert.Contains("Download result CSV", text, StringComparison.Ordinal);
        Assert.Contains("Recent bulk upload history", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontBulkUploadHistoryAsync", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontBulkUploadCheck", text, StringComparison.Ordinal);
        Assert.Contains("epc_storefront_bulk_upload.js", text, StringComparison.Ordinal);
        Assert.Contains("id=\"epc_bulk_process_progress\" style=\"display:none\"", text, StringComparison.Ordinal);
        Assert.Contains("Customer login required", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.Registration", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Compare PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("price_file", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutesAndAssets_ExposeCheckCrossSample()
    {
        Assert.Equal("/storefront/bulk-upload/check", EcomAeRoutes.StorefrontBulkUploadCheck);
        Assert.Equal("/storefront/bulk-upload/cross", EcomAeRoutes.StorefrontBulkUploadCross);
        Assert.Equal("/storefront/bulk-upload/add-selected", EcomAeRoutes.StorefrontBulkUploadAddSelected);
        Assert.Equal("/storefront/bulk-upload/sample.csv", EcomAeRoutes.StorefrontBulkUploadSample);
        Assert.Equal("/en/shop/bulk-upload", StorefrontPhpCanonical.BulkUpload);
        Assert.Equal("/storefront/bulk-upload-app", StorefrontAspNetCanonical.BulkUpload);

        var bridge = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("epc_storefront_bulk_upload.js", bridge, StringComparison.Ordinal);
        Assert.True(File.Exists(Find("content/general_pages/epc_storefront_bulk_upload.js")));
    }

    [Fact]
    public void Script_PostsToAspNetCheckNotPhpAjax()
    {
        var js = File.ReadAllText(Find("content/general_pages/epc_storefront_bulk_upload.js"));
        Assert.Contains("/storefront/bulk-upload/check", js, StringComparison.Ordinal);
        Assert.Contains("/storefront/bulk-upload/cross", js, StringComparison.Ordinal);
        Assert.Contains("/storefront/bulk-upload/add-selected", js, StringComparison.Ordinal);
        Assert.Contains("bulk_file", js, StringComparison.Ordinal);
        Assert.DoesNotContain("ajax_process.php", js, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", js, StringComparison.Ordinal);
    }

    private static StorefrontPartOfferDigest Offer(string brand, string article, decimal price, int days, int exist)
        => new(1, "list", brand, article, article, brand + " " + article, price, exist, "WH", days.ToString(), "", 1, 2, 1, 1);

    private static byte[] MinimalXlsx(params (string Brand, string Article, string Qty)[] rows)
    {
        var shared = new List<string>();
        string Share(string value)
        {
            shared.Add(value);
            return (shared.Count - 1).ToString();
        }

        var sb = new StringBuilder();
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        var r = 1;
        foreach (var row in rows)
        {
            sb.Append("<row r=\"").Append(r).Append("\">");
            sb.Append("<c r=\"A").Append(r).Append("\" t=\"s\"><v>").Append(Share(row.Brand)).Append("</v></c>");
            sb.Append("<c r=\"B").Append(r).Append("\" t=\"s\"><v>").Append(Share(row.Article)).Append("</v></c>");
            sb.Append("<c r=\"C").Append(r).Append("\" t=\"s\"><v>").Append(Share(row.Qty)).Append("</v></c>");
            sb.Append("</row>");
            r++;
        }

        sb.Append("</sheetData></worksheet>");

        var sst = new StringBuilder();
        sst.Append("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si>");
        // rebuild properly
        sst.Clear();
        sst.Append("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        foreach (var value in shared)
        {
            sst.Append("<si><t>").Append(value).Append("</t></si>");
        }

        sst.Append("</sst>");

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/></Types>");
            WriteEntry(zip, "xl/worksheets/sheet1.xml", sb.ToString());
            WriteEntry(zip, "xl/sharedStrings.xml", sst.ToString());
        }

        return output.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string Find(string relative)
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

        throw new FileNotFoundException(relative);
    }
}
