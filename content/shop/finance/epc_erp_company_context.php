<?php
/**
 * Advanced ERP — Company context (enterprise multi-company / legal-entity).
 *
 * enterprise lets one installation hold many legal entities (companies); a top-bar
 * company picker selects the active company and the whole app works in that
 * context. This layer adds that concept on top of the existing, user-managed
 * legal-entity master (`epc_erp_pm_legal_entities`, configured under
 * Business Unit ▸ Legal entities):
 *   - the active company is resolved from ?company= / session, defaulting to the
 *     first legal entity (auto-seeded from the tenant company profile),
 *   - per-company settings (industry pack, …) override the tenant-wide
 *     defaults, so each company can be a different industry / structure —
 *     exactly the enterprise flexibility.
 *
 * Additive + backward compatible: when no per-company override exists, the
 * existing tenant-wide platform setting is used, so current single-company
 * tenants keep working unchanged.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_pdf_modules.php';
require_once __DIR__ . '/epc_erp_company.php';

if (!function_exists('epc_erp_company_context_ensure')) {
    function epc_erp_company_context_ensure(PDO $db): void
    {
        epc_erp_pm_ensure_schema($db);
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_org_company_settings` (
            `company_id` int(11) NOT NULL,
            `setting_key` varchar(64) NOT NULL,
            `setting_value` text,
            PRIMARY KEY (`company_id`,`setting_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-company ERP settings'");

        // Auto-seed a default legal entity from the tenant company profile so
        // every tenant has at least one selectable company.
        $cnt = (int) $db->query('SELECT COUNT(*) FROM `epc_erp_pm_legal_entities` WHERE `active`=1')->fetchColumn();
        if ($cnt === 0) {
            $profile = function_exists('epc_co_profile_get') ? epc_co_profile_get($db) : array();
            $name = trim((string) ($profile['legal_name'] ?? $profile['trade_name'] ?? 'Main Company'));
            if ($name === '') {
                $name = 'Main Company';
            }
            $cur = strtoupper(trim((string) ($profile['base_currency'] ?? 'AED')));
            if ($cur === '') {
                $cur = 'AED';
            }
            $country = strtoupper(substr(trim((string) ($profile['country'] ?? '')), 0, 4));
            $trn = trim((string) ($profile['trn'] ?? ''));
            epc_erp_pm_save($db, 'epc_erp_pm_legal_entities', array(
                'code' => 'MAIN',
                'name' => $name,
                'country_code' => $country,
                'currency_code' => $cur,
                'trn' => $trn,
                'note' => 'Auto-created default company',
            ));
        }
    }
}

if (!function_exists('epc_erp_companies_list')) {
    /**
     * @return array<int,array<string,mixed>>
     */
    function epc_erp_companies_list(PDO $db): array
    {
        epc_erp_company_context_ensure($db);
        return epc_erp_pm_list($db, 'epc_erp_pm_legal_entities', true);
    }
}

if (!function_exists('epc_erp_active_company_id')) {
    /**
     * Resolve the active company id from ?company= (persisted to session) or
     * the session, defaulting to the first company. Only ids that belong to the
     * tenant are accepted.
     */
    function epc_erp_active_company_id(PDO $db): int
    {
        $companies = epc_erp_companies_list($db);
        $ids = array();
        foreach ($companies as $c) {
            $ids[] = (int) $c['id'];
        }
        if (!$ids) {
            return 0;
        }
        $sess = function_exists('session_status') && session_status() === PHP_SESSION_ACTIVE;
        if (isset($_GET['company'])) {
            $cid = (int) $_GET['company'];
            if (in_array($cid, $ids, true)) {
                if ($sess) {
                    $_SESSION['erp_active_company'] = $cid;
                }
                return $cid;
            }
        }
        if ($sess && isset($_SESSION['erp_active_company']) && in_array((int) $_SESSION['erp_active_company'], $ids, true)) {
            return (int) $_SESSION['erp_active_company'];
        }
        return (int) $ids[0];
    }
}

if (!function_exists('epc_erp_active_company')) {
    /**
     * @return array<string,mixed>
     */
    function epc_erp_active_company(PDO $db): array
    {
        $id = epc_erp_active_company_id($db);
        foreach (epc_erp_companies_list($db) as $c) {
            if ((int) $c['id'] === $id) {
                return $c;
            }
        }
        return array('id' => 0, 'code' => 'MAIN', 'name' => 'Main Company', 'currency_code' => 'AED', 'country_code' => '');
    }
}

