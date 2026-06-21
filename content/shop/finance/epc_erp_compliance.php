<?php
/**
 * Advanced ERP — Tax Compliance Engine (date-effective, auto-updating).
 *
 * When a country changes its tax law (new VAT rate, new e-invoice schema, new
 * return layout, new thresholds/due dates) the ERP picks up the new rule from
 * its effective date — no code change, no redeploy.
 *
 *  - Versioned rules per (country, rule_key) with valid_from/valid_to.
 *  - Resolver: "the rule in force on date X" — old documents keep the old rule,
 *    new ones use the new one automatically.
 *  - FTA / regulator autofetch: a fetched rule-set is STAGED as a pending
 *    version with a diff (old -> new) and applied on accept (or auto-apply per
 *    tenant) — a bad feed can never silently corrupt live books.
 *  - Audit + rollback of every version.
 *
 * Tax calc, the VAT return, e-invoicing and corporate tax all read through this
 * engine, so a change propagates everywhere at once.
 *
 * Additive: new epc_cmp_* tables. Tenant-isolated.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_cmp_ensure_schema')) {
    function epc_cmp_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cmp_rules` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `country` varchar(2) NOT NULL DEFAULT '',
            `rule_key` varchar(60) NOT NULL DEFAULT '',
            `value_json` mediumtext,
            `valid_from` int(11) NOT NULL DEFAULT 0,
            `valid_to` int(11) NOT NULL DEFAULT 0,
            `version` int(11) NOT NULL DEFAULT 1,
            `status` varchar(12) NOT NULL DEFAULT 'active',
            `source` varchar(40) NOT NULL DEFAULT 'manual',
            `note` varchar(255) DEFAULT NULL,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_lookup` (`country`,`rule_key`,`status`,`valid_from`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Date-effective tax compliance rules'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cmp_staging` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `country` varchar(2) NOT NULL DEFAULT '',
            `rule_key` varchar(60) NOT NULL DEFAULT '',
            `value_json` mediumtext,
            `valid_from` int(11) NOT NULL DEFAULT 0,
            `source` varchar(40) NOT NULL DEFAULT 'fta',
            `status` varchar(12) NOT NULL DEFAULT 'pending',
            `fetched_at` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_pending` (`country`,`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Staged regulator updates (pending apply)'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cmp_audit` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `country` varchar(2) NOT NULL DEFAULT '',
            `rule_key` varchar(60) NOT NULL DEFAULT '',
            `action` varchar(20) NOT NULL DEFAULT '',
            `detail` mediumtext,
            `actor` varchar(80) DEFAULT NULL,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_country` (`country`,`rule_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Compliance change audit'");
    }
}

if (!function_exists('epc_cmp_log')) {
    function epc_cmp_log(PDO $db, string $country, string $ruleKey, string $action, $detail, string $actor = ''): void
    {
        $db->prepare("INSERT INTO `epc_cmp_audit` (`country`,`rule_key`,`action`,`detail`,`actor`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array($country, $ruleKey, $action, is_string($detail) ? $detail : json_encode($detail), $actor, time()));
    }
}

/* ----------------------- Rule set + resolution ------------------------ */

if (!function_exists('epc_cmp_set_rule')) {
    /**
     * Add a new version of a rule effective from $validFrom. Closes the prior
     * active version (sets its valid_to to validFrom-1) so versions don't
     * overlap. Returns the new rule id.
     *
     * @param mixed $value scalar or array stored as JSON
     */
    function epc_cmp_set_rule(PDO $db, string $country, string $ruleKey, $value, int $validFrom, string $source = 'manual', string $note = '', string $actor = ''): int
    {
        epc_cmp_ensure_schema($db);
        // Close the currently-open version (valid_to=0) that starts on/before this one.
        $db->prepare("UPDATE `epc_cmp_rules` SET `valid_to`=? WHERE `country`=? AND `rule_key`=? AND `status`='active' AND `valid_to`=0 AND `valid_from`<=?")
           ->execute(array($validFrom - 1, $country, $ruleKey, $validFrom));
        $verRow = $db->prepare("SELECT COALESCE(MAX(`version`),0)+1 FROM `epc_cmp_rules` WHERE `country`=? AND `rule_key`=?");
        $verRow->execute(array($country, $ruleKey));
        $version = (int) $verRow->fetchColumn();
        $db->prepare("INSERT INTO `epc_cmp_rules` (`country`,`rule_key`,`value_json`,`valid_from`,`valid_to`,`version`,`status`,`source`,`note`,`time_created`) VALUES (?,?,?,?,0,?, 'active', ?, ?, ?)")
           ->execute(array($country, $ruleKey, json_encode($value), $validFrom, $version, $source, $note, time()));
        $id = (int) $db->lastInsertId();
        epc_cmp_log($db, $country, $ruleKey, 'set_rule', array('version' => $version, 'valid_from' => $validFrom, 'value' => $value, 'source' => $source), $actor);
        return $id;
    }
}

