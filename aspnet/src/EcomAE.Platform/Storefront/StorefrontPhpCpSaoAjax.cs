using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string SaoActionsMissing = "SAO actions are not in this database.";
    public const string SaoAlreadyDone = "Данное действие уже выполненно другим менеджером.";
    public const string SaoScriptNotExecuted = "Supplier action script was not executed.";

    public sealed record SaoExecBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("order_item_id")] string OrderItemId,
        [property: JsonPropertyName("sao_action_message")] string SaoActionMessage);

    public static Task<object> SaoExecAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? orderItemId,
        string? actionId,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            (_, token) => SaoExecBodyAsync(connection, orderItemId, actionId, token),
            cancellationToken);

    private static async Task<object> SaoExecBodyAsync(
        DbConnection connection,
        string? orderItemId,
        string? actionId,
        CancellationToken cancellationToken)
    {
        var item = orderItemId ?? string.Empty;
        var requested = ParseId(actionId);
        try
        {
            var allowed = false;
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("""
                SELECT `action_id`
                FROM `shop_sao_states_types_actions_link`
                WHERE `state_type_id` = (
                  SELECT `id` FROM `shop_sao_states_types_link`
                  WHERE `interface_type_id` = (
                    SELECT `interface_type` FROM `shop_storages`
                    WHERE `id` = (SELECT `t2_storage_id` FROM `shop_orders_items` WHERE `id` = ?)
                  )
                  AND `state_id` = (SELECT `sao_state` FROM `shop_orders_items` WHERE `id` = ?)
                )
                """);
            ErpDb.AddParameters(command, ParseId(item), ParseId(item));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                if (id == requested && requested > 0)
                {
                    allowed = true;
                }
            }

            if (!allowed)
            {
                return new SaoExecBody(false, item, SaoAlreadyDone);
            }

            return new SaoExecBody(false, item, SaoScriptNotExecuted);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, SaoActionsMissing);
        }
    }
}
