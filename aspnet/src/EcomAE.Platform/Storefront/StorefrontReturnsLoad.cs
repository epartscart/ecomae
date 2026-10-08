using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    /// <summary>One posted <c>images[item_id][n]</c> photo of a return line.</summary>
    public sealed record ReturnImage(int ItemId, string ContentType, long Size, Func<CancellationToken, Task<byte[]>> ReadAsync);

    public const long ReturnImageMaxBytes = 5242880;
    public const long ReturnImagesMaxBytes = 15728640;

    private static readonly string[] ReturnImageTypes = ["image/png", "image/jpeg", "image/jpg", "image/bmp"];

    private static readonly int[] ReturnStrings = [4571, 4572, 4573, 4574, 4575, 4576, 4577, 4578, 5636, 5637, 5638, 5639, 5686, 5687, 5688, 5689, 5690];

    private sealed class ReturnStop(string message) : Exception(message);

    private sealed record ReturnSplit(int ItemId, int NewItemId, int OrderId, int CountNeed, int Count, int ProductType);

    /// <summary>
    /// PHP content/shop/returns/ajax/ajax_load_returns_data.php with content/shop/returns/ajax/helper.php: the return header and lines in one
    /// transaction (a partial count clones the order line first), the photos, <c>return_new_manager</c> then
    /// <c>return_new_customer</c>, the lines moved to the <c>for_return</c> status, and the split lines reduced
    /// with their history rows.
    /// </summary>
    public static async Task<object> LoadReturnsFullAsync(
        DbConnection connection,
        string expectedTechKey,
        string postedTechKey,
        IReadOnlyList<ReturnLine> items,
        string userId,
        string totalSum,
        string officeId,
        IReadOnlyList<ReturnImage> images,
        string uploadDirectory,
        IStorefrontNotifyDispatcher? notify,
        CancellationToken cancellationToken)
    {
        var translator = new StorefrontPhpTranslator(connection);
        var text = new Dictionary<int, string>();
        foreach (var key in ReturnStrings)
        {
            text[key] = await translator.RawAsync(key.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false)
                ?? key.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.Equals(postedTechKey ?? string.Empty, expectedTechKey ?? string.Empty, StringComparison.Ordinal))
        {
            return ReturnError(ReturnsForbidden);
        }

        if (items.Count == 0)
        {
            return OrderItemsMissing;
        }

        var ids = string.Join(",", items.Select(item => item.ItemId.ToString(CultureInfo.InvariantCulture)));
        try
        {
            await ErpDb.ExecuteAsync(connection, null, "DROP TEMPORARY TABLE IF EXISTS `tmp`", cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                "CREATE TEMPORARY TABLE `tmp` SELECT * FROM `shop_orders_items` WHERE `id` IN (" + ids + ")",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return OrderItemsMissing;
        }

        var columns = new List<string>();
        await using (var show = connection.CreateCommand())
        {
            show.CommandText = "SHOW COLUMNS FROM `shop_orders_items`";
            await using var reader = await show.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty;
                if (!string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
                {
                    columns.Add("`" + name.Replace("`", "``", StringComparison.Ordinal) + "`");
                }
            }
        }

        var savedItems = new List<int>();
        var splits = new List<ReturnSplit>();
        var written = new List<string>();
        long returnId;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var item in items)
            {
                long count;
                try
                {
                    count = await ErpDb.LongAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders_returns_items` WHERE `item_id` = ?"),
                        cancellationToken,
                        item.ItemId).ConfigureAwait(false);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ReturnsMissing;
                }

                if (count > 0)
                {
                    throw new ReturnStop(text[4571]);
                }
            }

            int statusId;
            try
            {
                statusId = await ReturnStatusIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ReturnStatusesMissing;
            }

            if (statusId < 1)
            {
                throw new ReturnStop(text[4572] + ".");
            }

            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `shop_orders_returns` (`status_id`, `user_id`, `sum`) VALUES (?, ?, ?)"),
                    cancellationToken,
                    statusId,
                    userId,
                    totalSum).ConfigureAwait(false);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ReturnsHeaderMissing;
            }
            catch (DbException)
            {
                throw new ReturnStop(text[4573] + ".");
            }

            returnId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            foreach (var item in items)
            {
                var itemId = item.ItemId;
                var line = (await ReturnRowsAsync(connection, transaction, "SELECT `count_need`, `order_id`, `product_type` FROM `shop_orders_items` WHERE `id` = " + item.ItemId.ToString(CultureInfo.InvariantCulture), cancellationToken)
                    .ConfigureAwait(false)).FirstOrDefault();
                var countNeed = line is null ? 0 : ItemCount(line[0]);
                var wanted = ItemCount(item.Count);
                if (line is not null && wanted < countNeed && columns.Count > 0)
                {
                    var list = string.Join(", ", columns);
                    await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("UPDATE `tmp` SET `count_need` = ? WHERE `id` = ?"), cancellationToken, wanted, item.ItemId)
                        .ConfigureAwait(false);
                    if (await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("INSERT INTO `shop_orders_items` (" + list + ") SELECT " + list + " FROM `tmp` WHERE `id` = ?"),
                        cancellationToken,
                        item.ItemId).ConfigureAwait(false) > 0)
                    {
                        itemId = (int)await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                        splits.Add(new ReturnSplit(item.ItemId, itemId, ItemCount(line[1]), countNeed, wanted, ItemCount(line[2])));
                    }
                }

                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("INSERT INTO `shop_orders_returns_items` (`comment`, `reason_id`, `return_id`, `item_id`, `count_need`) VALUES (?, ?, ?, ?, ?)"),
                        cancellationToken,
                        HtmlCompat(item.Comment),
                        item.ReasonId,
                        returnId,
                        itemId,
                        item.Count).ConfigureAwait(false);
                }
                catch (DbException ex) when (!CpMissingSchema.IsMissing(ex))
                {
                    throw new ReturnStop(text[4574] + ".");
                }

                var returnItemId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                await SaveReturnImagesAsync(connection, transaction, images, item.ItemId, returnItemId, uploadDirectory, text, written, cancellationToken).ConfigureAwait(false);
                savedItems.Add(itemId);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ReturnStop stop)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            DeleteFiles(written);
            return ReturnError(stop.Message);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            DeleteFiles(written);
            return ReturnsHeaderMissing;
        }

        if (notify is not null)
        {
            await NotifyReturnAsync(connection, notify, returnId, officeId, userId, cancellationToken).ConfigureAwait(false);
        }

        await ReturnLinesStatusAsync(connection, savedItems, text, cancellationToken).ConfigureAwait(false);
        foreach (var split in splits)
        {
            await ReduceSplitLineAsync(connection, split, text, cancellationToken).ConfigureAwait(false);
        }

        return new ReturnLoadBody(true, "success", null);
    }

    /// <summary>
    /// PHP <c>uploadImages()</c>: every posted photo must be PNG, JPEG or BMP, at most 5 MB each and 15 MB together;
    /// the line's photos are stored under <c>content/files/returns_images/</c> and their path saved per return line.
    /// </summary>
    private static async Task SaveReturnImagesAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<ReturnImage> images,
        int itemId,
        long returnItemId,
        string uploadDirectory,
        IReadOnlyDictionary<int, string> text,
        List<string> written,
        CancellationToken cancellationToken)
    {
        if (images.Count == 0)
        {
            return;
        }

        if (images.Any(image => !ReturnImageTypes.Contains(image.ContentType, StringComparer.Ordinal)))
        {
            throw new ReturnStop(text[4575]);
        }

        long total = 0;
        foreach (var image in images)
        {
            if (image.Size > ReturnImageMaxBytes)
            {
                throw new ReturnStop(text[4576]);
            }

            total += image.Size;
            if (total > ReturnImagesMaxBytes)
            {
                throw new ReturnStop(text[4577]);
            }
        }

        foreach (var image in images.Where(image => image.ItemId == itemId))
        {
            Directory.CreateDirectory(uploadDirectory);
            var path = Path.Combine(uploadDirectory, Path.GetRandomFileName().Replace(".", string.Empty, StringComparison.Ordinal));
            await File.WriteAllBytesAsync(path, await image.ReadAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            written.Add(path);
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `shop_orders_returns_items_images` (`return_item_id`, `image`) VALUES (?, ?)"),
                    cancellationToken,
                    returnItemId,
                    path).ConfigureAwait(false);
            }
            catch (DbException)
            {
                throw new ReturnStop(text[4578]);
            }
        }
    }

    /// <summary>PHP <c>sendNotify()</c>: the posted office's managers (raw <c>users</c> list) first; the customer only when that notification exists.</summary>
    private static async Task NotifyReturnAsync(DbConnection connection, IStorefrontNotifyDispatcher notify, long returnId, string officeId, string userId, CancellationToken cancellationToken)
    {
        try
        {
            var managers = new List<StorefrontNotifyPerson>();
            var users = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `users` FROM `shop_offices` WHERE `id` = ?"), cancellationToken, officeId)
                .ConfigureAwait(false);
            foreach (var id in ProtocolIds(users))
            {
                managers.Add(StorefrontNotifyPerson.User((int)id));
            }

            var vars = new Dictionary<string, string>(StringComparer.Ordinal) { ["return_id"] = returnId.ToString(CultureInfo.InvariantCulture) };
            var answer = await notify.SendAsync(connection, "return_new_manager", vars, managers, cancellationToken).ConfigureAwait(false);
            if (answer.Found)
            {
                await notify.SendAsync(connection, "return_new_customer", vars, [StorefrontNotifyPerson.User(ItemCount(userId))], cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
        }
    }

    /// <summary>PHP <c>changeStatus()</c>: the returned lines take the <c>for_return</c> line status, with an order history row.</summary>
    private static async Task ReturnLinesStatusAsync(DbConnection connection, IReadOnlyList<int> itemIds, IReadOnlyDictionary<int, string> text, CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        try
        {
            var status = await ErpDb.LongAsync(connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_return` = 1 LIMIT 1", cancellationToken)
                .ConfigureAwait(false);
            if (status <= 0)
            {
                return;
            }

            var list = string.Join(",", itemIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            var orderId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ?"), cancellationToken, itemIds[0])
                .ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` IN (" + list + ")"), cancellationToken, status)
                .ConfigureAwait(false);
            await ReturnLogAsync(connection, orderId, text[5689] + " [" + string.Join(", ", itemIds) + "] " + text[5690], cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>
    /// The <c>change_count_arr</c> tail of ajax_load_returns_data.php: the original line keeps the rest of the count,
    /// a catalogue line's reservation is copied to the new line and reduced on the original, with two history rows.
    /// </summary>
    private static async Task ReduceSplitLineAsync(DbConnection connection, ReturnSplit split, IReadOnlyDictionary<int, string> text, CancellationToken cancellationToken)
    {
        var remaining = split.CountNeed - split.Count;
        try
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_orders_items` SET `count_need` = ? WHERE `id` = ?"), cancellationToken, remaining, split.ItemId)
                .ConfigureAwait(false);
            if (split.ProductType == 1)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    "INSERT INTO `shop_orders_items_details` (`id`, `order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`) "
                    + "SELECT NULL, `order_id`, " + split.NewItemId.ToString(CultureInfo.InvariantCulture) + ", `office_id`, `storage_id`, `storage_record_id`, "
                    + split.Count.ToString(CultureInfo.InvariantCulture) + ", `count_issued`, `count_canceled`, `price_purchase` FROM `shop_orders_items_details` WHERE `order_item_id` = "
                    + split.ItemId.ToString(CultureInfo.InvariantCulture),
                    cancellationToken).ConfigureAwait(false);
            }

            await ReturnLogAsync(
                connection,
                split.OrderId,
                text[5686] + " ID " + split.NewItemId.ToString(CultureInfo.InvariantCulture) + " " + text[5687] + " ID " + split.ItemId.ToString(CultureInfo.InvariantCulture)
                    + " " + text[5688] + " " + split.Count.ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
            if (split.ProductType == 1)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `shop_orders_items_details` SET `count_reserved` = ? WHERE `order_item_id` = ?"),
                    cancellationToken,
                    remaining,
                    split.ItemId).ConfigureAwait(false);
            }

            await ReturnLogAsync(
                connection,
                split.OrderId,
                text[5636] + " ID " + split.NewItemId.ToString(CultureInfo.InvariantCulture) + " " + text[5637] + " ID " + split.ItemId.ToString(CultureInfo.InvariantCulture)
                    + ". " + text[5638] + " " + split.CountNeed.ToString(CultureInfo.InvariantCulture) + " " + text[5639] + " " + remaining.ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static Task ReturnLogAsync(DbConnection connection, long orderId, string text, CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`is_robot`,`text`) VALUES (?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            orderId,
            UnixNow(),
            0,
            0,
            1,
            text);

    private static async Task<List<string[]>> ReturnRowsAsync(DbConnection connection, DbTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        var rows = new List<string[]>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[i] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static void DeleteFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
