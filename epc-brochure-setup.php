<?php
/**
 * Register product + full CP brochures + CP left-menu access.
 *
 * Works for tenant CP (e.g. epartscart) and Super CP (ecomae).
 *
 * CLI:  php epc-brochure-setup.php --apply [--host=www.epartscart.com]
 * HTTP: https://www.epartscart.com/epc-brochure-setup.php?token=...&apply=1
 *       https://www.ecomae.com/epc-brochure-setup.php?token=...&apply=1
 */
declare(strict_types=1);

$isCli = PHP_SAPI === 'cli';
if (!$isCli) {
	header('Content-Type: text/plain; charset=utf-8');
	require_once __DIR__ . '/epc_deploy_auth.php';
	epc_deploy_require_token();
}

define('_ASTEXE_', 1);
$docRoot = __DIR__;
require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();

$apply = $isCli
	? in_array('--apply', $argv ?? array(), true)
	: (!empty($_GET['apply']) || !empty($_POST['apply']));

$host = 'www.epartscart.com';
if ($isCli) {
	foreach ($argv ?? array() as $arg) {
		if (strpos($arg, '--host=') === 0) {
			$host = strtolower(trim(substr($arg, 7)));
		}
	}
} else {
	$reqHost = strtolower((string) ($_SERVER['HTTP_HOST'] ?? $host));
	if (strpos($reqHost, ':') !== false) {
		$reqHost = explode(':', $reqHost, 2)[0];
	}
	if ($reqHost !== '') {
		$host = $reqHost;
	}
	if (!empty($_GET['host'])) {
		$host = strtolower(trim((string) $_GET['host']));
	}
}

// Prefer per-host tenant DB mapping when present (shared ecomae docroot).
$epcTenantHostDbFile = $docRoot . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($DP_Config, $epcTk)) {
				$DP_Config->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}

function epc_brochure_setup_pdo($DP_Config): PDO
{
	$dbHost = trim((string) $DP_Config->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	return new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

/**
 * @param array{url:string,php:string,alias:string,title:string,desc:string,keywords:string,order:int,frontend:int} $spec
 */
function epc_brochure_upsert_content(PDO $pdo, array $spec, bool $apply): int
{
	$url = $spec['url'];
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = ? AND `url` = ? LIMIT 1');
	$st->execute(array((int) $spec['frontend'], $url));
	$id = (int) $st->fetchColumn();
	echo ($spec['frontend'] ? '  frontend' : '  backend') . " /{$url}";
	echo $id > 0 ? " id={$id}" : ' (new)';
	echo "\n";
	if (!$apply) {
		return $id;
	}
	$now = time();
	// CMS expects JSON module ids (see dp_core json_decode), not PHP serialize.
	$modules = '[]';
	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `content` = ?, `content_type` = ?, `title_tag` = ?, `description_tag` = ?,
			 `published_flag` = 1, `time_edited` = ?, `alias` = ?, `value` = ?, `keywords_tag` = ? WHERE `id` = ?'
		)->execute(array(
			$spec['php'], 'php', $spec['title'], $spec['desc'], $now,
			$spec['alias'], $spec['alias'], $spec['keywords'], $id,
		));
		echo "    updated\n";
		return $id;
	}
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, 1, ?, ?, 0, ?, ?, ?, ?, ?, ?, ?, ?, 0, ?, "", "", 0, 1, 0, ?, ?, ?)'
	)->execute(array(
		$url,
		$spec['alias'],
		$spec['alias'],
		$spec['desc'],
		(int) $spec['frontend'],
		'php',
		$spec['php'],
		$spec['title'],
		$spec['desc'],
		$spec['keywords'],
		'0',
		$modules,
		$now,
		$now,
		(int) $spec['order'],
	));
	$id = (int) $pdo->lastInsertId();
	echo "    inserted id={$id}\n";
	return $id;
}

function epc_brochure_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare(
		'INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)'
	)->execute(array($key, $en));
	$pdo->prepare(
		'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
	)->execute(array($key, 'en', $en));
	$pdo->prepare(
		'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
	)->execute(array($key, 'ru', $ru));
}

/** @return list<int> */
function epc_brochure_setup_access_groups(PDO $pdo): array
{
	$groups = array();
	foreach (array('control/config', 'control', 'control/cp-guideline', 'shop/orders/orders') as $refUrl) {
		$st = $pdo->prepare(
			'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
			 INNER JOIN `content` c ON c.`id` = ca.`content_id`
			 WHERE c.`url` = ? AND c.`is_frontend` = 0'
		);
		$st->execute(array($refUrl));
		while ($gid = $st->fetchColumn()) {
			$groups[(int) $gid] = true;
		}
	}
	try {
		$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1');
		while ($gid = $st->fetchColumn()) {
			$groups[(int) $gid] = true;
		}
	} catch (Throwable $e) {
	}
	if (!$groups) {
		$groups[1] = true;
	}
	return array_keys($groups);
}

