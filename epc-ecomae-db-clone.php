<?php
/**
 * Create ecomae DB via CloudPanel web + clone schema from epartscart using clpctl db:export/import.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$dbPass = trim((string) ($_GET['db_password'] ?? bin2hex(random_bytes(12))));
$srcDb = trim((string) ($_GET['src_db'] ?? 'epartscart'));

if ($clpPass === '') {
	exit("clp_pass required\n");
}

$cookie = '';
$login = epc_clp_web_login('admin', $clpPass, $cookie);
if (empty($login['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

$panel = epc_clp_panel_url();
$form = epc_clp_web_request($panel . '/site/www.ecomae.com/database/new', array(), $cookie);
if (!preg_match('/name="([^"]*\[_token\])" value="([^"]+)"/', $form, $m)) {
	$form = epc_clp_web_request($panel . '/site/www.ecomae.com/databases/new', array(), $cookie);
	preg_match('/name="([^"]*\[_token\])" value="([^"]+)"/', $form, $m);
}
if (empty($m[2])) {
	echo "DB form token not found (len=" . strlen($form) . ")\n";
} else {
	$prefix = 'site_database';
	$body = http_build_query(array(
		'site_database' => array(
			'name' => 'ecomae',
			'userName' => 'ecomae',
			'userPassword' => $dbPass,
			'submit' => 'Create',
			'_token' => $m[2],
		),
	));
	epc_clp_web_request($panel . '/site/www.ecomae.com/database/new', array(
		'method' => 'POST',
		'body' => $body,
	), $cookie);
	echo "DB create POST sent (password={$dbPass})\n";
}

$dump = '/tmp/ecomae-clone.sql.gz';
@unlink($dump);
$exp = epc_clp_run_cmd('/usr/bin/clpctl db:export --databaseName=' . escapeshellarg($srcDb) . ' --file=' . escapeshellarg($dump));
echo "export: " . $exp['output'] . "\n";
if (is_file($dump)) {
	$imp = epc_clp_run_cmd('/usr/bin/clpctl db:import --databaseName=ecomae --file=' . escapeshellarg($dump));
	echo "import: " . $imp['output'] . "\n";
} else {
	echo "export file missing — clone skipped\n";
}

echo "\nUse db_password={$dbPass} in config.local.php\n";
