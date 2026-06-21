<?php
/**
 * EPC Tax Toolkit — schema, kits, Super CP route, customer migration (per MySQL database).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_tax_toolkit.php';

function epc_tax_toolkit_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

/**
 * @return array{seeded:int,installed:int,content_id:int,menu_item_id:int,migration:array,profiles:int,kit_codes:array}
 */
function epc_tax_toolkit_cp_install(PDO $pdo, string $backendDir = 'cp', bool $apply = true, bool $migrate = false): array
{
	$backendDir = trim($backendDir, '/');
	if ($backendDir === '') {
		$backendDir = 'cp';
	}

	epc_tax_toolkit_ensure_schema($pdo);
	$seeded = epc_tax_toolkit_seed_kits($pdo);

	$result = array(
		'seeded' => $seeded,
		'installed' => 0,
		'content_id' => 0,
		'menu_item_id' => 0,
		'migration' => array('users' => 0, 'contacts' => 0, 'skipped' => 0, 'errors' => array()),
		'profiles' => 0,
		'kit_codes' => array(),
	);

	foreach (epc_tax_toolkit_catalog_definitions() as $def) {
		$result['kit_codes'][] = (string) $def['kit_code'];
	}

	if (!$apply) {
		return $result;
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php';

	epc_tax_toolkit_setup_lang($pdo, 'epc_portal_tax_toolkit_manage', 'Tax Toolkit', 'Налоговый набор');
	$installed = epc_tax_toolkit_install_defaults($pdo);
	$result['installed'] = count($installed);

	$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
	$itemUrl = '/<backend>/control/portal/epc_tax_toolkit_manage';
	$result['menu_item_id'] = (int) epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_portal_tax_toolkit_manage', $itemUrl, 11, '#15803d', 'fa-balance-scale', 1);

	$contentUrl = 'control/portal/epc_tax_toolkit_manage';
	$phpPath = '/<backend_dir>/content/control/portal/epc_tax_toolkit_manage.php';
	$now = time();

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('control/config'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('control'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		throw new RuntimeException('Parent content not found for Tax Toolkit CP route');
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($phpPath, 'epc_portal_tax_toolkit_manage', 'epc_portal_tax_toolkit_manage', $parentId, $level, 'epc_tax_toolkit_manage', $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 11)'
		)->execute(array(
			$contentUrl, $level, 'epc_tax_toolkit_manage', 'epc_portal_tax_toolkit_manage', $parentId,
			'Super CP — install tax jurisdiction kits and assign to customers by country',
			$phpPath, 'epc_portal_tax_toolkit_manage', $now, $now,
		));
		$contentId = (int) $pdo->lastInsertId();
	}
	$result['content_id'] = $contentId;

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('control/portal/epc_api_clients_manage'));
	$refId = (int) $ref->fetchColumn();
	if ($refId > 0 && $contentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($refId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($contentId, (int) $g['group_id']));
			} catch (Exception $e) {
			}
		}
	}

	if ($migrate) {
		$result['migration'] = epc_tax_toolkit_migrate_tenant($pdo, 'default', function_exists('epc_portal_host') ? epc_portal_host() : '');
		$counts = epc_tax_toolkit_profile_counts($pdo);
		$result['profiles'] = (int) ($counts['tenants'] ?? 0);
		$result['tenant'] = $result['migration'];
	}

	return $result;
}

function epc_tax_toolkit_setup_connect(array $cred, DP_Config $cfg): ?PDO
{
	$db = trim((string) ($cred['db'] ?? ''));
	if ($db === '') {
		return null;
	}
	$user = trim((string) ($cred['user'] ?? ''));
	if ($user === '') {
		$user = (string) $cfg->user;
	}
	$pass = (string) ($cred['pass'] ?? '');
	if ($pass === '') {
		$pass = (string) $cfg->password;
	}
	try {
		return new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return null;
	}
}
