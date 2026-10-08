using EcomAE.Platform.Configuration;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <see cref="StorefrontRegFormLoader"/> on a throwaway <c>ecomae_cpw_*</c> schema: the page it renders equals
/// <see cref="StorefrontRegForm.Render"/> of the rows, translations and session it should have read.
/// </summary>
public sealed class StorefrontRegFormLoaderTests
{
    private const string Ddl = """
        CREATE TABLE `reg_fields` (`id` INT PRIMARY KEY, `main_flag` INT, `name` VARCHAR(64), `caption` VARCHAR(64), `show_for` VARCHAR(64),
          `required_for` VARCHAR(64), `maxlen` INT, `regexp` VARCHAR(255), `widget_type` VARCHAR(32), `widget_options` TEXT, `example` VARCHAR(64), `order` INT);
        CREATE TABLE `reg_variants` (`id` INT PRIMARY KEY, `caption` VARCHAR(64), `order` INT);
        CREATE TABLE `sessions` (`id` INT AUTO_INCREMENT PRIMARY KEY, `session` VARCHAR(64), `user_id` INT, `csrf_guard_key` VARCHAR(64));
        CREATE TABLE `sms_api` (`id` INT PRIMARY KEY, `active` INT);
        CREATE TABLE `lang_text_strings` (`str_key` VARCHAR(64) PRIMARY KEY, `is_error` INT, `same` VARCHAR(8));
        CREATE TABLE `lang_text_strings_translation` (`str_key` VARCHAR(64), `lang_code` VARCHAR(8), `value` TEXT);
        INSERT INTO `reg_fields` VALUES
          (1, 1, 'email', 'c_email', '[1,2]', '[1,2]', 64, '', 'text', '{}', '', 1),
          (2, 0, 'name', 'c_name', '[1,2]', '[1]', 64, '^[A-Za-z ]+$', 'text', '{}', 'x_name', 2),
          (3, 0, 'company', 'c_company', '[2]', '[2]', 128, '', 'select', '{"a":1}', '', 3);
        INSERT INTO `reg_variants` VALUES (1, 'v_retail', 1), (2, 'v_trade', 2);
        INSERT INTO `sessions` (`session`, `user_id`, `csrf_guard_key`) VALUES ('guest-tok', 0, 'csrf-guest'), ('other', 5, 'csrf-other');
        INSERT INTO `lang_text_strings` VALUES ('c_name', 0, NULL), ('x_name', 0, NULL), ('v_retail', 0, NULL), ('v_trade', 0, NULL), ('4743', 0, NULL), ('4740', 0, NULL);
        INSERT INTO `lang_text_strings_translation` VALUES ('c_name', 'en', 'Name'), ('x_name', 'en', 'John Smith'), ('v_retail', 'en', 'Retail'),
          ('v_trade', 'en', 'Trade'), ('4743', 'en', 'Register'), ('4740', 'en', 'Already signed in');
        """;

    [Fact]
    public async Task Reads_rows_translations_session_and_providers_into_the_php_page()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";";
        var docRoot = Directory.CreateTempSubdirectory("regform-docroot-").FullName;
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            await ExecAsync(cs, Ddl);
            await File.WriteAllTextAsync(
                Path.Combine(docRoot, "config.epc-oauth.php"),
                "<?php\nreturn array(\n'google' => array('client_id' => 'gid', 'client_secret' => 'gsecret'),\n);\n");
            var php = new PhpReferenceOptions { PhpDocRoot = docRoot };
            var env = new StubEnvironment(docRoot);
            var connections = new StorefrontOrderPrintTests.FixedConnections(cs);
            var http = new DefaultHttpContext();
            http.Request.Host = new HostString("www.gulf-parts.com");
            var config = new Dictionary<string, string>(StringComparer.Ordinal) { ["min_password_len"] = "7", ["domain_path"] = "https://www.gulf-parts.com/" };

            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            var html = await StorefrontRegFormLoader.RenderAsync(
                http,
                connection,
                connections,
                php,
                env,
                new StorefrontRegFormLoader.Request(false, "/en", "guest-tok", "0", null, config, null),
                CancellationToken.None);

            Assert.Contains("Continue with Google", html, StringComparison.Ordinal);
            var uid = System.Text.RegularExpressions.Regex.Match(html, "epc_social_([0-9a-f]{8})").Groups[1].Value;
            Assert.Equal(8, uid.Length);
            var buttons = new StorefrontOAuthButtons().Render(
                ["google"],
                new StorefrontOAuthButtons.Options { Context = "storefront", ReturnUrl = "/en/", RequireTerms = true, Divider = false },
                null,
                uid);
            var t = new Dictionary<int, string> { [4743] = "Register", [4740] = "Already signed in" };
            var expected = StorefrontRegForm.Render(
                new StorefrontRegForm.Input
                {
                    AdditionalFields =
                    [
                        new("0", "name", "Name", "[1,2]", "[1]", "64", "^[A-Za-z ]+$", "text", "{}", "John Smith"),
                        new("0", "company", string.Empty, "[2]", "[2]", "128", string.Empty, "select", "{\"a\":1}", string.Empty),
                    ],
                    Variants = [new("1", "Retail"), new("2", "Trade")],
                    Communications = new StorefrontPhpAjax.Communications(false, false),
                    LangHref = "/en",
                    CsrfGuardKey = "csrf-guest",
                    SocialBlock = EpcRegistrationEnhancedRender.SocialBlock(true, "/en", string.Empty, "Gulf-parts", "Shop", buttons, null, null),
                    UsersAgreement = StorefrontAuthPartials.UsersAgreementModule("/en", _ => string.Empty),
                    OtpModal = new StorefrontOtpModal().Render(StorefrontRegForm.OtpModalOptions(string.Empty, string.Empty)),
                    MinPasswordLen = "7",
                    DomainPath = "https://www.gulf-parts.com/",
                },
                id => t.TryGetValue(id, out var s) ? s : string.Empty);
            Assert.Equal(expected, html);

            var signedIn = await StorefrontRegFormLoader.RenderAsync(
                http,
                connection,
                connections,
                php,
                env,
                new StorefrontRegFormLoader.Request(true, "/en", "other", "5", null, config, null),
                CancellationToken.None);
            Assert.Equal("Already signed in", signedIn);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
            Directory.Delete(docRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("www.gulf-parts.com", "Gulf-parts")]
    [InlineData("shop.example.ae", "Shop example ae")]
    [InlineData("epartscart.com", "Epartscart")]
    [InlineData("", "")]
    public void Host_trade_name_follows_php(string host, string expected)
        => Assert.Equal(expected, StorefrontRegFormLoader.HostTradeName(host));

    [Theory]
    [InlineData("/en", "en")]
    [InlineData("/ar", "ar")]
    [InlineData("", "en")]
    public void Lang_code_from_href(string href, string expected) => Assert.Equal(expected, StorefrontRegFormLoader.LangCode(href));

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class StubEnvironment(string root) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = root;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ApplicationName { get; set; } = "EcomAE.Platform";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = root;

        public string EnvironmentName { get; set; } = "Development";
    }
}
