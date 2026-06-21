<?php
/**
 * Advanced ERP — three-tier control layer.
 *
 *   1. USER controls     — per-user preferences (language, default branch /
 *                          warehouse, landing dashboard, items-per-page) layered
 *                          on top of the governance role/permission checks.
 *   2. ADMIN controls    — tenant admin console summary: licensed modules,
 *                          roles, org structure, subscription expiry for THIS
 *                          tenant (their own company only).
 *   3. PROVIDER controls — operator / super-admin console that runs per-tenant
 *                          on each tenant's OWN database (never co-mingled):
 *                          provision plan, set expiry, suspend / reactivate,
 *                          and a fleet roll-up assembled tenant-by-tenant.
 *
 * Isolation: every function takes the per-tenant $db handed in by the
 * framework. The provider fleet helper is given a *map of separate PDO
 * connections* (one per tenant) and queries each independently — there is no
 * shared cross-tenant table, so one tenant can never see another's rows.
 *
 * Additive (new prefs/lifecycle tables only), entitlement-aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_ctl_ensure_schema')) {
    function epc_ctl_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ctl_user_prefs` (
            `user_id` int(11) NOT NULL,
            `lang` varchar(8) NOT NULL DEFAULT '',
            `branch_id` int(11) NOT NULL DEFAULT 0,
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `landing` varchar(64) NOT NULL DEFAULT '',
            `page_size` int(11) NOT NULL DEFAULT 25,
            `prefs_json` text,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`user_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-user ERP preferences'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ctl_tenant` (
            `id` tinyint(1) NOT NULL DEFAULT 1,
            `tenant_code` varchar(64) NOT NULL DEFAULT '',
            `display_name` varchar(160) NOT NULL DEFAULT '',
            `plan` varchar(40) NOT NULL DEFAULT '',
            `status` varchar(16) NOT NULL DEFAULT 'active',
            `expires_at` int(11) NOT NULL DEFAULT 0,
            `provisioned_at` int(11) NOT NULL DEFAULT 0,
            `note` varchar(255) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Single-row tenant lifecycle (this DB = this tenant)'");
    }
}

/* =========================== 1. USER controls ========================= */

