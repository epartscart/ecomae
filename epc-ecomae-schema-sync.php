<?php
/**
 * Sync MySQL schema for platform CP: clone docpart -> ecomae, ensure docpart user works.
 * https://www.ecomae.com/epc-ecomae-schema-sync.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$apply = !empty($_GET['apply']);
$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$ecomaePass = trim((string) ($_GET['db_password'] ?? 'EcomaeDb_2026x'));
$docroot = '/home/ecomae/htdocs/www.ecomae.com';

echo "=== epc-ecomae-schema-sync ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

if ($apply) {
	$dump = '/tmp/epc-docpart-to-ecomae.sql';
	@unlink($dump);
	foreach (array(
		'mysqldump --single-transaction docpart > ' . escapeshellarg($dump) . ' 2>&1',
		'mysql -u ecomae -p' . escapeshellarg($ecomaePass) . ' ecomae < ' . escapeshellarg($dump) . ' 2>&1',
	) as $cmd) {
		echo epc_clp_run_cmd($cmd)['output'] . "\n";
	}
	$gz = '/home/ecomae/backups/epartscart-migration-20260527/docpart-database.sql.gz';
	if (is_file($gz)) {
		$cmd = 'gunzip -c ' . escapeshellarg($gz) . ' | mysql -u ecomae -p' . escapeshellarg($ecomaePass) . ' ecomae 2>&1';
		echo "import gz: " . epc_clp_run_cmd($cmd)['output'] . "\n";
	}
	if ($clpPass !== '') {
		$cookie = '';
		if (!empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
			$panel = epc_clp_panel_url();
			$editPath = '/site/www.ecomae.com/database/user/edit/docpart';
			$html = epc_clp_web_request($panel . $editPath, array(), $cookie);
			if (preg_match('/name="site_database_user_edit\[_token\]" value="([^"]+)"/', $html, $m)) {
				epc_clp_web_request($panel . $editPath, array(
					'method' => 'POST',
					'body' => http_build_query(array(
						'site_database_user_edit' => array(
							'password' => 'EpC4rt_Db_2026_xK9mQ2',
							'_token' => $m[1],
							'submit' => '',
						),
					)),
				), $cookie);
				echo "CloudPanel docpart password reset sent\n";
			}
		}
	}
}

$local = array(
	'password' => $ecomaePass,
	'db' => 'ecomae',
	'user' => 'ecomae',
	'domain_path' => 'https://www.ecomae.com/',
);
if ($apply) {
	$php = "<?php\n\$epc_config_local = " . var_export($local, true) . ";\n";
	file_put_contents($docroot . '/config.local.php', $php);
	echo "Wrote config.local.php\n";
}

try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $ecomaePass);
	$n = (int) $pdo->query('SHOW TABLES')->rowCount();
	echo "ecomae tables={$n}\n";
	$pdo->query('SELECT 1 FROM lang_text_strings LIMIT 1');
	echo "lang_text_strings=ok\n";
} catch (Exception $e) {
	echo 'ecomae probe: ' . $e->getMessage() . "\n";
}

try {
	$pdo2 = new PDO('mysql:host=127.0.0.1;dbname=docpart;charset=utf8', 'docpart', 'EpC4rt_Db_2026_xK9mQ2');
	echo 'docpart tables=' . $pdo2->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo 'docpart probe: ' . $e->getMessage() . "\n";
}

echo "=== done ===\n";
