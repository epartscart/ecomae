using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PIM custom attributes. The form and display-table renders and the name-to-code rule are checked against goldens that
/// <c>Fixtures/PimCustomFields/harness.py</c> records from the PR #8 PHP; the DB behaviour runs on a throwaway
/// <c>ecomae_cpw_*</c> schema.
/// </summary>
public sealed class ErpPimCustomFieldsTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PimCustomFields");

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement.EnumerateObject())
        {
            data.Add(testCase.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_php(string name)
    {
        var c = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement.GetProperty(name);
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name);
        var module = c.GetProperty("module").GetString()!;
        var itemId = c.GetProperty("itemId").GetInt64();

        var fields = c.GetProperty("fields").EnumerateArray().Select(f => new ErpPimCustomFields.Field(
            f.GetProperty("id").GetInt64(),
            f.GetProperty("name").GetString()!,
            f.GetProperty("code").GetString()!,
            f.GetProperty("field_type").GetString()!,
            f.GetProperty("description").GetString()!,
            f.GetProperty("required").GetInt32() != 0,
            f.GetProperty("show_inventory").GetInt32() != 0,
            f.GetProperty("show_sales").GetInt32() != 0,
            f.GetProperty("show_purchase").GetInt32() != 0,
            f.GetProperty("position").GetInt32())).ToList();
        var options = c.GetProperty("options").EnumerateObject().ToDictionary(
            p => long.Parse(p.Name, CultureInfo.InvariantCulture),
            p => (IReadOnlyList<ErpPimCustomFields.Option>)p.Value.EnumerateArray().Select(o => new ErpPimCustomFields.Option(
                o.GetProperty("id").GetInt64(),
                o.GetProperty("field_id").GetInt64(),
                o.GetProperty("label").GetString()!,
                o.GetProperty("value").GetString()!,
                o.GetProperty("position").GetInt32())).ToList());
        var values = c.GetProperty("values").EnumerateArray().Select(v => new ErpPimCustomFields.ItemValue(
            v.GetProperty("field_id").GetInt64(),
            NullableText(v, "value_text"),
            NullableText(v, "value_number") is { } n ? decimal.Parse(n, CultureInfo.InvariantCulture) : null,
            NullableText(v, "value_date"),
            NullableText(v, "value_bool") is { } b ? b != "0" : null,
            v.GetProperty("value_option_ids").GetString()!)).ToDictionary(v => v.FieldId);

        var moduleFields = fields.Where(f => module == "" || f.ShowsOn(module)).ToList();
        var existing = itemId > 0 ? values : new Dictionary<long, ErpPimCustomFields.ItemValue>();
        Assert.Equal(golden.GetProperty("form").GetString(), ErpPimCustomFields.RenderFormFields(moduleFields, options, existing));
        Assert.Equal(
            golden.GetProperty("table").GetString(),
            ErpPimCustomFields.RenderDisplayTable(ErpPimCustomFields.DisplayRows(fields, options, values, module)));

        var names = c.TryGetProperty("names", out var list) ? list.EnumerateArray().Select(e => e.GetString()!).ToList() : [];
        Assert.Equal(golden.GetProperty("codes").EnumerateArray().Select(e => e.GetString()!).ToList(), names.Select(ErpPimCustomFields.CodeFromName).ToList());
    }

    [Theory]
    [InlineData("Inventory, Non-Inventory,Service", new[] { "Inventory", "Non-Inventory", "Service" })]
    [InlineData("Red\r\nGreen\n\n , Blue,", new[] { "Red", "Green", "Blue" })]
    [InlineData("  ", new string[0])]
    public void Parses_option_lists(string raw, string[] expected)
        => Assert.Equal(expected, ErpPimCustomFields.ParseOptionList(raw));

    [Fact]
    public void Validates_required_number_and_date_before_the_item_is_written()
    {
        var fields = new List<ErpPimCustomFields.Field>
        {
            new(1, "Origin", "origin", "text", "", true, true, true, true),
            new(2, "Weight", "weight", "number", "", false, true, true, true),
            new(3, "Launch", "launch", "date", "", false, true, true, true),
            new(4, "Grade", "grade", "single_option", "", true, true, true, true),
            new(5, "Fragile", "fragile", "boolean", "", true, true, true, true),
        };
        var errors = ErpPimCustomFields.ValidatePost(fields, new Dictionary<string, IReadOnlyList<string>>
        {
            ["pim_field_1"] = [" "],
            ["pim_field_2"] = ["12kg"],
            ["pim_field_3"] = ["2026-02-30"],
            ["pim_field_4"] = ["0"],
        });
        Assert.Equal(["Origin is required.", "Weight must be a number.", "Launch must be a date (YYYY-MM-DD).", "Grade is required."], errors);

        Assert.Empty(ErpPimCustomFields.ValidatePost(fields, new Dictionary<string, IReadOnlyList<string>>
        {
            ["pim_field_1"] = ["Japan"],
            ["pim_field_2"] = ["-1.5"],
            ["pim_field_3"] = ["2026-02-28"],
            ["pim_field_4"] = ["9"],
        }));
    }

    [Fact]
    public async Task Creates_fields_options_and_values_on_a_throwaway_schema()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            await using var db = new MySqlConnection(cs);
            await db.OpenAsync();
            var ct = CancellationToken.None;
            await ErpPimCustomFields.EnsureSchemaAsync(db, ct);
            await ErpPimCustomFields.EnsureSchemaAsync(db, ct);

            var type = await ErpPimCustomFields.CreateFieldAsync(db, new("Inventory Type", "single_option", "How stock is tracked", true, true, true, true, "Inventory, Non-Inventory\nService"), ct);
            var again = await ErpPimCustomFields.CreateFieldAsync(db, new("Inventory  type!", "single_option", "", false, true, false, false, "A"), ct);
            var weight = await ErpPimCustomFields.CreateFieldAsync(db, new("Weight", "number", "", false, true, false, true), ct);
            var flag = await ErpPimCustomFields.CreateFieldAsync(db, new("Hazardous", "boolean", "", false, true, true, true), ct);
            var arabic = await ErpPimCustomFields.CreateFieldAsync(db, new("نوع", "bogus-type", "", false, false, true, false), ct);
            await Assert.ThrowsAsync<ErpWriteException>(() => ErpPimCustomFields.CreateFieldAsync(db, new("  ", "text", "", false, true, true, true), ct));

            var all = await ErpPimCustomFields.ListFieldsAsync(db, "", ct);
            Assert.Equal(["inventory_type", "inventory_type_2", "weight", "hazardous", "field"], all.OrderBy(f => f.Id).Select(f => f.Code));
            Assert.Equal("text", all.Single(f => f.Id == arabic).FieldType);
            Assert.Equal(3, all.Single(f => f.Id == type).OptionCount);
            Assert.Equal([type, again, weight, flag], (await ErpPimCustomFields.ListFieldsAsync(db, "inventory", ct)).Select(f => f.Id).Order());
            Assert.Equal([type, flag, arabic], (await ErpPimCustomFields.ListFieldsAsync(db, "sales", ct)).Select(f => f.Id).Order());

            var extra = await ErpPimCustomFields.AddOptionAsync(db, type, "Kit", ct);
            await Assert.ThrowsAsync<ErpWriteException>(() => ErpPimCustomFields.AddOptionAsync(db, weight, "x", ct));
            await Assert.ThrowsAsync<ErpWriteException>(() => ErpPimCustomFields.AddOptionAsync(db, 9999, "x", ct));
            var options = await ErpPimCustomFields.OptionsByFieldAsync(db, ct);
            Assert.Equal(["Inventory", "Non-Inventory", "Service", "Kit"], options[type].Select(o => o.Label));
            var service = options[type][2].Id;
            var foreign = options[again][0].Id;

            var post = new Dictionary<string, IReadOnlyList<string>>
            {
                ["pim_field_" + type] = [foreign.ToString(CultureInfo.InvariantCulture)],
                ["pim_field_" + weight] = ["12.50"],
            };
            var saved = await ErpPimCustomFields.SaveFromPostAsync(db, 42, post, "inventory", ct);
            Assert.Empty(saved.Errors);
            var rows = await ErpPimCustomFields.DisplayRowsAsync(db, 42, "", ct);
            Assert.Equal(["Hazardous=No", "Inventory Type=", "Weight=12.5"], rows.Select(r => r.Field.Name + "=" + r.DisplayValue));

            post["pim_field_" + type] = [service.ToString(CultureInfo.InvariantCulture)];
            post["pim_field_" + weight] = ["heavy"];
            post["pim_field_" + flag] = ["1"];
            saved = await ErpPimCustomFields.SaveFromPostAsync(db, 42, post, "inventory", ct);
            Assert.Equal(["Weight must be a number."], saved.Errors);
            rows = await ErpPimCustomFields.DisplayRowsAsync(db, 42, "sales", ct);
            Assert.Equal(["Hazardous=Yes", "Inventory Type=Service"], rows.Select(r => r.Field.Name + "=" + r.DisplayValue));
            Assert.Equal(1, await CountAsync(db, "SELECT COUNT(*) FROM `epc_pim_item_values` WHERE `item_id` = 42 AND `field_id` = " + type));

            var form = await ErpPimCustomFields.RenderFormFieldsAsync(db, "inventory", 42, ct);
            Assert.Contains("<option value=\"" + service + "\" selected>Service</option>", form, StringComparison.Ordinal);
            Assert.Contains("name=\"pim_field_" + weight + "\" value=\"12.5\"", form, StringComparison.Ordinal);

            Assert.True(await ErpPimCustomFields.DeactivateOptionAsync(db, extra, ct));
            Assert.False(await ErpPimCustomFields.DeactivateOptionAsync(db, extra, ct));
            Assert.True(await ErpPimCustomFields.DeactivateFieldAsync(db, weight, ct));
            Assert.DoesNotContain(await ErpPimCustomFields.ListFieldsAsync(db, "", ct), f => f.Id == weight);
            Assert.DoesNotContain(await ErpPimCustomFields.DisplayRowsAsync(db, 42, "", ct), r => r.Field.Id == weight);
            Assert.Equal(3, (await ErpPimCustomFields.ListFieldsAsync(db, "", ct)).Single(f => f.Id == type).OptionCount);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    private static string? NullableText(JsonElement element, string property)
        => element.GetProperty(property).ValueKind == JsonValueKind.Null ? null : element.GetProperty(property).GetString();

    private static async Task<long> CountAsync(MySqlConnection db, string sql)
    {
        await using var command = new MySqlCommand(sql, db);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
