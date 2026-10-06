using EcomAE.Platform.Cp;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public static async Task<object> NotificationTestAsync(
        System.Data.Common.DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        IReadOnlyDictionary<string, string> fields,
        ICpCommunicationsTestService tests,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Forbidden"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            (_, token) => NotificationBodyAsync(fields, tests, token),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> NotificationBodyAsync(
        IReadOnlyDictionary<string, string> fields,
        ICpCommunicationsTestService tests,
        CancellationToken cancellationToken)
    {
        if (!fields.ContainsKey("contact") || !fields.ContainsKey("type"))
        {
            return new FlagBody(false, "No params");
        }

        var type = OmsField(fields, "type");
        if (type != "email" && type != "phone")
        {
            return new FlagBody(false, "Incorrect type");
        }

        var written = await tests.SendTestAsync(type, OmsField(fields, "contact"), cancellationToken).ConfigureAwait(false);
        return new FlagBody(written.Succeeded, written.Message);
    }
}
