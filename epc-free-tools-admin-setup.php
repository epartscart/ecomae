<?php
/**
 * Register the BOC "Free Tools control" route in the Super CP menu/CMS:
 *   - control/portal/epc_free_tools_admin  (Free Tools usage + activate/deactivate)
 * Run: https://www.ecomae.com/epc-free-tools-admin-setup.php?token=<deploy-token>
 *
 * Mirrors epc-boc-setup.php so the new operator page is reachable with the same
 * access groups as the other BOC portal pages.
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
    exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
if (!$pdo instanceof PDO) {
    try {
        $pdo = new PDO(
            'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
            $cfg->user,
            $cfg->password,
            array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
        );
    } catch (Exception $e) {
        exit('DB connect failed: ' . $e->getMessage() . "\n");
    }
}

if (function_exists('epc_portal_db_ensure')) {
    epc_portal_db_ensure($pdo);
}

function epc_ftadmin_lang(PDO $pdo, string $key, string $en, string $ru): void
{
    $pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
    $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
    $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

/** Register one portal CMS page; returns its content id. */
function epc_ftadmin_page(PDO $pdo, string $slug, string $alias, string $key, string $desc): int
{
    $contentUrl = 'control/portal/' . $slug;
    $phpPath = '/<backend_dir>/content/control/portal/' . $slug . '.php';
    $now = time();

    $parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
    $parent->execute(array('control/config'));
    $parentRow = $parent->fetch(PDO::FETCH_ASSOC);
    if (!$parentRow) {
        $parent->execute(array('control'));
        $parentRow = $parent->fetch(PDO::FETCH_ASSOC);
    }
    if (!$parentRow) {
        echo "Parent content not found for {$slug}\n";
        return 0;
    }
    $parentId = (int) $parentRow['id'];
    $level = (int) $parentRow['level'] + 1;

    $existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
    $existing->execute(array($contentUrl));
    $contentId = (int) $existing->fetchColumn();

    if ($contentId > 0) {
        $pdo->prepare(
            'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
        )->execute(array($phpPath, $key, $key, $parentId, $level, $alias, $contentId));
    } else {
        $pdo->prepare(
            'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
             `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
             `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
             VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 5)'
        )->execute(array(
            $contentUrl, $level, $alias, $key, $parentId, $desc,
            $phpPath, $key, $now, $now,
        ));
        $contentId = (int) $pdo->lastInsertId();
    }

    // Copy access groups from an already-registered portal page so the same
    // operators can reach it.
    $ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
    $ref->execute(array('control/portal/epc_boc_vendor_control'));
    $refId = (int) $ref->fetchColumn();
    if ($refId <= 0) {
        $ref->execute(array('control/portal/epc_platform_governance'));
        $refId = (int) $ref->fetchColumn();
    }
    if ($refId > 0 && $contentId > 0) {
        $pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
        $groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
        $groups->execute(array($refId));
        $ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
        while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
            try { $ins->execute(array($contentId, (int) $g['group_id'])); } catch (Exception $e) {}
        }
    }
    return $contentId;
}

$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);

epc_ftadmin_lang($pdo, 'epc_free_tools_admin', 'Free Tools control', 'Бесплатные инструменты');
epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_free_tools_admin', '/<backend>/control/portal/epc_free_tools_admin', 28, '#dc2626', 'fas fa-magic', 1);
$ftId = epc_ftadmin_page($pdo, 'epc_free_tools_admin', 'epc_free_tools_admin', 'epc_free_tools_admin', 'BOC — Free Tools usage + activate/deactivate control');

echo "Free Tools admin content id: {$ftId}\n";
echo "URL:\n";
echo "  /cp/control/portal/epc_free_tools_admin\n";
echo "Done.\n";
