<?php
/**
 * Lightweight test API — UAE legislation Q&A (search + extract, no external AI).
 * GET/POST ?token=...&question=What is VAT rate in UAE?
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

define('_ASTEXE_', 1);

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	echo json_encode(array('ok' => false, 'message' => 'Database connection failed'));
	exit;
}

require_once __DIR__ . '/content/shop/finance/epc_uae_tax_compliance.php';

$question = trim((string)($_POST['question'] ?? $_GET['question'] ?? ''));
$result = epc_uae_tax_legislation_ask($db_link, $question);
echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
