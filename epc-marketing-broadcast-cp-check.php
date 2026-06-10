<?php
/**
 * Marketing Broadcast CP route + eval probe.
 * GET ?token=…&host=www.epartscart.com
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';

$host = trim((string) ($_GET['host'] ?? $_SERVER['HTTP_HOST'] ?? ''));
if ($host !== '') {
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
}

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
echo 'cp_db=' . $cfg->db . "\n";

$pdo = new PDO(
	'mysql:host=' . ($cfg->host ?: '127.0.0.1') . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$url = 'control/portal/epc_marketing_broadcast';
$st = $pdo->prepare('SELECT * FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0 LIMIT 1');
$st->execute(array($url));
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
	exit("content row missing\n");
}
echo 'content_id=' . $row['id'] . ' path=' . $row['content'] . "\n";

$php = str_replace('<backend_dir>', $cfg->backend_dir, $_SERVER['DOCUMENT_ROOT'] . ($row['content'] ?? ''));
echo 'php_path=' . $php . ' exists=' . (is_file($php) ? 'yes' : 'no') . "\n";

$panel = $_SERVER['DOCUMENT_ROOT'] . '/' . trim((string) $cfg->backend_dir, '/') . '/content/control/portal/epc_marketing_broadcast_panel.php';
echo 'panel=' . $panel . ' exists=' . (is_file($panel) ? 'yes' : 'no') . "\n";

$menuSt = $pdo->prepare(
	'SELECT ci.`id`, ci.`url`, ci.`items_group`, cg.`caption` AS grp
	 FROM `control_items` ci
	 LEFT JOIN `control_groups` cg ON cg.`id` = ci.`items_group`
	 WHERE ci.`caption` = ? LIMIT 1'
);
$menuSt->execute(array('epc_marketing_broadcast_cp'));
$menu = $menuSt->fetch(PDO::FETCH_ASSOC);
echo 'menu=' . json_encode($menu ?: array()) . "\n";

if (!is_file($php)) {
	exit("abort: main php missing\n");
}

function epc_mb_cp_check_run(string $phpPath, PDO $pdo, $cfg, bool $asAdmin, string $label): void
{
	echo "\n=== {$label} ===\n";
	if ($asAdmin) {
		try {
			$st = $pdo->prepare(
				'SELECT s.`session`, s.`user_id` FROM `sessions` s
				 WHERE s.`type` = 1 ORDER BY s.`last_activiti_time` DESC LIMIT 1'
			);
			$st->execute();
			$row = $st->fetch(PDO::FETCH_ASSOC);
			if ($row && !empty($row['session'])) {
				$_COOKIE['admin_session'] = (string) $row['session'];
				$_COOKIE['admin_u_id'] = (string) (int) ($row['user_id'] ?? 0);
				echo 'admin_cookie=set user_id=' . (int) ($row['user_id'] ?? 0) . "\n";
			} else {
				echo "admin_cookie=missing\n";
			}
		} catch (Throwable $e) {
			echo 'admin_cookie_error=' . $e->getMessage() . "\n";
		}
	}
	ob_start();
	try {
		global $DP_Config, $db_link;
		$DP_Config = $cfg;
		$db_link = $pdo;
		$_GET['tab'] = 'email';
		require $phpPath;
		$out = (string) ob_get_clean();
		echo 'eval_bytes=' . strlen($out) . "\n";
		echo 'has_hub=' . (stripos($out, 'epc-mb-hub') !== false ? 'yes' : 'no') . "\n";
		echo 'has_email=' . (stripos($out, 'epc-mb-email-form') !== false ? 'yes' : 'no') . "\n";
		if (stripos($out, 'Fatal error') !== false || stripos($out, 'Parse error') !== false) {
			echo "ERROR_SNIPPET:\n" . substr($out, 0, 500) . "\n";
		} elseif (strlen($out) < 200) {
			echo "OUTPUT_SNIPPET:\n" . substr(preg_replace('/\s+/', ' ', strip_tags($out)), 0, 200) . "\n";
		}
	} catch (Throwable $e) {
		ob_end_clean();
		echo 'eval_exception=' . $e->getMessage() . "\n";
		echo 'eval_trace=' . $e->getFile() . ':' . $e->getLine() . "\n";
	}
}

epc_mb_cp_check_run($php, $pdo, $cfg, false, 'guest');
epc_mb_cp_check_run($php, $pdo, $cfg, true, 'admin');
