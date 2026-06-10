<?php
/**
 * Advanced ERP — Module registry & per-tenant entitlements.
 *
 * Makes the suite pick-and-choose: each tenant runs only the modules they are
 * licensed for (e.g. "Customs + Logistics only", "E-invoice only", "Payroll
 * only", or "Full ERP"). Used by:
 *   - setup scripts (only build/register enabled modules),
 *   - CP menu + in-app guide (only show enabled modules),
 *   - module entry points (gate disabled code paths).
 *
 * Entitlements live in the tenant's OWN database, so they are tenant-isolated.
 * Additive: one new table. Default (no row) behaviour is configurable per call.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_mod_registry')) {
    /**
     * Master catalog of modules. Each: label, category, requires[] (module
     * codes this one depends on).
     *
     * @return array<string,array<string,mixed>>
     */
    function epc_mod_registry(): array
    {
        return array(
            // Foundation
            'core' => array('label' => 'Core (items, parties, settings)', 'category' => 'Foundation', 'requires' => array()),
            'inventory' => array('label' => 'Inventory & Warehouse', 'category' => 'Foundation', 'requires' => array('core')),
            'industry' => array('label' => 'Industry templates & packs', 'category' => 'Foundation', 'requires' => array('core')),
            'currency' => array('label' => 'Worldwide multi-currency', 'category' => 'Foundation', 'requires' => array('core')),
            'gl' => array('label' => 'General Ledger / Accounting', 'category' => 'Finance', 'requires' => array('core')),
            // Finance
            'tax' => array('label' => 'Worldwide Tax engine', 'category' => 'Finance', 'requires' => array('core')),
            'vat_return' => array('label' => 'VAT / GST filing', 'category' => 'Finance', 'requires' => array('tax', 'gl')),
            'corporate_tax' => array('label' => 'Corporate tax', 'category' => 'Finance', 'requires' => array('gl')),
            'bank_recon' => array('label' => 'AI bank reconciliation', 'category' => 'Finance', 'requires' => array('gl')),
            'einvoice' => array('label' => 'E-invoicing', 'category' => 'Finance', 'requires' => array('core')),
            'credit' => array('label' => 'Credit & collections', 'category' => 'Finance', 'requires' => array('gl')),
            'fixed_assets' => array('label' => 'Fixed assets & depreciation', 'category' => 'Finance', 'requires' => array('gl')),
            'reporting' => array('label' => 'Reporting center + insights', 'category' => 'Finance', 'requires' => array('gl')),
            // Supply chain
            'procurement' => array('label' => 'Procurement & RFQ', 'category' => 'Supply chain', 'requires' => array('inventory')),
            'planning' => array('label' => 'Demand forecasting & planning', 'category' => 'Supply chain', 'requires' => array('inventory')),
            'landed_cost' => array('label' => 'Landed cost', 'category' => 'Supply chain', 'requires' => array('inventory', 'logistics')),
            'logistics' => array('label' => 'Shipping & logistics', 'category' => 'Supply chain', 'requires' => array('core')),
            'customs' => array('label' => 'Customs / Bill of Entry', 'category' => 'Supply chain', 'requires' => array('logistics')),
            // Operations
            'manufacturing' => array('label' => 'Manufacturing (BOM, work orders)', 'category' => 'Operations', 'requires' => array('inventory')),
            'aftersales' => array('label' => 'After-sales (RMA, warranty, service)', 'category' => 'Operations', 'requires' => array('core')),
            'asset_maint' => array('label' => 'Asset maintenance (EAM)', 'category' => 'Operations', 'requires' => array('core')),
            'quality' => array('label' => 'Quality control / inspection', 'category' => 'Operations', 'requires' => array('inventory')),
            // CRM & sales
            'crm' => array('label' => 'CRM (advanced)', 'category' => 'CRM & Sales', 'requires' => array('core')),
            'pos' => array('label' => 'Retail & POS', 'category' => 'CRM & Sales', 'requires' => array('inventory')),
            // HR
            'hr' => array('label' => 'Human resources', 'category' => 'HR', 'requires' => array('core')),
            'payroll' => array('label' => 'Payroll', 'category' => 'HR', 'requires' => array('core')),
            // Platform
            'attachments' => array('label' => 'Document attachments', 'category' => 'Platform', 'requires' => array('core')),
            'workflow' => array('label' => 'Workflow / approvals', 'category' => 'Platform', 'requires' => array('core')),
            'messaging' => array('label' => 'Inter-department messaging', 'category' => 'Platform', 'requires' => array('core')),
            'dataio' => array('label' => 'Excel/CSV import-export', 'category' => 'Platform', 'requires' => array('core')),
            'audit' => array('label' => 'Audit trail', 'category' => 'Platform', 'requires' => array('core')),
            'roles' => array('label' => 'Roles & permissions', 'category' => 'Platform', 'requires' => array('core')),
        );
    }
}

