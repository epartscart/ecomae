<?php
/**
 * Disable obtaining modes whose handler files are missing (prevents checkout 500).
 * GET: token=…&apply=1
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
$apply = (string) ($_GET['apply'] ?? '') === '1';

$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$base = __DIR__ . '/content/shop/obtaining_modes';
$st = $pdo->query('SELECT id, caption, handler, available FROM shop_obtaining_modes ORDER BY id');
while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
	$handler = preg_replace('/[^a-zA-Z0-9_-]/', '', (string) $row['handler']);
	$interface = $base . '/' . $handler . '/customer_interface.php';
	$details = $base . '/' . $handler . '/show_details.php';
	$ok = $handler !== '' && is_file($interface) && is_file($details);
	echo 'id=' . $row['id'] . ' handler=' . $handler . ' available=' . $row['available']
		. ' files=' . ($ok ? 'OK' : 'MISSING') . "\n";
	if ($ok || !(int) $row['available']) {
		continue;
	}
	if (!$apply) {
		echo "  would disable id={$row['id']}\n";
		continue;
	}
	$upd = $pdo->prepare('UPDATE shop_obtaining_modes SET available = 0 WHERE id = ?');
	$upd->execute(array((int) $row['id']));
	echo "  DISABLED id={$row['id']}\n";
}
echo $apply ? "Done.\n" : "Dry-run. Re-run with &apply=1 to disable missing handlers.\n";
