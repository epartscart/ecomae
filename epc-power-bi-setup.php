<?php
/**
 * Register Super CP / tenant CP → Portal → Power BI (+ step-by-step guide).
 * Run: https://www.ecomae.com/epc-power-bi-setup.php?token=epartscart-deploy-2026
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
require_once __DIR__ . '/content/general_pages/epc_power_bi.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_portal_platform_pdo();
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

epc_portal_db_ensure($pdo);
epc_power_bi_ensure_schema($pdo);

function epc_pbi_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

/**
 * @return array{item_id:int,content_id:int,ok:bool}
 */
function epc_pbi_setup_register_page(
	PDO $pdo,
	int $portalGroup,
	int $parentId,
	int $level,
	int $refId,
	string $langKey,
	string $labelEn,
	string $labelRu,
	string $contentUrl,
	string $alias,
	string $menuUrl,
	string $phpPath,
	string $description,
	int $order,
	string $color,
	string $icon
): array {
	epc_pbi_setup_lang($pdo, $langKey, $labelEn, $labelRu);
	$itemId = epc_cp_mm_ensure_item($pdo, $portalGroup, $langKey, $menuUrl, $order, $color, $icon, 1);
	$now = time();

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($phpPath, $langKey, $langKey, $parentId, $level, $alias, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$contentUrl, $level, $alias, $langKey, $parentId,
			$description,
			$phpPath, $langKey, $now, $now, $order,
		));
		$contentId = (int) $pdo->lastInsertId();
	}

	if ($contentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		$groupIds = array();
		if ($refId > 0) {
			$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
			$groups->execute(array($refId));
			while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
				$groupIds[] = (int) $g['group_id'];
			}
		}
		if ($groupIds === array()) {
			// Fallback: grant groups that already have access to Portal parent / control home.
			foreach (array('control/config', 'control', 'control/portal/industry_settings') as $refUrl) {
				$rg = $pdo->prepare(
					'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
					 INNER JOIN `content` c ON c.`id` = ca.`content_id`
					 WHERE c.`url` = ? AND c.`is_frontend` = 0'
				);
				$rg->execute(array($refUrl));
				while ($g = $rg->fetch(PDO::FETCH_ASSOC)) {
					$groupIds[] = (int) $g['group_id'];
				}
				if ($groupIds !== array()) {
					break;
				}
			}
		}
		$groupIds = array_values(array_unique(array_filter($groupIds)));
		foreach ($groupIds as $gid) {
			try {
				$ins->execute(array($contentId, $gid));
			} catch (Exception $e) {
			}
		}
	}

	$verify = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 AND `published_flag` = 1 LIMIT 1');
	$verify->execute(array($contentUrl));
	$ok = (bool) $verify->fetchColumn();

	return array('item_id' => $itemId, 'content_id' => $contentId, 'ok' => $ok);
}

$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);

$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array('control/config'));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	$parent->execute(array('control'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
}
if (!$parentRow) {
	exit("Parent content not found\n");
}
$parentId = (int) $parentRow['id'];
$level = (int) $parentRow['level'] + 1;

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('control/portal/epc_api_documentation_guide'));
$refId = (int) $ref->fetchColumn();
if ($refId <= 0) {
	$ref->execute(array('control/portal/epc_integrations_hub'));
	$refId = (int) $ref->fetchColumn();
}

$settings = epc_pbi_setup_register_page(
	$pdo,
	$portalGroup,
	$parentId,
	$level,
	$refId,
	'epc_portal_power_bi',
	'Power BI',
	'Power BI',
	'control/portal/epc_power_bi',
	'epc_power_bi',
	'/<backend>/control/portal/epc_power_bi',
	'/<backend_dir>/content/control/portal/epc_power_bi.php',
	'Power BI connector — datasets, workspace config, embed',
	10,
	'#f2c811',
	'fa-bar-chart'
);

$guide = epc_pbi_setup_register_page(
	$pdo,
	$portalGroup,
	$parentId,
	$level,
	$refId,
	'epc_portal_power_bi_guide',
	'Power BI guide',
	'Руководство Power BI',
	'control/portal/epc_power_bi_guide',
	'epc_power_bi_guide',
	'/<backend>/control/portal/epc_power_bi_guide',
	'/<backend_dir>/content/control/portal/epc_power_bi_guide.php',
	'Power BI — step-by-step CP operator guide',
	11,
	'#ca8a04',
	'fa-book'
);

echo 'db: ' . $cfg->db . "\n";
echo 'schema: epc_power_bi_config + epc_power_bi_reports ensured' . "\n";
echo 'Settings menu id: ' . $settings['item_id'] . ' content id: ' . $settings['content_id'] . ' verify: ' . ($settings['ok'] ? 'ok' : 'MISSING') . "\n";
echo 'Guide menu id: ' . $guide['item_id'] . ' content id: ' . $guide['content_id'] . ' verify: ' . ($guide['ok'] ? 'ok' : 'MISSING') . "\n";
echo "CP settings: /cp/control/portal/epc_power_bi\n";
echo "CP guide:    /cp/control/portal/epc_power_bi_guide\n";
echo "API catalog: /epc-api/v1/powerbi/catalog\n";
echo "Docs: docs/POWER_BI.md\n";

if (!empty($_GET['diagnose'])) {
	$phpPath = str_replace('<backend_dir>', (string) $cfg->backend_dir, '/<backend_dir>/content/control/portal/epc_power_bi.php');
	$abs = __DIR__ . str_replace('/' . $cfg->backend_dir, '/' . $cfg->backend_dir, $phpPath);
	// Resolve the same way CMS does: DOCUMENT_ROOT + content column
	$abs = __DIR__ . '/cp/content/control/portal/epc_power_bi.php';
	echo "\n--- diagnose ---\n";
	echo 'php_file: ' . $abs . ' exists=' . (is_file($abs) ? 'yes' : 'no') . "\n";
	$row = $pdo->prepare('SELECT `id`,`url`,`content`,`published_flag`,`content_type` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$row->execute(array('control/portal/epc_power_bi'));
	$r = $row->fetch(PDO::FETCH_ASSOC);
	echo 'content_row: ' . json_encode($r, JSON_UNESCAPED_SLASHES) . "\n";
	$acc = (int) $pdo->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int) ($settings['content_id'] ?? 0))->fetchColumn();
	echo 'content_access_rows: ' . $acc . "\n";
	if (is_file(__DIR__ . '/content/general_pages/epc_bos_unified.php')) {
		require_once __DIR__ . '/content/general_pages/epc_bos_unified.php';
		if (function_exists('epc_bos_module_cp_url')) {
			$u = epc_bos_module_cp_url('www.ecomae.com', 'control/portal/epc_power_bi');
			echo 'bos_url: ' . $u . "\n";
			echo 'bos_url_ok: ' . (strpos($u, '/cp/control/portal/epc_power_bi') !== false && strpos($u, '/cp/content/') === false ? 'yes' : 'NO') . "\n";
		}
	}
}

echo "Done.\n";
