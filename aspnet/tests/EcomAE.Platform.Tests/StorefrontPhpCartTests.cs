using System.Globalization;
using System.Net;
using System.Net.Sockets;
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

public sealed class StorefrontPhpCartTests
{
    [Fact]
    public void SkuPhotoUrl_MatchesPhpBasename()
    {
        Assert.Equal(
            "/content/files/images/sku_media/pad%20photo.jpg",
            StorefrontPhpAjax.SkuPhotoUrl("folder/pad photo.jpg"));
        Assert.Equal(string.Empty, StorefrontPhpAjax.SkuPhotoUrl(".."));
    }

    [Fact]
    public void GuestCartDenial_MatchesPhp()
    {
        var body = (StorefrontPhpAjax.CartWriteBody)StorefrontPhpAjax.GuestCommerceDenied();
        Assert.False(body.Status);
        Assert.Equal("auth", body.Code);
        Assert.Equal(StorefrontPhpAjax.GuestAuthMessage, body.Message);
        Assert.Equal(StorefrontPhpAjax.GuestLoginUrl, body.LoginUrl);
    }

    [Fact]
    public async Task CartAndSkuMedia_OnThrowawayDatabase_ThenDropped()
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
            var missingSku = (StorefrontPhpAjax.SkuFailedBody)await StorefrontPhpAjax.SkuMediaLookupAsync(
                connection, "BOSCH", "0986", 0, CancellationToken.None);
            Assert.False(missingSku.Ok);
            Assert.Equal(StorefrontPhpAjax.SkuMediaMissing, missingSku.Error);
            Assert.Empty(missingSku.Photos);
            Assert.DoesNotContain("doesn't exist", missingSku.Error, StringComparison.OrdinalIgnoreCase);

            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_sku_profiles (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  product_id INT NOT NULL DEFAULT 0,
                  brand VARCHAR(64) NOT NULL,
                  article VARCHAR(64) NOT NULL,
                  article_key VARCHAR(64) NOT NULL,
                  title VARCHAR(255) NOT NULL,
                  status VARCHAR(24) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_sku_photos (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  profile_id INT NOT NULL,
                  file_name VARCHAR(255) NOT NULL,
                  alt VARCHAR(255) NOT NULL,
                  caption VARCHAR(255) NOT NULL,
                  photo_type VARCHAR(48) NOT NULL,
                  sort_order INT NOT NULL,
                  is_primary TINYINT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_sku_spec_groups (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  profile_id INT NOT NULL,
                  name VARCHAR(120) NOT NULL,
                  icon VARCHAR(48) NOT NULL,
                  sort_order INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_sku_spec_rows (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  group_id INT NOT NULL,
                  profile_id INT NOT NULL,
                  label VARCHAR(190) NOT NULL,
                  value TEXT NULL,
                  value_type VARCHAR(24) NOT NULL,
                  unit VARCHAR(48) NOT NULL,
                  sort_order INT NOT NULL
                )
                """);

            var empty = (StorefrontPhpAjax.SkuLookupBody)await StorefrontPhpAjax.SkuMediaLookupAsync(
                connection, "BOSCH", "0986", 0, CancellationToken.None);
            Assert.True(empty.Ok);
            Assert.Null(empty.Profile);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_sku_profiles"));

            await ExecuteAsync(connectionString, """
                INSERT INTO epc_sku_profiles (brand, article, article_key, title, status)
                VALUES ('BOSCH', '1111', '1111', 'Hidden', 'hidden')
                """);
            var hidden = (StorefrontPhpAjax.SkuLookupBody)await StorefrontPhpAjax.SkuMediaLookupAsync(
                connection, "bosch", "11-11", 0, CancellationToken.None);
            Assert.True(hidden.Ok);
            Assert.Null(hidden.Profile);

            await ExecuteAsync(connectionString, """
                INSERT INTO epc_sku_profiles (brand, article, article_key, title, status)
                VALUES ('BOSCH', '0986', '0986', 'Front pad', 'active')
                """);
            var profileId = await ScalarAsync(connectionString, "SELECT id FROM epc_sku_profiles WHERE article_key='0986'");
            await ExecuteAsync(connectionString, "INSERT INTO epc_sku_photos (profile_id, file_name, alt, caption, photo_type, sort_order, is_primary) VALUES (" + profileId + ", 'pad photo.jpg', 'Pad', 'Front', 'product', 0, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO epc_sku_spec_groups (profile_id, name, icon, sort_order) VALUES (" + profileId + ", 'Dimensions', 'fa-arrows-alt', 1)");
            var groupId = await ScalarAsync(connectionString, "SELECT id FROM epc_sku_spec_groups");
            await ExecuteAsync(connectionString, "INSERT INTO epc_sku_spec_rows (group_id, profile_id, label, value, value_type, unit, sort_order) VALUES (" + groupId + ", " + profileId + ", 'Width', '12', 'number', 'mm', 1)");

            var found = (StorefrontPhpAjax.SkuLookupBody)await StorefrontPhpAjax.SkuMediaLookupAsync(
                connection, "Bosch", "0986", 0, CancellationToken.None);
            Assert.True(found.Ok);
            Assert.Null(found.Error);
            Assert.Equal("/content/files/images/sku_media/pad%20photo.jpg", found.Url);
            var photo = Assert.Single(found.Photos);
            Assert.Equal("Pad", photo.Alt);
            Assert.Equal("Front", photo.Caption);
            Assert.Equal("product", photo.PhotoType);
            Assert.True(photo.IsPrimary);
            var spec = Assert.Single(found.Specs);
            Assert.Equal("Dimensions", spec.Name);
            Assert.Equal("fa-arrows-alt", spec.Icon);
            var row = Assert.Single(spec.Rows);
            Assert.Equal("Width", row.Label);
            Assert.Equal("12 mm", row.Value);
            Assert.Equal("number", row.ValueType);
            Assert.NotNull(found.Profile);
            Assert.Equal("Front pad", found.Profile!.Title);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_sku_profiles"));

            var missingCart = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.AddToBasketAsync(
                connection, 7, 0, true, Product("0986", "12.50", "10.00"), string.Empty, CancellationToken.None);
            Assert.False(missingCart.Status);
            Assert.Equal(StorefrontPhpAjax.CartMissing, missingCart.Message);
            Assert.DoesNotContain("doesn't exist", missingCart.Message, StringComparison.OrdinalIgnoreCase);

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_carts (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  product_type INT NOT NULL,
                  price DECIMAL(12,2) NOT NULL,
                  count_need INT NOT NULL,
                  time INT NOT NULL,
                  user_id INT NOT NULL,
                  session_id INT NOT NULL,
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
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_carts_details (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  cart_record_id INT NOT NULL,
                  storage_record_id INT NOT NULL,
                  count_reserved INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_storages_data (
                  id INT NOT NULL PRIMARY KEY,
                  exist INT NOT NULL,
                  reserved INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages_data (id, exist, reserved) VALUES (1, 5, 1)");
            await ExecuteAsync(connectionString, """
                CREATE TABLE sessions (
                  id INT NOT NULL PRIMARY KEY,
                  session VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  csrf_guard_key VARCHAR(64) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO sessions (id, session, user_id, csrf_guard_key) VALUES
                (11, 'guest-token', 0, 'guest-key'),
                (12, 'user-token', 7, 'user-key')
                """);

            var catalogue = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.AddToBasketAsync(
                connection, 7, 0, true, Product("0986", "12.50", "10.00", type: 1), string.Empty, CancellationToken.None);
            Assert.Equal("35", catalogue.Code);
            Assert.Equal(StorefrontPhpAjax.CartStringHash, catalogue.Message);
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));

            var unknown = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.AddToBasketAsync(
                connection, 7, 0, true, Product("0986", "12.50", "10.00", type: 9), string.Empty, CancellationToken.None);
            Assert.Equal("unknown_product_type", unknown.Code);
            Assert.Equal(StorefrontPhpAjax.CartStringUnknownType, unknown.Message);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts"));

            var added = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.AddToBasketAsync(
                connection, 7, 0, true, Product("0986", "12.50", "10.00"), string.Empty, CancellationToken.None);
            Assert.True(added.Status);
            Assert.Null(added.Code);
            Assert.Equal("BOSCH", await ScalarAsync(connectionString, "SELECT t2_manufacturer FROM shop_carts WHERE user_id=7"));
            Assert.Equal("0986", await ScalarAsync(connectionString, "SELECT t2_article FROM shop_carts WHERE user_id=7"));
            Assert.Equal("Pad", await ScalarAsync(connectionString, "SELECT t2_name FROM shop_carts WHERE user_id=7"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT count_need FROM shop_carts WHERE user_id=7"));
            Assert.Equal("12.50", await ScalarAsync(connectionString, "SELECT CAST(price AS CHAR) FROM shop_carts WHERE user_id=7"));
            var cartId = await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE user_id=7");
            await ExecuteAsync(connectionString, "INSERT INTO shop_carts_details (cart_record_id, storage_record_id, count_reserved) VALUES (" + cartId + ", 1, 1)");

            var duplicate = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.AddToBasketAsync(
                connection, 7, 0, true, Product("0986", "12.50", "10.00"), string.Empty, CancellationToken.None);
            Assert.False(duplicate.Status);
            Assert.Equal("already", duplicate.Code);
            Assert.Null(duplicate.Message);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE user_id=7"));

            var below = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.AddToBasketAsync(
                connection, 7, 0, true, Product("1000", "5.00", "10.00"), string.Empty, CancellationToken.None);
            Assert.Equal("no_margin", below.Code);
            Assert.Equal(StorefrontPhpAjax.NoMarginMessage, below.Message);
            var hashed = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.AddToBasketAsync(
                connection, 7, 0, true, Product("0999", "12.50", "10.00", hash: "dead"), "secret", CancellationToken.None);
            Assert.Equal("35.2", hashed.Code);
            Assert.Equal(StorefrontPhpAjax.HashExpiredMessage, hashed.Message);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts"));

            var guestAdd = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.AddToBasketAsync(
                connection, 0, 11, false, Product("0986", "12.50", "10.00"), string.Empty, CancellationToken.None);
            Assert.Equal("auth", guestAdd.Code);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts"));

            var same = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.ChangeCountAsync(
                connection, 7, 0, true, Count(cartId, 2), false, false, CancellationToken.None);
            Assert.Equal("the_same_count", same.Code);
            Assert.Equal(StorefrontPhpAjax.CartStringSameCount, same.Message);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT count_need FROM shop_carts WHERE id=" + cartId));

            var high = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.ChangeCountAsync(
                connection, 7, 0, true, Count(cartId, 12), false, false, CancellationToken.None);
            Assert.Equal("not_enough", high.Code);
            Assert.Equal(StorefrontPhpAjax.CartStringNotEnough, high.Message);
            Assert.Equal(2, high.CountNeed);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT count_need FROM shop_carts WHERE id=" + cartId));

            var raised = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.ChangeCountAsync(
                connection, 7, 0, true, Count(cartId, 4), false, false, CancellationToken.None);
            Assert.True(raised.Status);
            Assert.Equal(4, raised.CountNeed);
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT count_need FROM shop_carts WHERE id=" + cartId));

            var step = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.ChangeCountAsync(
                connection, 7, 0, true, Count(cartId, 3), false, false, CancellationToken.None);
            Assert.False(step.Status);
            Assert.Equal("error", step.Code);
            Assert.Equal(2, step.CountNeed);
            Assert.Equal(12, step.Step);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT count_need FROM shop_carts WHERE id=" + cartId));

            await ExecuteAsync(connectionString, "UPDATE shop_carts SET count_need=8, t2_exist=4, t2_min_order=6 WHERE id=" + cartId);
            var lowered = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.ChangeCountAsync(
                connection, 7, 0, true, Count(cartId, 4), false, false, CancellationToken.None);
            Assert.False(lowered.Status);
            Assert.Equal("not_enough", lowered.Code);
            Assert.Equal(StorefrontPhpAjax.CartStringMinOrder, lowered.Message);
            Assert.Equal(6, lowered.CountNeed);
            Assert.Equal("6", await ScalarAsync(connectionString, "SELECT count_need FROM shop_carts WHERE id=" + cartId));

            await ExecuteAsync(connectionString, CartSeed(9, 0, 2, "KEEP"));
            await ExecuteAsync(connectionString, CartSeed(0, 11, 2, "GUEST"));
            await ExecuteAsync(connectionString, CartSeed(0, 11, 2, "DROP"));
            await ExecuteAsync(connectionString, CartSeed(0, 11, 1, "CAT"));
            var type1Id = await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE t2_name='CAT'");
            await ExecuteAsync(connectionString, "INSERT INTO shop_carts_details (cart_record_id, storage_record_id, count_reserved) VALUES (" + type1Id + ", 1, 1)");
            var type1 = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.DeleteCartAsync(
                connection, 0, 11, Delete(type1Id), false, false, CancellationToken.None);
            Assert.True(type1.Status);
            Assert.Equal("ok", type1.Code);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE t2_name='CAT'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts_details WHERE cart_record_id=" + type1Id));
            Assert.Equal("6", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));

            var alienId = await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE user_id=9");
            var alien = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.DeleteCartAsync(
                connection, 7, 0, Delete(alienId), false, false, CancellationToken.None);
            Assert.Equal("alien_cart", alien.Code);
            Assert.Equal("Alien cart", alien.Message);
            Assert.Equal(alienId, await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE user_id=9"));

            var removed = (StorefrontPhpAjax.CartWriteBody)await StorefrontPhpAjax.DeleteCartAsync(
                connection, 7, 0, Delete(cartId), false, false, CancellationToken.None);
            Assert.True(removed.Status);
            Assert.Equal("ok", removed.Code);
            Assert.Equal("Ok", removed.Message);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE id=" + cartId));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts_details WHERE cart_record_id=" + cartId));

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var unknownAction = await client.GetAsync(StorefrontPhpAjax.SkuMediaPublicPath + "?action=nope");
            Assert.Equal(HttpStatusCode.OK, unknownAction.StatusCode);
            var unknownJson = await JsonDocument.ParseAsync(await unknownAction.Content.ReadAsStreamAsync());
            Assert.False(unknownJson.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("Unknown action", unknownJson.RootElement.GetProperty("error").GetString());
            Assert.False(unknownJson.RootElement.TryGetProperty("url", out _));

            var lookup = await client.GetAsync(StorefrontPhpAjax.SkuMediaPublicPath + "?action=lookup&brand=BOSCH&article=0986");
            var lookupJson = await JsonDocument.ParseAsync(await lookup.Content.ReadAsStreamAsync());
            Assert.True(lookupJson.RootElement.GetProperty("ok").GetBoolean());
            Assert.False(lookupJson.RootElement.TryGetProperty("source", out _));
            Assert.Equal("/content/files/images/sku_media/pad%20photo.jpg", lookupJson.RootElement.GetProperty("url").GetString());
            Assert.Equal("Front pad", lookupJson.RootElement.GetProperty("profile").GetProperty("title").GetString());
            Assert.Equal("12 mm", lookupJson.RootElement.GetProperty("specs")[0].GetProperty("rows")[0].GetProperty("value").GetString());

            var add = await client.PostAsync(
                StorefrontPhpAjax.AddToBasketPath,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["product_objects"] = Product("2222", "12.50", "10.00") }));
            var addJson = await JsonDocument.ParseAsync(await add.Content.ReadAsStreamAsync());
            Assert.Equal("auth", addJson.RootElement.GetProperty("code").GetString());
            Assert.Equal(StorefrontPhpAjax.GuestAuthMessage, addJson.RootElement.GetProperty("message").GetString());
            Assert.Equal(StorefrontPhpAjax.GuestLoginUrl, addJson.RootElement.GetProperty("login_url").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE t2_article='2222'"));

            var csrf = await client.PostAsync(
                StorefrontPhpAjax.ChangeCountPath,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["request_object"] = Count(cartId, 4) }));
            Assert.Equal("Error! CSRF 1", (await JsonDocument.ParseAsync(await csrf.Content.ReadAsStreamAsync())).RootElement.GetProperty("message").GetString());

            var guestId = await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE t2_name='GUEST'");
            using var change = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.ChangeCountPath);
            change.Headers.TryAddWithoutValidation("Cookie", "session=guest-token");
            change.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["csrf_guard_key"] = "guest-key",
                ["request_object"] = Count(guestId, 4)
            });
            var changeJson = await JsonDocument.ParseAsync(await (await client.SendAsync(change)).Content.ReadAsStreamAsync());
            Assert.Equal("auth", changeJson.RootElement.GetProperty("code").GetString());
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT count_need FROM shop_carts WHERE id=" + guestId));

            var deleteCsrf = await client.PostAsync(
                StorefrontPhpAjax.DeleteCartPath,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["request_object"] = Delete(alienId) }));
            Assert.Equal("Error! CSRF 1", (await JsonDocument.ParseAsync(await deleteCsrf.Content.ReadAsStreamAsync())).RootElement.GetProperty("message").GetString());

            using var alienRequest = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.DeleteCartPath);
            alienRequest.Headers.TryAddWithoutValidation("Cookie", "session=guest-token");
            alienRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["csrf_guard_key"] = "guest-key",
                ["request_object"] = Delete(alienId)
            });
            var alienJson = await JsonDocument.ParseAsync(await (await client.SendAsync(alienRequest)).Content.ReadAsStreamAsync());
            Assert.Equal("alien_cart", alienJson.RootElement.GetProperty("code").GetString());
            Assert.Equal("Alien cart", alienJson.RootElement.GetProperty("message").GetString());
            Assert.Equal(alienId, await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE user_id=9"));

            var dropId = await ScalarAsync(connectionString, "SELECT id FROM shop_carts WHERE t2_name='DROP'");
            await ExecuteAsync(connectionString, "INSERT INTO shop_carts_details (cart_record_id, storage_record_id, count_reserved) VALUES (" + dropId + ", 1, 1)");
            using var dropRequest = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.DeleteCartPath);
            dropRequest.Headers.TryAddWithoutValidation("Cookie", "session=guest-token");
            dropRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["csrf_guard_key"] = "guest-key",
                ["request_object"] = Delete(dropId)
            });
            var dropJson = await JsonDocument.ParseAsync(await (await client.SendAsync(dropRequest)).Content.ReadAsStreamAsync());
            Assert.True(dropJson.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("ok", dropJson.RootElement.GetProperty("code").GetString());
            Assert.Equal("Ok", dropJson.RootElement.GetProperty("message").GetString());
            Assert.Equal(dropId, dropJson.RootElement.GetProperty("records_to_del")[0].GetInt32().ToString(CultureInfo.InvariantCulture));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts WHERE id=" + dropId));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts_details WHERE cart_record_id=" + dropId));
            Assert.Equal("6", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id=1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id=1"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static string Product(string article, string price, string purchase, string hash = "", int type = 2)
        => "[{\"product_type\":" + type.ToString(CultureInfo.InvariantCulture)
            + ",\"manufacturer\":\"BOSCH\",\"article\":\"" + article + "\",\"article_show\":\"" + article
            + "\",\"name\":\"Pad\",\"exist\":10,\"time_to_exe\":\"1\",\"time_to_exe_guaranteed\":\"2\",\"storage\":\"S\",\"min_order\":2,\"probability\":100,\"price\":"
            + price + ",\"price_purchase\":" + purchase + ",\"markup\":0,\"office_id\":1,\"storage_id\":8,\"count_need\":2,\"json_params\":\"\""
            + (hash.Length == 0 ? string.Empty : ",\"check_hash\":\"" + hash + "\"") + "}]";

    private static string Count(string id, int count)
        => "{\"id\":" + id + ",\"count_need\":" + count.ToString(CultureInfo.InvariantCulture) + "}";

    private static string Delete(string id) => "{\"records_to_del\":[" + id + "]}";

    private static string CartSeed(int userId, int sessionId, int productType, string name)
        => "INSERT INTO shop_carts (product_type, price, count_need, time, user_id, session_id, t2_manufacturer, t2_article, t2_article_show, t2_name, t2_exist, t2_time_to_exe, t2_time_to_exe_guaranteed, t2_storage, t2_min_order, t2_probability, t2_markup, t2_price_purchase, t2_office_id, t2_storage_id, t2_product_json, t2_json_params) VALUES ("
            + productType.ToString(CultureInfo.InvariantCulture) + ", 1.00, 2, 1, "
            + userId.ToString(CultureInfo.InvariantCulture) + ", " + sessionId.ToString(CultureInfo.InvariantCulture)
            + ", 'BOSCH', '1', '1', '" + name + "', 10, '1', '2', 'S', 2, 100, '0', 1.00, 1, 8, '{}', '')";

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
