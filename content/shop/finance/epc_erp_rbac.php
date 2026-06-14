<?php
/**
 * Platform — role-based security (D365 F&O-style privileges → duties → roles).
 *
 * Mirrors the D365 security hierarchy:
 *   privilege  : granular access to a function at an access level
 *                (read < update < create < delete < full)
 *   duty       : a job task = a set of privileges
 *   role       : a job position = a set of duties (+ optional direct privileges)
 *   assignment : user → role
 *
 * Effective access is resolved by flattening a user's roles → duties →
 * privileges. The flatten + access-rank + can-check helpers are pure so the
 * security logic is deterministic and unit-testable. Multi-company aware.
 * Additive only (complements epc_erp_security.php request headers).
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_rbac_levels')) {
    /** @return array<string,int> access level -> rank */
    function epc_rbac_levels(): array
    {
        return array('read' => 1, 'update' => 2, 'create' => 3, 'delete' => 4, 'full' => 5);
    }
}

if (!function_exists('epc_rbac_ensure_schema')) {
    function epc_rbac_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rbac_privilege` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(60) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `access_level` varchar(8) NOT NULL DEFAULT 'read',
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Security privileges'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rbac_duty` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(60) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Security duties'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rbac_duty_priv` (
            `duty_id` int(11) NOT NULL DEFAULT 0,
            `privilege_id` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`duty_id`,`privilege_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Duty -> privilege'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rbac_role` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(60) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Security roles'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rbac_role_duty` (
            `role_id` int(11) NOT NULL DEFAULT 0,
            `duty_id` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`role_id`,`duty_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Role -> duty'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rbac_user_role` (
            `company_id` int(11) NOT NULL DEFAULT 0,
            `user_id` int(11) NOT NULL DEFAULT 0,
            `role_id` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`company_id`,`user_id`,`role_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User -> role'");
    }
}

/* ---------------- pure resolution ---------------- */

if (!function_exists('epc_rbac_access_rank')) {
    function epc_rbac_access_rank(string $level): int
    {
        $l = epc_rbac_levels();
        return $l[$level] ?? 0;
    }
}

if (!function_exists('epc_rbac_effective')) {
    /**
     * Pure: flatten a user's assigned roles into an effective privilege map.
     *
     * @param array<int,int> $assignedRoles role ids
     * @param array<int,array<int,int>> $roleToDuties role_id -> [duty_id]
     * @param array<int,array<int,int>> $dutyToPrivs duty_id -> [privilege_id]
     * @param array<int,array{code:string,access_level:string}> $privMeta privilege_id -> meta
     * @return array<string,string> privilege code -> highest access level
     */
    function epc_rbac_effective(array $assignedRoles, array $roleToDuties, array $dutyToPrivs, array $privMeta): array
    {
        $out = array();
        foreach ($assignedRoles as $roleId) {
            foreach ($roleToDuties[$roleId] ?? array() as $dutyId) {
                foreach ($dutyToPrivs[$dutyId] ?? array() as $privId) {
                    $meta = $privMeta[$privId] ?? null;
                    if ($meta === null) {
                        continue;
                    }
                    $code = (string) $meta['code'];
                    $lvl = (string) $meta['access_level'];
                    if (!isset($out[$code]) || epc_rbac_access_rank($lvl) > epc_rbac_access_rank($out[$code])) {
                        $out[$code] = $lvl;
                    }
                }
            }
        }
        ksort($out);
        return $out;
    }
}

if (!function_exists('epc_rbac_can')) {
    /**
     * Pure: does the effective privilege map grant >= needed access to code?
     *
     * @param array<string,string> $privMap code -> level
     */
    function epc_rbac_can(array $privMap, string $code, string $needed = 'read'): bool
    {
        if (!isset($privMap[$code])) {
            return false;
        }
        return epc_rbac_access_rank($privMap[$code]) >= epc_rbac_access_rank($needed);
    }
}

/* ---------------- privileges ---------------- */

if (!function_exists('epc_rbac_privilege_save')) {
    /** @param array<string,mixed> $data */
    function epc_rbac_privilege_save(PDO $db, int $companyId, array $data): int
    {
        epc_rbac_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        if ($code === '') {
            throw new Exception('Privilege code is required');
        }
        $level = (string) ($data['access_level'] ?? 'read');
        if (!isset(epc_rbac_levels()[$level])) {
            throw new Exception('Invalid access level');
        }
        $db->prepare("INSERT INTO `epc_rbac_privilege` (`company_id`,`code`,`name`,`access_level`) VALUES (?,?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `access_level`=VALUES(`access_level`)")
           ->execute(array($companyId, $code, (string) ($data['name'] ?? ''), $level));
        $st = $db->prepare("SELECT id FROM `epc_rbac_privilege` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_rbac_privileges')) {
    /** @return array<int,array<string,mixed>> */
    function epc_rbac_privileges(PDO $db, int $companyId = 0): array
    {
        epc_rbac_ensure_schema($db);
        $sql = "SELECT * FROM `epc_rbac_privilege` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY code";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- duties ---------------- */

if (!function_exists('epc_rbac_duty_save')) {
    /** @param array<string,mixed> $data */
    function epc_rbac_duty_save(PDO $db, int $companyId, array $data): int
    {
        epc_rbac_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        if ($code === '') {
            throw new Exception('Duty code is required');
        }
        $db->prepare("INSERT INTO `epc_rbac_duty` (`company_id`,`code`,`name`) VALUES (?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`)")
           ->execute(array($companyId, $code, (string) ($data['name'] ?? '')));
        $st = $db->prepare("SELECT id FROM `epc_rbac_duty` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_rbac_duty_attach_priv')) {
    function epc_rbac_duty_attach_priv(PDO $db, int $dutyId, int $privilegeId, bool $attach = true): void
    {
        epc_rbac_ensure_schema($db);
        if ($attach) {
            $db->prepare("INSERT IGNORE INTO `epc_rbac_duty_priv` (`duty_id`,`privilege_id`) VALUES (?,?)")->execute(array($dutyId, $privilegeId));
        } else {
            $db->prepare("DELETE FROM `epc_rbac_duty_priv` WHERE duty_id=? AND privilege_id=?")->execute(array($dutyId, $privilegeId));
        }
    }
}

if (!function_exists('epc_rbac_duties')) {
    /** @return array<int,array<string,mixed>> */
    function epc_rbac_duties(PDO $db, int $companyId = 0): array
    {
        epc_rbac_ensure_schema($db);
        $sql = "SELECT * FROM `epc_rbac_duty` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY code";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['priv_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_rbac_duty_priv` WHERE duty_id=" . (int) $r['id'])->fetchColumn();
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_rbac_duty_privileges')) {
    /** @return array<int,int> privilege ids */
    function epc_rbac_duty_privileges(PDO $db, int $dutyId): array
    {
        epc_rbac_ensure_schema($db);
        $st = $db->prepare("SELECT privilege_id FROM `epc_rbac_duty_priv` WHERE duty_id=?");
        $st->execute(array($dutyId));
        return array_map('intval', $st->fetchAll(PDO::FETCH_COLUMN));
    }
}

/* ---------------- roles ---------------- */

if (!function_exists('epc_rbac_role_save')) {
    /** @param array<string,mixed> $data */
    function epc_rbac_role_save(PDO $db, int $companyId, array $data): int
    {
        epc_rbac_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        if ($code === '') {
            throw new Exception('Role code is required');
        }
        $db->prepare("INSERT INTO `epc_rbac_role` (`company_id`,`code`,`name`) VALUES (?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`)")
           ->execute(array($companyId, $code, (string) ($data['name'] ?? '')));
        $st = $db->prepare("SELECT id FROM `epc_rbac_role` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_rbac_role_attach_duty')) {
    function epc_rbac_role_attach_duty(PDO $db, int $roleId, int $dutyId, bool $attach = true): void
    {
        epc_rbac_ensure_schema($db);
        if ($attach) {
            $db->prepare("INSERT IGNORE INTO `epc_rbac_role_duty` (`role_id`,`duty_id`) VALUES (?,?)")->execute(array($roleId, $dutyId));
        } else {
            $db->prepare("DELETE FROM `epc_rbac_role_duty` WHERE role_id=? AND duty_id=?")->execute(array($roleId, $dutyId));
        }
    }
}

if (!function_exists('epc_rbac_roles')) {
    /** @return array<int,array<string,mixed>> */
    function epc_rbac_roles(PDO $db, int $companyId = 0): array
    {
        epc_rbac_ensure_schema($db);
        $sql = "SELECT * FROM `epc_rbac_role` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY code";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['duty_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_rbac_role_duty` WHERE role_id=" . (int) $r['id'])->fetchColumn();
            $r['user_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_rbac_user_role` WHERE role_id=" . (int) $r['id'])->fetchColumn();
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_rbac_role_duties')) {
    /** @return array<int,int> duty ids */
    function epc_rbac_role_duties(PDO $db, int $roleId): array
    {
        epc_rbac_ensure_schema($db);
        $st = $db->prepare("SELECT duty_id FROM `epc_rbac_role_duty` WHERE role_id=?");
        $st->execute(array($roleId));
        return array_map('intval', $st->fetchAll(PDO::FETCH_COLUMN));
    }
}

/* ---------------- user assignment ---------------- */

if (!function_exists('epc_rbac_user_assign_role')) {
    function epc_rbac_user_assign_role(PDO $db, int $companyId, int $userId, int $roleId, bool $assign = true): void
    {
        epc_rbac_ensure_schema($db);
        if ($assign) {
            $db->prepare("INSERT IGNORE INTO `epc_rbac_user_role` (`company_id`,`user_id`,`role_id`) VALUES (?,?,?)")->execute(array($companyId, $userId, $roleId));
        } else {
            $db->prepare("DELETE FROM `epc_rbac_user_role` WHERE company_id=? AND user_id=? AND role_id=?")->execute(array($companyId, $userId, $roleId));
        }
    }
}

if (!function_exists('epc_rbac_user_roles')) {
    /** @return array<int,int> role ids */
    function epc_rbac_user_roles(PDO $db, int $companyId, int $userId): array
    {
        epc_rbac_ensure_schema($db);
        $st = $db->prepare("SELECT role_id FROM `epc_rbac_user_role` WHERE company_id=? AND user_id=?");
        $st->execute(array($companyId, $userId));
        return array_map('intval', $st->fetchAll(PDO::FETCH_COLUMN));
    }
}

if (!function_exists('epc_rbac_user_privileges')) {
    /**
     * DB-driven effective access for a user: resolves roles → duties →
     * privileges via the pure flatten helper.
     *
     * @return array<string,string> privilege code -> highest access level
     */
    function epc_rbac_user_privileges(PDO $db, int $companyId, int $userId): array
    {
        epc_rbac_ensure_schema($db);
        $roles = epc_rbac_user_roles($db, $companyId, $userId);
        if (empty($roles)) {
            return array();
        }
        $roleToDuties = array();
        foreach ($roles as $rid) {
            $roleToDuties[$rid] = epc_rbac_role_duties($db, $rid);
        }
        $dutyToPrivs = array();
        foreach ($roleToDuties as $duties) {
            foreach ($duties as $did) {
                if (!isset($dutyToPrivs[$did])) {
                    $dutyToPrivs[$did] = epc_rbac_duty_privileges($db, $did);
                }
            }
        }
        $privMeta = array();
        foreach (epc_rbac_privileges($db, $companyId) as $p) {
            $privMeta[(int) $p['id']] = array('code' => (string) $p['code'], 'access_level' => (string) $p['access_level']);
        }
        return epc_rbac_effective($roles, $roleToDuties, $dutyToPrivs, $privMeta);
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_rbac_summary')) {
    /** @return array{privileges:int,duties:int,roles:int,assignments:int} */
    function epc_rbac_summary(PDO $db, int $companyId = 0): array
    {
        epc_rbac_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        return array(
            'privileges' => (int) $db->query("SELECT COUNT(*) FROM `epc_rbac_privilege` WHERE 1=1" . $wa)->fetchColumn(),
            'duties' => (int) $db->query("SELECT COUNT(*) FROM `epc_rbac_duty` WHERE 1=1" . $wa)->fetchColumn(),
            'roles' => (int) $db->query("SELECT COUNT(*) FROM `epc_rbac_role` WHERE 1=1" . $wa)->fetchColumn(),
            'assignments' => (int) $db->query("SELECT COUNT(*) FROM `epc_rbac_user_role` WHERE 1=1" . $wa)->fetchColumn(),
        );
    }
}
