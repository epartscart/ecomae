<?php
/**
 * Advanced ERP — shared foundation helpers.
 *
 * Additive layer used by the industry foundation, advanced CRM, and the
 * advanced setup migration. Nothing here modifies existing tables: it only
 * provides small, reusable utilities (settings key/value store on the existing
 * `epc_price_settings` table, CP content-page registration, formatting).
 *
 * Safe for live tenants: all schema operations are CREATE TABLE IF NOT EXISTS
 * and INSERT ... ON DUPLICATE KEY UPDATE only.
 */

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_erp_adv_h')) {
    function epc_erp_adv_h($v): string
    {
        return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
    }
}

if (!function_exists('epc_erp_adv_money')) {
    function epc_erp_adv_money($n): string
    {
        return number_format((float) $n, 2, '.', ',');
    }
}

if (!function_exists('epc_erp_adv_settings_ensure')) {
    /**
     * Ensure the shared key/value settings table exists. Matches the schema
     * created by epc-price-management-setup.php so it is fully compatible.
     */
    function epc_erp_adv_settings_ensure(PDO $db): void
    {
        $db->exec(
            "CREATE TABLE IF NOT EXISTS `epc_price_settings` (
                `setting_key` varchar(128) NOT NULL,
                `setting_value` text,
                PRIMARY KEY (`setting_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8"
        );
    }
}

if (!function_exists('epc_erp_adv_get_setting')) {
    function epc_erp_adv_get_setting(PDO $db, string $key, string $default = ''): string
    {
        try {
            $st = $db->prepare('SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1');
            $st->execute(array($key));
            $val = $st->fetchColumn();
            return ($val === false || $val === null) ? $default : (string) $val;
        } catch (Exception $e) {
            return $default;
        }
    }
}

if (!function_exists('epc_erp_adv_set_setting')) {
    function epc_erp_adv_set_setting(PDO $db, string $key, string $value): void
    {
        epc_erp_adv_settings_ensure($db);
        $db->prepare(
            'INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`)
             VALUES (?, ?)
             ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)'
        )->execute(array($key, $value));
    }
}

if (!function_exists('epc_erp_adv_register_cp_page')) {
    /**
     * Register (or update) a backend CP content page row, mirroring the proven
     * pattern in epc-register-erp-guide-content.php.
     *
     * @param string $url      Full CP url, e.g. shop/finance/erp/advanced-guide
     * @param string $phpPath  e.g. /<backend_dir>/content/shop/finance/erp/x.php
     * @return array{status:bool,message:string,content_id:int}
     */
    function epc_erp_adv_register_cp_page(PDO $db, string $url, string $phpPath, string $title, string $seedValue): array
    {
        $url = trim($url, '/');
        $alias = substr($url, strrpos($url, '/') !== false ? strrpos($url, '/') + 1 : 0);
        $parentUrl = (strrpos($url, '/') !== false) ? substr($url, 0, strrpos($url, '/')) : '';

        $parentId = 0;
        $parentLevel = 0;
        if ($parentUrl !== '') {
            $p = $db->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
            $p->execute(array($parentUrl));
            $row = $p->fetch(PDO::FETCH_ASSOC);
            if (!$row) {
                return array(
                    'status' => false,
                    'message' => 'Parent CP page not found: ' . $parentUrl . ' (run epc-erp-cp-setup.php first)',
                    'content_id' => 0,
                );
            }
            $parentId = (int) $row['id'];
            $parentLevel = (int) $row['level'];
        }

        $ex = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
        $ex->execute(array($url));
        $contentId = (int) $ex->fetchColumn();
        $now = time();

        if ($contentId > 0) {
            $db->prepare(
                'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0,
                 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `url` = ?
                 WHERE `id` = ?'
            )->execute(array($phpPath, $title, $parentId, $parentLevel + 1, $alias, $url, $contentId));
        } else {
            $db->prepare(
                'INSERT INTO `content`
                (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
                 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
                 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
                 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 1)'
            )->execute(array(
                $url,
                $parentLevel + 1,
                $alias,
                $seedValue,
                $parentId,
                $title,
                $phpPath,
                $title,
                $now,
                $now,
            ));
            $contentId = (int) $db->lastInsertId();
        }

        return array('status' => true, 'message' => 'CP page registered', 'content_id' => $contentId);
    }
}

if (!function_exists('epc_erp_adv_register_guides')) {
    /**
     * Register the advanced ERP guide CP pages. backend_dir is the CP directory
     * name (e.g. value of $DP_Config->backend_dir).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_erp_adv_register_guides(PDO $db, string $backendDir): array
    {
        $base = '/' . trim($backendDir, '/') . '/content/shop/finance/erp/';
        $pages = array(
            array(
                'url' => 'shop/finance/erp/advanced-guide',
                'php' => $base . 'erp_advanced_guide_page.php',
                'title' => 'Advanced ERP guide',
                'seed' => 'epc_erp_advanced_guide_cp',
            ),
            array(
                'url' => 'shop/finance/erp/dashboard',
                'php' => $base . 'erp_dashboard_page.php',
                'title' => 'ERP dashboard',
                'seed' => 'epc_erp_dashboard_cp',
            ),
            array(
                'url' => 'shop/finance/erp/provider-console',
                'php' => $base . 'erp_provider_console_page.php',
                'title' => 'Provider console',
                'seed' => 'epc_erp_provider_console_cp',
            ),
            array(
                'url' => 'shop/finance/erp/guide',
                'php' => $base . 'erp_full_guide_page.php',
                'title' => 'ERP step-by-step guide',
                'seed' => 'epc_erp_full_guide_cp',
            ),
        );
        $out = array();
        foreach ($pages as $pg) {
            $out[] = epc_erp_adv_register_cp_page($db, $pg['url'], $pg['php'], $pg['title'], $pg['seed']);
        }
        return $out;
    }
}
