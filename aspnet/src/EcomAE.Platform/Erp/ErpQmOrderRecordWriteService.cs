using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_qm_order_record</c> twin; quality schema creation remains PHP-owned.</summary>
public interface IErpQmOrderRecordWriteService
{
    Task<ErpSimpleWriteResult> RecordAsync(long orderId, IReadOnlyDictionary<long, (decimal? Number, string Text)> values, CancellationToken cancellationToken = default);
}

public sealed class ErpQmOrderRecordWriteService : IErpQmOrderRecordWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpQmOrderRecordWriteService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpSimpleWriteResult> RecordAsync(long orderId, IReadOnlyDictionary<long, (decimal? Number, string Text)> values, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0) return ErpSimpleWriteResult.Fail("invalid", "A quality order id is required.");
        if (!_connections.IsConfigured) return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        long planId;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `plan_id` FROM `epc_qm_order` WHERE `id`=?";
            var parameter = command.CreateParameter();
            parameter.Value = orderId;
            command.Parameters.Add(parameter);
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (scalar is null || scalar is DBNull) return ErpSimpleWriteResult.Fail("invalid", "Quality order not found.");
            planId = Convert.ToInt64(scalar);
        }

        var tests = new List<(long Id, string Name, string Type, decimal? Min, decimal? Max, string Expected)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`,`name`,`test_type`,`min_val`,`max_val`,`expected` FROM `epc_qm_test` WHERE `plan_id`=? ORDER BY `sort`,`id`";
            var parameter = command.CreateParameter();
            parameter.Value = planId;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                tests.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetDecimal(4), reader.IsDBNull(5) ? string.Empty : reader.GetString(5)));
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_qm_result` WHERE `order_id`=?"), cancellationToken, orderId).ConfigureAwait(false);
        var verdict = tests.Count == 0 ? string.Empty : "pass";
        foreach (var test in tests)
        {
            values.TryGetValue(test.Id, out var value);
            var result = test.Type == "qualitative"
                ? string.Equals(test.Expected.Trim(), value.Text.Trim(), StringComparison.OrdinalIgnoreCase) && test.Expected.Trim().Length > 0 ? "pass" : "fail"
                : value.Number.HasValue && (!test.Min.HasValue || value.Number.Value >= test.Min.Value - 0.000000001m) && (!test.Max.HasValue || value.Number.Value <= test.Max.Value + 0.000000001m) ? "pass" : "fail";
            if (result != "pass") verdict = "fail";
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_qm_result` (`order_id`,`test_id`,`test_name`,`value_num`,`value_text`,`result`,`time_created`) VALUES (?,?,?,?,?,?,?)"), cancellationToken, orderId, test.Id, test.Name, value.Number, value.Text, result, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        }

        var writes = await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_qm_order` SET `status`='completed', `verdict`=? WHERE `id`=?"), cancellationToken, verdict, orderId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Quality results recorded", orderId) with { Writes = writes };
    }
}
