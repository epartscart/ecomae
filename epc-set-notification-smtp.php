<?php
/**
 * Set site notification SMTP (+ price-list IMAP) to a Gmail App Password account.
 *
 * POST (preferred) or GET:
 *   token=epartscart-deploy-2026
 *   email=epartscart@gmail.com
 *   password=<16-char Gmail App Password>
 *   from_name=eParts Cart   (optional)
 *   send_to=...             (optional test recipient; defaults to email)
 *   send=1                  (optional — send a test message after save)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$email = trim((string) ($_POST['email'] ?? $_GET['email'] ?? 'epartscart@gmail.com'));
$password = (string) ($_POST['password'] ?? $_GET['password'] ?? '');
$fromName = trim((string) ($_POST['from_name'] ?? $_GET['from_name'] ?? 'eParts Cart'));
$sendTo = trim((string) ($_POST['send_to'] ?? $_GET['send_to'] ?? $email));
$doSend = !empty($_POST['send']) || !empty($_GET['send']);

if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
	exit(json_encode(array('ok' => false, 'error' => 'Valid email required'), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}
$password = preg_replace('/\s+/', '', $password);
if ($password === '' || strlen($password) < 8) {
	exit(json_encode(array('ok' => false, 'error' => 'Gmail App Password required (16 chars, spaces removed)'), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

$configPath = __DIR__ . '/config.php';
if (!is_readable($configPath) || !is_writable($configPath)) {
	exit(json_encode(array('ok' => false, 'error' => 'config.php not writable'), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

$content = file_get_contents($configPath);
if ($content === false) {
	exit(json_encode(array('ok' => false, 'error' => 'Cannot read config.php'), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

$esc = static function (string $v): string {
	return str_replace(array('\\', "'"), array('\\\\', "\\'"), $v);
};

$map = array(
	'smtp_mode' => '1',
	'smtp_host' => 'smtp.gmail.com',
	'smtp_port' => '587',
	'smtp_encryption' => 'tls',
	'smtp_username' => $email,
	'smtp_password' => $password,
	'from_email' => $email,
	'from_name' => $fromName !== '' ? $fromName : 'eParts Cart',
	'prices_email_server' => 'imap.gmail.com',
	'prices_email_encryption' => 'ssl',
	'prices_email_port' => '993',
	'prices_email_username' => $email,
	'prices_email_password' => $password,
);

$counts = array();
foreach ($map as $key => $value) {
	$pattern = '/([\\t ]*public \\$' . preg_quote($key, '/') . " = ')[^']*(';)/";
	$new = preg_replace_callback(
		$pattern,
		static function (array $m) use ($value, $esc): string {
			return $m[1] . $esc((string) $value) . $m[2];
		},
		$content,
		1,
		$count
	);
	if ($new === null || $count < 1) {
		$counts[$key] = 0;
		continue;
	}
	$content = $new;
	$counts[$key] = $count;
}

$missing = array();
foreach ($counts as $k => $c) {
	if ((int) $c < 1) {
		$missing[] = $k;
	}
}
if ($missing) {
	exit(json_encode(array(
		'ok' => false,
		'error' => 'Could not update some config keys',
		'missing' => $missing,
		'replace_counts' => $counts,
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

$bak = $configPath . '.bak-smtp-' . date('YmdHis');
@copy($configPath, $bak);
if (file_put_contents($configPath, $content) === false) {
	exit(json_encode(array('ok' => false, 'error' => 'Cannot write config.php'), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

$lintOut = array();
$lintCode = 0;
exec('php -l ' . escapeshellarg($configPath) . ' 2>&1', $lintOut, $lintCode);
if ($lintCode !== 0) {
	@copy($bak, $configPath);
	exit(json_encode(array(
		'ok' => false,
		'error' => 'config.php lint failed — restored backup',
		'lint' => implode("\n", $lintOut),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

// Clear opcode cache so next require sees new values.
if (function_exists('opcache_invalidate')) {
	opcache_invalidate($configPath, true);
}

$report = array(
	'ok' => true,
	'message' => 'SMTP + IMAP notification mail set to Gmail App Password account',
	'email' => $email,
	'from_name' => $fromName,
	'password_length' => strlen($password),
	'replace_counts' => $counts,
	'backup' => basename($bak),
	'smtp' => array(
		'host' => 'smtp.gmail.com',
		'port' => '587',
		'encryption' => 'tls',
		'mode' => 1,
	),
	'imap' => array(
		'host' => 'imap.gmail.com',
		'port' => '993',
		'encryption' => 'ssl',
	),
	'send' => null,
);

if (!$doSend) {
	$report['hint'] = 'Add send=1 to deliver a test message (send_to= optional).';
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

if ($sendTo === '' || !filter_var($sendTo, FILTER_VALIDATE_EMAIL)) {
	$report['ok'] = false;
	$report['error'] = 'Invalid send_to';
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

define('_ASTEXE_', 1);
require_once $configPath;
require_once __DIR__ . '/lib/DocpartMailer/docpart_mailer.php';

$debugLines = array();
$mail = new DocpartMailer();
$mail->CharSet = 'UTF-8';
$mail->IsSMTP();
$mail->IsHTML(true);
// Keep debug off by default — SMTP transcripts can include AUTH credentials.
$mail->SMTPDebug = !empty($_GET['debug']) || !empty($_POST['debug']) ? 2 : 0;
$mail->Debugoutput = function ($line, $level) use (&$debugLines) {
	$line = trim((string) $line);
	if ($line === '' || stripos($line, 'CLIENT -> SERVER: AUTH') !== false || stripos($line, 'CLIENT -> SERVER: ') === 0 && strlen($line) < 40) {
		// still capture non-secret status lines below
	}
	// Redact obvious credential payloads from any debug capture.
	if (preg_match('/^(CLIENT -> SERVER: )([A-Za-z0-9+\/=]{8,})$/', $line)) {
		$line = 'CLIENT -> SERVER: [redacted]';
	}
	$debugLines[] = $line;
};
$mail->Subject = 'eParts Cart notification SMTP OK ' . date('Y-m-d H:i:s');
$mail->Body = '<p>Notification e-mail is configured for <strong>'
	. htmlspecialchars($email, ENT_QUOTES, 'UTF-8')
	. '</strong>.</p><p>Order / customer / LPO notifications should now send from this address.</p>';
$mail->addAddress($sendTo, $sendTo);

$sent = false;
$error = '';
try {
	$sent = (bool) $mail->Send();
	$error = (string) $mail->ErrorInfo;
} catch (Throwable $e) {
	$error = $e->getMessage();
}

$report['send'] = array(
	'sent' => $sent,
	'to' => $sendTo,
	'error_info' => $error,
	'debug_tail' => array_slice($debugLines, -20),
);
$report['ok'] = $sent;
if (!$sent) {
	$report['error'] = 'Config saved but test send failed — check App Password / Gmail SMTP access.';
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
