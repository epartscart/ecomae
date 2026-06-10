<?php
/**
 * Fix docpart MySQL credentials for Model C tenants (epartscart + taxofinca).
 * https://www.ecomae.com/epc-docpart-db-fix.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$apply = !empty($_GET['apply']);
$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$targetPass = trim((string) ($_GET['db_password'] ?? 'EpC4rt_Db_2026_xK9mQ2'));
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$configPath = $platformDocroot . '/config.php';

echo "=== docpart DB fix ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'target_password_len=' . strlen($targetPass) . "\n\n";

$candidates = array();
if ($targetPass !== '') {
	$candidates[] = $targetPass;
}
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
if (!empty($cfg->password)) {
	$candidates[] = (string) $cfg->password;
}
$scanRoots = array(
	'/home/epartscart/htdocs/www.epartscart.com',
	'/home/epartscart/htdocs/www.taxofinca.com',
	$platformDocroot,
);
foreach ($scanRoots as $root) {
	foreach (array('config.local.php', 'config.php') as $name) {
		$path = $root . '/' . $name;
		if (!is_file($path)) {
			continue;
		}
		$text = (string) @file_get_contents($path);
		if (preg_match("/['\"]password['\"]\s*=>\s*['\"]([^'\"]+)['\"]/", $text, $m)) {
			$candidates[] = $m[1];
		}
		if (preg_match('/public\s+\$password\s*=\s*[\'"]([^\'"]+)[\'"]/', $text, $m2)) {
			$candidates[] = $m2[1];
		}
	}
}
foreach (array(
	'EpC4rt_Db_2026_xK9mQ2',
	'2674f7feac3e3ac95ba8a965',
	'166397986a03c403fe2c4111',
	'79abee21d9e877496601e206',
	'ec9bbf589990e04516e5c121',
) as $p) {
	$candidates[] = $p;
}
$candidates = array_values(array_unique(array_filter($candidates)));

$workingPass = '';
foreach ($candidates as $pass) {
	try {
		$pdo = new PDO('mysql:host=127.0.0.1;dbname=docpart;charset=utf8', 'docpart', $pass);
		$tables = (int) $pdo->query('SHOW TABLES')->rowCount();
		echo "TRY ok len=" . strlen($pass) . " tables={$tables}\n";
		$workingPass = $pass;
		break;
	} catch (Exception $e) {
		echo 'TRY fail len=' . strlen($pass) . "\n";
	}
}

if ($workingPass === '' && $apply) {
	echo "\n=== MySQL ALTER USER docpart (root) ===\n";
	$sql = "ALTER USER 'docpart'@'localhost' IDENTIFIED BY " . var_export($targetPass, true) . "; FLUSH PRIVILEGES;";
	foreach (array(
		'mysql -e ' . escapeshellarg($sql),
		'sudo -n mysql -e ' . escapeshellarg($sql),
		'sudo mysql -e ' . escapeshellarg($sql),
	) as $cmd) {
		$r = epc_clp_run_cmd($cmd);
		echo $cmd . ': ' . substr($r['output'], 0, 200) . ' [exit=' . $r['code'] . "]\n";
		if ($r['code'] === 0) {
			try {
				$pdo = new PDO('mysql:host=127.0.0.1;dbname=docpart;charset=utf8', 'docpart', $targetPass);
				$workingPass = $targetPass;
				echo 'ALTER USER ok tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
				break;
			} catch (Exception $e) {
				echo 'still fail: ' . $e->getMessage() . "\n";
			}
		}
	}
}

function epc_ddf_try_docpart_pdo(string $pass): ?PDO
{
	foreach (array('127.0.0.1', 'localhost') as $host) {
		try {
			$pdo = new PDO("mysql:host={$host};dbname=docpart;charset=utf8", 'docpart', $pass);
			$pdo->query('SELECT 1');
			return $pdo;
		} catch (Exception $e) {
			echo "PDO {$host}: " . $e->getMessage() . "\n";
		}
	}
	return null;
}

function epc_ddf_clp_create_docpart_database(string $clpPass, string $targetPass): bool
{
	$cookie = '';
	if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
		echo "CloudPanel login failed\n";
		return false;
	}
	$panel = epc_clp_panel_url();
	$list = epc_clp_web_request($panel . '/site/www.ecomae.com/databases', array(), $cookie);
	if (stripos($list, 'database/user/edit/docpart') !== false
		|| preg_match('/>\s*docpart\s*</i', $list)) {
		echo "docpart database already listed in CloudPanel\n";
		return true;
	}
	$form = epc_clp_web_request($panel . '/site/www.ecomae.com/database/new', array(), $cookie);
	if (!preg_match('/name="site_database\[_token\]" value="([^"]+)"/', $form, $tm)) {
		$form = epc_clp_web_request($panel . '/site/www.ecomae.com/databases/new', array(), $cookie);
		if (!preg_match('/name="site_database\[_token\]" value="([^"]+)"/', $form, $tm)) {
			echo "DB create form token not found (len=" . strlen($form) . ")\n";
			echo substr($form, 0, 800) . "\n";
			return false;
		}
	}
	$body = http_build_query(array(
		'site_database' => array(
			'name' => 'docpart',
			'userName' => 'docpart',
			'userPassword' => $targetPass,
			'submit' => 'Create',
			'_token' => $tm[1],
		),
	));
	$resp = epc_clp_web_request($panel . '/site/www.ecomae.com/database/new', array(
		'method' => 'POST',
		'body' => $body,
	), $cookie);
	echo 'CLP create docpart database len=' . strlen($resp)
		. ' err=' . (stripos($resp, 'Error Occurred') !== false ? 'yes' : 'no') . "\n";
	return stripos($resp, 'Error Occurred') === false;
}

function epc_ddf_clp_import_docpart_from_epartscart(): bool
{
	$dump = '/tmp/docpart-from-epartscart.sql.gz';
	@unlink($dump);
	$exp = epc_clp_run_cmd('/usr/bin/clpctl db:export --databaseName=epartscart --file=' . escapeshellarg($dump));
	echo 'export epartscart: ' . substr(trim($exp['output']), 0, 300) . ' [exit=' . $exp['code'] . "]\n";
	if (!is_file($dump)) {
		foreach (glob('/home/ecomae/backups/*/docpart-database.sql.gz') ?: array() as $src) {
			if (@copy($src, $dump)) {
				echo 'copied backup ' . $src . "\n";
				break;
			}
		}
	}
	if (!is_file($dump)) {
		foreach (glob('/home/ecomae/backups/*/docpart_database.sql.gz') ?: array() as $src) {
			if (@copy($src, $dump)) {
				echo 'copied backup ' . $src . "\n";
				break;
			}
		}
	}
	if (!is_file($dump)) {
		echo "export file missing — import skipped\n";
		return false;
	}
	$imp = epc_clp_run_cmd('/usr/bin/clpctl db:import --databaseName=docpart --file=' . escapeshellarg($dump));
	echo 'import docpart: ' . substr(trim($imp['output']), 0, 300) . ' [exit=' . $imp['code'] . "]\n";
	return $imp['code'] === 0;
}

