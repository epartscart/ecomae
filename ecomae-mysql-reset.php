<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$newPass = trim((string) ($_GET['db_password'] ?? 'ec9bbf589990e04516e5c121'));
$cmds = array(
	"mysql -e " . escapeshellarg("ALTER USER 'ecomae'@'localhost' IDENTIFIED BY '{$newPass}'; FLUSH PRIVILEGES;"),
	"sudo mysql -e " . escapeshellarg("ALTER USER 'ecomae'@'localhost' IDENTIFIED BY '{$newPass}'; FLUSH PRIVILEGES;"),
	"/usr/bin/clpctl db:export --databaseName=ecomae --file=/tmp/ecomae-test.sql.gz 2>&1",
);
foreach ($cmds as $cmd) {
	exec($cmd . ' 2>&1', $out, $code);
	echo "cmd={$cmd}\ncode={$code}\n" . implode("\n", array_slice($out, 0, 5)) . "\n---\n";
	$out = array();
}

$cfg = "<?php\n\$epc_config_local = array(\n\t'password' => " . var_export($newPass, true) . ",\n\t'db' => 'ecomae',\n\t'user' => 'ecomae',\n\t'domain_path' => 'https://www.ecomae.com/',\n);\n";
$cpCfg = str_replace('www.ecomae.com', 'cp.ecomae.com', $cfg);
file_put_contents('/home/ecomae/htdocs/www.ecomae.com/config.local.php', $cfg);
file_put_contents('/home/ecomaecp/htdocs/cp.ecomae.com/config.local.php', $cpCfg);
echo "config written\n";

try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $newPass);
	echo "pdo ok\n";
} catch (Exception $e) {
	echo "pdo fail " . $e->getMessage() . "\n";
}
