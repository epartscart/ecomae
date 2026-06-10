<?php
/**
 * Register UAE tax compliance guide CP route + schema.
 * Run on tenant: https://www.epartscart.com/epc-uae-tax-compliance-setup.php?token=epartscart-deploy-2026
 * (also works on platform host if routed to tenant config)
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_uae_tax_compliance.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_uae_tax_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_uae_tax_lang($pdo, 'epc_uae_tax_compliance_cp', 'UAE tax compliance', 'UAE — налоговое соответствие');
epc_uae_tax_compliance_ensure_schema($pdo);
require_once __DIR__ . '/content/general_pages/epc_platform_governance.php';
$govSeeded = epc_platform_governance_seed($pdo);
require_once __DIR__ . '/content/shop/finance/epc_uae_tax_knowledge.php';
$kbSeeded = epc_uae_tax_knowledge_seed_kb($pdo);
$ftaFetch = epc_uae_fta_fetch_legislation_updates($pdo, true);
$legCount = count($ftaFetch['legislation'] ?? array());
$backfill = epc_uae_tax_legislation_backfill_summaries($pdo, false);

function epc_uae_tax_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 92)
{
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		throw new Exception('Parent not found: ' . $parentUrl);
	}
	$parentId = (int)$parentRow['id'];
	$level = (int)$parentRow['level'] + 1;
	$now = time();

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int)$existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
			 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `url` = ?, `value` = ?, `time_edited` = ?
			 WHERE `id` = ?'
		)->execute(array($phpPath, $title, $parentId, $level, $alias, $url, $valueKey, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$url, $level, $alias, $valueKey, $parentId, $title, $phpPath, $title, $now, $now, $order,
		));
		$contentId = (int)$pdo->lastInsertId();
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = (int) $st->fetchColumn();
	$groups = array($root > 0 ? $root : 1);
	$collect = function ($pid) use ($pdo, &$collect, &$groups) {
		$ch = $pdo->prepare('SELECT `id` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($pid));
		while ($r = $ch->fetch(PDO::FETCH_ASSOC)) {
			$groups[] = (int) $r['id'];
			$collect((int) $r['id']);
		}
	};
	if ($root > 0) {
		$collect($root);
	}
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	foreach (array_unique($groups) as $gid) {
		$ins->execute(array($contentId, (int) $gid));
	}
	return $contentId;
}

$guideId = epc_uae_tax_register_content(
	$pdo,
	'shop/finance/erp',
	'shop/finance/erp/uae-tax-compliance',
	'uae-tax-compliance',
	'epc_uae_tax_compliance_cp',
	'/<backend_dir>/content/shop/finance/erp/erp_uae_tax_compliance_page.php',
	'UAE tax compliance — eParts Cart',
	22
);

$menu = epc_cp_mainstream_menu_apply($pdo);

$accessCount = (int) $pdo->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int) $guideId)->fetchColumn();
echo "UAE tax compliance registered.\n";
echo "content_id: {$guideId}\n";
echo "content_access rows: {$accessCount}\n";
echo "ERP tab: ?area=finance&tab=tax_compliance\n";
echo "Guide URL: /" . $cfg->backend_dir . "/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1\n";
echo "Schema: epc_uae_vat_advance, epc_uae_tax_compliance_cache, epc_uae_ct_adjustments, epc_uae_tax_legislation_items\n";
echo "KB articles seeded: {$kbSeeded}\n";
echo "Governance rules seeded (increment): {$govSeeded}\n";
echo "Legislation fetched: {$legCount} (source: " . epc_uae_fta_legislation_url() . ")\n";
echo "Fetch message: " . ($ftaFetch['message'] ?? '') . "\n";
echo "Summaries backfilled: " . (int)($backfill['updated'] ?? 0) . " — " . ($backfill['message'] ?? '') . "\n";
