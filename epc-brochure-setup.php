<?php
/**
 * Register public /brochure page on a tenant (e.g. epartscart.com).
 * Usage: php epc-brochure-setup.php [--apply] [--host=www.epartscart.com]
 */
define('_ASTEXE_', 1);
$docRoot = __DIR__;
require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();

$apply = in_array('--apply', $argv ?? array(), true);
$host = 'www.epartscart.com';
foreach ($argv ?? array() as $arg) {
	if (strpos($arg, '--host=') === 0) {
		$host = strtolower(trim(substr($arg, 7)));
	}
}

function epc_brochure_setup_pdo($DP_Config): PDO
{
	$host = trim((string) $DP_Config->host);
	if ($host === '' || strtolower($host) === 'localhost') {
		$host = '127.0.0.1';
	}
	return new PDO(
		'mysql:host=' . $host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

$pdo = epc_brochure_setup_pdo($DP_Config);
$url = 'brochure';
$phpPath = '/content/general_pages/epc_epartscart_brochure.php';
$title = 'eParts Cart — Product brochure';
$desc = 'Graphical brochure: storefront, OMS, Control Panel, prices, warehouses, AI Parts Expert, and ERP for spare parts trading.';

$st = $pdo->prepare('SELECT `id`, `content`, `published_flag` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
$st->execute(array($url));
$row = $st->fetch(PDO::FETCH_ASSOC);

echo "Host context DB={$DP_Config->db} target_url=/{$url}\n";
if ($row) {
	echo '  existing content id=' . (int) $row['id'] . ' published=' . (int) $row['published_flag'] . "\n";
} else {
	echo "  no content row yet\n";
}

if (!$apply) {
	echo "Dry run. Re-run with --apply to upsert.\n";
	exit(0);
}

$now = time();
$modules = 'a:0:{}';
if ($row) {
	$pdo->prepare(
		'UPDATE `content` SET `content` = ?, `content_type` = ?, `title_tag` = ?, `description_tag` = ?,
		 `published_flag` = 1, `time_edited` = ?, `alias` = ?, `value` = ? WHERE `id` = ?'
	)->execute(array($phpPath, 'php', $title, $desc, $now, 'Brochure', 'Brochure', (int) $row['id']));
	echo "Updated content id=" . (int) $row['id'] . "\n";
} else {
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, 1, ?, ?, 0, ?, 1, ?, ?, ?, ?, ?, ?, 0, ?, "", "", 0, 1, 0, ?, ?, ?)'
	)->execute(array(
		$url,
		'Brochure',
		'Brochure',
		$desc,
		'php',
		$phpPath,
		$title,
		$desc,
		'epartscart, brochure, OMS, control panel, spare parts',
		'0',
		$modules,
		$now,
		$now,
		90,
	));
	echo "Inserted brochure content row.\n";
}

echo "Done. Visit https://{$host}/brochure\n";
