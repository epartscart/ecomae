using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public class CpNotificationSettingsWriteTests
{
    [Fact]
    public void Routes_expose_toggle()
    {
        Assert.Equal("/cp/notifications-app", EcomAeRoutes.ControlPanelNotificationsApp);
        Assert.Equal("/cp/notifications/toggle", EcomAeRoutes.ControlPanelNotificationsToggle);
    }

    [Fact]
    public void Normalize_type_and_flag()
    {
        Assert.Equal("email", CpNotificationSettingsWriteService.NormalizeType("EMAIL"));
        Assert.Equal("sms", CpNotificationSettingsWriteService.NormalizeType("sms"));
        Assert.Null(CpNotificationSettingsWriteService.NormalizeType("push"));
        Assert.Equal(1, CpNotificationSettingsWriteService.NormalizeFlag(9));
        Assert.Equal(0, CpNotificationSettingsWriteService.NormalizeFlag(0));
    }

    [Fact]
    public void Page_posts_native_toggle_forms()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpNotificationsApp.razor"));
        Assert.Contains("method=\"post\"", razor);
        Assert.Contains("action=\"/cp/notifications/toggle\"", razor);
        Assert.Contains("name=\"confirmWrites\"", razor);
        Assert.Contains("value=\"true\"", razor);
        Assert.Contains("name=\"notification_id\"", razor);
        Assert.Contains("name=\"type\"", razor);
        Assert.Contains("name=\"set_send\"", razor);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor);
        Assert.DoesNotContain("ASP.NET", razor);
        Assert.DoesNotContain("/php-reference/", razor);
        Assert.DoesNotContain("PhpParityModuleBody", razor);
        Assert.DoesNotContain("PhpReferenceOnlyHref", razor);
    }

    [Fact]
    public void Page_is_php_notifications_settings_twin()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpNotificationsApp.razor"));
        // notifications.php list
        Assert.Contains("ICpNotificationSettingsEditorService", razor);
        Assert.Contains("epc_comms_notify.css", razor);
        Assert.Contains("epc_comms_notify.js", razor);
        Assert.Contains("id=\"epc-cn-notify-root\"", razor);
        Assert.Contains("id=\"epc-cn-notify-q\"", razor);
        foreach (var chip in new[] { "all", "email", "sms", "off" })
        {
            Assert.Contains("data-filter-chip=\"" + chip + "\"", razor);
        }

        Assert.Contains("data-notify-row", razor);
        Assert.Contains("data-email-on=", razor);
        Assert.Contains("data-sms-on=", razor);
        Assert.Contains("id=\"check_uncheck_all\"", razor);
        Assert.Contains("action=\"/cp/notifications/restore\"", razor);
        Assert.Contains("name=\"notifications_ids\"", razor);
        Assert.Contains("set_default_checked", razor);
        Assert.Contains("set_default_one", razor);
        Assert.Contains("epc-cn-toggle is-on", razor);
        Assert.Contains("epc-cn-toggle is-off", razor);
        Assert.Contains("epc-cn-toggle is-na", razor);
        Assert.Contains("What this page is for", razor);
        Assert.Contains("How to use this page", razor);
        Assert.Contains("data-epc-inline=\"1\"", razor);
        foreach (var th in new[] { "<th>ID</th>", "<th>Caption</th>", "<th>Name</th>", "<th>Event</th>", "<th>Description</th>" })
        {
            Assert.Contains(th, razor);
        }

        // notification.php editor
        Assert.Contains("ReadId(ctx.Request, \"notification_id\")", razor);
        Assert.Contains("Notifications.OpenAsync", razor);
        Assert.Contains("action=\"/cp/notifications/save\"", razor);
        Assert.Contains("name=\"email_on\"", razor);
        Assert.Contains("name=\"email_subject\"", razor);
        Assert.Contains("name=\"email_body\"", razor);
        Assert.Contains("name=\"sms_on\"", razor);
        Assert.Contains("name=\"sms_body\"", razor);
        Assert.Contains("epc-cn-vars", razor);
        Assert.Contains("epc-cn-kv", razor);
        Assert.Contains("SendForNotConfirmed", razor);
        Assert.Contains("tinymce", razor);
        Assert.Contains("function set_default()", razor);
        Assert.Contains("DefaultEmailSubject", razor);
        Assert.Contains("DefaultEmailBody", razor);
        Assert.Contains("DefaultSmsBody", razor);
        Assert.Contains("ForeseenEmail == 1", razor);
        Assert.Contains("ForeseenSms == 1", razor);
    }

    [Fact]
    public void Write_service_mirrors_php_save_and_set_default()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpNotificationSettingsWriteService.cs"));
        Assert.Contains("RestoreDefaultsAsync", src);
        Assert.Contains("SaveAsync", src);
        Assert.Contains("BeginTransactionAsync", src);
        Assert.Contains("SELECT `lang_code` FROM `lang_languages`", src);
        Assert.Contains("SET `email_on` = `foreseen_email`, `sms_on` = `foreseen_sms` WHERE `id` = ?", src);
        Assert.Contains("SET `email_on` = ?, `sms_on` = ? WHERE `id` = ?", src);
        Assert.Contains("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?", src);
        Assert.Contains("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?", src);

        Assert.Equal([3L, 7L], CpNotificationSettingsWriteService.ParseIds("[3,7,3]"));
        Assert.Equal([5L], CpNotificationSettingsWriteService.ParseIds("5, x, 0"));
        Assert.Empty(CpNotificationSettingsWriteService.ParseIds("[bad"));
    }

    [Fact]
    public void Editor_service_parses_vars_and_translates_keys()
    {
        var vars = CpNotificationSettingsEditorService.ParseVars("[{\"name\":\"order_id\",\"caption\":\"2201\",\"type\":\"text\"},{\"name\":\"user\"}]");
        Assert.Equal(2, vars.Count);
        Assert.Equal("order_id", vars[0].Name);
        Assert.Equal("2201", vars[0].Caption);
        Assert.Equal("", vars[1].Caption);
        Assert.Empty(CpNotificationSettingsEditorService.ParseVars("{}"));
        Assert.Empty(CpNotificationSettingsEditorService.ParseVars("not json"));

        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpNotificationSettingsEditorService.cs"));
        Assert.Contains("FROM `notifications_settings` ORDER BY `id` ASC", src);
        Assert.Contains("WHERE `str_key` = ? ORDER BY `lang_code` = ? DESC", src);
        Assert.Contains("default_email_subject", src);
        Assert.Contains("send_for_not_confirmed", src);
    }

    [Fact]
    public void Catalog_marks_restore_and_save_write_live_gated()
    {
        foreach (var route in new[] { "/cp/notifications/restore", "/cp/notifications/save" })
        {
            var row = SurfacePayloadContractCatalog.Functions.First(item => item.AspNetRouteOrCapability == route);
            Assert.Equal("write-live-gated", row.Status);
        }

        var page = SurfacePayloadContractCatalog.Functions.First(item => item.AspNetRouteOrCapability == "/cp/notifications-app");
        Assert.NotEqual("presentation-shell-scaffolded", page.Status);
        Assert.DoesNotContain("stay Classic", page.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Digest_sql_omits_template_bodies()
    {
        Assert.Contains("notifications_settings", LegacySurfaceDashboardSql.SelectCpNotificationSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("email_body", LegacySurfaceDashboardSql.SelectCpNotificationSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("sms_text", LegacySurfaceDashboardSql.SelectCpNotificationSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("email_subject", LegacySurfaceDashboardSql.SelectCpNotificationSettings, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_toggle_write_live_gated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/notifications/toggle");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("set_send", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_registers_write_service()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpNotificationSettingsWriteService", src);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelNotificationsToggle", module);
        Assert.Contains("ControlPanelNotificationsRestore", module);
        Assert.Contains("ControlPanelNotificationsSave", module);
        Assert.Contains("ICpNotificationSettingsEditorService", src);
    }

    [Fact]
    public async Task Toggle_OnThrowawayDatabase_EnablesDisablesAndRefusesUnsupportedChannel()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_ntf_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        try
        {
            await ExecuteAsync(connectionString, """
                CREATE TABLE notifications_settings (
                  id BIGINT PRIMARY KEY,
                  name VARCHAR(64) NOT NULL,
                  caption VARCHAR(255) NOT NULL,
                  event VARCHAR(255) NOT NULL,
                  description VARCHAR(255) NOT NULL,
                  email_on INT NOT NULL,
                  sms_on INT NOT NULL,
                  foreseen_email INT NOT NULL,
                  foreseen_sms INT NOT NULL,
                  send_for_not_confirmed INT NOT NULL,
                  email_subject VARCHAR(255) NOT NULL,
                  email_body TEXT NOT NULL,
                  sms_body TEXT NOT NULL,
                  default_email_subject VARCHAR(255) NOT NULL,
                  default_email_body TEXT NOT NULL,
                  default_sms_body TEXT NOT NULL,
                  vars TEXT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings (str_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_languages (lang_code VARCHAR(8) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO lang_languages (lang_code) VALUES ('en')");
            await ExecuteAsync(connectionString, """
                INSERT INTO notifications_settings
                  (id, name, caption, event, description, email_on, sms_on, foreseen_email, foreseen_sms, send_for_not_confirmed,
                   email_subject, email_body, sms_body, default_email_subject, default_email_body, default_sms_body, vars)
                VALUES
                  (7, 'order placed', 'Order placed', 'A customer places an order', 'Sent to the customer',
                   0, 0, 1, 0, 0,
                   'Your order', '<p>Order %order_id%</p>', '',
                   'Your order', '<p>Order %order_id%</p>', '',
                   '[{"name":"order_id","caption":"Order"}]')
                """);

            var connections = new NotifyConnections(connectionString);
            var writes = new CpNotificationSettingsWriteService(connections);
            var on = await writes.ToggleAsync(new CpNotificationToggleRequest(7, "email", 1));
            Assert.True(on.Succeeded, on.Message);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT email_on FROM notifications_settings WHERE id = 7"));

            var off = await writes.ToggleAsync(new CpNotificationToggleRequest(7, "email", 0));
            Assert.True(off.Succeeded, off.Message);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT email_on FROM notifications_settings WHERE id = 7"));

            var sms = await writes.ToggleAsync(new CpNotificationToggleRequest(7, "sms", 1));
            Assert.False(sms.Succeeded);
            Assert.Equal("not_foreseen", sms.Code);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT sms_on FROM notifications_settings WHERE id = 7"));

            var saved = await writes.SaveAsync(new CpNotificationSaveRequest(7, true, "Order ready", "<p>Ready</p>", true, "no sms", "en"));
            Assert.True(saved.Succeeded, saved.Message);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT email_on FROM notifications_settings WHERE id = 7"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT sms_on FROM notifications_settings WHERE id = 7"));
            Assert.Equal("Order ready", await ScalarAsync(connectionString, "SELECT email_subject FROM notifications_settings WHERE id = 7"));

            var restored = await writes.RestoreDefaultsAsync([7]);
            Assert.True(restored.Succeeded, restored.Message);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT email_on FROM notifications_settings WHERE id = 7"));
            Assert.Equal("Your order", await ScalarAsync(connectionString, "SELECT email_subject FROM notifications_settings WHERE id = 7"));

            var rows = await new CpNotificationSettingsEditorService(connections).ListAsync("en");
            Assert.Equal("Order placed", Assert.Single(rows).Caption);
            Assert.Equal(1, rows[0].ForeseenEmail);
            Assert.Equal(0, rows[0].ForeseenSms);
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class NotifyConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public NotifyConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root from " + AppContext.BaseDirectory);
    }
}
