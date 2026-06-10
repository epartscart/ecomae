<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$row = $pdo->query('SELECT * FROM content WHERE id=462')->fetch(PDO::FETCH_ASSOC);
unset($row['content']);
echo json_encode($row, JSON_PRETTY_PRINT) . "\n";
