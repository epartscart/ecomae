using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpDataTransferServiceTests
{
    [Fact]
    public void Storage_is_offered_only_to_the_admins_listed_in_its_users_json()
    {
        Assert.True(CpDataTransferDeskService.StorageAllowsUser("[1,5,9]", 5));
        Assert.True(CpDataTransferDeskService.StorageAllowsUser("[\"7\"]", 7));
        Assert.False(CpDataTransferDeskService.StorageAllowsUser("[1,5,9]", 4));
        Assert.False(CpDataTransferDeskService.StorageAllowsUser("", 1));
        Assert.False(CpDataTransferDeskService.StorageAllowsUser("not json", 1));
        Assert.False(CpDataTransferDeskService.StorageAllowsUser("{\"users\":[1]}", 1));
    }

    [Fact]
    public void Export_options_follow_the_php_request_fields()
    {
        var options = CpCatalogueExportService.ParseOptions(new Dictionary<string, string>
        {
            ["output_format"] = "json",
            ["output_products_text"] = "1",
            ["output_products_images"] = "on",
            ["offices"] = "3,7,3",
            ["group_id"] = "2"
        });

        Assert.True(options.IsJson);
        Assert.True(options.OutputProductsText);
        Assert.True(options.OutputProductsImages);
        Assert.False(options.OutputProductsSuggestions);
        Assert.Equal([3L, 7L], options.Offices);
        Assert.Equal(2, options.GroupId);
    }

    [Fact]
    public void Export_file_name_carries_the_selected_format()
    {
        var stamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var xml = CpCatalogueExportService.ParseOptions(new Dictionary<string, string> { ["output_format"] = "xml" });
        var json = CpCatalogueExportService.ParseOptions(new Dictionary<string, string> { ["output_format"] = "json" });

        Assert.Equal("catalogue_dump_20260102030405.xml", CpCatalogueExportService.BuildFileName(xml, stamp));
        Assert.Equal("catalogue_dump_20260102030405.json", CpCatalogueExportService.BuildFileName(json, stamp));
    }

    [Fact]
    public void Download_path_resolution_rejects_traversal_and_unknown_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "epc-dt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "catalogue_dump_1.xml"), "<catalogue/>");
            var service = new CpCatalogueExportService(new UnconfiguredConnections(), root);

            Assert.NotNull(service.ResolveExportPath("catalogue_dump_1.xml"));
            Assert.Null(service.ResolveExportPath("../secrets.txt"));
            Assert.Null(service.ResolveExportPath("missing.xml"));
            Assert.Null(service.ResolveExportPath(""));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Clear_modes_match_the_php_clear_table_values()
    {
        Assert.Equal(CpCatalogueClearMode.None, CpCatalogueImportService.ParseClearMode("0"));
        Assert.Equal(CpCatalogueClearMode.StorageData, CpCatalogueImportService.ParseClearMode("1"));
        Assert.Equal(CpCatalogueClearMode.Catalogue, CpCatalogueImportService.ParseClearMode("2"));
        Assert.Equal(CpCatalogueClearMode.None, CpCatalogueImportService.ParseClearMode(null));
    }

    [Fact]
    public void Exported_xml_and_json_round_trip_back_into_import_offers()
    {
        var options = CpCatalogueExportService.ParseOptions(new Dictionary<string, string>
        {
            ["output_format"] = "xml",
            ["output_products_text"] = "1",
            ["offices"] = "1",
            ["group_id"] = "2"
        });

        var categories = new List<CpCatalogueExportService.CatalogueCategory>
        {
            new(10, 0, "Filters")
        };
        var offers = new List<CpCatalogueExportService.CatalogueOffer>
        {
            new(55, 10, 1, 3, "Oil filter", "oil-filter", 19.5m, 4m, "Long life", "img.jpg", [56])
        };

        var xml = CpCatalogueExportService.RenderXml(categories, offers, options);
        var fromXml = CpCatalogueImportService.ParsePayload(xml);
        Assert.Single(fromXml);
        Assert.Equal(10, fromXml[0].CategoryId);
        Assert.Equal("Oil filter", fromXml[0].Caption);
        Assert.Equal("oil-filter", fromXml[0].Alias);
        Assert.Equal(19.5m, fromXml[0].Price);
        Assert.Equal(4m, fromXml[0].Exist);

        var json = CpCatalogueExportService.RenderJson(categories, offers, options with { OutputFormat = "json" });
        var fromJson = CpCatalogueImportService.ParsePayload(json);
        Assert.Single(fromJson);
        Assert.Equal(fromXml[0], fromJson[0]);
    }

    [Fact]
    public void Malformed_uploads_parse_to_no_offers()
    {
        Assert.Empty(CpCatalogueImportService.ParsePayload(""));
        Assert.Empty(CpCatalogueImportService.ParsePayload("<catalogue><offers>"));
        Assert.Empty(CpCatalogueImportService.ParsePayload("{\"offers\":"));
        Assert.Empty(CpCatalogueImportService.ParsePayload("{\"rows\":[]}"));
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }
}