function epc_ddf_clp_recreate_docpart_user(string $clpPass, string $targetPass): bool
{
	$cookie = '';
	if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
		echo "CloudPanel login failed\n";
		return false;
	}
	$panel = epc_clp_panel_url();
	$databases = epc_clp_web_request($panel . '/site/www.ecomae.com/databases', array(), $cookie);
	if (stripos($databases, 'database/user/edit/docpart') !== false
		&& preg_match('#/site/www\.ecomae\.com/database/user/delete/docpart\?token=([^"\']+)#', $databases, $dm)) {
		$deleteUrl = $panel . '/site/www.ecomae.com/database/user/delete/docpart?token=' . urlencode($dm[1]);
		$delResp = epc_clp_web_request($deleteUrl, array('method' => 'POST', 'body' => ''), $cookie);
		echo 'CLP delete docpart user len=' . strlen($delResp) . "\n";
	} else {
		echo "docpart user not listed for delete (may already be absent)\n";
	}

	$newForm = epc_clp_web_request($panel . '/site/www.ecomae.com/database/user/new', array(), $cookie);
	if (!preg_match('/name="site_database_user\[_token\]" value="([^"]+)"/', $newForm, $tm)) {
		echo substr($newForm, 0, 1200) . "\n";
		return false;
	}
	$dbId = '';
	if (preg_match_all('/name="site_database_user\[database\]"[^>]*value="(\d+)"[^>]*>([^<]+)</', $newForm, $dbm, PREG_SET_ORDER)) {
		foreach ($dbm as $row) {
			if (stripos($row[2], 'docpart') !== false) {
				$dbId = $row[1];
				break;
			}
		}
	}
	if ($dbId === '' && preg_match('/name="site_database_user\[database\]"[^>]*>.*?value="(\d+)"[^>]*>docpart/s', $newForm, $dbm2)) {
		$dbId = $dbm2[1];
	}
	if ($dbId === '') {
		echo "docpart database id not found in CLP form\n";
		return false;
	}
	$data = array(
		'site_database_user' => array(
			'userName' => 'docpart',
			'password' => $targetPass,
			'database' => $dbId,
			'permissions' => 'rw',
			'_token' => $tm[1],
			'submit' => '',
		),
	);
	$createResp = epc_clp_web_request($panel . '/site/www.ecomae.com/database/user/new', array(
		'method' => 'POST',
		'body' => http_build_query($data),
	), $cookie);
	echo 'CLP create docpart user len=' . strlen($createResp)
		. ' err=' . (stripos($createResp, 'Error Occurred') !== false ? 'yes' : 'no') . "\n";
	return stripos($createResp, 'Error Occurred') === false;
}

