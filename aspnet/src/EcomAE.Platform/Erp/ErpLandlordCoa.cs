using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

public sealed record ErpLandlordAccount(long Id, string Code, string Name, string Type);

/// <summary>
/// Chart links for the landlord year book. Assets, rental income, and each expense
/// sit on the same chart the rest of the ERP uses. Year lines stay measurements:
/// a supplier bill still posts through the expense register.
/// </summary>
public static class ErpLandlordCoa
{
    public const string Asset = "1510";
    public const string Income = "4100";
    public const string ExpenseParent = "6210";
    public const string Through2024 = "6295";
    public const string OtherExpense = "6299";

    public static readonly IReadOnlyList<(string Category, string Code, string Name)> Expenses =
    [
        ("bank_charges", "6211", "Bank charges"),
        ("camp_head_lease", "6212", "Camp head lease"),
        ("vat", "6213", "VAT on property costs"),
        ("dcd_24x7", "6214", "DCD 24x7"),
        ("dewa", "6215", "DEWA"),
        ("ejari", "6216", "Ejari"),
        ("eid_allowance", "6217", "Eid allowance"),
        ("fire_alarm", "6218", "Fire alarm"),
        ("pest_control", "6219", "Pest control"),
        ("repairs", "6221", "Repairs"),
        ("security", "6222", "Security"),
        ("salary", "6223", "Salary"),
        ("garbage", "6224", "Garbage"),
        ("water_tank", "6225", "Water tank"),
        ("legal_case", "6226", "Legal"),
        ("cleaning", "6227", "Cleaning"),
        ("security_refund", "6228", "Security refund"),
        ("sewerage", "6229", "Sewerage"),
        ("community_service", "6231", "Community service"),
        ("villa_expenses", "6232", "Villa expenses"),
        ("final_settlement", "6233", "Final settlement"),
        ("title_deed_noc", "6234", "Title deed and NOC"),
        ("villa_68_69", "6235", "Villas 68 and 69"),
        ("villa_16_19", "6236", "Villas 16 and 19"),
        ("site_plan", "6237", "Site plan"),
    ];

    public static string CodeFor(string? kind, string? category)
    {
        var normalizedKind = (kind ?? "").Trim().ToLowerInvariant();
        if (normalizedKind == "income")
        {
            return Income;
        }

        if (normalizedKind == "investment")
        {
            return Asset;
        }

        if (normalizedKind == "through_2024")
        {
            return Through2024;
        }

        if (normalizedKind != "expense")
        {
            return "";
        }

        var key = Normalize(category);
        foreach (var row in Expenses)
        {
            if (row.Category == key)
            {
                return row.Code;
            }
        }

        return OtherExpense;
    }

    public static string Label(string? category)
    {
        var key = Normalize(category);
        foreach (var row in Expenses)
        {
            if (row.Category == key)
            {
                return row.Name;
            }
        }

        if (key is "" or "other")
        {
            return "Other expense";
        }

        if (key == "rental")
        {
            return "Rental income";
        }

        if (key == "recovered")
        {
            return "Recovered";
        }

        return key.Replace('_', ' ');
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var builder = new StringBuilder(value.Length);
        var underscore = false;
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                underscore = false;
            }
            else if (!underscore && builder.Length > 0)
            {
                builder.Append('_');
                underscore = true;
            }
        }

        if (builder.Length > 0 && builder[^1] == '_')
        {
            builder.Length--;
        }

        if (builder.Length <= 40)
        {
            return builder.ToString();
        }

        var clipped = builder.ToString(0, 40);
        return clipped.TrimEnd('_');
    }

    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpGlChartOfAccountsSeeder.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpRealEstateGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in Expenses)
        {
            await InsertAsync(connection, row.Code, row.Name, "expense", "debit", row.Name + " on a property. Measured on the year book.", now, cancellationToken).ConfigureAwait(false);
        }

        await InsertAsync(connection, Through2024, "Property costs through 2024", "expense", "debit", "Combined property costs from acquisition through 2024. Kept outside the later year return.", now, cancellationToken).ConfigureAwait(false);
        await InsertAsync(connection, OtherExpense, "Other property expense", "expense", "debit", "A property expense that does not have its own account yet.", now, cancellationToken).ConfigureAwait(false);

        var codes = string.Join(",", Expenses.Select(row => "'" + row.Code + "'").Append("'" + Through2024 + "'").Append("'" + OtherExpense + "'"));
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                "UPDATE `epc_erp_coa_accounts` child JOIN `epc_erp_coa_accounts` parent ON parent.`code`='" + ExpenseParent + "' SET child.`parent_id`=parent.`id` WHERE child.`parent_id`=0 AND child.`code` IN (" + codes + ")",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        await StampLinesAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<ErpLandlordAccount>> AccountsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal)
        {
            Asset, Income, ExpenseParent, Through2024, OtherExpense,
        };
        foreach (var row in Expenses)
        {
            wanted.Add(row.Code);
        }

        var rows = new List<ErpLandlordAccount>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id`,`code`,`name`,`account_type` FROM `epc_erp_coa_accounts` WHERE `active`=1 ORDER BY `code`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "";
                if (!wanted.Contains(code))
                {
                    continue;
                }

                rows.Add(new ErpLandlordAccount(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    code,
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? ""));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task StampLinesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append("UPDATE `epc_erp_re_year_lines` SET `coa_code`=CASE ");
        sql.Append("WHEN `kind`='income' THEN '").Append(Income).Append("' ");
        sql.Append("WHEN `kind`='investment' THEN '").Append(Asset).Append("' ");
        sql.Append("WHEN `kind`='through_2024' THEN '").Append(Through2024).Append("' ");
        foreach (var row in Expenses)
        {
            sql.Append("WHEN `kind`='expense' AND `category`='").Append(row.Category).Append("' THEN '").Append(row.Code).Append("' ");
        }

        sql.Append("WHEN `kind`='expense' THEN '").Append(OtherExpense).Append("' ELSE `coa_code` END ");
        sql.Append("WHERE `coa_code`=''");
        try
        {
            await ErpDb.ExecuteAsync(connection, null, sql.ToString(), cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static Task InsertAsync(
        DbConnection connection,
        string code,
        string name,
        string type,
        string side,
        string description,
        long now,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT IGNORE INTO `epc_erp_coa_accounts` (`code`,`name`,`account_type`,`normal_side`,`description`,`system_flag`,`time_created`) VALUES (?,?,?,?,?,1,?)"),
            cancellationToken,
            code,
            name,
            type,
            side,
            description,
            now);
}