if (!function_exists('epc_mod_bundles')) {
    /**
     * Ready-made plans/bundles for fast onboarding. 'full' is resolved
     * dynamically to every module.
     *
     * @return array<string,array<int,string>>
     */
    function epc_mod_bundles(): array
    {
        return array(
            'einvoice_only' => array('core', 'einvoice'),
            'payroll_only' => array('core', 'hr', 'payroll'),
            'customs_logistics' => array('core', 'logistics', 'customs'),
            'pos_retail' => array('core', 'inventory', 'industry', 'pos', 'crm'),
            'finance_suite' => array('core', 'gl', 'tax', 'vat_return', 'corporate_tax', 'bank_recon', 'einvoice', 'credit', 'fixed_assets', 'reporting', 'currency'),
            'supply_chain' => array('core', 'inventory', 'procurement', 'planning', 'logistics', 'landed_cost', 'customs'),
            'manufacturing_pack' => array('core', 'inventory', 'manufacturing', 'quality', 'planning'),
            'full' => array(), // expanded by epc_mod_resolve_bundle()
        );
    }
}

if (!function_exists('epc_mod_resolve_deps')) {
    /**
     * Expand a set of module codes to include all transitive dependencies.
     *
     * @param array<int,string> $codes
     * @return array<int,string>
     */
    function epc_mod_resolve_deps(array $codes): array
    {
        $reg = epc_mod_registry();
        $resolved = array();
        $stack = array_values($codes);
        while ($stack) {
            $code = array_pop($stack);
            if (!isset($reg[$code]) || isset($resolved[$code])) {
                continue;
            }
            $resolved[$code] = true;
            foreach ((array) $reg[$code]['requires'] as $dep) {
                if (!isset($resolved[$dep])) {
                    $stack[] = $dep;
                }
            }
        }
        return array_keys($resolved);
    }
}

if (!function_exists('epc_mod_resolve_bundle')) {
    /**
     * @return array<int,string> module codes for a bundle (deps included)
     */
    function epc_mod_resolve_bundle(string $bundle): array
    {
        $bundles = epc_mod_bundles();
        if ($bundle === 'full') {
            return array_keys(epc_mod_registry());
        }
        if (!isset($bundles[$bundle])) {
            return array();
        }
        return epc_mod_resolve_deps($bundles[$bundle]);
    }
}

