<?php
/**
 * Webhook retry processor — cron-ready script.
 *
 * Processes failed webhook deliveries with exponential backoff.
 * Moves exhausted deliveries to dead-letter queue.
 *
 * Cron setup (every minute):
 *   * * * * * cd /home/ecomae/htdocs/www.ecomae.com && php epc-webhooks-process.php >> /var/log/epc-webhooks.log 2>&1
 *
 * HTTP access:
 *   curl -sk "https://www.ecomae.com/epc-webhooks-process.php?token=epartscart-deploy-2026"
 */
define('_ASTEXE_', 1);

$isCli = (php_sapi_name() === 'cli');

if (!$isCli) {
	$token = isset($_GET['token']) ? (string) $_GET['token'] : '';
	if ($token !== 'epartscart-deploy-2026') {
		http_response_code(403);
		echo "Forbidden\n";
		exit(1);
	}
	header('Content-Type: text/plain; charset=utf-8');
}

echo "=== Webhook Retry Processor ===\n";
echo "Time: " . date('Y-m-d H:i:s') . "\n\n";

$configFile = __DIR__ . '/dp-config.php';
if (!file_exists($configFile)) {
	echo "ERROR: dp-config.php not found\n";
	exit(1);
}

$DP_Config = new stdClass();
require_once $configFile;

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 10)
	);
} catch (Exception $e) {
	echo "DB connection failed: " . $e->getMessage() . "\n";
	exit(1);
}

require_once __DIR__ . '/content/general_pages/epc_events.php';
require_once __DIR__ . '/content/general_pages/epc_webhooks.php';

$stats = epc_webhooks_process_retries($db);

echo "Results:\n";
echo "  Processed: " . $stats['processed'] . "\n";
echo "  Delivered: " . $stats['delivered'] . "\n";
echo "  Failed:    " . $stats['failed'] . "\n";
echo "  DLQ:       " . $stats['dlq'] . "\n";

$dlqStats = epc_webhooks_delivery_stats($db);
echo "\n24h Delivery Stats:\n";
echo "  Pending:   " . $dlqStats['pending'] . "\n";
echo "  Delivered: " . $dlqStats['delivered'] . "\n";
echo "  Failed:    " . $dlqStats['failed'] . "\n";
echo "  DLQ (unresolved): " . $dlqStats['dlq'] . "\n";

echo "\nDone.\n";
exit($stats['dlq'] > 0 ? 1 : 0);
