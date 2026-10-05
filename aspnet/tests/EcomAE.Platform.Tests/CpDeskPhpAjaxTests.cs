using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpDeskPhpAjaxTests
{
    [Fact]
    public async Task CustomerDocumentAndCrm_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        Assert.DoesNotContain("Database=docpart", connectionString, StringComparison.OrdinalIgnoreCase);
        try
        {
            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var staff = "admin_session=admin-token; admin_u_id=9";
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");

            var guest = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("action", "save_customer"), ("user_id", "7")), string.Empty);
            Assert.Equal("Access denied", guest.Json.RootElement.GetProperty("message").GetString());
            var noAction = await SendAsync(client, CpLegacyPhpAjaxLinks.UsersCustomerEndpoint, null, staff);
            Assert.Equal("No action", noAction.Json.RootElement.GetProperty("message").GetString());
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("action", "save_customer"), ("user_id", "7")), staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var missingBuyer = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "save_customer"), ("user_id", "7"), ("company", "Acme")), staff);
            Assert.Equal(StorefrontPhpAjax.BuyerProfilesMissing, missingBuyer.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_einvoice_buyer_profiles (
                  user_id INT NOT NULL PRIMARY KEY,
                  buyer_name VARCHAR(255) NOT NULL DEFAULT '',
                  trn VARCHAR(64) NOT NULL DEFAULT '',
                  tin VARCHAR(64) NOT NULL DEFAULT '',
                  legal_reg_no VARCHAR(64) NOT NULL DEFAULT '',
                  legal_reg_type VARCHAR(8) NOT NULL DEFAULT '',
                  authority_name VARCHAR(255) NOT NULL DEFAULT '',
                  address_line1 VARCHAR(255) NOT NULL DEFAULT '',
                  city VARCHAR(120) NOT NULL DEFAULT '',
                  emirate VARCHAR(120) NOT NULL DEFAULT '',
                  country_code VARCHAR(8) NOT NULL DEFAULT '',
                  phone VARCHAR(64) NOT NULL DEFAULT '',
                  email VARCHAR(120) NOT NULL DEFAULT '',
                  electronic_id VARCHAR(16) NOT NULL DEFAULT '',
                  peppol_endpoint VARCHAR(64) NOT NULL DEFAULT '',
                  buyer_onboarded TINYINT NOT NULL DEFAULT 0,
                  time_updated BIGINT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "CREATE TABLE users_profiles (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, data_key VARCHAR(64) NOT NULL, data_value VARCHAR(255) NOT NULL)");
            var saved = await SendAsync(client, CpLegacyPhpAjaxLinks.UsersCustomerEndpoint, Form(
                ("csrf_guard_key", "admin-csrf"),
                ("action", "save_customer"),
                ("user_id", "7"),
                ("buyer_name", "Nora"),
                ("company", "Acme"),
                ("address_line1", "Dock 4"),
                ("city", "Dubai"),
                ("phone", "050"),
                ("trn", "100-200"),
                ("country_code", "ae")), staff);
            Assert.True(saved.Json.RootElement.GetProperty("status").GetBoolean(), saved.Body);
            Assert.Equal("Customer profile saved", saved.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("Acme", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = 7 AND data_key = 'company'"));
            Assert.Equal("AE", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = 7 AND data_key = 'epc_reg_country'"));
            Assert.Equal("100200", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = 7 AND data_key = 'epc_reg_trn'"));
            Assert.Equal("Nora", await ScalarAsync(connectionString, "SELECT buyer_name FROM epc_einvoice_buyer_profiles WHERE user_id = 7"));

            var advanceMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "customer_advance"), ("user_id", "7"), ("amount", "25.50")), staff);
            Assert.Equal(StorefrontPhpAjax.CustomerAccountingMissing, advanceMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE users (user_id INT NOT NULL PRIMARY KEY)");
            await ExecuteAsync(connectionString, "INSERT INTO users (user_id) VALUES (7)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_accounting_codes (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, income TINYINT NOT NULL, name VARCHAR(255) NOT NULL, manual_available TINYINT NOT NULL, `key` VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_users_accounting (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, time BIGINT NOT NULL, income TINYINT NOT NULL, amount DECIMAL(12,2) NOT NULL, operation_code INT NOT NULL, active TINYINT NOT NULL, office_id INT NOT NULL)");
            var advance = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "customer_advance"), ("user_id", "7"), ("amount", "25.50")), staff);
            Assert.True(advance.Json.RootElement.GetProperty("status").GetBoolean(), advance.Body);
            Assert.Equal("Customer advance recorded", advance.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("25.50", await ScalarAsync(connectionString, "SELECT CAST(amount AS CHAR) FROM shop_users_accounting WHERE user_id = 7"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(income AS CHAR) FROM shop_users_accounting WHERE user_id = 7"));
            var invoice = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "einvoice_create"), ("order_id", "3")), staff);
            Assert.False(invoice.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.EinvoiceNotPosted, invoice.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_einvoice_documents'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));

            var companyMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.DocumentEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "save_company"), ("legal_name", "Acme LLC")), staff);
            Assert.False(companyMissing.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Contains("missing", companyMissing.Json.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_document_company'"));
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_document_company (
                  id INT NOT NULL PRIMARY KEY,
                  legal_name VARCHAR(255) NOT NULL DEFAULT '',
                  trade_name VARCHAR(255) NOT NULL DEFAULT '',
                  address_line1 VARCHAR(255) NOT NULL DEFAULT '',
                  address_line2 VARCHAR(255) NOT NULL DEFAULT '',
                  city VARCHAR(120) NOT NULL DEFAULT '',
                  country VARCHAR(80) NOT NULL DEFAULT '',
                  trn VARCHAR(32) NOT NULL DEFAULT '',
                  phone VARCHAR(64) NOT NULL DEFAULT '',
                  email VARCHAR(120) NOT NULL DEFAULT '',
                  website VARCHAR(120) NOT NULL DEFAULT '',
                  logo_path VARCHAR(255) NOT NULL DEFAULT '',
                  bank_name VARCHAR(120) NOT NULL DEFAULT '',
                  bank_iban VARCHAR(64) NOT NULL DEFAULT '',
                  legal_footer VARCHAR(4000) NOT NULL DEFAULT '',
                  updated_at BIGINT NOT NULL DEFAULT 0,
                  row_version INT NOT NULL DEFAULT 1
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO epc_document_company (id, legal_name) VALUES (1, '')");
            var company = await SendAsync(client, CpLegacyPhpAjaxLinks.DocumentEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "save_company"), ("legal_name", "Acme LLC")), staff);
            Assert.True(company.Json.RootElement.GetProperty("status").GetBoolean(), company.Body);
            Assert.Equal("Company profile saved", company.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("Acme LLC", await ScalarAsync(connectionString, "SELECT legal_name FROM epc_document_company WHERE id = 1"));
            var template = await SendAsync(client, CpLegacyPhpAjaxLinks.DocumentEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "save_template")), staff);
            Assert.Equal("Template code required", template.Json.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_crm_leads (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  company VARCHAR(255) NOT NULL,
                  contact_name VARCHAR(255) NOT NULL,
                  email VARCHAR(255) NOT NULL,
                  phone VARCHAR(64) NOT NULL,
                  source VARCHAR(64) NOT NULL,
                  status VARCHAR(32) NOT NULL,
                  owner_user_id INT NOT NULL,
                  expected_value DECIMAL(12,2) NOT NULL,
                  notes TEXT NOT NULL,
                  time_created BIGINT NOT NULL,
                  time_updated BIGINT NOT NULL
                )
                """);
            var lead = await SendAsync(client, CpLegacyPhpAjaxLinks.CrmEndpoint, Form(("action", "save_lead"), ("company", "Acme"), ("contact_name", "Nora")), staff);
            Assert.True(lead.Json.RootElement.GetProperty("status").GetBoolean(), lead.Body);
            Assert.Equal("Lead saved", lead.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("Acme", await ScalarAsync(connectionString, "SELECT company FROM epc_crm_leads WHERE contact_name = 'Nora'"));
            var crmQuiet = await SendAsync(client, CpLegacyPhpAjaxLinks.CrmEndpoint, null, staff);
            Assert.Equal("No action", crmQuiet.Json.RootElement.GetProperty("message").GetString());
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SaoExec_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var configRoot = Path.Combine(Path.GetTempPath(), "ecomae-sao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configRoot);
        File.WriteAllText(Path.Combine(configRoot, "config.php"), """
            <?php
            class DP_Config {
            public $tech_key = 'local-tech';
            }
            """);
        try
        {
            await using (var closed = await StartAsync(connectionString, configRoot, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var wrong = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.SaoExec + "?key=nope&order_item_id=12&sao_action_id=3", null, string.Empty);
                Assert.Equal(StorefrontPhpAjax.OrderItemWrongKey, wrong.Json.RootElement.GetProperty("message").GetString());
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=3", null, string.Empty);
                Assert.Equal(StorefrontPhpAjax.NoDbConnect, offline.Body);
            }

            await using var host = await StartAsync(connectionString, configRoot);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var staff = "admin_session=admin-token; admin_u_id=9";
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=3", null, staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=3&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal(StorefrontPhpAjax.SaoActionsMissing, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'shop_sao%'"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_items (id INT NOT NULL PRIMARY KEY, t2_storage_id INT NOT NULL, sao_state INT NOT NULL, sao_message VARCHAR(255) NOT NULL DEFAULT '')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, t2_storage_id, sao_state) VALUES (12, 4, 8)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages (id INT NOT NULL PRIMARY KEY, interface_type INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, interface_type) VALUES (4, 2)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_states_types_link (id INT NOT NULL PRIMARY KEY, interface_type_id INT NOT NULL, state_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_states_types_link (id, interface_type_id, state_id) VALUES (5, 2, 8)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_states_types_actions_link (id INT NOT NULL PRIMARY KEY, state_type_id INT NOT NULL, action_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_states_types_actions_link (id, state_type_id, action_id) VALUES (1, 5, 3)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_actions (id INT NOT NULL PRIMARY KEY, script VARCHAR(255) NOT NULL, name VARCHAR(255) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_actions (id, script, name) VALUES (3, 'do.php', 'Do')");

            var blocked = await SendAsync(client, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=9&csrf_guard_key=admin-csrf", null, staff);
            Assert.False(blocked.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("12", blocked.Json.RootElement.GetProperty("order_item_id").GetString());
            Assert.Equal(StorefrontPhpAjax.SaoAlreadyDone, blocked.Json.RootElement.GetProperty("sao_action_message").GetString());
            var refused = await SendAsync(client, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=3&csrf_guard_key=admin-csrf", null, staff);
            Assert.False(refused.Json.RootElement.GetProperty("status").GetBoolean(), refused.Body);
            Assert.Equal(StorefrontPhpAjax.SaoScriptNotExecuted, refused.Json.RootElement.GetProperty("sao_action_message").GetString());
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT sao_message FROM shop_orders_items WHERE id = 12"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
            if (Directory.Exists(configRoot))
            {
                Directory.Delete(configRoot, true);
            }
        }
    }

    private static async Task<ProbeHost> StartAsync(string connectionString, string? configRoot = null, bool configured = true)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(configured ? new FixedConnections(connectionString) : new UnconfiguredConnections());
        if (!string.IsNullOrEmpty(configRoot))
        {
            builder.Services.Configure<PhpReferenceOptions>(options => options.PhpDocRoot = configRoot);
        }
        builder.Services.AddSingleton<IErpWriteConnectionFactory>(new WriteConnections(connectionString));
        builder.Services.AddSingleton<IErpEinvoiceProfileWriteService>(sp => new ErpEinvoiceProfileWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpDocumentControlWriteService>(sp => new CpDocumentControlWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpCrmWriteService>(sp => new CpCrmWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<IErpVoucherNumberService, ErpVoucherNumberService>();
        builder.Services.AddSingleton<IErpGlPostingService, ErpGlPostingService>();
        builder.Services.AddSingleton<IErpAuditLogWriter, ErpAuditLogWriter>();
        builder.Services.AddSingleton<IErpSettlementAllocationService, ErpSettlementAllocationService>();
        builder.Services.AddSingleton<IErpAdvanceVatService, ErpAdvanceVatService>();
        builder.Services.AddSingleton<IErpCashWriteService, ErpCashWriteService>();
        builder.Services.AddSingleton<IStorefrontPriceAccess>(new GuestPrices());
        builder.Services.AddSingleton<ICpPriceImportService>(new IdleImports());
        builder.Services.AddSingleton(ReporterStub.Create());
        var app = builder.Build();
        app.UseMiddleware<CpLegacyPhpAjaxLinkMiddleware>();
        // Implicit routing already matched the PHP URL. Match again after the rewrite,
        // the same way Program.cs calls UseRouting after this middleware.
        app.UseRouting();
        StorefrontPhpAjaxEndpoints.Map(app);
        app.MapPost(EcomAeRoutes.CpCrmAction, async (HttpContext context, ICpCrmWriteService writes, CancellationToken cancellationToken) =>
        {
            if (!context.Items.ContainsKey(CpLegacyPhpAjaxLinks.OperatorPostItem))
            {
                return Results.Json(new { status = false, message = "confirm required" });
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var written = await writes.SaveLeadAsync(
                0,
                form["company"].ToString(),
                form["contact_name"].ToString(),
                string.Empty,
                string.Empty,
                "web",
                "new",
                9,
                0m,
                string.Empty,
                cancellationToken);
            return Results.Json(new { status = written.Succeeded, message = written.Message });
        });
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private static Dictionary<string, string> Form(params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            form[field.Key] = field.Value;
        }

        return form;
    }

    private static async Task<Sent> SendAsync(HttpClient client, string path, Dictionary<string, string>? form, string cookie)
    {
        using var request = new HttpRequestMessage(form is null ? HttpMethod.Get : HttpMethod.Post, path);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        JsonDocument json = null!;
        if (body.Length > 0 && body[0] is '{' or '[')
        {
            json = JsonDocument.Parse(body);
        }

        return new Sent(response.StatusCode, body, json);
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

    private sealed record Sent(HttpStatusCode Status, string Body, JsonDocument Json);

    private sealed class ProbeHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public ProbeHost(WebApplication app, Uri baseAddress)
        {
            _app = app;
            BaseAddress = baseAddress;
        }

        public Uri BaseAddress { get; }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class GuestPrices : IStorefrontPriceAccess
    {
        public ValueTask<StorefrontPriceAccessResult> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new StorefrontPriceAccessResult(StorefrontPriceAccessState.Guest, false, "**", string.Empty, string.Empty));

        public IReadOnlyList<StorefrontPartOfferDigest> RedactOffers(IReadOnlyList<StorefrontPartOfferDigest> offers) => offers;
    }

    private sealed class IdleImports : ICpPriceImportService
    {
        public Task<CpPriceImportResult> ImportUploadAsync(CpPriceImportRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Price upload was called.");

        public Task<CpPriceImportResult> ImportWizardDirectoryAsync(long priceId, string directory, bool? cleanBefore, long uploadedBy, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Price import was called.");

        public Task<IReadOnlyList<CpPriceImportResult>> ImportRemoteAsync(IReadOnlyList<long> priceIds, long uploadedBy, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Remote price import was called.");
    }

    private class ReporterStub : System.Reflection.DispatchProxy
    {
        public static ISurfaceDashboardSummaryReporter Create()
            => Create<ISurfaceDashboardSummaryReporter, ReporterStub>();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(targetMethod?.Name ?? "dispatch");
    }

    private sealed class WriteConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public WriteConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }

    private sealed class UnconfiguredConnections : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Database was called.");

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Database was called.");

        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Database was called.");

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Database was called.");
    }

    private sealed class FixedConnections : ITenantDbConnectionFactory
    {
        private readonly string _connectionString;

        public FixedConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => OpenAsync();

        private async Task<DbConnection> OpenAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}
