<?php
/**
 * Push portal zip from this server to remote deploy targets (same VPS).
 * https://www.epartscart.com/epc-portal-deploy-all.php?token=epartscart-deploy-2026&site=taxofinca
 */
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
epc_portal_db_ensure($pdo);

$site_key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site'] ?? 'taxofinca')));
$st = $pdo->prepare('SELECT * FROM `epc_portal_deploy_targets` WHERE `site_key` = ? LIMIT 1');
$st->execute(array($site_key));
$target = $st->fetch(PDO::FETCH_ASSOC);
if (!$target) {
	exit("Unknown site: {$site_key}\n");
}

$zipPath = '/tmp/docpart-epartscart-site.zip';
if (!is_file($zipPath)) {
	exit("Zip missing at {$zipPath}\n");
}

$hostHeader = $target['hostname'];
$httpHeader = "Host: {$hostHeader}\r\n";

$token = epc_deploy_token();
$data = file_get_contents($zipPath);
$chunkSize = 150000;
echo "Deploying " . strlen($data) . " bytes to {$target['hostname']}...\n";
$idx = 0;
for ($off = 0; $off < strlen($data); $off += $chunkSize) {
	$part = substr($data, $off, $chunkSize);
	$body = http_build_query(array(
		'token' => $token,
		'index' => (string) $idx,
		'data' => base64_encode($part),
		'final' => ($off + $chunkSize >= strlen($data)) ? '1' : '0',
	));
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => 'POST',
			'header' => $httpHeader . "Content-Type: application/x-www-form-urlencoded\r\n",
			'content' => $body,
			'timeout' => 300,
		),
	));
	$resp = @file_get_contents($target['chunk_url'], false, $ctx);
	echo "chunk {$idx}: " . trim((string) $resp) . "\n";
	$idx++;
}

$extractUrl = $target['extract_url'];
if (strpos($extractUrl, 'token=') === false) {
	$extractUrl .= (strpos($extractUrl, '?') !== false ? '&' : '?') . 'token=' . urlencode($token);
}
echo "\nExtracting...\n";
$extractResp = @file_get_contents($extractUrl, false, stream_context_create(array(
	'http' => array('timeout' => 300, 'header' => $httpHeader),
)));
echo substr((string) $extractResp, 0, 3000) . "\n";

echo "\nSetup...\n";
$setupResp = @file_get_contents($target['setup_url'], false, stream_context_create(array(
	'http' => array('timeout' => 120, 'header' => $httpHeader),
)));
echo substr((string) $setupResp, 0, 1500) . "\n";

$ok = ($extractResp !== false && strpos((string) $extractResp, 'exit=0') !== false);
$pdo->prepare('UPDATE `epc_portal_deploy_targets` SET `last_deploy_at` = ?, `last_deploy_status` = ?, `last_deploy_message` = ? WHERE `site_key` = ?')
	->execute(array(time(), $ok ? 'ok' : 'failed', substr((string) $extractResp, 0, 500), $site_key));
echo $ok ? "Done.\n" : "Extract may have failed — ensure taxofinca vhost + SSL are active.\n";
