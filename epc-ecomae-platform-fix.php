<?php
/**
 * Fix platform DB: clone docpart schema, deactivate legacy deploy targets.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$runAction = trim((string) ($_POST['run_action'] ?? $_GET['run_action'] ?? ''));
if ($runAction === 'ping') {
	echo "pong\n";
	exit;
}
if ($runAction === 'asap_isolate') {
	require __DIR__ . '/epc-asap-run-isolate.php';
	exit;
}
if ($runAction === 'asap_verify') {
	require __DIR__ . '/epc-asap-verify-isolation.php';
	exit;
}
if ($runAction === 'asap_clp_user') {
	require __DIR__ . '/epc-asap-clp-db-user.php';
	exit;
}
if ($runAction === 'install_poppler') {
	require __DIR__ . '/epc-install-poppler.php';
	exit;
}

$clpPass = trim((string) ($_GET['clp_pass'] ?? $_POST['clp_pass'] ?? ''));
$dbPass = trim((string) ($_GET['db_password'] ?? ''));
$srcDb = trim((string) ($_GET['src_db'] ?? 'docpart'));

$pushRel = str_replace('\\', '/', trim((string) ($_POST['push_rel'] ?? $_GET['push_rel'] ?? '')));
$pushB64 = (string) ($_POST['push_b64'] ?? $_GET['push_b64'] ?? '');
if ($pushRel !== '' && $pushB64 !== '' && strpos($pushRel, '..') === false && $pushRel[0] !== '/') {
	$bin = base64_decode($pushB64, true);
	if ($bin !== false) {
		foreach (array(
			'/home/ecomae/htdocs/www.ecomae.com/',
			'/home/ecomae/htdocs/cp.ecomae.com/',
			'/home/ecomaecp/htdocs/cp.ecomae.com/',
			'/home/epartscart/htdocs/www.epartscart.com/',
			'/home/electronicae/htdocs/www.electronicae.com/',
			'/home/epartscart/htdocs/www.taxofinca.com/',
			'/home/taxofinca/htdocs/www.taxofinca.com/',
			'/home/taxofinca/htdocs/www.taxofinca.com/public/',
			'/home/taxofinca/htdocs/taxofinca.com/',
			'/home/stylenlook/htdocs/www.stylenlook.com/',
			'/home/thejewellerytrend/htdocs/www.thejewellerytrend.com/',
		) as $root) {
			if (!is_dir($root)) {
				continue;
			}
			$dest = $root . $pushRel;
			$dir = dirname($dest);
			if (!is_dir($dir)) {
				mkdir($dir, 0755, true);
			}
			file_put_contents($dest, $bin);
		}
		echo "push_file: {$pushRel} bytes=" . strlen($bin) . "\n";
		$alsoRun = trim((string) ($_GET['also_run'] ?? $_POST['also_run'] ?? ''));
		if ($alsoRun === 'asap_isolate') {
			require __DIR__ . '/epc-asap-run-isolate.php';
			exit;
		}
		if ($alsoRun === 'asap_verify') {
			require __DIR__ . '/epc-asap-verify-isolation.php';
			exit;
		}
		if ($alsoRun === 'asap_clp_user') {
			require __DIR__ . '/epc-asap-clp-db-user.php';
			exit;
		}
		if ($alsoRun === '' && empty($_GET['also_clone']) && empty($_POST['also_clone'])) {
			exit("Push done.\n");
		}
	}
}

if ($clpPass !== '') {
	require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
	$cookie = '';
	epc_clp_web_login('admin', $clpPass, $cookie);
	if (!empty($_GET['reset_deploy_zip'])) {
		@unlink('/tmp/docpart-epartscart-site.zip');
		echo "reset_deploy_zip: removed stale zip if present\n";
	}
	$dump = '/tmp/ecomae-clone.sql.gz';
	@unlink($dump);
	$exp = epc_clp_run_cmd('/usr/bin/clpctl db:export --databaseName=' . escapeshellarg($srcDb) . ' --file=' . escapeshellarg($dump));
	echo "export: " . $exp['output'] . "\n";
	if (is_file($dump)) {
		$imp = epc_clp_run_cmd('/usr/bin/clpctl db:import --databaseName=ecomae --file=' . escapeshellarg($dump));
		echo "import: " . substr($imp['output'], 0, 500) . " code=" . $imp['code'] . "\n";
		if ($imp['code'] !== 0 || stripos($imp['output'], 'error') !== false) {
			$sql = '/tmp/ecomae-clone.sql';
			exec('gunzip -c ' . escapeshellarg($dump) . ' > ' . escapeshellarg($sql) . ' 2>&1', $gzOut, $gzCode);
			echo "gunzip code={$gzCode}\n";
			if (is_file($sql) && $dbPass !== '') {
				$mysql = 'mysql -u ecomae -p' . escapeshellarg($dbPass) . ' ecomae < ' . escapeshellarg($sql) . ' 2>&1';
				exec($mysql, $myOut, $myCode);
				echo "mysql fallback code={$myCode} " . implode(' ', array_slice($myOut, 0, 3)) . "\n";
			}
		}
	}
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
if ($dbPass !== '') {
	$cfg->password = $dbPass;
	$cfg->db = 'ecomae';
	$cfg->user = 'ecomae';
}

$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db, $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
epc_portal_db_ensure($pdo);

	$pdo->exec("UPDATE `epc_portal_deploy_targets` SET `active` = 0");
	$pdo->exec("DELETE FROM `epc_portal_deploy_targets` WHERE `site_key` != 'ecomae'");
$ins = $pdo->prepare(
	'INSERT INTO `epc_portal_deploy_targets` (`site_key`, `hostname`, `industry_code`, `chunk_url`, `extract_url`, `setup_url`, `active`)
	VALUES (\'ecomae\', \'www.ecomae.com\', \'platform_host\', ?, ?, ?, 1)
	ON DUPLICATE KEY UPDATE `active` = 1, `hostname` = VALUES(`hostname`)'
);
$token = epc_deploy_token();
$ins->execute(array(
	'https://www.ecomae.com/chunk-receiver.php',
	'https://www.ecomae.com/extract-zip-ecomae.php?token=' . $token,
	'https://www.ecomae.com/epc-ecomae-setup.php',
));

if ($dbPass !== '') {
	$cfgPhp = "<?php\n\$epc_config_local = array(\n\t'password' => " . var_export($dbPass, true) . ",\n\t'db' => 'ecomae',\n\t'user' => 'ecomae',\n\t'domain_path' => 'https://www.ecomae.com/',\n);\n";
	file_put_contents('/home/ecomae/htdocs/www.ecomae.com/config.local.php', $cfgPhp);
	$cpCfg = str_replace('www.ecomae.com', 'cp.ecomae.com', $cfgPhp);
	@file_put_contents('/home/ecomae/htdocs/cp.ecomae.com/config.local.php', $cpCfg);
	@file_put_contents('/home/ecomaecp/htdocs/cp.ecomae.com/config.local.php', $cpCfg);
	echo "Wrote config.local.php (if paths writable)\n";
}

$lang = (int) $pdo->query('SELECT COUNT(*) FROM `lang_languages`')->fetchColumn();
echo "lang_languages={$lang}\n";
$targets = $pdo->query('SELECT hostname FROM `epc_portal_deploy_targets` WHERE active=1')->fetchAll(PDO::FETCH_COLUMN);
echo "active targets: " . implode(', ', $targets) . "\n";
echo "Done.\n";