if (!function_exists('epc_erp_company_setting_get')) {
    function epc_erp_company_setting_get(PDO $db, int $companyId, string $key, string $default = ''): string
    {
        epc_erp_company_context_ensure($db);
        $st = $db->prepare('SELECT `setting_value` FROM `epc_org_company_settings` WHERE `company_id`=? AND `setting_key`=? LIMIT 1');
        $st->execute(array($companyId, $key));
        $v = $st->fetchColumn();
        return $v === false ? $default : (string) $v;
    }
}

if (!function_exists('epc_erp_company_setting_set')) {
    function epc_erp_company_setting_set(PDO $db, int $companyId, string $key, string $value): void
    {
        epc_erp_company_context_ensure($db);
        $db->prepare('INSERT INTO `epc_org_company_settings` (`company_id`,`setting_key`,`setting_value`) VALUES (?,?,?)
                      ON DUPLICATE KEY UPDATE `setting_value`=VALUES(`setting_value`)')
            ->execute(array($companyId, $key, $value));
    }
}

if (!function_exists('epc_erp_company_industry_pack')) {
    /**
     * Effective industry pack for a company: per-company override if set, else
     * the tenant-wide platform setting (backward compatible).
     */
    function epc_erp_company_industry_pack(PDO $db, int $companyId): string
    {
        $perCo = epc_erp_company_setting_get($db, $companyId, 'industry_pack', '__unset__');
        if ($perCo !== '__unset__') {
            return $perCo;
        }
        if (function_exists('epc_erp_platform_setting_get')) {
            return (string) epc_erp_platform_setting_get($db, 'active_industry_pack', '');
        }
        return '';
    }
}

if (!function_exists('epc_erp_company_industry_pack_set')) {
    function epc_erp_company_industry_pack_set(PDO $db, int $companyId, string $pack): void
    {
        epc_erp_company_setting_set($db, $companyId, 'industry_pack', $pack);
        // Keep the tenant-wide default in sync for legacy read paths.
        if (function_exists('epc_erp_platform_setting_set')) {
            epc_erp_platform_setting_set($db, 'active_industry_pack', $pack);
        }
    }
}

if (!function_exists('epc_erp_company_switch_url')) {
    /**
     * Build a URL that switches to a given company by appending/replacing the
     * `company` query parameter on the current request URI.
     */
    function epc_erp_company_switch_url(int $companyId): string
    {
        $uri = (string) ($_SERVER['REQUEST_URI'] ?? '/');
        $parts = explode('?', $uri, 2);
        $path = $parts[0];
        $query = array();
        if (isset($parts[1]) && $parts[1] !== '') {
            parse_str($parts[1], $query);
        }
        $query['company'] = (string) $companyId;
        return $path . '?' . http_build_query($query);
    }
}

if (!function_exists('epc_erp_company_picker_html')) {
    /**
     * Render the enterprise-style top-bar company picker (a small dropdown of the
     * tenant's legal entities, current one highlighted).
     */
    function epc_erp_company_picker_html(PDO $db): string
    {
        $companies = epc_erp_companies_list($db);
        if (count($companies) < 1) {
            return '';
        }
        $activeId = epc_erp_active_company_id($db);
        $activeName = 'Company';
        foreach ($companies as $c) {
            if ((int) $c['id'] === $activeId) {
                $activeName = (string) $c['code'] . ' · ' . (string) $c['name'];
            }
        }
        $h = static function (string $s): string {
            return htmlspecialchars($s, ENT_QUOTES, 'UTF-8');
        };
        $out = '<div class="epc-erp-company-picker dropdown" style="display:inline-block;">';
        $out .= '<button type="button" class="btn btn-default btn-xs dropdown-toggle" data-toggle="dropdown" title="Active company (legal entity)">';
        $out .= '<i class="fa fa-building"></i> <span class="epc-erp-company-name">' . $h($activeName) . '</span> <span class="caret"></span>';
        $out .= '</button>';
        $out .= '<ul class="dropdown-menu dropdown-menu-right epc-erp-company-menu">';
        $out .= '<li class="dropdown-header">Company / legal entity</li>';
        foreach ($companies as $c) {
            $sel = (int) $c['id'] === $activeId;
            $url = epc_erp_company_switch_url((int) $c['id']);
            $meta = trim((string) ($c['currency_code'] ?? '') . ((string) ($c['country_code'] ?? '') !== '' ? ' · ' . (string) $c['country_code'] : ''));
            $out .= '<li' . ($sel ? ' class="active"' : '') . '><a href="' . $h($url) . '">'
                . ($sel ? '<i class="fa fa-check"></i> ' : '<i class="fa fa-building-o"></i> ')
                . '<strong>' . $h((string) $c['code']) . '</strong> · ' . $h((string) $c['name'])
                . ' <span class="text-muted" style="font-size:11px;">' . $h($meta) . '</span></a></li>';
        }
        $out .= '</ul></div>';
        return $out;
    }
}
