<?php
/**
 * CP content audit — scan ALL backend content rows, detect missing PHP scripts on disk.
 * https://www.epartscart.com/epc-cp-content-audit.php?token=epartscart-deploy-2026
 * Apply DB fixes + inline setup scripts: &apply=1
 * JSON only (default).
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '0');
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$apply = !empty($_GET['apply']);
$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__), '/');
$baseHost = 'https://' . preg_replace('/^www\./', '', (string) ($_SERVER['HTTP_HOST'] ?? 'localhost'));

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

function epc_cca_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare(
		'INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`)
		 VALUES (?, ?, NULL, 0, 1, 1)'
	)->execute(array($key, $en));
	$pdo->prepare(
		'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?)
		 ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
	)->execute(array($key, 'en', $en));
	$pdo->prepare(
		'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?)
		 ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
	)->execute(array($key, 'ru', $ru));
}

function epc_cca_deploy_rel(string $backend, string $dbContentPath): string
{
	$path = str_replace('<backend_dir>', $backend, $dbContentPath);
	$path = ltrim($path, '/');
	if (strpos($path, $backend . '/content/') === 0) {
		return 'cp/content/' . substr($path, strlen($backend . '/content/'));
	}
	if (strpos($path, 'cp/content/') === 0) {
		return $path;
	}
	return '';
}

function epc_cca_run_setup(string $script, string $token, bool $apply): array
{
	$host = (string) ($_SERVER['HTTP_HOST'] ?? 'localhost');
	$qs = http_build_query(array(
		'token' => $token,
		'apply' => $apply ? '1' : '0',
	));
	$url = 'https://' . $host . '/' . ltrim($script, '/') . '?' . $qs;
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_TIMEOUT => 120,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_FOLLOWLOCATION => true,
	));
	$out = (string) curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	$json = json_decode($out, true);
	return array(
		'script' => $script,
		'http' => $code,
		'ok' => ($code >= 200 && $code < 400 && is_array($json) && !empty($json['ok'])),
		'summary' => is_array($json)
			? array(
				'ok' => $json['ok'] ?? null,
				'missing_files' => isset($json['missing_files']) ? count((array) $json['missing_files']) : null,
				'changes' => $json['changes'] ?? array(),
			)
			: array('raw' => substr(trim($out), 0, 300)),
	);
}

$report = array(
	'ok' => true,
	'apply' => $apply,
	'timestamp' => gmdate('c'),
	'hostname' => $_SERVER['HTTP_HOST'] ?? '',
	'db' => $cfg->db,
	'backend_dir' => $backend,
	'document_root' => $docRoot,
	'total_cp_rows' => 0,
	'php_rows' => 0,
	'missing_count' => 0,
	'missing' => array(),
	'deploy_rels' => array(),
	'priority_missing' => array(),
	'changes' => array(),
	'setups' => array(),
);

$rows = $pdo->query(
	"SELECT `id`, `url`, `content`, `content_type`, `published_flag`, `value`, `alias`
	 FROM `content`
	 WHERE `is_frontend` = 0
	 ORDER BY `url` ASC"
)->fetchAll(PDO::FETCH_ASSOC);

$report['total_cp_rows'] = count($rows);

foreach ($rows as $row) {
	if ((string) ($row['content_type'] ?? '') !== 'php') {
		continue;
	}
	$report['php_rows']++;

	$dbPath = trim((string) ($row['content'] ?? ''));
	if ($dbPath === '' || $dbPath === '0') {
		$report['missing'][] = array(
			'content_id' => (int) $row['id'],
			'url' => (string) $row['url'],
			'cp_url' => '/' . $backend . '/' . ltrim((string) $row['url'], '/'),
			'db_content_path' => $dbPath,
			'resolved_path' => null,
			'deploy_rel' => null,
			'reason' => 'empty_content_path',
		);
		$report['missing_count']++;
		$report['ok'] = false;
		continue;
	}

	$resolved = str_replace('<backend_dir>', $backend, $docRoot . $dbPath);
	$exists = is_file($resolved);
	if ($exists) {
		continue;
	}

	$deployRel = epc_cca_deploy_rel($backend, $dbPath);
	$entry = array(
		'content_id' => (int) $row['id'],
		'url' => (string) $row['url'],
		'cp_url' => '/' . $backend . '/' . ltrim((string) $row['url'], '/'),
		'db_content_path' => $dbPath,
		'resolved_path' => $resolved,
		'deploy_rel' => $deployRel !== '' ? $deployRel : null,
		'published_flag' => (int) $row['published_flag'],
		'reason' => 'file_missing',
	);
	$report['missing'][] = $entry;
	$report['missing_count']++;
	$report['ok'] = false;

	if ($deployRel !== '') {
		$report['deploy_rels'][$deployRel] = true;
	}

	$url = (string) $row['url'];
	if (
		strpos($url, 'shop/orders') === 0
		|| strpos($url, 'shop/finance') === 0
		|| strpos($url, 'shop/catalogue') === 0
		|| strpos($url, 'shop/prices') === 0
		|| strpos($url, 'shop/customer') === 0
		|| strpos($url, 'shop/crm') === 0
	) {
		$report['priority_missing'][] = $entry;
	}
}

$report['deploy_rels'] = array_values(array_keys($report['deploy_rels']));
sort($report['deploy_rels']);

if ($apply) {
	epc_cca_lang($pdo, '4756', 'Page script missing', 'Скрипт страницы отсутствует');
	$report['changes'][] = 'lang string 4756 ensured';

	$token = epc_deploy_token();
	$setupScripts = array(
		'epc-orders-cp-setup.php',
		'epc-orders-items-cp-setup.php',
	);
	foreach ($setupScripts as $script) {
		if (is_file(__DIR__ . '/' . $script)) {
			$report['setups'][] = epc_cca_run_setup($script, $token, true);
		}
	}

	$pub = $pdo->prepare(
		"UPDATE `content` SET `published_flag` = 1, `time_edited` = ?
		 WHERE `is_frontend` = 0 AND `content_type` = 'php' AND `published_flag` = 0"
	);
	$pub->execute(array(time()));
	if ($pub->rowCount() > 0) {
		$report['changes'][] = 'published ' . $pub->rowCount() . ' unpublished CP rows';
	}

	if (!empty($report['deploy_rels'])) {
		$report['changes'][] = count($report['deploy_rels']) . ' files need deploy via push_one.py';
		$report['deploy_hint'] = 'python tools/deploy_cp_content_tree.py'
			. ' OR python tools/push_one.py ' . implode(' ', array_slice($report['deploy_rels'], 0, 5))
			. (count($report['deploy_rels']) > 5 ? ' ...' : '');
	}

	if (function_exists('opcache_reset')) {
		@opcache_reset();
		$report['changes'][] = 'opcache_reset';
	}
}

$report['audit_url'] = $baseHost . '/epc-cp-content-audit.php?token=' . rawurlencode(epc_deploy_token());
$report['hint'] = $report['missing_count'] === 0
	? 'All CP PHP scripts present on disk.'
	: ($apply
		? 'DB/setup applied. Deploy missing files: python tools/deploy_cp_content_tree.py'
		: 'Dry run — add apply=1 for setup scripts, then deploy cp/content via push_one.py');

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
