<?php
declare(strict_types=1);
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
$cfg = new DP_Config();
epc_portal_apply_config($cfg);
echo 'cp_db=' . $cfg->db . "\n";
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$url = 'control/portal/epc_platform_failover_guide';
$st = $pdo->prepare('SELECT * FROM `content` WHERE `url` = ? LIMIT 5');
$st->execute(array($url));
foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
	echo 'id=' . $r['id'] . ' url=' . $r['url'] . ' is_frontend=' . $r['is_frontend'] . ' published=' . $r['published_flag'] . ' content=' . $r['content'] . "\n";
}
$st2 = $pdo->prepare('SELECT * FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0 LIMIT 1');
$st2->execute(array($url));
$row = $st2->fetch(PDO::FETCH_ASSOC);
echo 'dp_core_match: ' . ($row ? 'yes id=' . $row['id'] : 'no') . "\n";
$php = str_replace('<backend_dir>', $cfg->backend_dir, $_SERVER['DOCUMENT_ROOT'] . ($row['content'] ?? ''));
echo 'php_path: ' . $php . "\n";
echo 'php_exists: ' . (is_file($php) ? 'yes' : 'no') . "\n";