if (!function_exists('epc_cmp_resolve')) {
    /**
     * Resolve the rule value in force on $atDate. Returns decoded value or
     * $default if none applies.
     *
     * @return mixed
     */
    function epc_cmp_resolve(PDO $db, string $country, string $ruleKey, int $atDate = 0, $default = null)
    {
        epc_cmp_ensure_schema($db);
        $atDate = $atDate > 0 ? $atDate : time();
        $st = $db->prepare("SELECT `value_json` FROM `epc_cmp_rules`
                            WHERE `country`=? AND `rule_key`=? AND `status`='active'
                              AND `valid_from`<=?
                              AND (`valid_to`=0 OR `valid_to`>=?)
                            ORDER BY `valid_from` DESC LIMIT 1");
        $st->execute(array($country, $ruleKey, $atDate, $atDate));
        $val = $st->fetchColumn();
        if ($val === false) {
            return $default;
        }
        return json_decode((string) $val, true);
    }
}

if (!function_exists('epc_cmp_history')) {
    /**
     * Full version history for a rule (newest first).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_cmp_history(PDO $db, string $country, string $ruleKey): array
    {
        epc_cmp_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_cmp_rules` WHERE `country`=? AND `rule_key`=? ORDER BY `version` DESC");
        $st->execute(array($country, $ruleKey));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ------------------- FTA / regulator autofetch staging ---------------- */

if (!function_exists('epc_cmp_stage_update')) {
    /**
     * Stage a fetched regulator update (does NOT apply it). Returns a record
     * including the diff against the value currently in force.
     *
     * @param mixed $value
     * @return array<string,mixed> {staging_id, country, rule_key, old, new, changed}
     */
    function epc_cmp_stage_update(PDO $db, string $country, string $ruleKey, $value, int $validFrom, string $source = 'fta'): array
    {
        epc_cmp_ensure_schema($db);
        $current = epc_cmp_resolve($db, $country, $ruleKey, $validFrom);
        $db->prepare("INSERT INTO `epc_cmp_staging` (`country`,`rule_key`,`value_json`,`valid_from`,`source`,`status`,`fetched_at`) VALUES (?,?,?,?,?, 'pending', ?)")
           ->execute(array($country, $ruleKey, json_encode($value), $validFrom, $source, time()));
        $id = (int) $db->lastInsertId();
        epc_cmp_log($db, $country, $ruleKey, 'staged', array('source' => $source, 'valid_from' => $validFrom, 'old' => $current, 'new' => $value));
        return array(
            'staging_id' => $id,
            'country' => $country,
            'rule_key' => $ruleKey,
            'old' => $current,
            'new' => $value,
            'changed' => json_encode($current) !== json_encode($value),
            'valid_from' => $validFrom,
            'source' => $source,
        );
    }
}

if (!function_exists('epc_cmp_pending_updates')) {
    /**
     * List staged-but-not-applied regulator updates (optionally per country).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_cmp_pending_updates(PDO $db, string $country = ''): array
    {
        epc_cmp_ensure_schema($db);
        if ($country !== '') {
            $st = $db->prepare("SELECT * FROM `epc_cmp_staging` WHERE `status`='pending' AND `country`=? ORDER BY `id` ASC");
            $st->execute(array($country));
        } else {
            $st = $db->query("SELECT * FROM `epc_cmp_staging` WHERE `status`='pending' ORDER BY `id` ASC");
        }
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_cmp_apply_staged')) {
    /**
     * Apply a staged update: promotes it to a new active, date-effective rule
     * version and marks the staging row applied. This is the "immediate change"
     * step the user accepts (or auto-apply can call it).
     *
     * @return array<string,mixed> {applied, rule_id}
     */
    function epc_cmp_apply_staged(PDO $db, int $stagingId, string $actor = ''): array
    {
        epc_cmp_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_cmp_staging` WHERE `id`=?");
        $st->execute(array($stagingId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            throw new Exception('Staging row not found');
        }
        if ((string) $row['status'] !== 'pending') {
            return array('applied' => false, 'rule_id' => 0, 'reason' => 'not_pending');
        }
        $value = json_decode((string) $row['value_json'], true);
        $ruleId = epc_cmp_set_rule($db, (string) $row['country'], (string) $row['rule_key'], $value, (int) $row['valid_from'], (string) $row['source'], 'auto-fetched', $actor);
        $db->prepare("UPDATE `epc_cmp_staging` SET `status`='applied' WHERE `id`=?")->execute(array($stagingId));
        epc_cmp_log($db, (string) $row['country'], (string) $row['rule_key'], 'applied', array('staging_id' => $stagingId, 'rule_id' => $ruleId), $actor);
        return array('applied' => true, 'rule_id' => $ruleId);
    }
}

if (!function_exists('epc_cmp_reject_staged')) {
    function epc_cmp_reject_staged(PDO $db, int $stagingId, string $actor = ''): void
    {
        epc_cmp_ensure_schema($db);
        $db->prepare("UPDATE `epc_cmp_staging` SET `status`='rejected' WHERE `id`=? AND `status`='pending'")->execute(array($stagingId));
        epc_cmp_log($db, '', '', 'rejected', array('staging_id' => $stagingId), $actor);
    }
}

if (!function_exists('epc_cmp_rollback_rule')) {
    /**
     * Roll back the latest version of a rule: retire it and re-open the prior
     * version (clear its valid_to). Safe for live tenants.
     *
     * @return array<string,mixed> {rolled_back, reactivated_version}
     */
    function epc_cmp_rollback_rule(PDO $db, string $country, string $ruleKey, string $actor = ''): array
    {
        epc_cmp_ensure_schema($db);
        $latest = $db->prepare("SELECT * FROM `epc_cmp_rules` WHERE `country`=? AND `rule_key`=? AND `status`='active' ORDER BY `version` DESC LIMIT 1");
        $latest->execute(array($country, $ruleKey));
        $cur = $latest->fetch(PDO::FETCH_ASSOC);
        if (!$cur) {
            return array('rolled_back' => false, 'reactivated_version' => 0);
        }
        $db->prepare("UPDATE `epc_cmp_rules` SET `status`='retired' WHERE `id`=?")->execute(array((int) $cur['id']));
        $prev = $db->prepare("SELECT * FROM `epc_cmp_rules` WHERE `country`=? AND `rule_key`=? AND `version`<? ORDER BY `version` DESC LIMIT 1");
        $prev->execute(array($country, $ruleKey, (int) $cur['version']));
        $p = $prev->fetch(PDO::FETCH_ASSOC);
        $reactivated = 0;
        if ($p) {
            $db->prepare("UPDATE `epc_cmp_rules` SET `status`='active', `valid_to`=0 WHERE `id`=?")->execute(array((int) $p['id']));
            $reactivated = (int) $p['version'];
        }
        epc_cmp_log($db, $country, $ruleKey, 'rollback', array('retired_version' => (int) $cur['version'], 'reactivated_version' => $reactivated), $actor);
        return array('rolled_back' => true, 'reactivated_version' => $reactivated);
    }
}

/* --------------------------- Country presets -------------------------- */

if (!function_exists('epc_cmp_seed_uae')) {
    /**
     * Seed the UAE (FTA) baseline ruleset: 5% VAT from 2018-01-01, registration
     * threshold AED 375,000, e-invoice schema tag, VAT return period quarterly.
     * Idempotent-ish (adds versions; intended for first-time setup).
     */
    function epc_cmp_seed_uae(PDO $db): void
    {
        epc_cmp_ensure_schema($db);
        $jan2018 = strtotime('2018-01-01');
        if (epc_cmp_resolve($db, 'AE', 'vat_standard_rate', $jan2018) === null) {
            epc_cmp_set_rule($db, 'AE', 'vat_standard_rate', 0.05, $jan2018, 'fta', 'UAE VAT introduced');
            epc_cmp_set_rule($db, 'AE', 'vat_registration_threshold', 375000, $jan2018, 'fta');
            epc_cmp_set_rule($db, 'AE', 'vat_return_period', 'quarterly', $jan2018, 'fta');
            epc_cmp_set_rule($db, 'AE', 'einvoice_schema', 'PINT-AE', strtotime('2026-07-01'), 'fta', 'UAE e-invoicing phase 1');
            epc_cmp_set_rule($db, 'AE', 'corporate_tax_rate', 0.09, strtotime('2023-06-01'), 'fta', 'UAE CT 9% over AED 375k');
        }
    }
}
