using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>SAO state from PHP <c>shop_sao_states</c> with its mapped order-item status.</summary>
public sealed record CpSaoState(
    long Id,
    string Name,
    long StatusId,
    string ColorBackground,
    string ColorText);

public sealed record CpSaoItemStatus(long Id, string Name, string Color);

/// <summary>Order item queued for the PHP SAO robot (<c>shop_orders_items.sao_robot &gt; 0</c>).</summary>
public sealed record CpSaoRobotItem(
    long Id,
    long OrderId,
    string Article,
    string Manufacturer,
    string Name,
    long StateId,
    string StateName,
    long ActionId,
    string ActionName,
    string Message);

public sealed record CpSaoAction(long Id, string Name, string ButtonClass, string FontAwesome);

public sealed record CpSaoDesk(
    bool Available,
    string Message,
    IReadOnlyList<CpSaoState> States,
    IReadOnlyList<CpSaoItemStatus> ItemStatuses,
    IReadOnlyList<CpSaoRobotItem> RobotQueue,
    IReadOnlyList<CpSaoAction> Actions)
{
    public static CpSaoDesk Unavailable(string message)
        => new(false, message, [], [], [], []);
}

/// <summary>
/// Typed twin of the PHP SAO surface: the <c>states_statuses_link</c> editor grid plus the robot queue that
/// <c>sao/robot.php</c> drains through <c>ajax_exec_action.php</c>.
/// </summary>
public interface ICpSaoDeskService
{
    Task<CpSaoDesk> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class CpSaoDeskService : ICpSaoDeskService
{
    private const int RobotQueueLimit = 100;

    private readonly IErpWriteConnectionFactory _connections;

    public CpSaoDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpSaoDesk> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpSaoDesk.Unavailable("No database configured — SAO state mapping is read-only.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var states = new List<CpSaoState>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`status_id`,0), IFNULL(`color_background`,''), IFNULL(`color_text`,'') "
                                  + "FROM `shop_sao_states` ORDER BY `id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    states.Add(new CpSaoState(
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        r.GetString(1),
                        Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture),
                        r.GetString(3),
                        r.GetString(4)));
                }
            }

            var statuses = new List<CpSaoItemStatus>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`color`,'') FROM `shop_orders_items_statuses_ref` ORDER BY `order`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    statuses.Add(new CpSaoItemStatus(
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        r.GetString(1),
                        r.GetString(2)));
                }
            }

            var queue = await LoadRobotQueueAsync(connection, cancellationToken).ConfigureAwait(false);
            var actions = await LoadActionsAsync(connection, cancellationToken).ConfigureAwait(false);

            return new CpSaoDesk(true, "", states, statuses, queue, actions);
        }
        catch (DbException ex)
        {
            return CpSaoDesk.Unavailable("SAO states could not be loaded: " + ex.Message);
        }
    }

    private static async Task<IReadOnlyList<CpSaoRobotItem>> LoadRobotQueueAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var queue = new List<CpSaoRobotItem>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `shop_orders_items`.`id`, IFNULL(`shop_orders_items`.`order_id`,0), "
                              + "IFNULL(`shop_orders_items`.`article`,''), IFNULL(`shop_orders_items`.`manufacturer`,''), "
                              + "IFNULL(`shop_orders_items`.`name`,''), IFNULL(`shop_orders_items`.`sao_state`,0), "
                              + "IFNULL(`shop_sao_states`.`name`,''), IFNULL(`shop_orders_items`.`sao_robot`,0), "
                              + "IFNULL(`shop_sao_actions`.`name`,''), IFNULL(`shop_orders_items`.`sao_message`,'') "
                              + "FROM `shop_orders_items` "
                              + "LEFT JOIN `shop_sao_states` ON `shop_sao_states`.`id` = `shop_orders_items`.`sao_state` "
                              + "LEFT JOIN `shop_sao_actions` ON `shop_sao_actions`.`id` = `shop_orders_items`.`sao_robot` "
                              + "WHERE `shop_orders_items`.`sao_robot` > 0 ORDER BY `shop_orders_items`.`id` DESC LIMIT "
                              + RobotQueueLimit.ToString(CultureInfo.InvariantCulture);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                queue.Add(new CpSaoRobotItem(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                    r.GetString(2),
                    r.GetString(3),
                    r.GetString(4),
                    Convert.ToInt64(r.GetValue(5), CultureInfo.InvariantCulture),
                    r.GetString(6),
                    Convert.ToInt64(r.GetValue(7), CultureInfo.InvariantCulture),
                    r.GetString(8),
                    r.GetString(9)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return queue;
    }

    private static async Task<IReadOnlyList<CpSaoAction>> LoadActionsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var actions = new List<CpSaoAction>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`btn_class`,''), IFNULL(`fontawesome`,'') FROM `shop_sao_actions` ORDER BY `id`";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                actions.Add(new CpSaoAction(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.GetString(1),
                    r.GetString(2),
                    r.GetString(3)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return actions;
    }
}
