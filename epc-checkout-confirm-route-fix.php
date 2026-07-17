<?php
/**
 * Register CMS alias shop/checkout_confirm → checkout_confirm.php
 * (legacy underscore URL used by some delivery handlers).
 *
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

$canonical = 'shop/checkout/confirm';
$aliasUrl = 'shop/checkout_confirm';
$phpPath = '/content/shop/order_process/checkout_confirm.php';

$st = $pdo->prepare('SELECT id, url, content, published_flag, parent FROM content WHERE url = ? LIMIT 1');
$st->execute(array($canonical));
$canon = $st->fetch(PDO::FETCH_ASSOC);
if (!$canon) {
	exit("canonical row missing: {$canonical}\n");
}

$st->execute(array($aliasUrl));
$alias = $st->fetch(PDO::FETCH_ASSOC);

echo "canonical id={$canon['id']} content={$canon['content']}\n";
echo "alias_exists=" . ($alias ? 'Y id=' . $alias['id'] : 'N') . "\n";
echo "apply=" . ($apply ? '1' : '0') . "\n";

if (!$apply) {
	exit("Dry-run. Re-run with &apply=1 to upsert alias {$aliasUrl}\n");
}

if ($alias) {
	$upd = $pdo->prepare(
		'UPDATE content SET content_type = ?, content = ?, published_flag = 1, is_frontend = 1, parent = ?, alias = ? WHERE id = ?'
	);
	$upd->execute(array('php', $phpPath, (int) $canon['parent'], 'checkout_confirm', (int) $alias['id']));
	echo "updated alias id={$alias['id']}\n";
} else {
	// Copy useful meta from canonical where possible
	$ins = $pdo->prepare(
		'INSERT INTO content
		(url, alias, level, value, parent, description, is_frontend, content_type, content,
		 title_tag, description_tag, keywords_tag, author_tag, main_flag, modules_array, css_js,
		 robots_tag, system_flag, published_flag, open, time_created, time_edited, `order`, `count`)
		 SELECT ?, ?, level, value, parent, description, 1, ?, ?,
		 title_tag, description_tag, keywords_tag, author_tag, 0, modules_array, css_js,
		 robots_tag, system_flag, 1, open, ?, ?, `order`, `count`
		 FROM content WHERE id = ?'
	);
	$now = time();
	$ins->execute(array($aliasUrl, 'checkout_confirm', 'php', $phpPath, $now, $now, (int) $canon['id']));
	echo "inserted alias id=" . $pdo->lastInsertId() . "\n";
}

// Clear simple page caches if present
foreach (glob(__DIR__ . '/content/files/epc_cache/epc_cp_menu_rows_v1_*.json') ?: array() as $f) {
	@unlink($f);
}
echo "Done. Try /en/shop/checkout_confirm and /en/shop/checkout/confirm\n";
