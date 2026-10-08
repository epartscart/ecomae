using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twin for <c>epc_erp_processflow.php</c> demo + task-engine actions:
/// <c>pf_seed_demo</c> (<c>epc_pf_seed_demo</c>), <c>pf_clear_demo</c>
/// (<c>epc_pf_clear_demo</c>) and <c>pf_sync_orders</c> (<c>epc_pf_sync_all_tasks</c>).
/// Demo rows are tagged exactly like PHP (<c>@pf-demo.local</c> emails,
/// <c>[PF-DEMO]</c> notes, <c>DEMO-PF-*</c> case references, category <c>demo</c>)
/// so clear is a 1:1 cleanup. Case start/act/cancel reuse the already-live pf
/// write services; process/step/dept-head writes reuse theirs. Fail-closed when
/// the PHP-provisioned tables are missing.
/// </summary>
public interface IErpPfDemoSyncWriteService
{
    Task<ErpPfDemoSyncResult> SeedDemoAsync(int adminId, CancellationToken cancellationToken = default);
    Task<ErpPfDemoSyncResult> ClearDemoAsync(CancellationToken cancellationToken = default);
    Task<ErpPfDemoSyncResult> SyncTasksAsync(int adminId, int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// PHP <c>epc_pf_sync_order_case</c> / <c>epc_pf_sync_po_case</c> / <c>epc_pf_sync_pay_case</c> /
/// <c>epc_pf_sync_exp_case</c>: create the subject's lifecycle case if missing and auto-advance it to the
/// subject's real status. Runs <c>epc_pf_ensure_schema</c> first and never throws (returns 0 like PHP).
/// <paramref name="adminId"/> is PHP <c>epc_pf_admin_id()</c> (0 outside the CP);
/// <paramref name="sessionUserId"/> is PHP <c>epc_pf_user_id()</c>, the fallback actor/assignee.
/// </summary>
public interface IErpProcessFlowSyncService
{
    Task<long> SyncOrderCaseAsync(long orderId, int adminId, long sessionUserId, CancellationToken cancellationToken = default);
    Task<long> SyncPoCaseAsync(long poId, int adminId, long sessionUserId, CancellationToken cancellationToken = default);
    Task<long> SyncPayCaseAsync(long batchId, int adminId, long sessionUserId, CancellationToken cancellationToken = default);
    Task<long> SyncExpCaseAsync(long reportId, int adminId, long sessionUserId, CancellationToken cancellationToken = default);
}

public sealed record ErpPfDemoSyncResult(
    bool Ok,
    string Message,
    int Writes,
    object? Payload)
{
    public static ErpPfDemoSyncResult Fail(string message) => new(false, message, 0, null);
    public static ErpPfDemoSyncResult Success(int writes, object payload) => new(true, "ok", writes, payload);
}

public sealed class ErpPfDemoSyncWriteService : IErpPfDemoSyncWriteService, IErpProcessFlowSyncService
{
    private const string DemoEmail = "@pf-demo.local";
    private const long DemoUidBase = 700000;
    private const string DemoTag = "[PF-DEMO]";

    private const string OrderCategory = "order_lifecycle";
    private const string PoCategory = "po_lifecycle";
    private const string PayCategory = "payment_lifecycle";
    private const string ExpCategory = "expense_lifecycle";

    private static readonly string[] DemoLocations =
    {
        "Dubai HQ", "Abu Dhabi Branch", "Sharjah Branch", "Jebel Ali Warehouse", "Al Ain Branch"
    };

    private static readonly string[] FirstNames =
    {
        "Ahmed", "Mohammed", "Fatima", "Sara", "Omar", "Layla", "Yusuf", "Aisha", "Khalid", "Noura",
        "Hassan", "Mariam", "Ali", "Huda", "Rashid", "Salma", "Tariq", "Reem", "Faisal", "Mona",
        "Bilal", "Zainab", "Imran", "Dana", "Saeed"
    };

    private static readonly string[] LastNames =
    {
        "Al Mansoori", "Khan", "Hussain", "Al Marri", "Sharma", "Patel", "Al Naqbi", "Rahman",
        "Mehta", "Al Suwaidi", "Iqbal", "Nair", "Al Hashimi", "Farooq", "Das", "Al Balushi",
        "Siddiqui", "Kapoor", "Al Zaabi", "Joseph"
    };

    private static readonly (string Code, string Name)[] DemoDepartments =
    {
        ("sales", "Sales"),
        ("logistics", "Logistics"),
        ("marketing", "Marketing"),
        ("finance", "Finance"),
        ("hr", "Human Resources"),
        ("it", "Information Technology"),
        ("purchase", "Purchase"),
        ("accounts", "Accounts"),
    };

    private static readonly Dictionary<string, string[]> TitlesByDept = new(StringComparer.Ordinal)
    {
        ["sales"] = new[] { "Sales Executive", "Account Manager", "Sales Officer", "Business Dev Exec" },
        ["logistics"] = new[] { "Logistics Officer", "Warehouse Supervisor", "Dispatch Coordinator", "Fleet Officer" },
        ["marketing"] = new[] { "Marketing Executive", "Content Specialist", "Brand Officer", "Digital Marketer" },
        ["finance"] = new[] { "Accountant", "Finance Officer", "AP/AR Specialist", "Treasury Officer" },
        ["hr"] = new[] { "HR Officer", "Recruiter", "Payroll Officer", "HR Coordinator" },
        ["it"] = new[] { "IT Support", "System Admin", "Developer", "Network Officer" },
        ["purchase"] = new[] { "Buyer", "Procurement Officer", "Sourcing Specialist", "Vendor Coordinator" },
        ["accounts"] = new[] { "Accounts Officer", "Bookkeeper", "GL Accountant", "Audit Assistant" },
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpPfProcessSaveWriteService _processSave;
    private readonly IErpPfStepSaveWriteService _stepSave;
    private readonly IErpPfSetDeptHeadWriteService _deptHead;
    private readonly IErpPfCaseStartWriteService _caseStart;
    private readonly IErpPfCaseActWriteService _caseAct;
    private readonly IErpPfCaseCancelWriteService _caseCancel;

    public ErpPfDemoSyncWriteService(
        IErpWriteConnectionFactory connections,
        IErpPfProcessSaveWriteService processSave,
        IErpPfStepSaveWriteService stepSave,
        IErpPfSetDeptHeadWriteService deptHead,
        IErpPfCaseStartWriteService caseStart,
        IErpPfCaseActWriteService caseAct,
        IErpPfCaseCancelWriteService caseCancel)
    {
        _connections = connections;
        _processSave = processSave;
        _stepSave = stepSave;
        _deptHead = deptHead;
        _caseStart = caseStart;
        _caseAct = caseAct;
        _caseCancel = caseCancel;
    }

    public async Task<ErpPfDemoSyncResult> SeedDemoAsync(int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpPfDemoSyncResult.Fail("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return ErpPfDemoSyncResult.Fail("Process-flow tables are not provisioned");
        }

        var clear = await ClearDemoOnAsync(connection, cancellationToken).ConfigureAwait(false);

        // PHP epc_pf_seed_employees: clear demo staff first, then head + 5 per location per dept.
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "DELETE FROM `epc_erp_staff_profiles` WHERE `email` LIKE '%" + DemoEmail + "'",
            cancellationToken).ConfigureAwait(false);

        var writes = clear;
        var uid = DemoUidBase;
        var seq = 0;
        var count = 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var heads = new Dictionary<string, long>(StringComparer.Ordinal);

        for (var d = 0; d < DemoDepartments.Length; d++)
        {
            var (code, deptName) = DemoDepartments[d];
            var titles = TitlesByDept.TryGetValue(code, out var t)
                ? t
                : new[] { "Officer", "Specialist", "Coordinator", "Associate" };
            var headLoc = DemoLocations[d % DemoLocations.Length];

            uid++;
            seq++;
            var headName = FirstNames[seq % FirstNames.Length] + " " + LastNames[seq % LastNames.Length];
            var headEmail = headName.ToLowerInvariant().Replace(' ', '.') + "." + seq.ToString(CultureInfo.InvariantCulture) + DemoEmail;
            await InsertStaffAsync(
                connection,
                uid, code, headName, "Head of " + deptName, headEmail,
                "+9715" + (1000000 + seq).ToString(CultureInfo.InvariantCulture),
                headLoc, BuForDept(code), LegalEntityForLocation(headLoc), now, cancellationToken).ConfigureAwait(false);
            heads[code] = uid;
            count++;
            writes++;

            foreach (var loc in DemoLocations)
            {
                for (var k = 0; k < 5; k++)
                {
                    uid++;
                    seq++;
                    var name = FirstNames[seq % FirstNames.Length] + " " + LastNames[(seq * 3) % LastNames.Length];
                    var email = name.ToLowerInvariant().Replace(' ', '.') + "." + seq.ToString(CultureInfo.InvariantCulture) + DemoEmail;
                    await InsertStaffAsync(
                        connection,
                        uid, code, name, titles[k % titles.Length], email,
                        "+9715" + (1000000 + seq).ToString(CultureInfo.InvariantCulture),
                        loc, BuForDept(code), LegalEntityForLocation(loc), now, cancellationToken).ConfigureAwait(false);
                    count++;
                    writes++;
                }
            }
        }

        // PHP epc_pf_seed_demo: dept heads for every configured department.
        foreach (var (code, _) in DemoDepartments)
        {
            var headUid = heads.TryGetValue(code, out var h) && h > 0 ? h : adminId;
            var res = await _deptHead.SaveAsync(code, headUid, cancellationToken).ConfigureAwait(false);
            if (res.Succeeded)
            {
                writes++;
            }
        }

        var blueprintCount = 0;
        var processIds = new List<long>();
        foreach (var bp in DemoBlueprints)
        {
            var saved = await _processSave.SaveAsync(
                new ErpPfProcessSaveWriteRequest(0, bp.Name, bp.Description, "demo"), cancellationToken).ConfigureAwait(false);
            if (!saved.Succeeded)
            {
                return ErpPfDemoSyncResult.Fail(saved.Message);
            }

            processIds.Add(saved.Id);
            writes++;
            var stepNo = 1;
            foreach (var s in bp.Steps)
            {
                var stepRes = await _stepSave.SaveAsync(
                    new ErpPfStepSaveWriteRequest(saved.Id, s.Name, s.Type, 0, s.Department, stepNo++, s.SlaHours, null),
                    cancellationToken).ConfigureAwait(false);
                if (!stepRes.Succeeded)
                {
                    return ErpPfDemoSyncResult.Fail(stepRes.Message);
                }

                writes++;
            }

            blueprintCount++;
        }

        // Pool of demo employees as initiators (PHP ORDER BY RAND() LIMIT 40).
        var demoEmps = new List<long>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `user_id` FROM `epc_erp_staff_profiles` WHERE `email` LIKE '%" + DemoEmail + "' ORDER BY RAND() LIMIT 40";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                demoEmps.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        var rng = new Random();
        long PickEmp() => demoEmps.Count == 0 ? adminId : demoEmps[rng.Next(demoEmps.Count)];

        var cases = 0;
        for (var i = 0; i < DemoCases.Length; i++)
        {
            var t = DemoCases[i];
            var pid = t.ProcessIndex < processIds.Count ? processIds[t.ProcessIndex] : processIds[i % processIds.Count];
            var started = await _caseStart.StartAsync(
                new ErpPfCaseStartWriteRequest(pid, t.Title, "DEMO-PF-" + (i + 1).ToString(CultureInfo.InvariantCulture), t.Priority, PickEmp(), null, 0, adminId),
                cancellationToken).ConfigureAwait(false);
            if (!started.Succeeded)
            {
                return ErpPfDemoSyncResult.Fail(started.Message);
            }

            cases++;
            writes++;
            var caseId = started.Id;
            for (var a = 0; a < t.Advance; a++)
            {
                var curNo = await CaseCurrentStepAsync(connection, caseId, cancellationToken).ConfigureAwait(false);
                if (curNo is null)
                {
                    break;
                }

                var actor = await ActiveStepAssigneeAsync(connection, caseId, curNo.Value, cancellationToken).ConfigureAwait(false);
                var act = await _caseAct.ActAsync(
                    new ErpPfCaseActWriteRequest(caseId, "approve", "Reviewed and approved (sample data)", actor > 0 ? actor : adminId),
                    cancellationToken).ConfigureAwait(false);
                writes += act.Writes;
                if (act.CaseStatus != "open")
                {
                    break;
                }
            }
        }

        // PHP epc_pf_seed_demo_finance_tasks: tagged supplier-payment batches + expense claims.
        var finance = await SeedDemoFinanceTasksAsync(connection, demoEmps, adminId, rng, now, cancellationToken).ConfigureAwait(false);
        writes += finance;

        // PHP epc_pf_sync_all_tasks(300) backfill.
        var sync = await SyncAllOnAsync(connection, adminId, 300, cancellationToken).ConfigureAwait(false);
        writes += sync.Writes;

        return ErpPfDemoSyncResult.Success(
            writes,
            new { processes = blueprintCount, cases, employees = count, sync = sync.Payload });
    }

    public async Task<ErpPfDemoSyncResult> ClearDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpPfDemoSyncResult.Fail("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return ErpPfDemoSyncResult.Fail("Process-flow tables are not provisioned");
        }

        var cleared = await ClearDemoOnAsync(connection, cancellationToken).ConfigureAwait(false);
        return ErpPfDemoSyncResult.Success(cleared, new { cleared });
    }

    public async Task<ErpPfDemoSyncResult> SyncTasksAsync(int adminId, int limit, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpPfDemoSyncResult.Fail("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return ErpPfDemoSyncResult.Fail("Process-flow tables are not provisioned");
        }

        var res = await SyncAllOnAsync(connection, adminId, limit <= 0 ? 300 : limit, cancellationToken).ConfigureAwait(false);
        return res;
    }

    public Task<long> SyncOrderCaseAsync(long orderId, int adminId, long sessionUserId, CancellationToken cancellationToken = default)
        => SyncOneAsync(orderId, adminId, sessionUserId, SyncOrderCaseOnAsync, cancellationToken);

    public Task<long> SyncPoCaseAsync(long poId, int adminId, long sessionUserId, CancellationToken cancellationToken = default)
        => SyncOneAsync(poId, adminId, sessionUserId, SyncPoCaseOnAsync, cancellationToken);

    public Task<long> SyncPayCaseAsync(long batchId, int adminId, long sessionUserId, CancellationToken cancellationToken = default)
        => SyncOneAsync(batchId, adminId, sessionUserId, SyncPayCaseOnAsync, cancellationToken);

    public Task<long> SyncExpCaseAsync(long reportId, int adminId, long sessionUserId, CancellationToken cancellationToken = default)
        => SyncOneAsync(reportId, adminId, sessionUserId, SyncExpCaseOnAsync, cancellationToken);

    private async Task<long> SyncOneAsync(
        long subjectId,
        int adminId,
        long sessionUserId,
        Func<DbConnection, long, int, long, CancellationToken, Task<long>> sync,
        CancellationToken cancellationToken)
    {
        if (subjectId <= 0 || !_connections.IsConfigured)
        {
            return 0;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpPfSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            return await sync(connection, subjectId, adminId, sessionUserId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    /* ---------- demo clearing (PHP epc_pf_clear_demo) ---------- */

    private static async Task<int> ClearDemoOnAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var removed = 0;
        var caseIds = await IdsAsync(
            connection,
            "SELECT `id` FROM `epc_pf_cases` WHERE `reference` LIKE 'DEMO-PF%'",
            cancellationToken).ConfigureAwait(false);
        foreach (var cid in caseIds)
        {
            removed += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_pf_case_steps` WHERE `case_id` = ?"), cancellationToken, cid).ConfigureAwait(false);
            removed += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_pf_cases` WHERE `id` = ?"), cancellationToken, cid).ConfigureAwait(false);
        }

        var procIds = await IdsAsync(
            connection,
            "SELECT `id` FROM `epc_pf_processes` WHERE `category` = 'demo'",
            cancellationToken).ConfigureAwait(false);
        foreach (var pid in procIds)
        {
            removed += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_pf_steps` WHERE `process_id` = ?"), cancellationToken, pid).ConfigureAwait(false);
            removed += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_pf_processes` WHERE `id` = ?"), cancellationToken, pid).ConfigureAwait(false);
        }

        // PHP wraps the staff / finance cleanup in try/catch — keep the same.
        try
        {
            var demoUids = await IdsAsync(
                connection,
                "SELECT `user_id` FROM `epc_erp_staff_profiles` WHERE `email` LIKE '%" + DemoEmail + "'",
                cancellationToken).ConfigureAwait(false);
            if (demoUids.Count > 0)
            {
                removed += await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    "DELETE FROM `epc_pf_dept_heads` WHERE `head_user_id` IN (" + string.Join(',', demoUids) + ")",
                    cancellationToken).ConfigureAwait(false);
            }

            removed += await ErpDb.ExecuteAsync(
                connection,
                null,
                "DELETE FROM `epc_erp_staff_profiles` WHERE `email` LIKE '%" + DemoEmail + "'",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        try
        {
            var payIds = await IdsAsync(
                connection,
                "SELECT `id` FROM `epc_erp_payment_batches` WHERE `notes` LIKE '%" + DemoTag + "%'",
                cancellationToken).ConfigureAwait(false);
            var expIds = await IdsAsync(
                connection,
                "SELECT `id` FROM `epc_erp_expense_reports` WHERE `notes` LIKE '%" + DemoTag + "%'",
                cancellationToken).ConfigureAwait(false);
            removed += await KillSubjectCasesAsync(connection, "erp_payment", payIds, cancellationToken).ConfigureAwait(false);
            removed += await KillSubjectCasesAsync(connection, "erp_expense", expIds, cancellationToken).ConfigureAwait(false);
            removed += await ErpDb.ExecuteAsync(
                connection,
                null,
                "DELETE FROM `epc_erp_payment_batches` WHERE `notes` LIKE '%" + DemoTag + "%'",
                cancellationToken).ConfigureAwait(false);
            removed += await ErpDb.ExecuteAsync(
                connection,
                null,
                "DELETE FROM `epc_erp_expense_reports` WHERE `notes` LIKE '%" + DemoTag + "%'",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        return removed;
    }

    private static async Task<int> KillSubjectCasesAsync(
        DbConnection connection, string subjectType, IReadOnlyList<long> subjectIds, CancellationToken cancellationToken)
    {
        var removed = 0;
        foreach (var sid in subjectIds)
        {
            var caseIds = await IdsParamAsync(
                connection,
                ErpDb.Positional("SELECT `id` FROM `epc_pf_cases` WHERE `subject_type` = ? AND `subject_id` = ?"),
                cancellationToken,
                subjectType,
                sid).ConfigureAwait(false);
            foreach (var cid in caseIds)
            {
                removed += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_pf_case_steps` WHERE `case_id` = ?"), cancellationToken, cid).ConfigureAwait(false);
                removed += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_pf_cases` WHERE `id` = ?"), cancellationToken, cid).ConfigureAwait(false);
            }
        }

        return removed;
    }

    /* ---------- demo finance tasks (PHP epc_pf_seed_demo_finance_tasks) ---------- */

    private static async Task<int> SeedDemoFinanceTasksAsync(
        DbConnection connection,
        IReadOnlyList<long> demoEmps,
        int adminId,
        Random rng,
        long now,
        CancellationToken cancellationToken)
    {
        var writes = 0;
        try
        {
            var have = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT COUNT(*) FROM `epc_erp_payment_batches` WHERE `notes` LIKE '%" + DemoTag + "%'",
                cancellationToken).ConfigureAwait(false);
            if (have == 0)
            {
                var batches = new (string No, string Type, double Total, int Lines, string Status)[]
                {
                    ("PAY-" + now.ToString(CultureInfo.InvariantCulture) + "-1", "sepa", 18450.00, 6, "processed"),
                    ("PAY-" + now.ToString(CultureInfo.InvariantCulture) + "-2", "local", 7320.50, 3, "submitted"),
                    ("PAY-" + now.ToString(CultureInfo.InvariantCulture) + "-3", "cheque", 2150.00, 1, "draft"),
                };
                foreach (var b in batches)
                {
                    writes += await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("INSERT INTO `epc_erp_payment_batches` (`batch_no`,`batch_type`,`account_id`,`total_amount`,`line_count`,`status`,`execution_date`,`notes`,`admin_id`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?)"),
                        cancellationToken,
                        b.No, b.Type, 0, b.Total, b.Lines, b.Status, now, "Supplier settlement run " + DemoTag, adminId, now, now).ConfigureAwait(false);
                }
            }

            var haveE = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT COUNT(*) FROM `epc_erp_expense_reports` WHERE `notes` LIKE '%" + DemoTag + "%'",
                cancellationToken).ConfigureAwait(false);
            if (haveE == 0)
            {
                var reports = new (string No, string Title, double Total, string Status)[]
                {
                    ("EXP-" + now.ToString(CultureInfo.InvariantCulture) + "-1", "Client visit — Abu Dhabi", 845.50, "paid"),
                    ("EXP-" + now.ToString(CultureInfo.InvariantCulture) + "-2", "Trade show booth — DWTC", 3200.00, "approved"),
                    ("EXP-" + now.ToString(CultureInfo.InvariantCulture) + "-3", "Courier & customs charges", 612.75, "submitted"),
                    ("EXP-" + now.ToString(CultureInfo.InvariantCulture) + "-4", "Team training materials", 1480.00, "submitted"),
                };
                foreach (var r in reports)
                {
                    var staff = demoEmps.Count == 0 ? adminId : demoEmps[rng.Next(demoEmps.Count)];
                    writes += await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("INSERT INTO `epc_erp_expense_reports` (`report_no`,`staff_user_id`,`title`,`total_amount`,`status`,`period_from`,`period_to`,`notes`,`admin_id`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?)"),
                        cancellationToken,
                        r.No, staff, r.Title, r.Total, r.Status, now - 20L * 86400, now, "Expense claim " + DemoTag, adminId, now, now).ConfigureAwait(false);
                }
            }
        }
        catch (DbException)
        {
            // PHP wraps this in try/catch.
        }

        return writes;
    }

    /* ---------- task engine sync (PHP epc_pf_sync_all_tasks) ---------- */

    private async Task<ErpPfDemoSyncResult> SyncAllOnAsync(
        DbConnection connection, int adminId, int limit, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["orders"] = 0,
            ["purchase_orders"] = 0,
            ["payments"] = 0,
            ["expenses"] = 0,
        };
        var writes = 0;

        try
        {
            var ids = await IdsAsync(
                connection,
                "SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 ORDER BY `id` DESC LIMIT " + Math.Max(1, limit).ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
            foreach (var oid in ids)
            {
                if (await SyncOrderCaseOnAsync(connection, oid, adminId, adminId, cancellationToken).ConfigureAwait(false) > 0)
                {
                    counts["orders"]++;
                    writes++;
                }
            }
        }
        catch (DbException)
        {
        }

        try
        {
            var ids = await IdsAsync(
                connection,
                "SELECT `id` FROM `epc_erp_purchase_orders` ORDER BY `id` DESC LIMIT " + Math.Max(1, limit).ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
            foreach (var pid in ids)
            {
                if (await SyncPoCaseOnAsync(connection, pid, adminId, adminId, cancellationToken).ConfigureAwait(false) > 0)
                {
                    counts["purchase_orders"]++;
                    writes++;
                }
            }
        }
        catch (DbException)
        {
        }

        try
        {
            var ids = await IdsAsync(
                connection,
                "SELECT `id` FROM `epc_erp_payment_batches` ORDER BY `id` DESC LIMIT " + Math.Max(1, limit).ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
            foreach (var bid in ids)
            {
                if (await SyncPayCaseOnAsync(connection, bid, adminId, adminId, cancellationToken).ConfigureAwait(false) > 0)
                {
                    counts["payments"]++;
                    writes++;
                }
            }
        }
        catch (DbException)
        {
        }

        try
        {
            var ids = await IdsAsync(
                connection,
                "SELECT `id` FROM `epc_erp_expense_reports` ORDER BY `id` DESC LIMIT " + Math.Max(1, limit).ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
            foreach (var rid in ids)
            {
                if (await SyncExpCaseOnAsync(connection, rid, adminId, adminId, cancellationToken).ConfigureAwait(false) > 0)
                {
                    counts["expenses"]++;
                    writes++;
                }
            }
        }
        catch (DbException)
        {
        }

        return ErpPfDemoSyncResult.Success(writes, counts);
    }

    private async Task<long> SyncOrderCaseOnAsync(DbConnection connection, long orderId, int adminId, long fallbackUserId, CancellationToken cancellationToken)
    {
        if (orderId <= 0)
        {
            return 0;
        }

        try
        {
            var order = await OrderFactsAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
            if (order is null)
            {
                return 0;
            }

            var pid = await EnsureOrderProcessAsync(cancellationToken).ConfigureAwait(false);
            var existing = await FindCaseAsync(connection, "shop_order", orderId, pid, cancellationToken).ConfigureAwait(false);
            long caseId;
            if (existing is null)
            {
                var customer = order.Customer;
                var title = "Customer order #" + orderId.ToString(CultureInfo.InvariantCulture)
                    + (customer.Length > 0 ? " — " + customer : string.Empty);
                var started = await _caseStart.StartAsync(
                    new ErpPfCaseStartWriteRequest(pid, title, "Order #" + orderId.ToString(CultureInfo.InvariantCulture), "normal", adminId, "shop_order", orderId, adminId, fallbackUserId),
                    cancellationToken).ConfigureAwait(false);
                if (!started.Succeeded)
                {
                    return 0;
                }

                caseId = started.Id;
            }
            else
            {
                caseId = existing.Value.Id;
                if (existing.Value.Status != "open")
                {
                    return caseId;
                }
            }

            var goal = order.Done ? 99 : order.Step;
            await AutoAdvanceAsync(connection, caseId, goal, "Auto-advanced from order status", adminId, fallbackUserId, cancellationToken).ConfigureAwait(false);
            return caseId;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private async Task<long> SyncPoCaseOnAsync(DbConnection connection, long poId, int adminId, long fallbackUserId, CancellationToken cancellationToken)
    {
        if (poId <= 0)
        {
            return 0;
        }

        try
        {
            var f = await PoFactsAsync(connection, poId, cancellationToken).ConfigureAwait(false);
            if (f is null)
            {
                return 0;
            }

            var pid = await EnsurePoProcessAsync(cancellationToken).ConfigureAwait(false);
            var existing = await FindCaseAsync(connection, "erp_po", poId, pid, cancellationToken).ConfigureAwait(false);
            long caseId;
            if (existing is null)
            {
                var title = "Purchase order " + (f.PoNo.Length > 0 ? f.PoNo : "#" + poId.ToString(CultureInfo.InvariantCulture))
                    + (f.Supplier.Length > 0 ? " — " + f.Supplier : string.Empty);
                var started = await _caseStart.StartAsync(
                    new ErpPfCaseStartWriteRequest(pid, title, f.PoNo.Length > 0 ? f.PoNo : "PO #" + poId.ToString(CultureInfo.InvariantCulture), "normal", adminId, "erp_po", poId, adminId, fallbackUserId),
                    cancellationToken).ConfigureAwait(false);
                if (!started.Succeeded)
                {
                    return 0;
                }

                caseId = started.Id;
            }
            else
            {
                caseId = existing.Value.Id;
                if (existing.Value.Status != "open")
                {
                    return caseId;
                }
            }

            if (f.Cancelled)
            {
                try
                {
                    await _caseCancel.CancelAsync(caseId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                return caseId;
            }

            var goal = f.Done ? 99 : f.Step;
            await AutoAdvanceAsync(connection, caseId, goal, "Auto-advanced from PO status", adminId, fallbackUserId, cancellationToken).ConfigureAwait(false);
            return caseId;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private async Task<long> SyncPayCaseOnAsync(DbConnection connection, long batchId, int adminId, long fallbackUserId, CancellationToken cancellationToken)
    {
        if (batchId <= 0)
        {
            return 0;
        }

        try
        {
            var f = await PayFactsAsync(connection, batchId, cancellationToken).ConfigureAwait(false);
            if (f is null)
            {
                return 0;
            }

            var pid = await EnsurePayProcessAsync(cancellationToken).ConfigureAwait(false);
            var existing = await FindCaseAsync(connection, "erp_payment", batchId, pid, cancellationToken).ConfigureAwait(false);
            long caseId;
            if (existing is null)
            {
                var title = "Supplier payment " + (f.BatchNo.Length > 0 ? f.BatchNo : "#" + batchId.ToString(CultureInfo.InvariantCulture));
                var started = await _caseStart.StartAsync(
                    new ErpPfCaseStartWriteRequest(pid, title, f.BatchNo.Length > 0 ? f.BatchNo : "PAY #" + batchId.ToString(CultureInfo.InvariantCulture), "normal", adminId, "erp_payment", batchId, adminId, fallbackUserId),
                    cancellationToken).ConfigureAwait(false);
                if (!started.Succeeded)
                {
                    return 0;
                }

                caseId = started.Id;
            }
            else
            {
                caseId = existing.Value.Id;
                if (existing.Value.Status != "open")
                {
                    return caseId;
                }
            }

            if (f.Cancelled)
            {
                try
                {
                    await _caseCancel.CancelAsync(caseId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                return caseId;
            }

            var goal = f.Done ? 99 : f.Step;
            await AutoAdvanceAsync(connection, caseId, goal, "Auto-advanced from payment-batch status", adminId, fallbackUserId, cancellationToken).ConfigureAwait(false);
            return caseId;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private async Task<long> SyncExpCaseOnAsync(DbConnection connection, long reportId, int adminId, long fallbackUserId, CancellationToken cancellationToken)
    {
        if (reportId <= 0)
        {
            return 0;
        }

        try
        {
            var f = await ExpFactsAsync(connection, reportId, cancellationToken).ConfigureAwait(false);
            if (f is null)
            {
                return 0;
            }

            // PHP: a claim only becomes a tracked task once submitted or rejected.
            if (!f.Submitted && !f.Rejected)
            {
                return 0;
            }

            var pid = await EnsureExpProcessAsync(cancellationToken).ConfigureAwait(false);
            var existing = await FindCaseAsync(connection, "erp_expense", reportId, pid, cancellationToken).ConfigureAwait(false);
            long caseId;
            if (existing is null)
            {
                var label = f.Title.Length > 0 ? f.Title : "Expense claim";
                var title = label + " " + (f.ReportNo.Length > 0 ? f.ReportNo : "#" + reportId.ToString(CultureInfo.InvariantCulture));
                var initiator = f.StaffId > 0 ? f.StaffId : adminId;
                var started = await _caseStart.StartAsync(
                    new ErpPfCaseStartWriteRequest(pid, title, f.ReportNo.Length > 0 ? f.ReportNo : "EXP #" + reportId.ToString(CultureInfo.InvariantCulture), "normal", initiator, "erp_expense", reportId, adminId, fallbackUserId),
                    cancellationToken).ConfigureAwait(false);
                if (!started.Succeeded)
                {
                    return 0;
                }

                caseId = started.Id;
            }
            else
            {
                caseId = existing.Value.Id;
                if (existing.Value.Status != "open")
                {
                    return caseId;
                }
            }

            if (f.Rejected)
            {
                try
                {
                    await _caseCancel.CancelAsync(caseId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                return caseId;
            }

            var goal = f.Done ? 99 : f.Step;
            await AutoAdvanceAsync(connection, caseId, goal, "Auto-advanced from expense-claim status", adminId, fallbackUserId, cancellationToken).ConfigureAwait(false);
            return caseId;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /* ---------- built-in lifecycle processes (PHP ensure_*_process) ---------- */

    private async Task<long> EnsureOrderProcessAsync(CancellationToken cancellationToken)
        => await EnsureProcessAsync(
            OrderCategory,
            "Customer Order → Delivery",
            "Runs automatically for every customer order (online portal or manual). The case is created when the order arrives and advances by itself as the order is quoted, paid, procured, dispatched, delivered and invoiced.",
            new (string, string, string, int)[]
            {
                ("Order received", "department", "sales", 4),
                ("Quotation confirmed", "dept_head", "sales", 12),
                ("Payment received", "dept_head", "finance", 24),
                ("Goods ready / procured", "dept_head", "purchase", 48),
                ("Out for delivery", "dept_head", "logistics", 12),
                ("Delivered", "department", "logistics", 24),
                ("Invoiced & closed", "dept_head", "accounts", 12),
            },
            cancellationToken).ConfigureAwait(false);

    private async Task<long> EnsurePoProcessAsync(CancellationToken cancellationToken)
        => await EnsureProcessAsync(
            PoCategory,
            "Procurement → Goods receipt",
            "Runs automatically for every purchase order. The case is created when the PO is raised and advances by itself as it is approved, goods are received and the supplier invoice is matched.",
            new (string, string, string, int)[]
            {
                ("PO raised", "department", "purchase", 8),
                ("PO approved", "dept_head", "purchase", 24),
                ("Goods received", "dept_head", "logistics", 48),
                ("Supplier invoice matched & closed", "dept_head", "accounts", 24),
            },
            cancellationToken).ConfigureAwait(false);

    private async Task<long> EnsurePayProcessAsync(CancellationToken cancellationToken)
        => await EnsureProcessAsync(
            PayCategory,
            "Supplier payment → Settlement",
            "Runs automatically for every outbound supplier payment batch. The case is created when the batch is prepared and advances by itself as it is submitted for release and the payment is processed.",
            new (string, string, string, int)[]
            {
                ("Payment batch prepared", "department", "accounts", 8),
                ("Submitted for release", "dept_head", "accounts", 24),
                ("Payment processed & settled", "dept_head", "finance", 24),
            },
            cancellationToken).ConfigureAwait(false);

    private async Task<long> EnsureExpProcessAsync(CancellationToken cancellationToken)
        => await EnsureProcessAsync(
            ExpCategory,
            "Expense claim → Reimbursement",
            "Runs automatically for every staff expense report. The case is created when the claim is submitted and advances by itself as it is approved by the manager and reimbursed by accounts.",
            new (string, string, string, int)[]
            {
                ("Expense claim submitted", "initiator", "", 8),
                ("Manager approval", "dept_head", "hr", 24),
                ("Reimbursed by accounts", "dept_head", "accounts", 24),
            },
            cancellationToken).ConfigureAwait(false);

    private async Task<long> EnsureProcessAsync(
        string category,
        string name,
        string description,
        (string Name, string Type, string Dept, int Sla)[] steps,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_pf_processes` WHERE `category` = ? ORDER BY `id` LIMIT 1"),
            cancellationToken,
            category).ConfigureAwait(false);
        if (existing > 0)
        {
            return existing;
        }

        var saved = await _processSave.SaveAsync(
            new ErpPfProcessSaveWriteRequest(0, name, description, category), cancellationToken).ConfigureAwait(false);
        if (!saved.Succeeded)
        {
            return 0;
        }

        var stepNo = 1;
        foreach (var s in steps)
        {
            await _stepSave.SaveAsync(
                new ErpPfStepSaveWriteRequest(saved.Id, s.Name, s.Type, 0, s.Dept, stepNo++, s.Sla, null),
                cancellationToken).ConfigureAwait(false);
        }

        return saved.Id;
    }

    /* ---------- auto-advance (PHP epc_pf_auto_advance_case / inline loops) ---------- */

    private async Task AutoAdvanceAsync(
        DbConnection connection, long caseId, long goal, string comment, int adminId, long fallbackUserId, CancellationToken cancellationToken)
    {
        var guard = 0;
        while (guard++ < 12)
        {
            var cur = await CaseCurrentStepAsync(connection, caseId, cancellationToken).ConfigureAwait(false);
            if (cur is null || cur.Value >= goal)
            {
                break;
            }

            var actor = await ActiveStepAssigneeAsync(connection, caseId, cur.Value, cancellationToken).ConfigureAwait(false);
            var res = await _caseAct.ActAsync(
                new ErpPfCaseActWriteRequest(caseId, "approve", comment, actor > 0 ? actor : adminId, fallbackUserId),
                cancellationToken).ConfigureAwait(false);
            if (!res.Succeeded || res.CaseStatus != "open")
            {
                break;
            }
        }
    }

    /* ---------- facts ---------- */

    private sealed record OrderFacts(string Customer, long SoId, long InvoiceId, bool So, bool Procured, bool Fulfilled, bool Delivered, bool Invoiced, int Step, bool Done);

    private async Task<OrderFacts?> OrderFactsAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        string customer;
        try
        {
            await using var cmd = connection.CreateCommand();
            // PHP SELECT * with `?? ''`: the stock shop_orders table has no name/surname/email columns.
            cmd.CommandText = ErpDb.Positional("SELECT * FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1");
            AddParam(cmd, orderId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            string Column(string name)
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (string.Equals(reader.GetName(i), name, StringComparison.Ordinal))
                    {
                        return reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                    }
                }

                return string.Empty;
            }

            var cust = (Column("name") + " " + Column("surname")).Trim();
            if (cust.Length == 0)
            {
                cust = Column("email").Trim();
            }

            customer = cust;
        }
        catch (DbException)
        {
            return null;
        }

        var so = false;
        var procured = false;
        var fulfilled = false;
        var invoiced = false;
        long soId = 0;
        long invoiceId = 0;
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT `id`, `status`, `sales_invoice_id`, `fulfillment_status` FROM `epc_erp_sales_orders` WHERE `shop_order_id` = ? ORDER BY `id` DESC LIMIT 1");
            AddParam(cmd, orderId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                so = true;
                soId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var status = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                invoiceId = reader.IsDBNull(2) ? 0 : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
                var fs = reader.IsDBNull(3) ? "open" : reader.GetString(3);
                if (invoiceId > 0 || status == "invoiced")
                {
                    invoiced = true;
                }

                if (fs is "partial" or "fulfilled")
                {
                    procured = true;
                }

                if (fs == "fulfilled")
                {
                    fulfilled = true;
                }
            }
        }
        catch (DbException)
        {
        }

        var delivered = false;
        try
        {
            if (await ErpOrderCompletionGuard.IsCompleteAsync(connection, orderId, cancellationToken).ConfigureAwait(false))
            {
                delivered = true;
                fulfilled = true;
                procured = true;
            }
        }
        catch (Exception)
        {
        }

        var paid = procured || fulfilled || delivered || invoiced;

        var step = 1;
        if (so) step = 2;
        if (paid) step = Math.Max(step, 3);
        if (procured) step = Math.Max(step, 4);
        if (fulfilled) step = Math.Max(step, 5);
        if (delivered) step = Math.Max(step, 6);
        var done = false;
        if (invoiced)
        {
            step = 7;
            done = true;
        }

        return new OrderFacts(customer, soId, invoiceId, so, procured, fulfilled, delivered, invoiced, step, done);
    }

    private sealed record PoFacts(string PoNo, string Supplier, bool Approved, bool Partial, bool Received, bool Invoiced, bool Cancelled, int Step, bool Done);

    private async Task<PoFacts?> PoFactsAsync(DbConnection connection, long poId, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT p.`status`, p.`purchase_id`, p.`po_no`, (SELECT s.`name` FROM `epc_erp_suppliers` s WHERE s.`id` = p.`supplier_id` LIMIT 1) AS supplier FROM `epc_erp_purchase_orders` p WHERE p.`id` = ? LIMIT 1");
            AddParam(cmd, poId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var status = reader.IsDBNull(0) ? "draft" : reader.GetString(0);
            var purchaseId = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            var poNo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var supplier = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

            var cancelled = status == "cancelled";
            var approved = status is "approved" or "partial" or "received";
            var partial = status == "partial";
            var received = status == "received";
            var invoiced = purchaseId > 0 || received;

            var step = 1;
            if (approved) step = Math.Max(step, 2);
            if (partial) step = Math.Max(step, 3);
            var done = false;
            if (received) step = 4;
            if (invoiced && received)
            {
                step = 4;
                done = true;
            }

            return new PoFacts(poNo, supplier, approved, partial, received, invoiced, cancelled, step, done);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private sealed record PayFacts(string BatchNo, bool Submitted, bool Processed, bool Cancelled, int Step, bool Done);

    private async Task<PayFacts?> PayFactsAsync(DbConnection connection, long batchId, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT `status`, `batch_no` FROM `epc_erp_payment_batches` WHERE `id` = ? LIMIT 1");
            AddParam(cmd, batchId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var status = reader.IsDBNull(0) ? "draft" : reader.GetString(0);
            var batchNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var cancelled = status == "cancelled";
            var submitted = status is "submitted" or "processed";
            var processed = status == "processed";
            var step = submitted ? 2 : 1;
            var done = false;
            if (processed)
            {
                step = 3;
                done = true;
            }

            return new PayFacts(batchNo, submitted, processed, cancelled, step, done);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private sealed record ExpFacts(string ReportNo, long StaffId, string Title, bool Submitted, bool Approved, bool Paid, bool Rejected, int Step, bool Done);

    private async Task<ExpFacts?> ExpFactsAsync(DbConnection connection, long reportId, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT `status`, `report_no`, `staff_user_id`, `title` FROM `epc_erp_expense_reports` WHERE `id` = ? LIMIT 1");
            AddParam(cmd, reportId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var status = reader.IsDBNull(0) ? "draft" : reader.GetString(0);
            var reportNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var staffId = reader.IsDBNull(2) ? 0 : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
            var title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var rejected = status == "rejected";
            var submitted = status is "submitted" or "approved" or "paid";
            var approved = status is "approved" or "paid";
            var paid = status == "paid";
            var step = approved ? 2 : 1;
            var done = false;
            if (paid)
            {
                step = 3;
                done = true;
            }

            return new ExpFacts(reportNo, staffId, title, submitted, approved, paid, rejected, step, done);
        }
        catch (DbException)
        {
            return null;
        }
    }

    /* ---------- helpers ---------- */

    private async Task<(long Id, string Status)?> FindCaseAsync(
        DbConnection connection, string subjectType, long subjectId, long processId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT `id`, `status` FROM `epc_pf_cases` WHERE `subject_type` = ? AND `subject_id` = ? AND `process_id` = ? ORDER BY `id` LIMIT 1");
        AddParam(cmd, subjectType);
        AddParam(cmd, subjectId);
        AddParam(cmd, processId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
    }

    private static async Task<long?> CaseCurrentStepAsync(DbConnection connection, long caseId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT `current_step_no`, `status` FROM `epc_pf_cases` WHERE `id` = ?");
        AddParam(cmd, caseId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var status = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        if (status != "open")
        {
            return null;
        }

        return reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
    }

    private static async Task<long> ActiveStepAssigneeAsync(DbConnection connection, long caseId, long stepNo, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `assignee_id` FROM `epc_pf_case_steps` WHERE `case_id` = ? AND `step_no` = ? AND `status` = 'active' LIMIT 1"),
                cancellationToken,
                caseId,
                stepNo).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static async Task InsertStaffAsync(
        DbConnection connection,
        long userId,
        string deptCode,
        string name,
        string title,
        string email,
        string phone,
        string location,
        string businessUnit,
        string legalEntity,
        long now,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_staff_profiles` (`user_id`,`department_code`,`display_name`,`job_title`,`email`,`phone`,`location`,`business_unit`,`legal_entity`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,1,?)"),
            cancellationToken,
            userId, deptCode, name, title, email, phone, location, businessUnit, legalEntity, now).ConfigureAwait(false);
    }

    private static string BuForDept(string deptCode)
    {
        return deptCode switch
        {
            "sales" or "marketing" => "Retail BU",
            "purchase" or "logistics" => "Distribution BU",
            _ => "Corporate BU",
        };
    }

    private static string LegalEntityForLocation(string location)
        => location is "Sharjah Branch" or "Jebel Ali Warehouse" ? "ECOM AE FZE" : "ECOM AE Trading LLC";

    private static async Task<bool> ProvisionedAsync(DbConnection connection, CancellationToken cancellationToken)
        => await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_processes", "name", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_steps", "process_id", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_cases", "process_id", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_case_steps", "case_id", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_dept_heads", "department_code", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_erp_staff_profiles", "email", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_erp_staff_profiles", "location", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_erp_staff_profiles", "business_unit", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_erp_staff_profiles", "legal_entity", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_erp_payment_batches", "notes", cancellationToken).ConfigureAwait(false)
            && await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_erp_expense_reports", "notes", cancellationToken).ConfigureAwait(false);

    private static async Task<List<long>> IdsAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return ids;
    }

    private static async Task<List<long>> IdsParamAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] parameters)
    {
        var ids = new List<long>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters)
        {
            AddParam(cmd, p);
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return ids;
    }

    private static void AddParam(DbCommand cmd, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = "@p" + cmd.Parameters.Count.ToString(CultureInfo.InvariantCulture);
        p.Value = value is null ? DBNull.Value : value;
        cmd.Parameters.Add(p);
    }

    private static readonly (string Name, string Description, (string Name, string Type, string Department, int SlaHours)[] Steps)[] DemoBlueprints =
    {
        ("Purchase requisition approval",
            "Requisition raised by a department, reviewed by its head, sourced by Purchase, signed off by Finance.",
            new[]
            {
                ("Raise requisition", "department", "sales", 8),
                ("Department head review", "dept_head", "sales", 24),
                ("Source & raise PO", "dept_head", "purchase", 48),
                ("Finance sign-off", "dept_head", "finance", 24),
            }),
        ("Customer complaint resolution",
            "Complaint logged, triaged by Sales head, resolved by Logistics, closed by Sales.",
            new[]
            {
                ("Log complaint", "department", "sales", 4),
                ("Sales head triage", "dept_head", "sales", 12),
                ("Logistics resolution", "dept_head", "logistics", 48),
                ("Confirm & close", "initiator", "sales", 12),
            }),
        ("New staff onboarding",
            "HR initiates, department head confirms role, IT grants access, Finance sets up payroll.",
            new[]
            {
                ("HR initiate onboarding", "dept_head", "hr", 24),
                ("Department head confirm role", "dept_head", "sales", 24),
                ("IT grant ERP/CP access", "dept_head", "it", 24),
                ("Finance set up payroll", "dept_head", "finance", 24),
            }),
        ("Customer credit request",
            "Customer asks for a credit limit. Sales logs it, Sales head reviews, Finance runs a credit check, Finance head approves the limit, Sales confirms back to the customer.",
            new[]
            {
                ("Sales log credit request", "department", "sales", 4),
                ("Sales head review", "dept_head", "sales", 12),
                ("Finance credit check", "department", "finance", 24),
                ("Finance head approve limit", "dept_head", "finance", 24),
                ("Sales confirm to customer", "initiator", "sales", 8),
            }),
        ("Goods delivery to customer",
            "Sales raises the delivery, Logistics arranges it, a driver dispatches and delivers, goods are unloaded, the customer confirms, and Sales records the signed delivery-note receipt.",
            new[]
            {
                ("Sales raise delivery order", "department", "sales", 6),
                ("Logistics arrange delivery", "dept_head", "logistics", 12),
                ("Driver dispatch & drive", "department", "logistics", 24),
                ("Unload goods at customer", "department", "logistics", 6),
                ("Customer confirms receipt", "initiator", "sales", 12),
                ("Sales record delivery-note receipt", "dept_head", "sales", 8),
            }),
    };

    private static readonly (string Title, string Priority, int ProcessIndex, int Advance)[] DemoCases =
    {
        ("Restock packaging materials", "high", 0, 0),
        ("Office laptops requisition", "normal", 0, 1),
        ("Damaged shipment — order #10231", "urgent", 1, 2),
        ("Late delivery complaint — order #10244", "high", 1, 3),
        ("Onboard A. Khan (Sales exec)", "normal", 2, 0),
        ("Onboard R. Mehta (Warehouse)", "normal", 2, 1),
        ("Spare parts purchase — Abu Dhabi", "high", 0, 2),
        ("Wrong item delivered — order #10310", "urgent", 1, 3),
        ("Onboard S. Patel (Finance)", "normal", 2, 0),
        ("Forklift maintenance requisition", "normal", 0, 1),
        ("Refund dispute — order #10355", "high", 1, 2),
        ("Onboard D. Joseph (IT support)", "low", 2, 3),
        ("Credit request — Gulf Spare Parts LLC", "high", 3, 2),
        ("Credit request — Al Noor Motors", "normal", 3, 4),
        ("Delivery — order #10410 to Sharjah", "urgent", 4, 3),
        ("Delivery — order #10422 to Al Ain", "high", 4, 5),
    };
}
