using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPhpShopTests
{
    [Fact]
    public async Task ShopPhpRoutes_OnThrowawayDatabase_ThenDropped()
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
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            var missingGarage = (StorefrontPhpAjax.NotepadBody)await StorefrontPhpAjax.AddToNotepadAsync(
                connection, 7, "5", "{\"article\":\"A1\",\"manufacturer\":\"B\",\"name\":\"N\",\"exist\":1,\"price\":2}", CancellationToken.None);
            Assert.False(missingGarage.Status);
            Assert.Equal(StorefrontPhpAjax.GarageMissing, missingGarage.Message);
            Assert.DoesNotContain("doesn't exist", missingGarage.Message, StringComparison.OrdinalIgnoreCase);

            var missingArticles = (StorefrontPhpAjax.ArticleListBody)await StorefrontPhpAjax.ArticleListAsync(
                connection, 0, null, "{\"value\":\"\"}", CancellationToken.None);
            Assert.False(missingArticles.Status);
            Assert.Equal(StorefrontPhpAjax.ArticleQueriesMissing, missingArticles.Message);
            Assert.Empty(missingArticles.List);

            var missingPay = (StorefrontPhpAjax.FinanceResult)await StorefrontPhpAjax.CreateOperationAsync(
                connection, 7, "{\"amount\":10}", CancellationToken.None);
            Assert.False(missingPay.Result);
            Assert.Equal(StorefrontPhpAjax.PaymentSystemsMissing, missingPay.Message);

            var missingReturns = (StorefrontPhpAjax.ReturnsBody)await StorefrontPhpAjax.CheckReturnsAsync(
                connection, "1", CancellationToken.None);
            Assert.False(missingReturns.Status);
            Assert.Equal(StorefrontPhpAjax.ReturnsMissing, missingReturns.Message);

            foreach (var statement in Schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await ExecuteAsync(connectionString, statement);
            }
            await ExecuteAsync(connectionString, """
                INSERT INTO sessions (id, session, user_id, csrf_guard_key) VALUES
                (11, 'guest-token', 0, 'guest-key'),
                (12, 'user-token', 7, 'user-key'),
                (13, 'other-token', 8, 'other-key')
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_catalogue_products (id, caption) VALUES (9, 'Pad')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, currency) VALUES (8, 'AED')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_currencies (iso_code, rate) VALUES ('AED', 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages_data (id, storage_id, price, price_purchase, exist, reserved) VALUES (1, 8, 10.00, 4.00, 5, 0)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_statuses_ref (id, for_created) VALUES (2, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items_statuses_ref (id, for_created, check_for_return, count_flag) VALUES (3, 1, 1, 1), (5, 0, 0, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_obtaining_modes (id) VALUES (1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_accounting_codes (id, `key`) VALUES (3, '3_income_by_customer'), (4, '4_income_for_direct_pay')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_garage (id, user_id, caption, marka, vin, note, active, year, model) VALUES (4, 7, 'Daily', 'Toyota', 'VIN123', 'city', 0, 2010, 'Corolla'), (6, 8, 'Other', 'Ford', 'ZZZ', 'nope', 0, 2001, 'Focus')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_stat_article_queries (id, user_id, article, manufacturer, name) VALUES (5, 0, 'ZZ9', 'ACME', 'Widget')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices_data (article, manufacturer, name) VALUES ('S560', 'X', 'Filter')");
            await ExecuteAsync(connectionString, CartRow(7, 0, 2, "Keep", 0, "12.50", "10.00"));
            await ExecuteAsync(connectionString, CartRow(7, 0, 2, "Buy", 1, "12.50", "10.00"));
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, user_id, session_id, time, successfully_created, status, paid, how_get, how_get_json, phone_not_auth, email_not_auth, office_id) VALUES (40, 8, 0, 1, 1, 2, 0, 1, '{}', '', '', 4), (41, 7, 0, 1, 1, 2, 1, 1, '{}', '', '', 4)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, order_id, product_type, price, count_need, product_id, status, t2_manufacturer, t2_article, t2_article_show, t2_name, t2_exist, t2_time_to_exe, t2_time_to_exe_guaranteed, t2_storage, t2_min_order, t2_probability, t2_markup, t2_price_purchase, t2_office_id, t2_storage_id, t2_product_json, sao_state, sao_robot, t2_json_params) VALUES (10, 40, 2, 10.00, 1, 0, 3, '', '', '', 'Line', 1, '', '', '', 1, 100, '0', 4.00, 4, 8, '', 0, 0, ''), (11, 40, 2, 10.00, 1, 0, 5, '', '', '', 'Skip', 1, '', '', '', 1, 100, '0', 4.00, 4, 8, '', 0, 0, '')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_returns_items (item_id) VALUES (10)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_messages (order_id, is_customer, text, time, return_id) VALUES (40, 1, 'hello', 1700000000, 0)");

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var user = "session=user-token";
            var guest = "session=guest-token";
            var other = "session=other-token";

            var guestNote = await PostJsonAsync(client, StorefrontPhpAjax.NotepadPath, userForm("guest-key", ("garage", "0"), ("product", "{\"article\":\"A1\"}")), guest);
            Assert.Equal("2063", guestNote.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_garage_notepad"));

            var alien = await PostJsonAsync(client, StorefrontPhpAjax.NotepadPath, userForm("user-key", ("garage", "6"), ("product", "{\"article\":\"A1\",\"manufacturer\":\"B\",\"name\":\"N\",\"exist\":1,\"price\":2}")), user);
            Assert.Equal("2064", alien.RootElement.GetProperty("message").GetString());

            var emptyArticle = await PostJsonAsync(client, StorefrontPhpAjax.NotepadPath, userForm("user-key", ("garage", "0"), ("product", "{\"article\":\"\",\"manufacturer\":\"B\"}")), user);
            Assert.Equal("2068", emptyArticle.RootElement.GetProperty("message").GetString());

            var noted = await PostJsonAsync(client, StorefrontPhpAjax.NotepadPath, userForm("user-key", ("garage", "4"), ("product", "{\"article\":\"A&1\",\"manufacturer\":\"<b>Bosch</b>\",\"name\":\"A & B\",\"exist\":3,\"price\":9.5}")), user);
            Assert.True(noted.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("2066", noted.RootElement.GetProperty("message").GetString());
            Assert.Equal("A&amp;1", await ScalarAsync(connectionString, "SELECT article FROM shop_docpart_garage_notepad WHERE user_id=7"));
            Assert.Equal("Bosch", await ScalarAsync(connectionString, "SELECT brend FROM shop_docpart_garage_notepad WHERE user_id=7"));
            Assert.Equal("A &amp; B", await ScalarAsync(connectionString, "SELECT name FROM shop_docpart_garage_notepad WHERE user_id=7"));
            Assert.StartsWith("4225 ", await ScalarAsync(connectionString, "SELECT comment FROM shop_docpart_garage_notepad WHERE user_id=7"), StringComparison.Ordinal);

            var noAccess = await PostJsonAsync(client, StorefrontPhpAjax.GarageCarsPath, userForm("user-key", ("request_object", "{\"action\":\"delete_car\",\"car_id\":6,\"user_id\":7}")), user);
            Assert.Equal("No Access", noAccess.RootElement.GetProperty("message").GetString());
            Assert.Equal("6", await ScalarAsync(connectionString, "SELECT id FROM shop_docpart_garage WHERE id=6"));

            var found = await PostJsonAsync(client, StorefrontPhpAjax.GarageCarsPath, userForm("user-key", ("request_object", "{\"action\":\"search\",\"search_str\":\"vin1\"}")), user);
            Assert.True(found.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("4", found.RootElement.GetProperty("list")[0].GetString());

            var linked = await PostJsonAsync(client, StorefrontPhpAjax.GarageCarsPath, userForm("user-key", ("request_object", "{\"action\":\"check_car\",\"car_id\":4,\"user_id\":7,\"order_id\":99}")), user);
            Assert.Equal(1, linked.RootElement.GetProperty("flag").GetInt32());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_garage_orders WHERE order_id=99 AND garage_id=4"));
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_cars (id, caption) VALUES (3, 'Toyota')");
            await ExecuteAsync(connectionString, "UPDATE shop_docpart_garage SET mark_id=3 WHERE id=4");
            using (var linkedTable = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.GarageCarsPath))
            {
                linkedTable.Headers.TryAddWithoutValidation("Cookie", user);
                linkedTable.Content = new FormUrlEncodedContent(userForm("user-key", ("request_object", "{\"action\":\"get_table_cars\",\"car_id\":4,\"customer_id\":7,\"order_id\":99}")));
                var linkedBody = await (await client.SendAsync(linkedTable)).Content.ReadAsStringAsync();
                Assert.Contains("onclick=\"check_car(0, 4);\"", linkedBody, StringComparison.Ordinal);
                Assert.Contains("color:#66bf05", linkedBody, StringComparison.Ordinal);
                Assert.Contains("<th>5608</th>", linkedBody, StringComparison.Ordinal);
            }

            var unlinked = await PostJsonAsync(client, StorefrontPhpAjax.GarageCarsPath, userForm("user-key", ("request_object", "{\"action\":\"check_car\",\"car_id\":4,\"user_id\":7,\"order_id\":99}")), user);
            Assert.Equal(0, unlinked.RootElement.GetProperty("flag").GetInt32());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_garage_orders WHERE order_id=99"));

            var unknown = await PostJsonAsync(client, StorefrontPhpAjax.GarageCarsPath, userForm("user-key", ("request_object", "{\"action\":\"nope\",\"car_id\":4}")), user);
            Assert.False(unknown.RootElement.GetProperty("status").GetBoolean());

            using (var emptyCars = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.GarageCarsPath))
            {
                emptyCars.Headers.TryAddWithoutValidation("Cookie", user);
                emptyCars.Content = new FormUrlEncodedContent(userForm("user-key", ("request_object", "{\"action\":\"get_table_cars\",\"car_id\":4,\"customer_id\":9,\"order_id\":0}")));
                var emptyBody = await (await client.SendAsync(emptyCars)).Content.ReadAsStringAsync();
                Assert.Equal("5609", emptyBody);
            }

            using (var table = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.GarageCarsPath))
            {
                table.Headers.TryAddWithoutValidation("Cookie", user);
                table.Content = new FormUrlEncodedContent(userForm("user-key", ("request_object", "{\"action\":\"get_table_cars\",\"car_id\":4,\"customer_id\":7,\"order_id\":0}")));
                var tableBody = await (await client.SendAsync(table)).Content.ReadAsStringAsync();
                Assert.Contains("Daily", tableBody, StringComparison.Ordinal);
                Assert.Contains("VIN123", tableBody, StringComparison.Ordinal);
                Assert.Contains("Toyota - Corolla", tableBody, StringComparison.Ordinal);
                Assert.Contains("<th>630</th><th>4044</th><th>VIN</th>", tableBody, StringComparison.Ordinal);
                Assert.Contains("onclick=\"edit_car(4);\" class=\"btn btn-ar btn-primary\" title=\"2270\"", tableBody, StringComparison.Ordinal);
                Assert.Contains("onclick=\"delete_car(4);\" title=\"2224\"", tableBody, StringComparison.Ordinal);
                Assert.DoesNotContain("<th>5608</th>", tableBody, StringComparison.Ordinal);
                Assert.DoesNotContain("5609", tableBody, StringComparison.Ordinal);
            }

            using (var openTable = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.GarageCarsPath))
            {
                openTable.Headers.TryAddWithoutValidation("Cookie", user);
                openTable.Content = new FormUrlEncodedContent(userForm("user-key", ("request_object", "{\"action\":\"get_table_cars\",\"car_id\":4,\"customer_id\":7,\"order_id\":99}")));
                var openBody = await (await client.SendAsync(openTable)).Content.ReadAsStringAsync();
                Assert.Contains("onclick=\"check_car(1, 4);\"", openBody, StringComparison.Ordinal);
                Assert.Contains("color:#a9a9a9", openBody, StringComparison.Ordinal);
            }

            var blocked = await PostJsonAsync(
                client,
                StorefrontPhpAjax.CheckoutCreatePath,
                userForm("user-key"),
                user + "; how_get=" + Uri.EscapeDataString("{\"mode\":1,\"office_id\":4}"));
            Assert.Equal(StorefrontPhpAjax.CheckoutFailPrefix + "4471", blocked.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders WHERE id NOT IN (40,41)"));

            var created = await PostJsonAsync(
                client,
                StorefrontPhpAjax.CheckoutCreatePath,
                userForm("user-key"),
                user + "; users_agreement=yes; how_get=" + Uri.EscapeDataString("{\"mode\":1,\"office_id\":4}"));
            Assert.True(created.RootElement.GetProperty("status").GetBoolean());
            var orderId = created.RootElement.GetProperty("order_id").GetInt32();
            Assert.Equal("4493: " + orderId.ToString(CultureInfo.InvariantCulture), created.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT successfully_created FROM shop_orders WHERE id=" + orderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT office_id FROM shop_orders WHERE id=" + orderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("Buy", await ScalarAsync(connectionString, "SELECT t2_name FROM shop_orders_items WHERE order_id=" + orderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE t2_name='Buy'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE t2_name='Keep'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_garage_orders WHERE order_id=" + orderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + " AND text='Order email to admin admin@127.0.0.1: FAILED after retry' AND is_robot=1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + " AND text='Order email to customer (user #7): FAILED' AND is_robot=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE text LIKE '%: sent%'"));

            var forbiddenMessages = await GetJsonAsync(client, StorefrontPhpAjax.OrderMessagesPath + "?order_id=" + orderId.ToString(CultureInfo.InvariantCulture), other);
            Assert.Equal("Forbidden", forbiddenMessages.RootElement.GetProperty("message").GetString());
            Assert.Equal(501, forbiddenMessages.RootElement.GetProperty("code").GetInt32());

            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_messages (order_id, is_customer, text, time, return_id) VALUES (" + orderId.ToString(CultureInfo.InvariantCulture) + ", 1, 'hello', 1700000000, 0)");
            var messages = await GetJsonAsync(
                client,
                StorefrontPhpAjax.OrderMessagesPath + "?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&csrf_guard_key=user-key",
                user);
            Assert.Equal(JsonValueKind.Array, messages.RootElement.ValueKind);
            var stamp = DateTimeOffset.FromUnixTimeSeconds(1700000000).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            Assert.Equal(stamp, messages.RootElement[0].GetProperty("time").GetString());
            Assert.True(messages.RootElement[0].GetProperty("is_customer").GetBoolean());
            Assert.Equal("hello", messages.RootElement[0].GetProperty("text").GetString());

            using (var send = new HttpRequestMessage(HttpMethod.Get, StorefrontPhpAjax.SendMessagePath + "?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&text=" + Uri.EscapeDataString("a <b> & \"") + "&csrf_guard_key=user-key"))
            {
                send.Headers.TryAddWithoutValidation("Cookie", user);
                var sendBody = await (await client.SendAsync(send)).Content.ReadAsStringAsync();
                Assert.Equal("true", sendBody);
            }

            Assert.Equal("a &lt;b&gt; &amp; &quot;", await ScalarAsync(connectionString, "SELECT text FROM shop_orders_messages WHERE order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + " ORDER BY id DESC LIMIT 1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + " AND text='Order message email to admin admin@127.0.0.1: FAILED' AND is_robot=1"));
            var before = await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_messages");
            using (var alienSend = new HttpRequestMessage(HttpMethod.Get, StorefrontPhpAjax.SendMessagePath + "?order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&text=nope&csrf_guard_key=other-key"))
            {
                alienSend.Headers.TryAddWithoutValidation("Cookie", other);
                var alienJson = await JsonDocument.ParseAsync(await (await client.SendAsync(alienSend)).Content.ReadAsStreamAsync());
                Assert.Equal(501, alienJson.RootElement.GetProperty("code").GetInt32());
                Assert.Equal("Forbidden", alienJson.RootElement.GetProperty("message").GetString());
            }

            Assert.Equal(before, await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_messages"));

            var zero = await PostJsonAsync(client, StorefrontPhpAjax.CreateOperationPath, userForm("user-key", ("request_object", "{\"amount\":0}")), user);
            Assert.Equal("Forbidden", zero.RootElement.GetProperty("message").GetString());
            var guestPay = await PostJsonAsync(client, StorefrontPhpAjax.CreateOperationPath, userForm("guest-key", ("request_object", "{\"amount\":25}")), guest);
            Assert.Equal("Forbidden", guestPay.RootElement.GetProperty("message").GetString());
            var topped = await PostJsonAsync(client, StorefrontPhpAjax.CreateOperationPath, userForm("user-key", ("request_object", "{\"amount\":25}")), user);
            Assert.True(topped.RootElement.GetProperty("result").GetBoolean());
            Assert.False(topped.RootElement.GetProperty("pay_system").GetBoolean());
            Assert.Equal(0, topped.RootElement.GetProperty("payment_account_id").GetInt32());
            var operationId = topped.RootElement.GetProperty("operation").GetInt32();
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT income FROM shop_users_accounting WHERE id=" + operationId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT active FROM shop_users_accounting WHERE id=" + operationId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT office_id FROM shop_users_accounting WHERE id=" + operationId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT operation_code FROM shop_users_accounting WHERE id=" + operationId.ToString(CultureInfo.InvariantCulture)));

            var wrongUser = await PostJsonAsync(client, StorefrontPhpAjax.CreateOperationPath, userForm("user-key", ("request_object", "{\"amount\":10,\"order_id\":40}")), user);
            Assert.False(wrongUser.RootElement.GetProperty("result").GetBoolean());
            Assert.False(wrongUser.RootElement.GetProperty("user").GetBoolean());
            var paid = await PostJsonAsync(client, StorefrontPhpAjax.CreateOperationPath, userForm("user-key", ("request_object", "{\"amount\":10,\"order_id\":41}")), user);
            Assert.Equal("Forbidden", paid.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, user_id, session_id, time, successfully_created, status, paid, how_get, how_get_json, phone_not_auth, email_not_auth, office_id) VALUES (90, 7, 0, 1, 1, 2, 0, 1, '{}', '', '', 4)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, order_id, product_type, price, count_need, product_id, status, t2_manufacturer, t2_article, t2_article_show, t2_name, t2_exist, t2_time_to_exe, t2_time_to_exe_guaranteed, t2_storage, t2_min_order, t2_probability, t2_markup, t2_price_purchase, t2_office_id, t2_storage_id, t2_product_json, sao_state, sao_robot, t2_json_params) VALUES (90, 90, 2, 15.00, 1, 0, 3, '', '', '', 'Due', 1, '', '', '', 1, 100, '0', 4.00, 4, 8, '', 0, 0, '')");
            var partial = await PostJsonAsync(client, StorefrontPhpAjax.CreateOperationPath, userForm("user-key", ("request_object", "{\"amount\":5,\"order_id\":90,\"pay_handler\":\"not_a_gateway\"}")), user);
            Assert.Equal("Forbidden", partial.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_users_accounting WHERE operation_code=4"));
            var over = await PostJsonAsync(client, StorefrontPhpAjax.CreateOperationPath, userForm("user-key", ("request_object", "{\"amount\":20,\"order_id\":90}")), user);
            Assert.Equal("Forbidden", over.RootElement.GetProperty("message").GetString());
            var orderPay = await PostJsonAsync(client, StorefrontPhpAjax.CreateOperationPath, userForm("user-key", ("request_object", "{\"amount\":15,\"order_id\":90,\"pay_handler\":\"not_a_gateway\"}")), user);
            Assert.True(orderPay.RootElement.GetProperty("result").GetBoolean());
            Assert.False(orderPay.RootElement.GetProperty("pay_system").GetBoolean());
            var orderPayId = orderPay.RootElement.GetProperty("operation").GetInt32();
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT operation_code FROM shop_users_accounting WHERE id=" + orderPayId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT active FROM shop_users_accounting WHERE id=" + orderPayId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT income FROM shop_users_accounting WHERE id=" + orderPayId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("90", await ScalarAsync(connectionString, "SELECT pay_orders FROM shop_users_accounting WHERE id=" + orderPayId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT paid FROM shop_orders WHERE id=90"));

            var returns = await PostJsonAsync(client, StorefrontPhpAjax.ReturnsCheckPath, userForm("user-key", ("items_id", "10,11")), user);
            Assert.True(returns.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(1, returns.RootElement.GetProperty("count_confirm").GetInt32());
            Assert.Equal(1, returns.RootElement.GetProperty("count_complete").GetInt32());
            Assert.False(returns.RootElement.GetProperty("all_complete").GetBoolean());

            var recent = await PostJsonAsync(client, StorefrontPhpAjax.ArticleListPath, userForm("user-key", ("request_object", "{\"value\":\"\"}")), user + "; shop_stat=5");
            Assert.False(recent.RootElement.TryGetProperty("status", out _));
            Assert.Equal("ZZ9", recent.RootElement.GetProperty("list")[0].GetProperty("article").GetString());
            Assert.Equal("4194", recent.RootElement.GetProperty("list")[1].GetProperty("name").GetString());
            Assert.Equal("4195", recent.RootElement.GetProperty("list")[2].GetProperty("name").GetString());
            Assert.Equal("4196", recent.RootElement.GetProperty("list")[3].GetProperty("name").GetString());
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT user_id FROM shop_stat_article_queries WHERE id=5"));

            var priced = await PostJsonAsync(client, StorefrontPhpAjax.ArticleListPath, userForm("user-key", ("request_object", "{\"value\":\"S56\"}")), user);
            Assert.False(priced.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.CatalogueArticlePropertiesMissing, priced.RootElement.GetProperty("message").GetString());
            Assert.Contains("S560", priced.RootElement.GetProperty("list").EnumerateArray().Select(row => row.GetProperty("article").GetString()));
            Assert.Contains("S56545", priced.RootElement.GetProperty("list").EnumerateArray().Select(row => row.GetProperty("article").GetString()));

            var keepId = await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE t2_name='Keep'");
            var toggled = await PostJsonAsync(client, StorefrontPhpAjax.CheckForOrderPath, userForm("user-key", ("request_object", "{\"records\":[" + keepId + "]}")), user);
            Assert.True(toggled.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("ok", toggled.RootElement.GetProperty("code").GetString());
            Assert.Equal("Ok", toggled.RootElement.GetProperty("message").GetString());
            Assert.Equal(1, toggled.RootElement.GetProperty("records")[0].GetProperty("checked_for_order").GetInt32());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT checked_for_order FROM shop_carts WHERE id=" + keepId));
            var back = await PostJsonAsync(client, StorefrontPhpAjax.CheckForOrderPath, userForm("user-key", ("request_object", "{\"records\":[" + keepId + "]}")), user);
            Assert.Equal(0, back.RootElement.GetProperty("records")[0].GetProperty("checked_for_order").GetInt32());

            var hash = Type1Hash(9, 1, 8, "1", "10.00", string.Empty);
            var missingCaption = await PostJsonAsync(
                client,
                StorefrontPhpAjax.AddToBasketPath,
                new Dictionary<string, string>
                {
                    ["product_objects"] = "[{\"product_type\":1,\"product_id\":9,\"office_id\":1,\"storage_id\":8,\"storage_record_id\":\"1\",\"price\":\"10.00\",\"count_need\":2,\"exist\":7,\"time_to_exe\":\"1\",\"time_to_exe_guaranteed\":\"2\",\"check_hash\":\"" + hash + "\"}]"
                },
                user);
            Assert.False(missingCaption.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.CatalogueArticlePropertiesMissing, missingCaption.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", missingCaption.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE product_type=1"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE lang_text_strings (
                  str_key VARCHAR(64) NOT NULL PRIMARY KEY,
                  same VARCHAR(8) NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE lang_text_strings_translation (
                  str_key VARCHAR(64) NOT NULL,
                  lang_code VARCHAR(8) NOT NULL,
                  value VARCHAR(255) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_categories_properties_map (
                  id INT NOT NULL PRIMARY KEY,
                  category_id INT NOT NULL,
                  value VARCHAR(64) NOT NULL,
                  property_type_id INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_properties_values_text (
                  product_id INT NOT NULL,
                  property_id INT NOT NULL,
                  value VARCHAR(64) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_properties_values_list (
                  product_id INT NOT NULL,
                  property_id INT NOT NULL,
                  value INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_line_lists_items (
                  id INT NOT NULL PRIMARY KEY,
                  value VARCHAR(64) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "UPDATE shop_catalogue_products SET category_id=3 WHERE id=9");
            await ExecuteAsync(connectionString, """
                INSERT INTO lang_text_strings (str_key, same) VALUES
                ('k_art', NULL), ('k_mfr', NULL), ('k_sku', NULL), ('k_bosch', NULL), ('Pad', NULL)
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
                ('k_art', 'ru', 'Артикул'), ('k_art', 'en', 'Article'),
                ('k_mfr', 'ru', 'Производитель'), ('k_mfr', 'en', 'Manufacturer'),
                ('k_sku', 'en', 'C110-X'), ('k_sku', 'ru', 'C110-X'),
                ('k_bosch', 'en', 'Bosch'), ('Pad', 'en', 'Pad')
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_categories_properties_map (id, category_id, value, property_type_id) VALUES (1, 3, 'k_art', 3), (2, 3, 'k_mfr', 5)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_properties_values_text (product_id, property_id, value) VALUES (9, 1, 'k_sku')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_properties_values_list (product_id, property_id, value) VALUES (9, 2, 15)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_line_lists_items (id, value) VALUES (15, 'k_bosch')");

            var catalogued = await PostJsonAsync(client, StorefrontPhpAjax.ArticleListPath, userForm("user-key", ("request_object", "{\"value\":\"C110\"}")), user);
            Assert.False(catalogued.RootElement.TryGetProperty("status", out _));
            Assert.Contains(catalogued.RootElement.GetProperty("list").EnumerateArray(), row => row.GetProperty("article").GetString() == "C110-X" && row.GetProperty("manufacturer").GetString() == "Bosch" && row.GetProperty("name").GetString() == "Pad");

            var added = await PostJsonAsync(
                client,
                StorefrontPhpAjax.AddToBasketPath,
                new Dictionary<string, string>
                {
                    ["product_objects"] = "[{\"product_type\":1,\"product_id\":9,\"office_id\":1,\"storage_id\":8,\"storage_record_id\":\"1\",\"price\":\"10.00\",\"count_need\":2,\"exist\":7,\"time_to_exe\":\"1\",\"time_to_exe_guaranteed\":\"2\",\"check_hash\":\"" + hash + "\"}]"
                },
                user);
            Assert.True(added.RootElement.GetProperty("status").GetBoolean());
            Assert.False(added.RootElement.TryGetProperty("code", out _));
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));
            Assert.Equal("Pad", await ScalarAsync(connectionString, "SELECT t2_name FROM shop_carts WHERE product_type=1"));
            Assert.Equal("BOSCH", await ScalarAsync(connectionString, "SELECT t2_manufacturer FROM shop_carts WHERE product_type=1"));
            Assert.Equal("C110X", await ScalarAsync(connectionString, "SELECT t2_article FROM shop_carts WHERE product_type=1"));
            Assert.Equal("C110-X", await ScalarAsync(connectionString, "SELECT t2_article_show FROM shop_carts WHERE product_type=1"));
            var type1Id = await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE product_type=1");
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT count_reserved FROM shop_carts_details WHERE cart_record_id=" + type1Id));
            Assert.Equal("10.00", await ScalarAsync(connectionString, "SELECT CAST(price AS CHAR) FROM shop_carts_details WHERE cart_record_id=" + type1Id));
            Assert.Equal("4.00", await ScalarAsync(connectionString, "SELECT CAST(price_purchase AS CHAR) FROM shop_carts_details WHERE cart_record_id=" + type1Id));

            var same = await PostJsonAsync(client, StorefrontPhpAjax.ChangeCountPath, userForm("user-key", ("request_object", "{\"id\":" + type1Id + ",\"count_need\":2}")), user);
            Assert.Equal("the_same_count", same.RootElement.GetProperty("code").GetString());
            Assert.Equal(StorefrontPhpAjax.CartStringSameCount, same.RootElement.GetProperty("message").GetString());

            var raised = await PostJsonAsync(client, StorefrontPhpAjax.ChangeCountPath, userForm("user-key", ("request_object", "{\"id\":" + type1Id + ",\"count_need\":3}")), user);
            Assert.True(raised.RootElement.GetProperty("status").GetBoolean(), raised.RootElement.TryGetProperty("message", out var raisedMessage) ? raisedMessage.GetString() : raised.RootElement.ToString());
            Assert.Equal(3, raised.RootElement.GetProperty("count_need").GetInt32());
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));

            var lowered = await PostJsonAsync(client, StorefrontPhpAjax.ChangeCountPath, userForm("user-key", ("request_object", "{\"id\":" + type1Id + ",\"count_need\":1}")), user);
            Assert.True(lowered.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT count_reserved FROM shop_carts_details WHERE cart_record_id=" + type1Id));

            var removed = await PostJsonAsync(client, StorefrontPhpAjax.DeleteCartPath, userForm("user-key", ("request_object", "{\"records_to_del\":[" + type1Id + "]}")), user);
            Assert.True(removed.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("ok", removed.RootElement.GetProperty("code").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE id=" + type1Id));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts_details WHERE cart_record_id=" + type1Id));
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));

            var again = await PostJsonAsync(
                client,
                StorefrontPhpAjax.AddToBasketPath,
                new Dictionary<string, string>
                {
                    ["product_objects"] = "[{\"product_type\":1,\"product_id\":9,\"office_id\":1,\"storage_id\":8,\"storage_record_id\":\"1\",\"price\":\"10.00\",\"count_need\":2,\"exist\":7,\"time_to_exe\":\"1\",\"time_to_exe_guaranteed\":\"2\",\"check_hash\":\"" + hash + "\"}]"
                },
                user);
            Assert.True(again.RootElement.GetProperty("status").GetBoolean());
            var officeCart = await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE product_type=1");
            await ExecuteAsync(connectionString, "UPDATE shop_storages_data SET exist=0 WHERE id=1");
            await ExecuteAsync(connectionString, "ALTER TABLE shop_storages_data ADD COLUMN product_id INT NOT NULL DEFAULT 0, ADD COLUMN arrival_time INT NOT NULL DEFAULT 0");
            await ExecuteAsync(connectionString, "ALTER TABLE shop_offices_storages_map ADD COLUMN additional_time INT NOT NULL DEFAULT 0");
            await ExecuteAsync(connectionString, "UPDATE shop_storages_data SET product_id=9 WHERE id=1");
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_geo (id INT NOT NULL PRIMARY KEY)
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_offices (id INT NOT NULL PRIMARY KEY)
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_offices_geo_map (geo_id INT NOT NULL, office_id INT NOT NULL)
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_geo (id) VALUES (1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices (id) VALUES (2)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices_geo_map (geo_id, office_id) VALUES (1, 2)");
            await ExecuteAsync(connectionString, "INSERT INTO users_groups_bind (user_id, group_id) VALUES (7, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices_storages_map (office_id, storage_id, group_id, min_point, max_point, markup, additional_time) VALUES (2, 9, 1, 0, 1000, 0, 0), (2, 10, 1, 0, 1000, 0, 0)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages_data (id, storage_id, price, price_purchase, exist, reserved, product_id, arrival_time) VALUES (3, 9, 10.00, 6.00, 1, 0, 9, 1), (4, 10, 10.00, 6.00, 1, 0, 9, 2000000000)");
            var toppedUp = await PostJsonAsync(client, StorefrontPhpAjax.ChangeCountPath, userForm("user-key", ("request_object", "{\"id\":" + officeCart + ",\"count_need\":4}")), user);
            Assert.True(toppedUp.RootElement.GetProperty("status").GetBoolean(), toppedUp.RootElement.TryGetProperty("message", out var officeMessage) ? officeMessage.GetString() : toppedUp.RootElement.ToString());
            Assert.Equal(4, toppedUp.RootElement.GetProperty("count_need").GetInt32());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=3"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=3"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=4"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=4"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT office_id FROM shop_carts_details WHERE storage_record_id=3"));
            Assert.Equal("9", await ScalarAsync(connectionString, "SELECT storage_id FROM shop_carts_details WHERE storage_record_id=3"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT count_reserved FROM shop_carts_details WHERE storage_record_id=3"));
            Assert.Equal("10.00", await ScalarAsync(connectionString, "SELECT CAST(price_purchase AS CHAR) FROM shop_carts_details WHERE storage_record_id=3"));
            Assert.Equal("0.00", await ScalarAsync(connectionString, "SELECT CAST(price AS CHAR) FROM shop_carts_details WHERE storage_record_id=3"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT office_id FROM shop_carts_details WHERE storage_record_id=4"));
            Assert.Equal("10", await ScalarAsync(connectionString, "SELECT storage_id FROM shop_carts_details WHERE storage_record_id=4"));

            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, currency) VALUES (9, 'AED'), (10, 'AED')");
            var ordersBeforeCopy = await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders");
            var missingDetails = await PostJsonAsync(
                client,
                StorefrontPhpAjax.CheckoutCreatePath,
                userForm("user-key"),
                user + "; users_agreement=yes; how_get=" + Uri.EscapeDataString("{\"mode\":1,\"office_id\":4}"));
            Assert.False(missingDetails.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.CheckoutFailPrefix + StorefrontPhpAjax.OrderItemDetailsMissing, missingDetails.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", missingDetails.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(ordersBeforeCopy, await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders_items_details'"));
            Assert.Equal(officeCart, await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE product_type=1"));
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts_details WHERE cart_record_id=" + officeCart));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=3"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=3"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=4"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=4"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_items_details (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  order_id INT NOT NULL,
                  order_item_id INT NOT NULL,
                  office_id INT NOT NULL,
                  storage_id INT NOT NULL,
                  storage_record_id INT NOT NULL,
                  count_reserved INT NOT NULL,
                  count_issued INT NOT NULL,
                  count_canceled INT NOT NULL,
                  price_purchase DECIMAL(12,2) NOT NULL
                )
                """);
            var copiedOrder = await PostJsonAsync(
                client,
                StorefrontPhpAjax.CheckoutCreatePath,
                userForm("user-key"),
                user + "; users_agreement=yes; how_get=" + Uri.EscapeDataString("{\"mode\":1,\"office_id\":4}"));
            Assert.True(copiedOrder.RootElement.GetProperty("status").GetBoolean(), copiedOrder.RootElement.TryGetProperty("message", out var copiedMessage) ? copiedMessage.GetString() : copiedOrder.RootElement.ToString());
            var copiedOrderId = copiedOrder.RootElement.GetProperty("order_id").GetInt32();
            Assert.Equal("4493: " + copiedOrderId.ToString(CultureInfo.InvariantCulture), copiedOrder.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT successfully_created FROM shop_orders WHERE id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT office_id FROM shop_orders WHERE id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT paid FROM shop_orders WHERE id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT product_type FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("9", await ScalarAsync(connectionString, "SELECT product_id FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("BOSCH", await ScalarAsync(connectionString, "SELECT t2_manufacturer FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("C110X", await ScalarAsync(connectionString, "SELECT t2_article FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("C110-X", await ScalarAsync(connectionString, "SELECT t2_article_show FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("Pad", await ScalarAsync(connectionString, "SELECT t2_name FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT count_need FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("0.00", await ScalarAsync(connectionString, "SELECT CAST(t2_price_purchase AS CHAR) FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("8", await ScalarAsync(connectionString, "SELECT sao_state FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("8", await ScalarAsync(connectionString, "SELECT sao_robot FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Contains("\"product_id\":9", await ScalarAsync(connectionString, "SELECT t2_product_json FROM shop_orders_items WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)), StringComparison.Ordinal);
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_items_details WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT count_reserved FROM shop_orders_items_details WHERE storage_record_id=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT count_issued FROM shop_orders_items_details WHERE storage_record_id=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT count_canceled FROM shop_orders_items_details WHERE storage_record_id=1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT office_id FROM shop_orders_items_details WHERE storage_record_id=1"));
            Assert.Equal("8", await ScalarAsync(connectionString, "SELECT storage_id FROM shop_orders_items_details WHERE storage_record_id=1"));
            Assert.Equal("4.00", await ScalarAsync(connectionString, "SELECT CAST(price_purchase AS CHAR) FROM shop_orders_items_details WHERE storage_record_id=1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT count_reserved FROM shop_orders_items_details WHERE storage_record_id=3"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT office_id FROM shop_orders_items_details WHERE storage_record_id=3"));
            Assert.Equal("9", await ScalarAsync(connectionString, "SELECT storage_id FROM shop_orders_items_details WHERE storage_record_id=3"));
            Assert.Equal("6.00", await ScalarAsync(connectionString, "SELECT CAST(price_purchase AS CHAR) FROM shop_orders_items_details WHERE storage_record_id=3"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT count_reserved FROM shop_orders_items_details WHERE storage_record_id=4"));
            Assert.Equal("10", await ScalarAsync(connectionString, "SELECT storage_id FROM shop_orders_items_details WHERE storage_record_id=4"));
            Assert.Equal("6.00", await ScalarAsync(connectionString, "SELECT CAST(price_purchase AS CHAR) FROM shop_orders_items_details WHERE storage_record_id=4"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=3"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=4"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE id=" + officeCart));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts_details WHERE cart_record_id=" + officeCart));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE t2_name='Keep'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture) + " AND text='Order email to admin admin@127.0.0.1: FAILED after retry' AND is_robot=1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE order_id=" + copiedOrderId.ToString(CultureInfo.InvariantCulture) + " AND text='Order email to customer (user #7): FAILED' AND is_robot=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_logs WHERE text LIKE '%: sent%'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));

            var activated = await PostJsonAsync(client, StorefrontPhpAjax.GarageCarsPath, userForm("user-key", ("request_object", "{\"action\":\"active_car\",\"car_id\":4,\"user_id\":7}")), user);
            Assert.True(activated.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT active FROM shop_docpart_garage WHERE id=4"));
            var cleared = await PostJsonAsync(client, StorefrontPhpAjax.GarageCarsPath, userForm("user-key", ("request_object", "{\"action\":\"active_car\",\"car_id\":4,\"user_id\":7}")), user);
            Assert.True(cleared.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT active FROM shop_docpart_garage WHERE id=4"));
            var deleted = await PostJsonAsync(client, StorefrontPhpAjax.GarageCarsPath, userForm("user-key", ("request_object", "{\"action\":\"delete_car\",\"car_id\":4,\"user_id\":7}")), user);
            Assert.True(deleted.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_garage WHERE id=4"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_garage WHERE id=6"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Dictionary<string, string> userForm(string csrf, params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, string> { ["csrf_guard_key"] = csrf };
        foreach (var field in fields)
        {
            form[field.Key] = field.Value;
        }

        return form;
    }

    private static string Type1Hash(int productId, int officeId, int storageId, string recordId, string price, string tech)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
            productId.ToString(CultureInfo.InvariantCulture)
            + officeId.ToString(CultureInfo.InvariantCulture)
            + storageId.ToString(CultureInfo.InvariantCulture)
            + recordId
            + price
            + tech))).ToLowerInvariant();

    private static string CartRow(int userId, int sessionId, int productType, string name, int check, string price, string purchase)
        => "INSERT INTO shop_carts (product_type, product_id, price, count_need, time, user_id, session_id, checked_for_order, t2_manufacturer, t2_article, t2_article_show, t2_name, t2_exist, t2_time_to_exe, t2_time_to_exe_guaranteed, t2_storage, t2_min_order, t2_probability, t2_markup, t2_price_purchase, t2_office_id, t2_storage_id, t2_product_json, t2_json_params) VALUES ("
            + productType.ToString(CultureInfo.InvariantCulture) + ", 0, " + price + ", 1, 1, "
            + userId.ToString(CultureInfo.InvariantCulture) + ", " + sessionId.ToString(CultureInfo.InvariantCulture) + ", " + check.ToString(CultureInfo.InvariantCulture)
            + ", 'BOSCH', '1', '1', '" + name + "', 10, '1', '2', 'S', 1, 100, '0', " + purchase + ", 4, 8, '{}', '')";

    private const string Schema = """
        CREATE TABLE sessions (
          id INT NOT NULL PRIMARY KEY,
          session VARCHAR(64) NOT NULL,
          user_id INT NOT NULL,
          csrf_guard_key VARCHAR(64) NOT NULL
        );
        CREATE TABLE shop_docpart_garage (
          id INT NOT NULL PRIMARY KEY,
          user_id INT NOT NULL,
          caption VARCHAR(64) NOT NULL,
          marka VARCHAR(64) NOT NULL,
          vin VARCHAR(64) NOT NULL,
          note VARCHAR(64) NOT NULL,
          active INT NOT NULL,
          year INT NOT NULL,
          model VARCHAR(64) NOT NULL,
          mark_id INT NOT NULL DEFAULT 0
        );
        CREATE TABLE shop_docpart_cars (
          id INT NOT NULL PRIMARY KEY,
          caption VARCHAR(64) NOT NULL
        );
        CREATE TABLE shop_docpart_garage_notepad (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          user_id INT NOT NULL,
          garage_id INT NOT NULL,
          brend VARCHAR(64) NOT NULL,
          article VARCHAR(64) NOT NULL,
          name VARCHAR(255) NOT NULL,
          exist INT NOT NULL,
          price DECIMAL(12,2) NOT NULL,
          comment VARCHAR(64) NOT NULL
        );
        CREATE TABLE shop_docpart_garage_orders (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          garage_id INT NOT NULL,
          order_id INT NOT NULL
        );
        CREATE TABLE shop_stat_article_queries (
          id INT NOT NULL PRIMARY KEY,
          user_id INT NOT NULL,
          article VARCHAR(64) NOT NULL,
          manufacturer VARCHAR(64) NOT NULL,
          name VARCHAR(64) NOT NULL
        );
        CREATE TABLE shop_docpart_prices_data (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          article VARCHAR(64) NOT NULL,
          manufacturer VARCHAR(64) NOT NULL,
          name VARCHAR(64) NOT NULL
        );
        CREATE TABLE shop_catalogue_products (
          id INT NOT NULL PRIMARY KEY,
          caption VARCHAR(64) NOT NULL,
          category_id INT NOT NULL DEFAULT 0
        );
        CREATE TABLE shop_storages (
          id INT NOT NULL PRIMARY KEY,
          currency VARCHAR(8) NOT NULL
        );
        CREATE TABLE shop_currencies (
          iso_code VARCHAR(8) NOT NULL,
          rate DECIMAL(12,4) NOT NULL
        );
        CREATE TABLE shop_storages_data (
          id INT NOT NULL PRIMARY KEY,
          storage_id INT NOT NULL,
          price DECIMAL(12,2) NOT NULL,
          price_purchase DECIMAL(12,2) NOT NULL,
          exist INT NOT NULL,
          reserved INT NOT NULL
        );
        CREATE TABLE shop_carts (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          product_type INT NOT NULL,
          product_id INT NOT NULL,
          price DECIMAL(12,2) NOT NULL,
          count_need INT NOT NULL,
          time INT NOT NULL,
          user_id INT NOT NULL,
          session_id INT NOT NULL,
          checked_for_order INT NOT NULL DEFAULT 1,
          t2_manufacturer VARCHAR(64) NOT NULL,
          t2_article VARCHAR(64) NOT NULL,
          t2_article_show VARCHAR(64) NOT NULL,
          t2_name VARCHAR(255) NOT NULL,
          t2_exist INT NOT NULL,
          t2_time_to_exe VARCHAR(16) NOT NULL,
          t2_time_to_exe_guaranteed VARCHAR(16) NOT NULL,
          t2_storage VARCHAR(64) NOT NULL,
          t2_min_order INT NOT NULL,
          t2_probability INT NOT NULL,
          t2_markup VARCHAR(16) NOT NULL,
          t2_price_purchase DECIMAL(12,2) NOT NULL,
          t2_office_id INT NOT NULL,
          t2_storage_id INT NOT NULL,
          t2_product_json TEXT NOT NULL,
          t2_json_params TEXT NOT NULL
        );
        CREATE TABLE shop_carts_details (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          cart_record_id INT NOT NULL,
          office_id INT NOT NULL,
          storage_id INT NOT NULL,
          storage_record_id INT NOT NULL,
          count_reserved INT NOT NULL,
          price DECIMAL(12,2) NOT NULL DEFAULT 0,
          price_purchase DECIMAL(12,2) NOT NULL
        );
        CREATE TABLE users_groups_bind (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          user_id INT NOT NULL,
          group_id INT NOT NULL
        );
        CREATE TABLE shop_offices_storages_map (
          office_id INT NOT NULL,
          storage_id INT NOT NULL,
          group_id INT NOT NULL,
          min_point DECIMAL(12,2) NOT NULL,
          max_point DECIMAL(12,2) NOT NULL,
          markup DECIMAL(12,2) NOT NULL
        );
        CREATE TABLE shop_orders (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          user_id INT NOT NULL,
          session_id INT NOT NULL,
          time INT NOT NULL,
          successfully_created INT NOT NULL,
          status INT NOT NULL,
          paid INT NOT NULL,
          how_get INT NOT NULL,
          how_get_json TEXT NOT NULL,
          phone_not_auth VARCHAR(64) NOT NULL,
          email_not_auth VARCHAR(64) NOT NULL,
          office_id INT NOT NULL DEFAULT 0
        );
        CREATE TABLE shop_orders_items (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          order_id INT NOT NULL,
          product_type INT NOT NULL,
          price DECIMAL(12,2) NOT NULL,
          count_need INT NOT NULL,
          product_id INT NOT NULL,
          status INT NOT NULL,
          t2_manufacturer VARCHAR(64) NOT NULL,
          t2_article VARCHAR(64) NOT NULL,
          t2_article_show VARCHAR(64) NOT NULL,
          t2_name VARCHAR(255) NOT NULL,
          t2_exist INT NOT NULL,
          t2_time_to_exe VARCHAR(16) NOT NULL,
          t2_time_to_exe_guaranteed VARCHAR(16) NOT NULL,
          t2_storage VARCHAR(64) NOT NULL,
          t2_min_order INT NOT NULL,
          t2_probability INT NOT NULL,
          t2_markup VARCHAR(16) NOT NULL,
          t2_price_purchase DECIMAL(12,2) NOT NULL,
          t2_office_id INT NOT NULL,
          t2_storage_id INT NOT NULL,
          t2_product_json TEXT NOT NULL,
          sao_state INT NOT NULL,
          sao_robot INT NOT NULL,
          t2_json_params TEXT NOT NULL
        );
        CREATE TABLE shop_orders_statuses_ref (
          id INT NOT NULL PRIMARY KEY,
          for_created INT NOT NULL
        );
        CREATE TABLE shop_orders_items_statuses_ref (
          id INT NOT NULL PRIMARY KEY,
          for_created INT NOT NULL,
          check_for_return INT NOT NULL,
          count_flag INT NOT NULL
        );
        CREATE TABLE shop_obtaining_modes (
          id INT NOT NULL PRIMARY KEY
        );
        CREATE TABLE shop_orders_messages (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          order_id INT NOT NULL,
          is_customer INT NOT NULL,
          text TEXT NOT NULL,
          time INT NOT NULL,
          return_id INT NOT NULL
        );
        CREATE TABLE shop_orders_returns_items (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          item_id INT NOT NULL
        );
        CREATE TABLE shop_users_accounting (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          user_id INT NOT NULL,
          time INT NOT NULL,
          income INT NOT NULL,
          amount DECIMAL(12,2) NOT NULL,
          operation_code INT NULL,
          active INT NOT NULL,
          pay_orders VARCHAR(64) NOT NULL,
          office_id INT NOT NULL,
          order_id INT NULL
        );
        CREATE TABLE shop_accounting_codes (
          id INT NOT NULL PRIMARY KEY,
          `key` VARCHAR(64) NOT NULL
        );
        CREATE TABLE shop_payment_systems (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          handler VARCHAR(64) NOT NULL,
          active INT NOT NULL,
          anable INT NOT NULL
        );
        CREATE TABLE shop_orders_logs (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
          order_id INT NOT NULL,
          time INT NOT NULL,
          user_id INT NOT NULL,
          is_manager INT NOT NULL,
          text TEXT NOT NULL,
          is_robot INT NOT NULL
        );
        """;

    private static async Task<JsonDocument> PostJsonAsync(HttpClient client, string path, Dictionary<string, string> form, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = new FormUrlEncodedContent(form);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(body);
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<ProbeHost> StartAsync(string connectionString)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new FixedConnections(connectionString));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(new GuestPrices());
        builder.Services.AddSingleton(ReporterStub.Create());
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

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

    private sealed class FixedConnections : ITenantDbConnectionFactory
    {
        private readonly string _connectionString;

        public FixedConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => OpenAsync();

        private async Task<System.Data.Common.DbConnection> OpenAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }

    private class ReporterStub : System.Reflection.DispatchProxy
    {
        public ReporterStub()
        {
        }

        public static ISurfaceDashboardSummaryReporter Create()
            => Create<ISurfaceDashboardSummaryReporter, ReporterStub>();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(targetMethod?.Name ?? "dispatch");
    }
}
