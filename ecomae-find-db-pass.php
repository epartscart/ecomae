<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$newPass = trim((string) ($_GET['db_password'] ?? 'ec9bbf589990e04516e5c121'));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
foreach (array(
	'/site/www.ecomae.com/database',
	'/site/www.ecomae.com/databases',
	'/site/www.ecomae.com/database/ecomae',
) as $path) {
	$html = epc_clp_web_request($panel . $path, array(), $cookie);
	echo "=== {$path} len=" . strlen($html) . " ===\n";
	if (preg_match('/databaseUserPassword[^>]*value="([^"]*)"/', $html, $m)) {
		echo "password_field={$m[1]}\n";
	}
	if (preg_match_all('/ecomae[^<]{0,80}/', $html, $mm)) {
		echo implode("\n", array_slice($mm[0], 0, 8)) . "\n";
	}
}
$exp = epc_clp_run_cmd('/usr/bin/clpctl db:export --databaseName=ecomae --file=/tmp/ecomae-probe.sql.gz');
echo "export code=" . $exp['code'] . "\n";

$candidates = array($newPass, 'EpC4rt_Db_2026_xK9mQ2', '166397986a03c403fe2c4111');
foreach ($candidates as $pass) {
	try {
		$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $pass);
		echo "WORKS pass_len=" . strlen($pass) . "\n";
		$cfg = "<?php\n\$epc_config_local = array('password'=>" . var_export($pass, true) . ",'db'=>'ecomae','user'=>'ecomae','domain_path'=>'https://www.ecomae.com/');\n";
		file_put_contents('/home/ecomae/htdocs/www.ecomae.com/config.local.php', $cfg);
		file_put_contents('/home/ecomaecp/htdocs/cp.ecomae.com/config.local.php', str_replace('www.', 'cp.', $cfg));
		exit("fixed config\n");
	} catch (Exception $e) {
		echo "fail len=" . strlen($pass) . " " . $e->getMessage() . "\n";
	}
}
