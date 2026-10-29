using System.Data;
using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Raised for ERP write validation failures. Message text mirrors the PHP exception text.</summary>
public sealed class ErpWriteException : Exception
{
    public ErpWriteException(string message) : base(message)
    {
    }
}

/// <summary>Thin ADO helpers for the ERP write services (parameterised, PHP-PDO shaped).</summary>
internal static class ErpDb
{
    public static async Task<int> ExecuteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        await using var command = CreateCommand(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<object?> ScalarAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        await using var command = CreateCommand(connection, transaction, sql, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    public static async Task<string?> StringAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        var value = await ScalarAsync(connection, transaction, sql, cancellationToken, parameters).ConfigureAwait(false);
        return value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// True when the column is already known, or information_schema says it is there.
    /// A present column is remembered for this process so later pages skip the scan.
    /// A missing column is not remembered — the next ensure can still add it.
    /// </summary>
    public static async Task<bool> ColumnKnownAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        var key = ErpSchemaMemo.Key(connection, "col:" + table + "." + column);
        if (ErpSchemaMemo.IsReady(key))
        {
            return true;
        }

        var count = await LongAsync(
            connection,
            transaction,
            Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        if (count > 0)
        {
            ErpSchemaMemo.Remember(key);
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when <c>SHOW TABLES</c> for this database already listed the table.
    /// The list is reused for a short time so later pages skip information_schema.
    /// </summary>
    public static Task<bool> TableKnownAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
        => ErpTableCatalog.ContainsAsync(connection, table, cancellationToken);

    /// <summary>True when the failure is MySQL 1146 / SQLSTATE 42S02 (table doesn't exist).</summary>
    public static bool IsMissingTable(DbException exception)
        => exception.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
           || exception.Message.Contains("42S02", StringComparison.Ordinal);

    public static async Task<long> LongAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        var value = await ScalarAsync(connection, transaction, sql, cancellationToken, parameters).ConfigureAwait(false);
        return value is null ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    public static async Task<decimal> DecimalAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        var value = await ScalarAsync(connection, transaction, sql, cancellationToken, parameters).ConfigureAwait(false);
        return value is null ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    public static async Task<long> LastInsertIdAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
        => await LongAsync(connection, transaction, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);

    /// <summary>Runs DDL / statements whose failure must not break the caller (PHP ensure-schema semantics).</summary>
    public static async Task TryExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var createKey = string.Empty;
        if (IsRepeatableCreate(sql))
        {
            createKey = ErpSchemaMemo.Key(connection, sql);
            if (ErpSchemaMemo.IsReady(createKey))
            {
                return;
            }
        }

        try
        {
            await ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
            if (createKey.Length > 0)
            {
                ErpSchemaMemo.Remember(createKey);
            }
        }
        catch (DbException)
        {
            // PHP ensure-schema helpers ignore DDL failures on locked-down grants.
        }
    }

    private static bool IsRepeatableCreate(string sql)
    {
        var start = 0;
        while (start < sql.Length && char.IsWhiteSpace(sql[start]))
        {
            start++;
        }

        return sql.AsSpan(start).StartsWith("CREATE TABLE IF NOT EXISTS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Binds positional values onto a command the caller builds itself (readers).</summary>
    public static void AddParameters(DbCommand command, params object?[] parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p" + i.ToString(CultureInfo.InvariantCulture);
            parameter.Value = parameters[i] ?? DBNull.Value;
            parameter.Direction = ParameterDirection.Input;
            command.Parameters.Add(parameter);
        }
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction? transaction, string sql, object?[] parameters)
    {
        var command = connection.CreateCommand();
        // Positional is idempotent: SQL already in @pN form has no '?' left to rewrite,
        // and raw '?' SQL gets converted so callers never have to remember the helper.
        command.CommandText = Positional(sql);
        command.Transaction = transaction;
        AddParameters(command, parameters);
        return command;
    }

    /// <summary>Rewrites PHP-style positional <c>?</c> placeholders into the named form the helpers bind.</summary>
    public static string Positional(string sql)
    {
        var builder = new System.Text.StringBuilder(sql.Length + 16);
        var index = 0;
        foreach (var ch in sql)
        {
            if (ch == '?')
            {
                builder.Append("@p").Append(index.ToString(CultureInfo.InvariantCulture));
                index++;
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}
