using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpSmsOperatorsParityTests
{
    [Fact]
    public void Sender_resolution_follows_php_candidate_order_and_default()
    {
        Assert.Equal("+971500000001", CpSmsOperatorsDeskService.ResolveSender(new Dictionary<string, string>
        {
            ["sender_number"] = "+971500000001",
            ["from"] = "+971500000002",
        }));
        Assert.Equal("+971500000002", CpSmsOperatorsDeskService.ResolveSender(new Dictionary<string, string>
        {
            ["sender_number"] = "  ",
            ["from"] = "+971500000002",
        }));
        Assert.Equal("EPCSHOP", CpSmsOperatorsDeskService.ResolveSender(new Dictionary<string, string>
        {
            ["sender_id"] = "EPCSHOP",
        }));
        Assert.Equal("+971567607011", CpSmsOperatorsDeskService.DefaultSenderNumber);
        Assert.Equal(
            CpSmsOperatorsDeskService.DefaultSenderNumber,
            CpSmsOperatorsDeskService.ResolveSender(new Dictionary<string, string>()));
    }

    [Fact]
    public void Descriptor_parameters_keep_type_caption_and_options()
    {
        var fields = CpSmsOperatorsDeskService.ParseFields(
            """
            [
              {"name":"api_url","type":"text","caption":"API URL"},
              {"name":"retries","type":"number","caption":"Retries"},
              {"name":"route","type":"select","caption":"Route","options":[{"value":"a","caption":"Alpha"},{"value":"b"}]}
            ]
            """,
            """{"api_url":"https://sms.example/send","retries":3,"route":"b"}""");

        Assert.Equal(3, fields.Count);
        Assert.Equal("https://sms.example/send", fields[0].Value);
        Assert.Equal("number", fields[1].Type);
        Assert.Equal("3", fields[1].Value);
        Assert.Equal("Alpha", fields[2].Options[0].Caption);
        Assert.Equal("b", fields[2].Options[1].Caption);
        Assert.Equal("b", fields[2].Value);
    }

    [Fact]
    public void Associative_parameter_maps_are_normalised_to_text_fields()
    {
        var fields = CpSmsOperatorsDeskService.ParseFields(
            """{"login":"Login","password":"Password"}""",
            """{"login":"epc"}""");

        Assert.Equal(2, fields.Count);
        Assert.Equal("text", fields[0].Type);
        Assert.Equal("Login", fields[0].Caption);
        Assert.Equal("epc", fields[0].Value);
        Assert.True(fields[1].IsSecret);
    }

    [Fact]
    public void Malformed_parameter_json_yields_no_fields()
    {
        Assert.Empty(CpSmsOperatorsDeskService.ParseFields("not json", null));
        Assert.Empty(CpSmsOperatorsDeskService.ParseFields(null, """{"a":"b"}"""));
        Assert.Empty(CpSmsOperatorsDeskService.ParseValues("[1,2]"));
    }

    [Fact]
    public void Credential_fields_are_never_rendered_back_to_the_browser()
    {
        Assert.True(CpSmsOperatorsDeskService.IsSecretField("anything", "password"));
        Assert.True(CpSmsOperatorsDeskService.IsSecretField("api_key", "text"));
        Assert.True(CpSmsOperatorsDeskService.IsSecretField("AppSid", "text"));
        Assert.True(CpSmsOperatorsDeskService.IsSecretField("client_secret", "text"));
        Assert.True(CpSmsOperatorsDeskService.IsSecretField("access_token", "text"));
        Assert.False(CpSmsOperatorsDeskService.IsSecretField("sender_number", "text"));

        var fields = CpSmsOperatorsDeskService.ParseFields(
            """[{"name":"api_key","type":"text","caption":"API key"},{"name":"sender_number","type":"text","caption":"Sender"}]""",
            """{"api_key":"live-secret-value","sender_number":""}""");

        Assert.Equal(string.Empty, fields[0].Value);
        Assert.True(fields[0].HasStoredValue);
        Assert.True(fields[1].IsSender);
        Assert.Equal(CpSmsOperatorsDeskService.DefaultSenderNumber, fields[1].Value);
    }

    [Fact]
    public void Operator_cards_group_epc_handlers_under_the_php_mena_heading()
    {
        var mena = new CpSmsOperatorCard(1, "Etisalat", "epc_etisalat", "", true, true, []);
        var other = new CpSmsOperatorCard(2, "SMSC", "smsc", "", false, false, []);
        Assert.Equal("UAE / GCC / Pakistan", mena.Group);
        Assert.Equal("Other operators", other.Group);

        var view = new CpSmsOperatorsDeskView([mena, other], 2, "+971567607011", "+971567607011", "database", "");
        Assert.Equal("Etisalat", view.ActiveOperator!.Name);
        Assert.Equal("SMSC", view.Selected!.Name);
    }

    [Fact]
    public async Task Unconfigured_db_returns_typed_empty_view()
    {
        var desk = new CpSmsOperatorsDeskService(new UnconfiguredConnections());
        var view = await desk.LoadAsync(null);
        Assert.Equal("unconfigured", view.Source);
        Assert.Empty(view.Operators);
        Assert.Equal(0, view.SelectedId);
        Assert.Equal(CpSmsOperatorsDeskService.DefaultSenderNumber, view.ActiveSender);
    }

    [Fact]
    public void Blank_posted_field_keeps_the_stored_credential()
    {
        var merged = CpSmsWhatsappWriteService.MergeFieldValues(
            """{"api_key":"stored-key","sender_number":"+971500000001"}""",
            new Dictionary<string, string>
            {
                ["api_key"] = "",
                ["sender_number"] = "+971500000009",
            });

        Assert.Contains("\"api_key\":\"stored-key\"", merged, StringComparison.Ordinal);
        Assert.Contains("\"sender_number\":\"+971500000009\"", merged, StringComparison.Ordinal);

        var fresh = CpSmsWhatsappWriteService.MergeFieldValues(
            "not json",
            new Dictionary<string, string> { ["login"] = "epc" });
        Assert.Equal("""{"login":"epc"}""", fresh);
    }

    [Fact]
    public void Service_reads_the_php_table_with_php_filter_and_order()
    {
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpSmsOperatorsDeskService.cs"));
        Assert.Contains("FROM `sms_api`", service, StringComparison.Ordinal);
        Assert.Contains("IFNULL(`control_available`, 0) = ?", service, StringComparison.Ordinal);
        Assert.Contains("ORDER BY CASE WHEN `handler` LIKE ? THEN 0 ELSE 1 END, `id` ASC", service, StringComparison.Ordinal);
        Assert.Contains("\"epc_%\"", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_renders_the_php_operator_workspace_not_a_digest()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpSmsWhatsappApp.razor"));
        Assert.Contains("ICpSmsOperatorsDeskService", razor, StringComparison.Ordinal);
        Assert.Contains("Current SMS operator", razor, StringComparison.Ordinal);
        Assert.Contains("Select SMS operator", razor, StringComparison.Ordinal);
        Assert.Contains("id=\"system_selector\"", razor, StringComparison.Ordinal);
        Assert.Contains("id=\"mysql_options_div_fields\"", razor, StringComparison.Ordinal);
        Assert.Contains("How messaging is structured", razor, StringComparison.Ordinal);
        Assert.Contains("TDRA", razor, StringComparison.Ordinal);
        Assert.Contains("Unifonic", razor, StringComparison.Ordinal);
        Assert.Contains("type=\"password\"", razor, StringComparison.Ordinal);
        Assert.Contains("Stored — leave blank to keep it.", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("field.IsSecret ? field.Value", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Activate_endpoint_collects_parameter_inputs_under_admin_cp_gate()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var index = module.IndexOf("ControlPanelSmsWhatsappActivate", StringComparison.Ordinal);
        Assert.True(index > 0);
        var window = module.Substring(index, 2400);
        Assert.Contains("LegacySessionKind.Admin", window, StringComparison.Ordinal);
        Assert.Contains("Capabilities.Contains(\"cp\")", window, StringComparison.Ordinal);
        Assert.Contains("StartsWith(\"p_\"", window, StringComparison.Ordinal);
        Assert.Contains("fieldValues", window, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpSmsOperatorsDeskService, EcomAE.Platform.Cp.CpSmsOperatorsDeskService", program, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
