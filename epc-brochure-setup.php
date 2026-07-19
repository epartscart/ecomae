<?php
/**
 * Register product + full CP brochures on a tenant (e.g. epartscart.com).
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

/**
 * @param array{url:string,php:string,alias:string,title:string,desc:string,keywords:string,order:int,frontend:int} $spec
 */
function epc_brochure_upsert_content(PDO $pdo, array $spec, bool $apply): void
{
	$url = $spec['url'];
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = ? AND `url` = ? LIMIT 1');
	$st->execute(array((int) $spec['frontend'], $url));
	$id = (int) $st->fetchColumn();
	echo ($spec['frontend'] ? '  frontend' : '  backend') . " /{$url}";
	echo $id > 0 ? " id={$id}" : ' (new)';
	echo "\n";
	if (!$apply) {
		return;
	}
	$now = time();
	$modules = 'a:0:{}';
	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `content` = ?, `content_type` = ?, `title_tag` = ?, `description_tag` = ?,
			 `published_flag` = 1, `time_edited` = ?, `alias` = ?, `value` = ?, `keywords_tag` = ? WHERE `id` = ?'
		)->execute(array(
			$spec['php'], 'php', $spec['title'], $spec['desc'], $now,
			$spec['alias'], $spec['alias'], $spec['keywords'], $id,
		));
		echo "    updated\n";
		return;
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
	echo "    inserted\n";
}

$pdo = epc_brochure_setup_pdo($DP_Config);
echo "Host context DB={$DP_Config->db}\n";

$pages = array(
	array(
		'url' => 'brochure',
		'php' => '/content/general_pages/epc_epartscart_brochure.php',
		'alias' => 'Brochure',
		'title' => 'eParts Cart — Product brochure',
		'desc' => 'Graphical brochure: storefront, OMS, Control Panel overview for spare parts trading.',
		'keywords' => 'epartscart, brochure, OMS, control panel, spare parts',
		'order' => 90,
		'frontend' => 1,
	),
	array(
		'url' => 'brochure-cp',
		'php' => '/content/general_pages/epc_epartscart_cp_brochure.php',
		'alias' => 'CP Brochure',
		'title' => 'eParts Cart — Full Control Panel brochure',
		'desc' => 'Every Client CP function: OMS, prices, warehouses, ERP modules, AI, marketing, and more — printable catalogue.',
		'keywords' => 'control panel, CP brochure, OMS, ERP, warehouses, AI agent',
		'order' => 91,
		'frontend' => 1,
	),
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

foreach ($pages as $spec) {
	epc_brochure_upsert_content($pdo, $spec, $apply);
}

if (!$apply) {
	echo "Dry run. Re-run with --apply to upsert.\n";
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

echo "Done.\n";
echo "  https://{$host}/brochure\n";
echo "  https://{$host}/brochure-cp\n";
echo "  https://{$host}/{$backend}/control/cp_brochure\n";
