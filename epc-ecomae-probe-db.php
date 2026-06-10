<?php
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$bootstrap = (string) ($_POST['bootstrap'] ?? '');
if ($bootstrap !== '') {
	$bin = base64_decode($bootstrap, true);
	if ($bin === false || $bin === '') {
		exit("bad bootstrap\n");
	}
	file_put_contents(__FILE__, $bin);
	exit("probe self-updated bytes=" . strlen($bin) . "\n");
}

$putRel = str_replace('\\', '/', trim((string) ($_POST['put_rel'] ?? '')));
$putB64 = (string) ($_POST['put_b64'] ?? '');
if ($putRel !== '' && $putB64 !== '' && strpos($putRel, '..') === false && $putRel[0] !== '/') {
	$bin = base64_decode($putB64, true);
	if ($bin === false) {
		exit("bad put_b64\n");
	}
	$dest = __DIR__ . '/' . $putRel;
	$dir = dirname($dest);
	if (!is_dir($dir)) {
		mkdir($dir, 0755, true);
	}
	$n = file_put_contents($dest, $bin);
	exit("wrote {$putRel} bytes={$n}\n");
}

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
$cfg = new DP_Config();
epc_portal_apply_config($cfg);
echo 'db=' . $cfg->db . ' user=' . $cfg->user . ' pass_len=' . strlen($cfg->password) . "\n";
try {
	$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db, $cfg->user, $cfg->password);
	$n = $pdo->query('SHOW TABLES')->rowCount();
	echo "connect=ok tables={$n}\n";
} catch (Exception $e) {
	echo 'connect=fail ' . $e->getMessage() . "\n";
}