if (!function_exists('epc_ctl_user_prefs_save')) {
    /**
     * Save / merge a user's preferences (idempotent upsert).
     *
     * @param array<string,mixed> $prefs lang, branch_id, warehouse_id, landing, page_size, extra[]
     * @return array<string,mixed> the stored prefs
     */
    function epc_ctl_user_prefs_save(PDO $db, int $userId, array $prefs): array
    {
        epc_ctl_ensure_schema($db);
        $cur = epc_ctl_user_prefs_get($db, $userId);
        $merged = array(
            'lang' => (string) ($prefs['lang'] ?? $cur['lang']),
            'branch_id' => (int) ($prefs['branch_id'] ?? $cur['branch_id']),
            'warehouse_id' => (int) ($prefs['warehouse_id'] ?? $cur['warehouse_id']),
            'landing' => (string) ($prefs['landing'] ?? $cur['landing']),
            'page_size' => (int) ($prefs['page_size'] ?? $cur['page_size']),
            'extra' => isset($prefs['extra']) && is_array($prefs['extra']) ? $prefs['extra'] : $cur['extra'],
        );
        if ($merged['page_size'] < 1) {
            $merged['page_size'] = 25;
        }
        $db->prepare("INSERT INTO `epc_ctl_user_prefs` (`user_id`,`lang`,`branch_id`,`warehouse_id`,`landing`,`page_size`,`prefs_json`,`time_updated`)
                      VALUES (?,?,?,?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `lang`=VALUES(`lang`),`branch_id`=VALUES(`branch_id`),`warehouse_id`=VALUES(`warehouse_id`),`landing`=VALUES(`landing`),`page_size`=VALUES(`page_size`),`prefs_json`=VALUES(`prefs_json`),`time_updated`=VALUES(`time_updated`)")
           ->execute(array($userId, $merged['lang'], $merged['branch_id'], $merged['warehouse_id'], $merged['landing'], $merged['page_size'], json_encode($merged['extra']), time()));
        return $merged;
    }
}

if (!function_exists('epc_ctl_user_prefs_get')) {
    /**
     * Read a user's preferences, returning sane defaults when none stored.
     *
     * @return array<string,mixed>
     */
    function epc_ctl_user_prefs_get(PDO $db, int $userId): array
    {
        epc_ctl_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_ctl_user_prefs` WHERE `user_id`=?");
        $st->execute(array($userId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return array('lang' => '', 'branch_id' => 0, 'warehouse_id' => 0, 'landing' => '', 'page_size' => 25, 'extra' => array());
        }
        $extra = array();
        if (!empty($row['prefs_json'])) {
            $d = json_decode((string) $row['prefs_json'], true);
            if (is_array($d)) {
                $extra = $d;
            }
        }
        return array(
            'lang' => (string) $row['lang'],
            'branch_id' => (int) $row['branch_id'],
            'warehouse_id' => (int) $row['warehouse_id'],
            'landing' => (string) $row['landing'],
            'page_size' => (int) $row['page_size'],
            'extra' => $extra,
        );
    }
}

if (!function_exists('epc_ctl_user_console')) {
    /**
     * Build the data a single end-user's home needs: their effective
     * permissions, unread notifications and saved preferences. Pure assembly
     * over the governance layer (so permission logic stays in one place).
     *
     * @return array<string,mixed> {permissions, unread, prefs}
     */
    function epc_ctl_user_console(PDO $db, int $userId): array
    {
        $perms = function_exists('epc_gov_user_permissions') ? epc_gov_user_permissions($db, $userId) : array();
        $unread = function_exists('epc_ntf_unread_count') ? epc_ntf_unread_count($db, $userId) : 0;
        return array(
            'permissions' => $perms,
            'unread' => (int) $unread,
            'prefs' => epc_ctl_user_prefs_get($db, $userId),
        );
    }
}

/* =========================== 2. ADMIN controls ======================= */

if (!function_exists('epc_ctl_admin_console')) {
    /**
     * Tenant-admin overview for THIS tenant only: which modules are licensed,
     * how many roles/users exist, and subscription status. Reads only this
     * tenant's DB.
     *
     * @return array<string,mixed> {modules_enabled, modules_total, roles, plan, status, expires_at, expired}
     */
    function epc_ctl_admin_console(PDO $db): array
    {
        epc_ctl_ensure_schema($db);
        $enabled = function_exists('epc_mod_enabled_list') ? epc_mod_enabled_list($db) : array();
        $total = function_exists('epc_mod_registry') ? count(epc_mod_registry()) : 0;
        $roles = 0;
        try {
            $roles = (int) $db->query("SELECT COUNT(*) FROM `epc_gov_roles`")->fetchColumn();
        } catch (Exception $e) {
        }
        $life = epc_ctl_tenant_get($db);
        return array(
            'modules_enabled' => count($enabled),
            'modules_list' => $enabled,
            'modules_total' => $total,
            'roles' => $roles,
            'plan' => $life['plan'],
            'status' => $life['status'],
            'expires_at' => $life['expires_at'],
            'expired' => $life['expires_at'] > 0 && $life['expires_at'] < time(),
        );
    }
}

/* ========================= 3. PROVIDER controls ====================== */

if (!function_exists('epc_ctl_tenant_get')) {
    /**
     * Read this tenant's lifecycle record (single row). Defaults when unset.
     *
     * @return array<string,mixed>
     */
    function epc_ctl_tenant_get(PDO $db): array
    {
        epc_ctl_ensure_schema($db);
        $row = $db->query("SELECT * FROM `epc_ctl_tenant` WHERE `id`=1")->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return array('tenant_code' => '', 'display_name' => '', 'plan' => '', 'status' => 'active', 'expires_at' => 0, 'provisioned_at' => 0, 'note' => '');
        }
        return array(
            'tenant_code' => (string) $row['tenant_code'],
            'display_name' => (string) $row['display_name'],
            'plan' => (string) $row['plan'],
            'status' => (string) $row['status'],
            'expires_at' => (int) $row['expires_at'],
            'provisioned_at' => (int) $row['provisioned_at'],
            'note' => (string) $row['note'],
        );
    }
}

if (!function_exists('epc_ctl_provision_tenant')) {
    /**
     * Operator action: provision / re-plan THIS tenant — record identity, apply
     * the entitlement bundle (exclusive, so they get exactly the plan), set
     * expiry, mark active. Idempotent.
     *
     * @param array<string,mixed> $spec tenant_code, display_name, plan(bundle), expires_at, note
     * @return array<string,mixed> {plan, status, modules}
     */
    function epc_ctl_provision_tenant(PDO $db, array $spec): array
    {
        epc_ctl_ensure_schema($db);
        $plan = (string) ($spec['plan'] ?? 'full');
        $expires = (int) ($spec['expires_at'] ?? 0);
        $modules = array();
        if (function_exists('epc_mod_apply_bundle')) {
            $modules = epc_mod_apply_bundle($db, $plan, true, $expires);
        }
        $db->prepare("INSERT INTO `epc_ctl_tenant` (`id`,`tenant_code`,`display_name`,`plan`,`status`,`expires_at`,`provisioned_at`,`note`)
                      VALUES (1,?,?,?, 'active', ?, ?, ?)
                      ON DUPLICATE KEY UPDATE `tenant_code`=VALUES(`tenant_code`),`display_name`=VALUES(`display_name`),`plan`=VALUES(`plan`),`status`='active',`expires_at`=VALUES(`expires_at`),`note`=VALUES(`note`)")
           ->execute(array(
               (string) ($spec['tenant_code'] ?? ''),
               (string) ($spec['display_name'] ?? ''),
               $plan,
               $expires,
               time(),
               (string) ($spec['note'] ?? ''),
           ));
        return array('plan' => $plan, 'status' => 'active', 'modules' => $modules);
    }
}

if (!function_exists('epc_ctl_set_tenant_status')) {
    /**
     * Operator action: suspend / reactivate THIS tenant. Suspending does not
     * delete any data — it only flips the status flag the app gate reads.
     *
     * @return string the new status
     */
    function epc_ctl_set_tenant_status(PDO $db, string $status): string
    {
        epc_ctl_ensure_schema($db);
        $status = in_array($status, array('active', 'suspended'), true) ? $status : 'active';
        // Ensure a row exists, then update.
        $db->exec("INSERT IGNORE INTO `epc_ctl_tenant` (`id`,`status`,`provisioned_at`) VALUES (1,'active'," . time() . ")");
        $db->prepare("UPDATE `epc_ctl_tenant` SET `status`=? WHERE `id`=1")->execute(array($status));
        return $status;
    }
}

if (!function_exists('epc_ctl_tenant_active')) {
    /**
     * App gate: is THIS tenant active and not expired? Used to block access for
     * a suspended / lapsed subscription.
     */
    function epc_ctl_tenant_active(PDO $db): bool
    {
        $t = epc_ctl_tenant_get($db);
        if ($t['status'] !== 'active') {
            return false;
        }
        if ($t['expires_at'] > 0 && $t['expires_at'] < time()) {
            return false;
        }
        return true;
    }
}

if (!function_exists('epc_ctl_fleet_overview')) {
    /**
     * Operator fleet roll-up across MANY tenants. $connections is a map of
     * tenantKey => PDO (each a SEPARATE tenant database). Each tenant is
     * queried independently on its own connection — there is no shared table
     * and no cross-tenant SQL, preserving isolation.
     *
     * @param array<string,PDO> $connections
     * @return array<string,mixed> {tenants:[...], count, active, suspended, expired}
     */
    function epc_ctl_fleet_overview(array $connections): array
    {
        $tenants = array();
        $active = 0;
        $suspended = 0;
        $expired = 0;
        foreach ($connections as $key => $conn) {
            if (!($conn instanceof PDO)) {
                continue;
            }
            try {
                $t = epc_ctl_tenant_get($conn);
                $mods = function_exists('epc_mod_enabled_list') ? count(epc_mod_enabled_list($conn)) : 0;
                $isExpired = $t['expires_at'] > 0 && $t['expires_at'] < time();
                if ($t['status'] === 'suspended') {
                    $suspended++;
                } elseif ($isExpired) {
                    $expired++;
                } else {
                    $active++;
                }
                $tenants[] = array(
                    'key' => (string) $key,
                    'tenant_code' => $t['tenant_code'],
                    'display_name' => $t['display_name'],
                    'plan' => $t['plan'],
                    'status' => $t['status'],
                    'expires_at' => $t['expires_at'],
                    'expired' => $isExpired,
                    'modules' => $mods,
                );
            } catch (Exception $e) {
                $tenants[] = array('key' => (string) $key, 'error' => $e->getMessage());
            }
        }
        return array(
            'tenants' => $tenants,
            'count' => count($tenants),
            'active' => $active,
            'suspended' => $suspended,
            'expired' => $expired,
        );
    }
}
