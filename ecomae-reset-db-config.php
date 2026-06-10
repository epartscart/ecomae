<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$newPass = trim((string) ($_GET['db_password'] ?? 'ec9bbf589990e04516e5c121'));
if ($clpPass !== '') {
	$cookie = '';
	if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
		exit("CloudPanel login failed\n");
	}
	$r = epc_clp_web_add_database($cookie, 'www.ecomae.com', 'ecomae', 'ecomae', $newPass);
	echo "clp db reset: " . json_encode($r) . "\n";
}

$cfg = "<?php\n\$epc_config_local = array(\n\t'password' => " . var_export($newPass, true) . ",\n\t'db' => 'ecomae',\n\t'user' => 'ecomae',\n\t'domain_path' => 'https://www.ecomae.com/',\n);\n";
$cpCfg = str_replace('www.ecomae.com', 'cp.ecomae.com', $cfg);
foreach (array(
	'/home/ecomae/htdocs/www.ecomae.com/config.local.php',
	'/home/ecomaecp/htdocs/cp.ecomae.com/config.local.php',
) as $path) {
	file_put_contents($path, $path === '/home/ecomaecp/htdocs/cp.ecomae.com/config.local.php' ? $cpCfg : $cfg);
	echo "wrote {$path}\n";
}

try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $newPass);
	echo "pdo ok tables=" . $pdo->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo "pdo fail " . $e->getMessage() . "\n";
}