function epc_brochure_setup_menu(PDO $pdo, string $menuUrl, string $caption, string $icon, string $color): void
{
	$systemGroup = (int) $pdo->query('SELECT `id` FROM `control_groups` ORDER BY `order` ASC LIMIT 1')->fetchColumn();
	if ($systemGroup <= 0) {
		$systemGroup = 1;
	}
	$menuOrder = 2;
	$ref = $pdo->prepare('SELECT `order` FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
	$ref->execute(array('%/control/cp-guideline%'));
	$refOrder = $ref->fetchColumn();
	if ($refOrder !== false) {
		$menuOrder = max(1, (int) $refOrder + 1);
	}

	$menuCheck = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? OR `caption` = ? LIMIT 1');
	$menuCheck->execute(array($menuUrl, $caption));
	$controlId = (int) $menuCheck->fetchColumn();

	if ($controlId <= 0) {
		$pdo->prepare(
			'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1)'
		)->execute(array($systemGroup, $caption, $menuUrl, '', $menuOrder, $color, $icon, ''));
		$controlId = (int) $pdo->lastInsertId();
		echo "control_items created: {$controlId} ({$caption})\n";
		return;
	}
	$pdo->prepare(
		'UPDATE `control_items` SET `items_group` = ?, `caption` = ?, `url` = ?, `order` = ?, `background_color` = ?, `fontawesome_class` = ?, `show_anyway` = 1 WHERE `id` = ?'
	)->execute(array($systemGroup, $caption, $menuUrl, $menuOrder, $color, $icon, $controlId));
	echo "control_items updated: {$controlId} ({$caption})\n";
}

$pdo = epc_brochure_setup_pdo($DP_Config);
echo "Host={$host} DB={$DP_Config->db}\n";

$isSuperHost = (strpos($host, 'ecomae.com') !== false);

$pages = array(
	array(
		'url' => 'control/cp_brochure',
		'php' => '/<backend_dir>/content/control/epc_cp_brochure_page.php',
		'alias' => 'CP full brochure',
		'title' => 'Control Panel — full brochure',
		'desc' => 'Printable catalogue of every Control Panel function for training and customer education.',
		'keywords' => 'cp brochure, training, capabilities',
		'order' => 12,
		'frontend' => 0,
	),
);

if ($isSuperHost) {
	$pages[] = array(
		'url' => 'control/portal/epc_boc_product_brochure',
		'php' => '/<backend_dir>/content/control/portal/epc_boc_product_brochure.php',
		'alias' => 'Product brochure',
		'title' => 'Product brochure — Super CP',
		'desc' => 'Open the marketing product brochure and full CP deck from BOC Knowledge.',
		'keywords' => 'brochure, marketing, BOC',
		'order' => 13,
		'frontend' => 0,
	);
} else {
	$pages[] = array(
		'url' => 'brochure',
		'php' => '/content/general_pages/epc_epartscart_brochure.php',
		'alias' => 'Brochure',
		'title' => 'eParts Cart — Product brochure',
		'desc' => 'Graphical brochure: storefront, OMS, Control Panel overview for spare parts trading.',
		'keywords' => 'epartscart, brochure, OMS, control panel, spare parts',
		'order' => 90,
		'frontend' => 1,
	);
	$pages[] = array(
		'url' => 'brochure-cp',
		'php' => '/content/general_pages/epc_epartscart_cp_brochure.php',
		'alias' => 'CP Brochure',
		'title' => 'eParts Cart — Full Control Panel brochure',
		'desc' => 'Every Client CP function: OMS, prices, warehouses, ERP modules, AI, marketing, and more — printable catalogue.',
		'keywords' => 'control panel, CP brochure, OMS, ERP, warehouses, AI agent',
		'order' => 91,
		'frontend' => 1,
	);
}

$contentIds = array();
foreach ($pages as $spec) {
	$contentIds[$spec['url']] = epc_brochure_upsert_content($pdo, $spec, $apply);
}

if (!$apply) {
	echo "Dry run. Re-run with --apply or apply=1 to upsert.\n";
	exit(0);
}

$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}

$pdo->prepare(
	'UPDATE `content` SET `content` = ? WHERE `is_frontend` = 0 AND `url` = ?'
)->execute(array(
	'/' . $backend . '/content/control/epc_cp_brochure_page.php',
	'control/cp_brochure',
));

if ($isSuperHost) {
	$pdo->prepare(
		'UPDATE `content` SET `content` = ? WHERE `is_frontend` = 0 AND `url` = ?'
	)->execute(array(
		'/' . $backend . '/content/control/portal/epc_boc_product_brochure.php',
		'control/portal/epc_boc_product_brochure',
	));
}

epc_brochure_setup_lang($pdo, 'epc_cp_brochure', 'CP brochure', 'Брошюра CP');
epc_brochure_setup_lang($pdo, 'epc_cp_brochure_desc', 'Full Control Panel brochure — every function', 'Полная брошюра панели управления');
echo "lang strings: epc_cp_brochure\n";

$accessGroups = epc_brochure_setup_access_groups($pdo);
foreach (array('control/cp_brochure', 'control/portal/epc_boc_product_brochure') as $url) {
	if (!isset($contentIds[$url]) || (int) $contentIds[$url] <= 0) {
		$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 0 AND `url` = ? LIMIT 1');
		$st->execute(array($url));
		$contentIds[$url] = (int) $st->fetchColumn();
	}
	$cid = (int) ($contentIds[$url] ?? 0);
	if ($cid <= 0) {
		continue;
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($cid));
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	foreach ($accessGroups as $gid) {
		$ins->execute(array($cid, (int) $gid));
	}
	echo "content_access {$url} id={$cid}: " . implode(',', $accessGroups) . "\n";
}

epc_brochure_setup_menu(
	$pdo,
	'/<backend>/control/cp_brochure',
	'epc_cp_brochure',
	'fas fa-book',
	'#0f766e'
);

echo "Done.\n";
echo "  https://{$host}/brochure\n";
if ($isSuperHost) {
	echo "  https://{$host}/brochure/cp\n";
	echo "  https://{$host}/{$backend}/control/cp_brochure\n";
	echo "  https://{$host}/{$backend}/control/portal/epc_boc_product_brochure\n";
} else {
	echo "  https://{$host}/brochure-cp\n";
	echo "  https://{$host}/{$backend}/control/cp_brochure\n";
}
