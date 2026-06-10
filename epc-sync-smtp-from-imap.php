<?php
/**
 * Copy working Gmail IMAP app password to SMTP settings in config.php.
 * Run once when order e-mails fail with BadCredentials but price-list IMAP works.
 * GET: token=epartscart-deploy-2026&key=<tech_key>&apply=1
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

$configPath = __DIR__ . '/config.php';
require_once $configPath;
$cfg = new DP_Config();
if ((string)($_GET['key'] ?? '') !== $cfg->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

$report = array(
	'ok' => false,
	'smtp_username' => (string)$cfg->smtp_username,
	'from_email' => (string)$cfg->from_email,
	'smtp_password_length' => strlen((string)$cfg->smtp_password),
	'imap_password_length' => strlen((string)$cfg->prices_email_password),
	'imap_username' => (string)$cfg->prices_email_username,
);

if ((string)$cfg->prices_email_password === '') {
	$report['error'] = 'prices_email_password is empty — set Gmail App Password in CP first.';
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

if ((string)$cfg->smtp_password === (string)$cfg->prices_email_password) {
	$report['ok'] = true;
	$report['message'] = 'SMTP password already matches IMAP app password.';
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

if (empty($_GET['apply'])) {
	$report['hint'] = 'Add &apply=1 to copy prices_email_password → smtp_password in config.php';
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

$content = file_get_contents($configPath);
if ($content === false) {
	exit(json_encode(array('ok' => false, 'error' => 'Cannot read config.php')));
}

$esc = function ($v) {
	return str_replace(array('\\', "'"), array('\\\\', "\\'"), (string)$v);
};
$newPass = $esc($cfg->prices_email_password);

$updated = 0;
$pattern = '/public \$smtp_password = \'[^\']*\';/';
$content = preg_replace(
	$pattern,
	"public \$smtp_password = '" . $newPass . "';",
	$content,
	1,
	$updated
);

if ($updated < 1) {
	exit(json_encode(array('ok' => false, 'error' => 'Could not update smtp_password in config.php')));
}

if (file_put_contents($configPath, $content) === false) {
	exit(json_encode(array('ok' => false, 'error' => 'Cannot write config.php')));
}

$report['ok'] = true;
$report['message'] = 'smtp_password synced from prices_email_password. Re-run epc-test-smtp-send.php&send=1 to verify.';
$report['smtp_password_length_after'] = strlen((string)$cfg->prices_email_password);

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
