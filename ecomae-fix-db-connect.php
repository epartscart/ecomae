<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$dbPass = trim((string) ($_GET['db_password'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}

if ($dbPass === '') {
	$cfgPath = __DIR__ . '/config.local.php';
	if (is_file($cfgPath)) {
		$epc_config_local = null;
		require $cfgPath;
		$dbPass = (string) ($epc_config_local['password'] ?? '');
	}
}
if ($dbPass === '') {
	exit("db_password required\n");
}

try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $dbPass);
	echo 'ecomae before ok tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo 'ecomae before fail ' . $e->getMessage() . "\n";
	echo "Run ecomae-clp-recreate-db-user.php if password drifted\n";
	exit(1);
}

$cfgWww = "<?php\n\$epc_config_local = array(\n\t'password' => " . var_export($dbPass, true) . ",\n\t'db' => 'ecomae',\n\t'user' => 'ecomae',\n\t'domain_path' => 'https://www.ecomae.com/',\n\t'from_name' => 'ecomae',\n\t'from_email' => 'hello@ecomae.com',\n);\n";
$paths = array(
	'/home/ecomae/htdocs/www.ecomae.com/config.local.php' => $cfgWww,
	'/home/ecomae/htdocs/cp.ecomae.com/config.local.php' => str_replace('www.ecomae.com', 'cp.ecomae.com', $cfgWww),
);
foreach ($paths as $path => $content) {
	file_put_contents($path, $content);
	echo "wrote {$path}\n";
}

require_once __DIR__ . '/content/general_pages/epc_portal.php';
$doc = epc_portal_docpart_config();
try {
	$pdo2 = new PDO('mysql:host=127.0.0.1;dbname=' . $doc->db, $doc->user, $doc->password);
	echo 'docpart ok tables=' . $pdo2->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo 'docpart fail ' . $e->getMessage() . "\n";
}

define('_ASTEXE_', 1);
foreach (array('www.ecomae.com', 'cp.ecomae.com', 'www.epartscart.com') as $host) {
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['DOCUMENT_ROOT'] = __DIR__;
	require __DIR__ . '/config.php';
	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	try {
		$pdo3 = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db, $cfg->user, $cfg->password);
		echo "{$host} -> db={$cfg->db} ok\n";
	} catch (Exception $e) {
		echo "{$host} -> db={$cfg->db} FAIL " . $e->getMessage() . "\n";
	}
}
