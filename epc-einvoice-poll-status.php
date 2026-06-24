<?php
/**
 * E-invoice ASP status poller — cron-ready script.
 *
 * Polls ASP API for pending/submitted invoice statuses, updates local records.
 * Also retries failed submissions with exponential backoff (max 3 retries).
 *
 * Cron setup (every 5 minutes):
 *   0,5,10,15,20,25,30,35,40,45,50,55 * * * * cd /home/ecomae/htdocs/www.ecomae.com && php epc-einvoice-poll-status.php >> /var/log/epc-einvoice-poll.log 2>&1
 *
 * HTTP access:
 *   curl -sk "https://www.ecomae.com/epc-einvoice-poll-status.php?token=epartscart-deploy-2026"
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

echo "=== E-invoice ASP Status Poll ===\n";
echo "Time: " . date('Y-m-d H:i:s') . "\n\n";

// Bootstrap platform DB connection
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

require_once __DIR__ . '/content/shop/finance/epc_einvoice.php';

// Check if ASP API mode is enabled
$mode = epc_einvoice_get_setting($db, 'asp_api_mode', 'manual');
if ($mode !== 'api') {
	echo "ASP API mode not enabled (current: $mode). Nothing to poll.\n";
	echo "Enable via ERP → E-invoice Settings → ASP API Mode = 'api'\n";
	exit(0);
}

// Poll all pending submissions
$results = epc_einvoice_poll_all_pending($db);

echo "Results:\n";
echo "  Polled:   " . $results['polled'] . "\n";
echo "  Accepted: " . $results['accepted'] . "\n";
echo "  Rejected: " . $results['rejected'] . "\n";
echo "  Pending:  " . $results['pending'] . "\n";
echo "  Errors:   " . $results['errors'] . "\n";
echo "\nDone.\n";

if (!$isCli) {
	exit(0);
}
exit($results['errors'] > 0 ? 1 : 0);
