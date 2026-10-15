using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public static class ErpLandlordSql
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_re_year_lines` (
              `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
              `code` VARCHAR(40) NOT NULL,
              `property_id` BIGINT NOT NULL,
              `unit_id` BIGINT NOT NULL DEFAULT 0,
              `line_year` SMALLINT NOT NULL,
              `kind` VARCHAR(20) NOT NULL,
              `category` VARCHAR(40) NOT NULL DEFAULT '',
              `amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
              `spread` VARCHAR(20) NOT NULL DEFAULT 'building',
              `parent_id` BIGINT NOT NULL DEFAULT 0,
              `movement_date` DATE DEFAULT NULL,
              `note` VARCHAR(255) NOT NULL DEFAULT '',
              `status` VARCHAR(20) NOT NULL DEFAULT 'recorded',
              `import_id` BIGINT NOT NULL DEFAULT 0,
              `coa_code` VARCHAR(16) NOT NULL DEFAULT '',
              `reversal_of` BIGINT NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `uk_re_year_line` (`code`),
              KEY `idx_re_year` (`line_year`,`property_id`,`unit_id`,`kind`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        foreach (var alter in new[]
        {
            "ALTER TABLE `epc_erp_properties` ADD COLUMN `rent_start_year` INT NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_properties` ADD COLUMN `planned_units` INT NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_properties` ADD COLUMN `holder_name` VARCHAR(190) NOT NULL DEFAULT ''",
            "ALTER TABLE `epc_erp_re_year_lines` ADD COLUMN `coa_code` VARCHAR(16) NOT NULL DEFAULT ''",
            "ALTER TABLE `epc_erp_re_year_lines` ADD COLUMN `reversal_of` BIGINT NOT NULL DEFAULT 0",
        })
        {
            try
            {
                await ErpDb.ExecuteAsync(connection, null, alter, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
            }
        }
    }

    public static async Task ApplyOpeningAsync(DbConnection connection, DbTransaction tx, CancellationToken cancellationToken)
    {
        var portfolioId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_re_portfolios` WHERE `code`=? LIMIT 1", cancellationToken, ErpLandlordOpening.PortfolioCode).ConfigureAwait(false);
        if (portfolioId <= 0)
        {
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_portfolios` (`code`,`name`,`owner_name`,`status`,`import_id`) VALUES (?,?,?,'active',0)"),
                cancellationToken, ErpLandlordOpening.PortfolioCode, ErpLandlordOpening.PortfolioName, "Named on each property").ConfigureAwait(false);
            portfolioId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        }

        foreach (var property in ErpLandlordOpening.Properties)
        {
            var propertyId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_properties` WHERE `code`=? LIMIT 1", cancellationToken, property.Code).ConfigureAwait(false);
            if (propertyId <= 0)
            {
                var start = property.StartYear > 0
                    ? property.StartYear.ToString(CultureInfo.InvariantCulture) + "-01-01"
                    : null;
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                    "INSERT INTO `epc_erp_properties` (`company_id`,`code`,`name`,`type`,`ownership`,`city`,`country`,`status`,`created_at`,`emirate`,`community`,`notes`,`portfolio_id`,`acquired_date`,`rent_start_year`,`planned_units`,`holder_name`) VALUES (0,?,?,?,'owned',?, 'AE','active',?,?,?,?,?,?,?,?,?)"),
                    cancellationToken, property.Code, property.Name, Clip(property.Type, 40, "residential"), Clip(property.City, 80),
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Clip(property.Emirate, 40), Clip(property.City, 80), Clip(property.Note, 255),
                    portfolioId, start, property.StartYear, property.PlannedUnits, Clip(property.Holder, 190)).ConfigureAwait(false);
                propertyId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
            }

            if (property.Holder.Length > 0)
            {
                var ownerId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_re_owners` WHERE `property_id`=? AND `owner_name`=? LIMIT 1", cancellationToken, propertyId, property.Holder).ConfigureAwait(false);
                if (ownerId <= 0)
                {
                    await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                        "INSERT INTO `epc_erp_re_owners` (`property_id`,`owner_name`,`ownership_pct`,`effective_from`,`status`) VALUES (?,?,100,?, 'active')"),
                        cancellationToken, propertyId, Clip(property.Holder, 190), property.StartYear > 0 ? property.StartYear.ToString(CultureInfo.InvariantCulture) + "-01-01" : null).ConfigureAwait(false);
                }
            }
        }

        foreach (var line in ErpLandlordOpening.Lines)
        {
            var propertyId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_properties` WHERE `code`=? LIMIT 1", cancellationToken, line.PropertyCode).ConfigureAwait(false);
            if (propertyId <= 0)
            {
                continue;
            }

            await SaveAsync(connection, tx, new ErpLandlordEntry(
                propertyId, 0, ErpLandlordOpening.SheetYear, line.Kind, line.Category, line.Amount, "building", line.Note, "2025-12-31", line.Code), 0, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<ErpSimpleWriteResult> SaveAsync(
        DbConnection connection, DbTransaction tx, ErpLandlordEntry entry, long importId, CancellationToken cancellationToken)
    {
        var kind = (entry.Kind ?? "").Trim().ToLowerInvariant();
        if (kind is not ("income" or "expense" or "investment" or "recovery" or "through_2024"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose income, expense, investment, recovery, or combined costs through 2024.");
        }

        if (entry.PropertyId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the property.");
        }

        var year = kind == "through_2024" ? 2024 : entry.Year;
        if (kind != "through_2024" && (year < 1990 || year > 2100))
        {
            return ErpSimpleWriteResult.Fail("invalid", "The year is required.");
        }

        if (entry.Amount < 0 || (entry.Amount == 0 && kind is not "recovery"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "The amount must be greater than zero. Recovery can stay at zero until the amount is known.");
        }

        var spread = (entry.Spread ?? "building").Trim().ToLowerInvariant();
        if (spread is not ("building" or "flat" or "all_flats"))
        {
            spread = "building";
        }

        if (spread == "flat" && entry.UnitId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the flat for this line.");
        }

        var category = ErpLandlordCoa.Normalize(entry.Category);
        if (category.Length == 0)
        {
            category = kind == "expense" ? "other" : kind;
        }

        category = Clip(category, 40);
        var coa = ErpLandlordCoa.CodeFor(kind, category);
        var code = Clip(string.IsNullOrWhiteSpace(entry.Code) ? NewCode(year, kind, entry.PropertyId) : entry.Code, 40);
        var existing = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_re_year_lines` WHERE `code`=? LIMIT 1", cancellationToken, code).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Ok("Already on the year book.", existing);
        }

        var unitId = spread == "flat" ? entry.UnitId : 0;
        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_year_lines` (`code`,`property_id`,`unit_id`,`line_year`,`kind`,`category`,`amount`,`spread`,`parent_id`,`movement_date`,`note`,`status`,`import_id`,`coa_code`) VALUES (?,?,?,?,?,?,?,?,0,?,?,'recorded',?,?)"),
            cancellationToken, code, entry.PropertyId, unitId, year, kind, category, entry.Amount, spread,
            string.IsNullOrWhiteSpace(entry.MovementDate) ? null : entry.MovementDate, Clip(entry.Note, 255), importId, coa).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        if (kind == "expense" && spread == "all_flats")
        {
            var split = await DistributeAsync(connection, tx, id, 0, cancellationToken).ConfigureAwait(false);
            return split.Succeeded
                ? split
                : ErpSimpleWriteResult.Ok(split.Message + " The amount stays on the building.", id);
        }

        return ErpSimpleWriteResult.Ok("Recorded on " + year.ToString(CultureInfo.InvariantCulture) + ".", id);
    }

    public static async Task<ErpSimpleWriteResult> DistributeAsync(
        DbConnection connection, DbTransaction tx, long lineId, long unitId, CancellationToken cancellationToken)
    {
        var line = await ReadLineAsync(connection, tx, lineId, cancellationToken).ConfigureAwait(false);
        if (line is null || line.ParentId != 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the building expense to distribute.");
        }

        if (!string.Equals(line.Kind, "expense", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Only an expense can be distributed to flats.");
        }

        var children = await LongAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_re_year_lines` WHERE `parent_id`=? AND `status`<>'void'", cancellationToken, lineId).ConfigureAwait(false);
        if (children > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This expense is already distributed. Void it and record it again to change the split.");
        }

        if (unitId > 0)
        {
            var belongs = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_re_units` WHERE `id`=? AND `property_id`=? LIMIT 1", cancellationToken, unitId, line.PropertyId).ConfigureAwait(false);
            if (belongs <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "That flat is not on this building.");
            }

            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_year_lines` SET `spread`='flat',`unit_id`=?,`note`=? WHERE `id`=?"),
                cancellationToken, unitId, Clip("All of this expense on one flat. " + line.Note, 255), lineId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("The expense sits on the chosen flat.", lineId);
        }

        var units = new List<(long Id, string Code)>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = ErpDb.Positional("SELECT `id`,`code` FROM `epc_erp_re_units` WHERE `property_id`=? ORDER BY `code`");
            ErpDb.AddParameters(command, line.PropertyId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                units.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        if (units.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No flats are listed on this building yet. The expense stays on the building until the flat list arrives.");
        }

        var parts = ErpLandlordCalc.SplitEven(line.Amount, units.Count);
        for (var i = 0; i < units.Count; i++)
        {
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_year_lines` (`code`,`property_id`,`unit_id`,`line_year`,`kind`,`category`,`amount`,`spread`,`parent_id`,`movement_date`,`note`,`status`,`import_id`,`coa_code`) VALUES (?,?,?,?,?,?,?,'flat',?,?,?,'recorded',0,?)"),
                cancellationToken, ChildCode(line.Code, units[i].Id), line.PropertyId, units[i].Id, line.Year, line.Kind, line.Category, parts[i], lineId,
                string.IsNullOrWhiteSpace(line.MovementDate) ? null : line.MovementDate, Clip("Equal share of " + line.Code, 255), ErpLandlordCoa.CodeFor(line.Kind, line.Category)).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_year_lines` SET `spread`='all_flats' WHERE `id`=?"),
            cancellationToken, lineId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Split equally across " + units.Count.ToString(CultureInfo.InvariantCulture) + " flats.", lineId);
    }

    public static async Task<ErpSimpleWriteResult> VoidAsync(DbConnection connection, DbTransaction tx, long lineId, CancellationToken cancellationToken)
    {
        if (lineId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the line to void.");
        }

        var head = await HeadAsync(connection, tx, lineId, cancellationToken).ConfigureAwait(false);
        if (head is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That line is not on the year book.");
        }

        if (string.Equals(head.Status, "void", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "That line is already void.");
        }

        if (head.ParentId > 0)
        {
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_year_lines` SET `status`='void' WHERE `id`=?"),
                cancellationToken, lineId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("That flat share is void and stays off this flat.", lineId);
        }

        var paired = head.ReversalOf > 0 ? head.ReversalOf : lineId;
        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_year_lines` SET `status`='void' WHERE `id`=? OR `parent_id`=? OR `reversal_of`=? OR `id`=?"),
            cancellationToken, lineId, lineId, lineId, paired).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("The line is void and stays off the return.", lineId);
    }

    public static async Task<ErpSimpleWriteResult> UpdateAsync(
        DbConnection connection, DbTransaction tx, long lineId, ErpLandlordEntry entry, CancellationToken cancellationToken)
    {
        if (lineId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the line to change.");
        }

        var head = await HeadAsync(connection, tx, lineId, cancellationToken).ConfigureAwait(false);
        if (head is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That line is not on the year book.");
        }

        if (!string.Equals(head.Status, "recorded", StringComparison.OrdinalIgnoreCase) || head.ParentId != 0 || head.ReversalOf != 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Only a recorded line can be changed.");
        }

        var children = await LongAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_re_year_lines` WHERE `parent_id`=? AND `status`<>'void'", cancellationToken, lineId).ConfigureAwait(false);
        if (children > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This line is split across flats. Void or delete it, then record the shares you want.");
        }

        var kind = (entry.Kind ?? "").Trim().ToLowerInvariant();
        if (kind is not ("income" or "expense" or "investment" or "recovery" or "through_2024"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose income, expense, investment, recovery, or combined costs through 2024.");
        }

        if (entry.PropertyId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the property.");
        }

        var property = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_properties` WHERE `id`=? LIMIT 1", cancellationToken, entry.PropertyId).ConfigureAwait(false);
        if (property <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That property is not on the register.");
        }

        var year = kind == "through_2024" ? 2024 : entry.Year;
        if (kind != "through_2024" && (year < 1990 || year > 2100))
        {
            return ErpSimpleWriteResult.Fail("invalid", "The year is required.");
        }

        if (entry.Amount < 0 || (entry.Amount == 0 && kind is not "recovery"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "The amount must be greater than zero. Recovery can stay at zero until the amount is known.");
        }

        var spread = (entry.Spread ?? "building").Trim().ToLowerInvariant();
        if (spread is not ("building" or "flat" or "all_flats"))
        {
            spread = "building";
        }

        if (spread == "flat" && entry.UnitId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the flat for this line.");
        }

        var category = ErpLandlordCoa.Normalize(entry.Category);
        if (category.Length == 0)
        {
            category = kind == "expense" ? "other" : kind;
        }

        category = Clip(category, 40);
        var unitId = spread == "flat" ? entry.UnitId : 0;
        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_year_lines` SET `property_id`=?,`unit_id`=?,`line_year`=?,`kind`=?,`category`=?,`amount`=?,`spread`=?,`movement_date`=?,`note`=?,`coa_code`=? WHERE `id`=?"),
            cancellationToken, entry.PropertyId, unitId, year, kind, category, entry.Amount, spread == "all_flats" ? "building" : spread,
            string.IsNullOrWhiteSpace(entry.MovementDate) ? null : entry.MovementDate, Clip(entry.Note, 255), ErpLandlordCoa.CodeFor(kind, category), lineId).ConfigureAwait(false);
        if (spread == "all_flats")
        {
            var split = await DistributeAsync(connection, tx, lineId, 0, cancellationToken).ConfigureAwait(false);
            return split.Succeeded
                ? split
                : ErpSimpleWriteResult.Ok(split.Message + " The amount stays on the building.", lineId);
        }

        return ErpSimpleWriteResult.Ok("Saved the change on this line.", lineId);
    }

    public static async Task<ErpSimpleWriteResult> DeleteAsync(
        DbConnection connection, DbTransaction tx, long lineId, CancellationToken cancellationToken)
    {
        if (lineId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the line to delete.");
        }

        var head = await HeadAsync(connection, tx, lineId, cancellationToken).ConfigureAwait(false);
        if (head is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That line is not on the year book.");
        }

        if (head.ParentId > 0)
        {
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "DELETE FROM `epc_erp_re_year_lines` WHERE `id`=?"),
                cancellationToken, lineId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Deleted that flat share.", lineId);
        }

        if (head.ReversalOf > 0)
        {
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_year_lines` SET `status`='recorded' WHERE `id`=? AND `status`='reversed'"),
                cancellationToken, head.ReversalOf).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "DELETE FROM `epc_erp_re_year_lines` WHERE `id`=?"),
                cancellationToken, lineId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Deleted the reversal. The original line is recorded again.", lineId);
        }

        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
            "DELETE FROM `epc_erp_re_year_lines` WHERE `id`=? OR `parent_id`=? OR `reversal_of`=?"),
            cancellationToken, lineId, lineId, lineId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Deleted. The line is off the year book.", lineId);
    }

    public static async Task<ErpSimpleWriteResult> ReverseAsync(
        DbConnection connection, DbTransaction tx, long lineId, CancellationToken cancellationToken)
    {
        if (lineId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the line to reverse.");
        }

        var head = await HeadAsync(connection, tx, lineId, cancellationToken).ConfigureAwait(false);
        if (head is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That line is not on the year book.");
        }

        if (head.ParentId != 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Reverse the building line. The flat shares follow it.");
        }

        if (head.ReversalOf != 0 || string.Equals(head.Status, "reversal", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "This line is already a reversal.");
        }

        if (!string.Equals(head.Status, "recorded", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Only a recorded line can be reversed.");
        }

        if (head.Amount == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A zero line has nothing to reverse. Void or delete it.");
        }

        var children = await LongAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_re_year_lines` WHERE `parent_id`=? AND `status`<>'void'", cancellationToken, lineId).ConfigureAwait(false);
        if (children > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This line is split across flats. Void or delete it, then record the shares you want.");
        }

        var line = await ReadLineAsync(connection, tx, lineId, cancellationToken).ConfigureAwait(false);
        if (line is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That line is not on the year book.");
        }

        var code = Clip("R" + line.Code, 40);
        var existing = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_re_year_lines` WHERE `code`=? LIMIT 1", cancellationToken, code).ConfigureAwait(false);
        if (existing > 0)
        {
            code = Clip(code[..Math.Min(code.Length, 33)] + "-" + Guid.NewGuid().ToString("N")[..6], 40);
        }

        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_year_lines` (`code`,`property_id`,`unit_id`,`line_year`,`kind`,`category`,`amount`,`spread`,`parent_id`,`movement_date`,`note`,`status`,`import_id`,`coa_code`,`reversal_of`) VALUES (?,?,?,?,?,?,?,?,0,?,?,'reversal',0,?,?)"),
            cancellationToken, code, line.PropertyId, line.UnitId, line.Year, line.Kind, line.Category, -line.Amount, line.Spread,
            string.IsNullOrWhiteSpace(line.MovementDate) ? null : line.MovementDate, Clip("Reversal of " + line.Code + ". " + line.Note, 255),
            string.IsNullOrWhiteSpace(line.CoaCode) ? ErpLandlordCoa.CodeFor(line.Kind, line.Category) : line.CoaCode, lineId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_year_lines` SET `status`='reversed' WHERE `id`=?"),
            cancellationToken, lineId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Reversed. The opposite amount is on the year, so this line no longer changes the return.", lineId);
    }

    public static async Task<ErpSimpleWriteResult> ImportYearRowAsync(
        DbConnection connection, DbTransaction tx, IReadOnlyDictionary<string, string> row, long importId, CancellationToken cancellationToken)
    {
        var propertyCode = Text(row, "propertycode");
        var propertyId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_properties` WHERE `code`=? LIMIT 1", cancellationToken, propertyCode).ConfigureAwait(false);
        if (propertyId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Property " + propertyCode + " was not found.");
        }

        long unitId = 0;
        var unitCode = Text(row, "unitcode");
        if (unitCode.Length > 0)
        {
            unitId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_re_units` WHERE `property_id`=? AND `code`=? LIMIT 1", cancellationToken, propertyId, unitCode).ConfigureAwait(false);
            if (unitId <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Flat " + unitCode + " was not found on " + propertyCode + ".");
            }
        }

        int.TryParse(Text(row, "year"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year);
        var saved = await SaveAsync(connection, tx, new ErpLandlordEntry(
            propertyId,
            unitId,
            year,
            Text(row, "kind"),
            Text(row, "category"),
            ErpRealEstateWorkbook.Money(Text(row, "amount")),
            unitId > 0 ? "flat" : (Text(row, "spread").Length == 0 ? "building" : Text(row, "spread")),
            Text(row, "note"),
            Text(row, "movementdate"),
            Text(row, "code")), importId, cancellationToken).ConfigureAwait(false);
        return saved;
    }

    public static async Task RecordExpenseMeasurementAsync(
        DbConnection connection, DbTransaction tx, IReadOnlyDictionary<string, string> row, long importId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(Text(row, "year"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) || year <= 0)
        {
            return;
        }

        var propertyId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_properties` WHERE `code`=? LIMIT 1", cancellationToken, Text(row, "propertycode")).ConfigureAwait(false);
        if (propertyId <= 0)
        {
            return;
        }

        long unitId = 0;
        var unitCode = Text(row, "unitcode");
        if (unitCode.Length > 0)
        {
            unitId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_re_units` WHERE `property_id`=? AND `code`=? LIMIT 1", cancellationToken, propertyId, unitCode).ConfigureAwait(false);
        }

        var spread = Text(row, "spread");
        if (spread.Length == 0)
        {
            spread = unitId > 0 ? "flat" : "building";
        }

        if (unitId <= 0 && string.Equals(spread, "flat", StringComparison.OrdinalIgnoreCase))
        {
            spread = "building";
        }

        await SaveAsync(connection, tx, new ErpLandlordEntry(
            propertyId,
            unitId,
            year,
            "expense",
            Text(row, "category"),
            ErpRealEstateWorkbook.Money(Text(row, "amount")),
            spread,
            Text(row, "note"),
            Text(row, "expensedate"),
            "Y" + year.ToString(CultureInfo.InvariantCulture) + "-" + Text(row, "code")), importId, cancellationToken).ConfigureAwait(false);
    }

    public static async Task StampPropertyAsync(
        DbConnection connection, DbTransaction tx, long propertyId, IReadOnlyDictionary<string, string> row, CancellationToken cancellationToken)
    {
        if (propertyId <= 0)
        {
            return;
        }

        if (!row.ContainsKey("holder") && !row.ContainsKey("rentstartyear") && !row.ContainsKey("plannedunits"))
        {
            return;
        }

        int.TryParse(Text(row, "rentstartyear"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start);
        int.TryParse(Text(row, "plannedunits"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var planned);
        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
            "UPDATE `epc_erp_properties` SET `holder_name`=IF(?='',`holder_name`,?), `rent_start_year`=IF(?=0,`rent_start_year`,?), `planned_units`=IF(?=0,`planned_units`,?) WHERE `id`=?"),
            cancellationToken, Text(row, "holder"), Clip(Text(row, "holder"), 190), start, start, planned, planned, propertyId).ConfigureAwait(false);
        var holder = Text(row, "holder");
        if (holder.Length == 0)
        {
            return;
        }

        var ownerId = await LongAsync(connection, tx, "SELECT IFNULL(`id`,0) FROM `epc_erp_re_owners` WHERE `property_id`=? AND `owner_name`=? LIMIT 1", cancellationToken, propertyId, holder).ConfigureAwait(false);
        if (ownerId <= 0)
        {
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_owners` (`property_id`,`owner_name`,`ownership_pct`,`status`) VALUES (?,?,100,'active')"),
                cancellationToken, propertyId, Clip(holder, 190)).ConfigureAwait(false);
        }
    }

    public static async Task<IReadOnlyList<ErpLandlordProperty>> PropertiesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ErpLandlordProperty>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.`id`, p.`code`, p.`name`, IFNULL(p.`holder_name`,''), IFNULL(p.`rent_start_year`,0), IFNULL(p.`planned_units`,0), IFNULL(p.`type`,''),
                   (SELECT COUNT(*) FROM `epc_erp_re_units` u WHERE u.`property_id`=p.`id`)
            FROM `epc_erp_properties` p
            ORDER BY p.`name`
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpLandlordProperty(
                ReadLong(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3),
                ReadInt(reader, 4), ReadInt(reader, 5), ReadInt(reader, 7), ReadText(reader, 6)));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<ErpLandlordUnit>> UnitsAsync(DbConnection connection, long propertyId, CancellationToken cancellationToken)
    {
        var rows = new List<ErpLandlordUnit>();
        await using var command = connection.CreateCommand();
        command.CommandText = propertyId > 0
            ? ErpDb.Positional("SELECT `id`,`property_id`,`code`,`name` FROM `epc_erp_re_units` WHERE `property_id`=? ORDER BY `code`")
            : "SELECT `id`,`property_id`,`code`,`name` FROM `epc_erp_re_units` ORDER BY `property_id`,`code`";
        if (propertyId > 0)
        {
            ErpDb.AddParameters(command, propertyId);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpLandlordUnit(ReadLong(reader, 0), ReadLong(reader, 1), ReadText(reader, 2), ReadText(reader, 3)));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<ErpLandlordLine>> LinesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ErpLandlordLine>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.`id`, l.`code`, l.`property_id`, l.`unit_id`, l.`line_year`, l.`kind`, l.`category`, l.`amount`, l.`spread`, l.`parent_id`,
                   IFNULL(DATE_FORMAT(l.`movement_date`,'%Y-%m-%d'),''), l.`note`, l.`status`, p.`code`, p.`name`, IFNULL(u.`code`,''), IFNULL(u.`name`,''), IFNULL(l.`coa_code`,'')
            FROM `epc_erp_re_year_lines` l
            JOIN `epc_erp_properties` p ON p.`id`=l.`property_id`
            LEFT JOIN `epc_erp_re_units` u ON u.`id`=l.`unit_id`
            ORDER BY l.`line_year`, p.`name`, l.`kind`, l.`id`
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadLine(reader));
        }

        return rows;
    }

    public static async Task<ErpLandlordMeasure> MeasureLightAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var byYear = new Dictionary<int, (decimal Income, decimal Expense)>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `line_year`, `kind`, IFNULL(SUM(`amount`),0)
                FROM `epc_erp_re_year_lines`
                WHERE `parent_id`=0 AND `status`<>'void' AND `kind` IN ('income','expense')
                GROUP BY `line_year`, `kind`
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var year = ReadInt(reader, 0);
                var kind = ReadText(reader, 1);
                var amount = ReadMoney(reader, 2);
                byYear.TryGetValue(year, out var pair);
                if (string.Equals(kind, "income", StringComparison.OrdinalIgnoreCase))
                {
                    pair.Income += amount;
                }
                else
                {
                    pair.Expense += amount;
                }

                byYear[year] = pair;
            }
        }
        catch (DbException)
        {
            return ErpLandlordMeasure.Empty;
        }

        if (byYear.Count == 0)
        {
            return ErpLandlordMeasure.Empty;
        }

        var latest = 0;
        decimal allIncome = 0;
        decimal allExpense = 0;
        foreach (var row in byYear)
        {
            allIncome += row.Value.Income;
            allExpense += row.Value.Expense;
            if (row.Key > latest && (row.Value.Income != 0 || row.Value.Expense != 0))
            {
                latest = row.Key;
            }
        }

        byYear.TryGetValue(latest, out var chosen);
        return new ErpLandlordMeasure(latest, chosen.Income, chosen.Expense, allIncome, allExpense);
    }

    private sealed record LineHead(string Status, long ParentId, long ReversalOf, decimal Amount);

    private static async Task<LineHead?> HeadAsync(DbConnection connection, DbTransaction tx, long id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = ErpDb.Positional(
            "SELECT `status`,`parent_id`,IFNULL(`reversal_of`,0),`amount` FROM `epc_erp_re_year_lines` WHERE `id`=? LIMIT 1");
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new LineHead(ReadText(reader, 0), ReadLong(reader, 1), ReadLong(reader, 2), ReadMoney(reader, 3));
    }

    private static async Task<ErpLandlordLine?> ReadLineAsync(DbConnection connection, DbTransaction tx, long id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`code`,`property_id`,`unit_id`,`line_year`,`kind`,`category`,`amount`,`spread`,`parent_id`,IFNULL(DATE_FORMAT(`movement_date`,'%Y-%m-%d'),''),`note`,`status`,'','','','',IFNULL(`coa_code`,'') FROM `epc_erp_re_year_lines` WHERE `id`=? LIMIT 1");
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadLine(reader);
    }

    private static ErpLandlordLine ReadLine(DbDataReader reader)
        => new(
            ReadLong(reader, 0), ReadText(reader, 1), ReadLong(reader, 2), ReadLong(reader, 3), ReadInt(reader, 4),
            ReadText(reader, 5), ReadText(reader, 6), ReadMoney(reader, 7), ReadText(reader, 8), ReadLong(reader, 9),
            ReadText(reader, 10), ReadText(reader, 11), ReadText(reader, 12), ReadText(reader, 13), ReadText(reader, 14),
            ReadText(reader, 15), ReadText(reader, 16), ReadText(reader, 17));

    private static long ReadLong(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static int ReadInt(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static decimal ReadMoney(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static string ReadText(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";

    private static async Task<long> LongAsync(DbConnection connection, DbTransaction? tx, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static string NewCode(int year, string kind, long propertyId)
        => Clip("M" + year.ToString(CultureInfo.InvariantCulture) + "-" + kind[..Math.Min(3, kind.Length)] + "-" + propertyId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..6], 40);

    private static string ChildCode(string parent, long unitId)
        => Clip(parent + "-U" + unitId.ToString(CultureInfo.InvariantCulture), 40);

    private static string Text(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value.Trim() : "";

    private static string Clip(string? value, int max, string fallback = "")
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
