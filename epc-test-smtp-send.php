<?php
/**
 * SMTP send probe — verify Gmail delivery with full PHPMailer error output.
 * GET: token=epartscart-deploy-2026&key=<tech_key>&to=786yawer@gmail.com&send=1
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
if ((string)($_GET['key'] ?? '') !== $cfg->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

$to = trim((string)($_GET['to'] ?? '786yawer@gmail.com'));
if ($to === '' || !filter_var($to, FILTER_VALIDATE_EMAIL)) {
	exit(json_encode(array('ok' => false, 'error' => 'Invalid to address')));
}

$report = array(
	'ok' => false,
	'tested_at' => date('c'),
	'to' => $to,
	'smtp' => array(
		'mode' => (int)$cfg->smtp_mode,
		'host' => (string)$cfg->smtp_host,
		'port' => (string)$cfg->smtp_port,
		'encryption' => (string)$cfg->smtp_encryption,
		'username' => (string)$cfg->smtp_username,
		'from_email' => (string)$cfg->from_email,
		'from_name' => (string)$cfg->from_name,
		'password_set' => ((string)$cfg->smtp_password !== ''),
		'password_length' => strlen((string)$cfg->smtp_password),
		'username_matches_from' => (strtolower(trim((string)$cfg->smtp_username)) === strtolower(trim((string)$cfg->from_email))),
	),
	'warnings' => array(),
	'send' => null,
);

if ((int)$cfg->smtp_mode !== 1) {
	$report['warnings'][] = 'smtp_mode is off — site may use PHP mail() elsewhere but DocpartMailer uses SMTP when mode=1.';
}
if ((string)$cfg->smtp_password === '' || strlen((string)$cfg->smtp_password) < 8) {
	$report['warnings'][] = 'smtp_password missing or too short — use Gmail App Password (16 chars).';
}
if (!$report['smtp']['username_matches_from']) {
	$report['warnings'][] = 'smtp_username and from_email differ — Gmail may reject or spam-filter messages.';
}

if (empty($_GET['send'])) {
	$report['hint'] = 'Add &send=1 to send a test e-mail.';
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

require_once __DIR__ . '/lib/DocpartMailer/docpart_mailer.php';

$debugLines = array();
$mail = new DocpartMailer();
$mail->CharSet = 'UTF-8';
$mail->IsSMTP();
$mail->IsHTML(true);
$mail->SMTPDebug = 2;
$mail->Debugoutput = function ($line, $level) use (&$debugLines) {
	$debugLines[] = trim((string)$line);
};
$mail->Subject = 'eParts Cart SMTP test ' . date('Y-m-d H:i:s');
$mail->Body = '<p>This is a delivery test from <strong>epc-test-smtp-send.php</strong> on '
	. htmlspecialchars((string)$cfg->domain_path, ENT_QUOTES, 'UTF-8') . '</p>'
	. '<p>If you received this, order / LPO / customer notifications should work. Check spam if order e-mails are missing.</p>';
$mail->addAddress($to, $to);

$sent = false;
$error = '';
try {
	$sent = (bool)$mail->Send();
	$error = (string)$mail->ErrorInfo;
} catch (Throwable $e) {
	$error = $e->getMessage();
}

$report['send'] = array(
	'sent' => $sent,
	'error_info' => $error,
	'host_used' => (string)$mail->Host,
	'port_used' => (string)$mail->Port,
	'smtp_auth' => !empty($mail->SMTPAuth),
);
$report['ok'] = $sent;
$report['debug_tail'] = array_slice($debugLines, -25);

if (!$sent) {
	$report['warnings'][] = 'SMTP send failed — update CP → Communications with Gmail App Password for the sending account.';
	if (stripos($error, 'auth') !== false || stripos(implode("\n", $debugLines), '535') !== false) {
		$report['warnings'][] = 'Authentication failed — create App Password at https://myaccount.google.com/apppasswords';
	}
} else {
	$report['message'] = 'Test e-mail accepted by SMTP server. Check inbox and spam for ' . $to;
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