if ($workingPass === '' && $apply && epc_clp_available()) {
	echo "\n=== clpctl db:add docpart (full grants) ===\n";
	$r = epc_clp_provision_database(array(
		'domain' => 'www.ecomae.com',
		'database_name' => 'docpart',
		'database_user' => 'docpart',
		'database_password' => $targetPass,
	));
	foreach ($r['log'] as $line) {
		echo $line . "\n";
	}
	$pdo = epc_ddf_try_docpart_pdo($targetPass);
	if ($pdo instanceof PDO) {
		$workingPass = $targetPass;
		echo 'after clpctl db:add: tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
	}
}

if ($workingPass === '' && $apply && $clpPass !== '') {
	echo "\n=== CloudPanel create docpart database ===\n";
	epc_ddf_clp_create_docpart_database($clpPass, $targetPass);
	$pdo = epc_ddf_try_docpart_pdo($targetPass);
	if ($pdo instanceof PDO && (int) $pdo->query('SHOW TABLES')->rowCount() < 5) {
		echo "\n=== Import docpart schema from epartscart ===\n";
		epc_ddf_clp_import_docpart_from_epartscart();
		$pdo = epc_ddf_try_docpart_pdo($targetPass);
	}
	if ($pdo instanceof PDO) {
		$workingPass = $targetPass;
		echo 'after CLP database create: tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
	}
}

if ($workingPass === '' && $apply && $clpPass !== '') {
	echo "\n=== CloudPanel recreate docpart user ===\n";
	if (epc_ddf_clp_recreate_docpart_user($clpPass, $targetPass)) {
		$pdo = epc_ddf_try_docpart_pdo($targetPass);
		if ($pdo instanceof PDO) {
			$workingPass = $targetPass;
			echo 'after CLP recreate: tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
		}
	}
}

if ($workingPass === '') {
	exit("\nFAIL: no working docpart password. Pass clp_pass= with apply=1 to reset via CloudPanel.\n");
}

$pdoCheck = epc_ddf_try_docpart_pdo($workingPass);
if ($apply && $pdoCheck instanceof PDO) {
	$tableCount = (int) $pdoCheck->query('SHOW TABLES')->rowCount();
	echo "\n=== docpart table count: {$tableCount} ===\n";
	if ($tableCount < 50 || !empty($_GET['force_import'])) {
		echo "=== Import docpart schema from epartscart ===\n";
		epc_ddf_clp_import_docpart_from_epartscart();
		$pdoCheck = epc_ddf_try_docpart_pdo($workingPass);
		if ($pdoCheck instanceof PDO) {
			echo 'after import: tables=' . $pdoCheck->query('SHOW TABLES')->rowCount() . "\n";
		}
	}
}

if ($apply && is_file($configPath) && is_writable($configPath)) {
	$text = (string) file_get_contents($configPath);
	$newText = preg_replace(
		'/public\s+\$password\s*=\s*[\'"][^\'"]*[\'"]/',
		"public \$password = " . var_export($workingPass, true),
		$text,
		1
	);
	if ($newText !== null && $newText !== $text) {
		file_put_contents($configPath, $newText);
		echo "Updated {$configPath} password\n";
	}
}

$ecomaeDbPass = '';
if (is_file($platformDocroot . '/config.local.php')) {
	$epc_config_local = null;
	require $platformDocroot . '/config.local.php';
	$ecomaeDbPass = (string) ($epc_config_local['password'] ?? '');
}
if ($apply && $ecomaeDbPass !== '') {
	try {
		$pdoE = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $ecomaeDbPass);
		epc_portal_db_ensure($pdoE);
		foreach (array('epartscart', 'taxofinca', 'electronicae', 'stylenlook', 'thejewellerytrend') as $siteKey) {
			$tpl = epc_portal_tenant_templates()[$siteKey];
			$save = epc_portal_save_tenant($pdoE, array(
				'site_key' => $siteKey,
				'hostname' => $tpl['hostname'],
				'industry_code' => $tpl['industry'],
				'status' => 'live',
				'trade_name' => $tpl['trade_name'],
				'hub_name' => $tpl['hub_name'],
				'from_email' => $tpl['from_email'],
				'db_name' => 'docpart',
				'db_user' => 'docpart',
				'db_password' => $workingPass,
				'notes' => 'epc-docpart-db-fix.php',
			));
			echo "tenant {$siteKey}: " . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
		}
	} catch (Exception $e) {
		echo 'ecomae tenant sync: ' . $e->getMessage() . "\n";
	}
}

function epc_ddf_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$hint = '';
	if (is_string($body) && $body !== '') {
		if (stripos($body, 'No DB connect') !== false) {
			$hint = ' [no-db]';
		} elseif (stripos($body, 'License error') !== false) {
			$hint = ' [license]';
		} elseif (stripos($body, '<html') !== false || stripos($body, '<!DOCTYPE') !== false) {
			$hint = ' [html]';
		}
	}
	return "HTTP {$code}{$hint}";
}

echo "\n=== Probes ===\n";
foreach (array('www.epartscart.com', 'www.taxofinca.com') as $host) {
	echo "  https://{$host}/cp/: " . epc_ddf_probe("https://{$host}/cp/") . "\n";
}
echo "\nDone.\n";
