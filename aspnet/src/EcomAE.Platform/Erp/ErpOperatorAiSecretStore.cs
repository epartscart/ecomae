using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace EcomAE.Platform.Erp;

public sealed record ErpOperatorAiSecret(bool Found, bool Readable, string Token, string Extras);

public interface IErpOperatorAiSecretStore
{
    Task<IReadOnlyList<string>> ListAsync(int userId, CancellationToken cancellationToken);

    Task<ErpOperatorAiSecret> ReadAsync(int userId, string provider, CancellationToken cancellationToken);

    Task<bool> SaveAsync(int userId, string provider, string token, string extras, CancellationToken cancellationToken);

    Task RemoveAsync(int userId, string provider, CancellationToken cancellationToken);
}

public sealed class ErpOperatorAiProtector
{
    public const string Purpose = "EcomAE.Erp.OperatorAiTools.v1";

    private readonly IDataProtector _protector;

    public ErpOperatorAiProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string token)
        => _protector.Protect(token);

    public string? Unprotect(string payload)
    {
        try
        {
            return _protector.Unprotect(payload);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

public sealed class ErpOperatorAiSecretStore : IErpOperatorAiSecretStore
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly ErpOperatorAiProtector _protector;

    public ErpOperatorAiSecretStore(IErpWriteConnectionFactory connections, ErpOperatorAiProtector protector)
    {
        _connections = connections;
        _protector = protector;
    }

    public async Task<IReadOnlyList<string>> ListAsync(int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0 || !_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureOperatorAiToolsAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `provider` FROM `epc_operator_ai_tools` WHERE `user_id` = ?");
            ErpDb.AddParameters(command, userId);
            var found = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    found.Add(reader.GetString(0));
                }
            }

            return found;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<ErpOperatorAiSecret> ReadAsync(int userId, string provider, CancellationToken cancellationToken)
    {
        if (userId <= 0 || !_connections.IsConfigured || !Known(provider))
        {
            return new ErpOperatorAiSecret(false, false, string.Empty, string.Empty);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureOperatorAiToolsAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `token_protected`, `extras` FROM `epc_operator_ai_tools` WHERE `user_id` = ? AND `provider` = ? LIMIT 1");
            ErpDb.AddParameters(command, userId, provider);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new ErpOperatorAiSecret(false, false, string.Empty, string.Empty);
            }

            var extras = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var plain = reader.IsDBNull(0) ? null : _protector.Unprotect(reader.GetString(0));
            return plain is null
                ? new ErpOperatorAiSecret(true, false, string.Empty, extras)
                : new ErpOperatorAiSecret(true, true, plain, extras);
        }
        catch (DbException)
        {
            return new ErpOperatorAiSecret(false, false, string.Empty, string.Empty);
        }
    }

    public async Task<bool> SaveAsync(int userId, string provider, string token, string extras, CancellationToken cancellationToken)
    {
        if (userId <= 0 || !_connections.IsConfigured || !Known(provider) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var protectedToken = _protector.Protect(token);
            var storedExtras = extras.Length > 800 ? extras[..800] : extras;
            var updated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureOperatorAiToolsAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_operator_ai_tools` (`user_id`, `provider`, `token_protected`, `extras`, `updated_at`) "
                    + "VALUES (?, ?, ?, ?, ?) "
                    + "ON DUPLICATE KEY UPDATE `token_protected` = VALUES(`token_protected`), `extras` = VALUES(`extras`), `updated_at` = VALUES(`updated_at`)"),
                cancellationToken,
                userId,
                provider,
                protectedToken,
                storedExtras,
                updated).ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    public async Task RemoveAsync(int userId, string provider, CancellationToken cancellationToken)
    {
        if (userId <= 0 || !_connections.IsConfigured || !Known(provider))
        {
            return;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureOperatorAiToolsAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_operator_ai_tools` WHERE `user_id` = ? AND `provider` = ?"),
                cancellationToken,
                userId,
                provider).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // A missing table leaves the tool disconnected, which is the state remove wants.
        }
    }

    private static bool Known(string provider)
        => provider is "chatgpt" or "cursor" or "devin";
}