if (!function_exists('epc_mod_ensure_schema')) {
    function epc_mod_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_mod_entitlements` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `module_code` varchar(40) NOT NULL,
            `enabled` tinyint(1) NOT NULL DEFAULT 1,
            `expires_at` int(11) NOT NULL DEFAULT 0,
            `note` varchar(190) DEFAULT NULL,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_module` (`module_code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-tenant module entitlements'");
    }
}

if (!function_exists('epc_mod_set')) {
    /**
     * Enable/disable one module. Enabling also enables its dependencies.
     */
    function epc_mod_set(PDO $db, string $code, bool $enabled, int $expiresAt = 0, string $note = ''): void
    {
        epc_mod_ensure_schema($db);
        $reg = epc_mod_registry();
        if (!isset($reg[$code])) {
            throw new Exception('Unknown module: ' . $code);
        }
        $codes = $enabled ? epc_mod_resolve_deps(array($code)) : array($code);
        $now = time();
        $stmt = $db->prepare(
            "INSERT INTO `epc_mod_entitlements` (`module_code`,`enabled`,`expires_at`,`note`,`time_updated`)
             VALUES (?,?,?,?,?)
             ON DUPLICATE KEY UPDATE `enabled`=VALUES(`enabled`), `expires_at`=VALUES(`expires_at`), `note`=VALUES(`note`), `time_updated`=VALUES(`time_updated`)"
        );
        foreach ($codes as $c) {
            $stmt->execute(array($c, $enabled ? 1 : 0, $expiresAt, $note, $now));
        }
    }
}

if (!function_exists('epc_mod_apply_bundle')) {
    /**
     * Enable exactly the modules in a bundle (others left as-is unless
     * $exclusive, in which case all non-bundle modules are disabled).
     *
     * @return array<int,string> the enabled module codes
     */
    function epc_mod_apply_bundle(PDO $db, string $bundle, bool $exclusive = false, int $expiresAt = 0): array
    {
        epc_mod_ensure_schema($db);
        $codes = epc_mod_resolve_bundle($bundle);
        if (!$codes) {
            throw new Exception('Unknown bundle: ' . $bundle);
        }
        $now = time();
        if ($exclusive) {
            $db->exec("UPDATE `epc_mod_entitlements` SET `enabled`=0, `time_updated`=" . $now);
        }
        $stmt = $db->prepare(
            "INSERT INTO `epc_mod_entitlements` (`module_code`,`enabled`,`expires_at`,`note`,`time_updated`)
             VALUES (?,?,?,?,?)
             ON DUPLICATE KEY UPDATE `enabled`=1, `expires_at`=VALUES(`expires_at`), `time_updated`=VALUES(`time_updated`)"
        );
        foreach ($codes as $c) {
            $stmt->execute(array($c, 1, $expiresAt, 'bundle:' . $bundle, $now));
        }
        return $codes;
    }
}

if (!function_exists('epc_mod_enabled')) {
    /**
     * Is a module enabled for this tenant? Respects expiry. When no entitlement
     * rows exist at all, falls back to $defaultWhenUnset (so a fresh tenant can
     * default to either everything-on or everything-off per deployment policy).
     */
    function epc_mod_enabled(PDO $db, string $code, bool $defaultWhenUnset = true): bool
    {
        epc_mod_ensure_schema($db);
        $count = (int) $db->query("SELECT COUNT(*) FROM `epc_mod_entitlements`")->fetchColumn();
        if ($count === 0) {
            return $defaultWhenUnset;
        }
        $st = $db->prepare("SELECT `enabled`,`expires_at` FROM `epc_mod_entitlements` WHERE `module_code`=?");
        $st->execute(array($code));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return false;
        }
        if ((int) $row['enabled'] !== 1) {
            return false;
        }
        $exp = (int) $row['expires_at'];
        if ($exp > 0 && $exp < time()) {
            return false;
        }
        return true;
    }
}

if (!function_exists('epc_mod_enabled_list')) {
    /**
     * @return array<int,string> enabled module codes
     */
    function epc_mod_enabled_list(PDO $db, bool $defaultWhenUnset = true): array
    {
        epc_mod_ensure_schema($db);
        $out = array();
        foreach (array_keys(epc_mod_registry()) as $code) {
            if (epc_mod_enabled($db, $code, $defaultWhenUnset)) {
                $out[] = $code;
            }
        }
        return $out;
    }
}
