<?php
/**
 * SMTP for CP / storefront auth OTP — merges DP_Config, config.local, config.epc-smtp.php.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

/**
 * @return array<string, mixed>
 */
function epc_auth_smtp_file_config(): array
{
	$file = $_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php';
	if (!is_file($file)) {
		return array();
	}
	$cfg = require $file;
	return is_array($cfg) ? $cfg : array();
}

/**
 * Tenant CP SMTP overlay from site_settings.integrations_json.
 *
 * @return array<string, mixed>
 */
function epc_auth_smtp_tenant_overlay(): array
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return array();
	}
	$dbFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	if (!is_file($dbFile)) {
		return array();
	}
	require_once $dbFile;
	$settings = epc_portal_load_site_settings();
	$smtp = $settings['integrations']['smtp'] ?? null;
	if (!is_array($smtp) || empty($smtp['use_tenant_smtp'])) {
		return array();
	}
	$out = array();
	foreach (array('smtp_host', 'smtp_port', 'smtp_encryption', 'smtp_username', 'smtp_password', 'from_name', 'from_email') as $k) {
		if (!empty($smtp[$k])) {
			$out[$k] = (string) $smtp[$k];
		}
	}
	return $out;
}

/**
 * Effective SMTP settings for auth mail (overlay wins over DP_Config).
 *
 * @return array<string, mixed>
 */
function epc_auth_smtp_effective_config(): array
{
	global $DP_Config;
	if (!isset($DP_Config) || !is_object($DP_Config)) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$DP_Config = new DP_Config();
	}
	$dp = $DP_Config;
	$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (is_file($portalFile)) {
		require_once $portalFile;
		if (function_exists('epc_portal_apply_config')) {
			epc_portal_apply_config($dp);
		}
	} elseif (is_file($_SERVER['DOCUMENT_ROOT'] . '/config.local.php')) {
		$epc_config_local = null;
		require $_SERVER['DOCUMENT_ROOT'] . '/config.local.php';
		if (isset($epc_config_local) && is_array($epc_config_local)) {
			foreach ($epc_config_local as $key => $value) {
				if (property_exists($dp, $key)) {
					$dp->$key = $value;
				}
			}
		}
	}
	$base = array(
		'smtp_mode' => (string) $dp->smtp_mode,
		'smtp_host' => (string) $dp->smtp_host,
		'smtp_port' => (string) $dp->smtp_port,
		'smtp_encryption' => (string) $dp->smtp_encryption,
		'smtp_username' => (string) $dp->smtp_username,
		'smtp_password' => (string) $dp->smtp_password,
		'from_email' => (string) $dp->from_email,
		'from_name' => (string) $dp->from_name,
		'allow_mail_fallback' => false,
	);
	$overlay = epc_auth_smtp_file_config();
	$source = 'config.php';
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/config.local.php')) {
		$source = 'config.php + config.local.php';
	}
	if ($overlay !== array()) {
		$source = 'config.epc-smtp.php';
		foreach ($overlay as $key => $value) {
			if ($key === 'allow_mail_fallback') {
				$base['allow_mail_fallback'] = !empty($value);
				continue;
			}
			if (array_key_exists($key, $base) && $value !== null && $value !== '') {
				$base[$key] = (string) $value;
			}
		}
	}
	$tenantOverlay = epc_auth_smtp_tenant_overlay();
	if ($tenantOverlay !== array()) {
		$source = 'tenant integrations (site_settings)';
		foreach ($tenantOverlay as $key => $value) {
			if ($key === 'allow_mail_fallback') {
				$base['allow_mail_fallback'] = !empty($value);
				continue;
			}
			if (array_key_exists($key, $base) && $value !== null && $value !== '') {
				$base[$key] = (string) $value;
			}
		}
	}
	$base['_source'] = $source;
	$base['_epc_smtp_file'] = is_file($_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php');
	return $base;
}

/**
 * Recommended provider presets for the CP SMTP form.
 *
 * @return array<string, array<string, string>>
 */
function epc_auth_smtp_recommended_presets(): array
{
	return array(
		'gmail_tls' => array(
			'label' => 'Gmail (TLS 587)',
			'smtp_host' => 'smtp.gmail.com',
			'smtp_port' => '587',
			'smtp_encryption' => 'tls',
			'note' => 'username must equal from_email; password = 16-char App Password.',
		),
		'gmail_ssl' => array(
			'label' => 'Gmail (SSL 465)',
			'smtp_host' => 'smtp.gmail.com',
			'smtp_port' => '465',
			'smtp_encryption' => 'ssl',
			'note' => 'username must equal from_email; password = 16-char App Password.',
		),
		'hostinger_ssl' => array(
			'label' => 'Hostinger mailbox (SSL 465)',
			'smtp_host' => 'smtp.hostinger.com',
			'smtp_port' => '465',
			'smtp_encryption' => 'ssl',
			'note' => 'Use the VPS / CloudPanel mailbox password (e.g. hello@ecomae.com).',
		),
		'hostinger_tls' => array(
			'label' => 'Hostinger mailbox (TLS 587)',
			'smtp_host' => 'smtp.hostinger.com',
			'smtp_port' => '587',
			'smtp_encryption' => 'tls',
			'note' => 'Use the VPS / CloudPanel mailbox password (e.g. hello@ecomae.com).',
		),
	);
}

/**
 * Validate an SMTP settings submission. Errors block save; warnings are advisory.
 *
 * @param array<string, mixed> $in
 * @return array{errors:string[], warnings:string[]}
 */
function epc_auth_smtp_validate_input(array $in): array
{
	$errors = array();
	$warnings = array();
	$modeOn = !empty($in['smtp_mode']);
	$host = strtolower(trim((string) ($in['smtp_host'] ?? '')));
	$port = trim((string) ($in['smtp_port'] ?? ''));
	$user = strtolower(trim((string) ($in['smtp_username'] ?? '')));
	$from = strtolower(trim((string) ($in['from_email'] ?? '')));
	$enc = strtolower(trim((string) ($in['smtp_encryption'] ?? '')));
	$pass = (string) ($in['smtp_password'] ?? '');
	$passLen = strlen($pass);

	if ($from === '' || !filter_var($from, FILTER_VALIDATE_EMAIL)) {
		$errors[] = 'from_email must be a valid email address.';
	}
	if ($modeOn) {
		if ($host === '') {
			$errors[] = 'smtp_host is required when SMTP mode is on.';
		}
		if ($port === '' || !ctype_digit($port)) {
			$errors[] = 'smtp_port must be a number (587 for TLS, 465 for SSL).';
		}
		if (!in_array($enc, array('tls', 'ssl', ''), true)) {
			$errors[] = 'smtp_encryption must be tls, ssl, or empty.';
		}
		// Password length only enforced when a new password is being set (blank = keep existing).
		if ($pass !== '' && $passLen < 8) {
			$errors[] = 'smtp_password looks too short (' . $passLen . ' chars).';
		}
	}

	$isGmail = strpos($host, 'gmail') !== false || strpos($host, 'googlemail') !== false;
	if ($isGmail) {
		if ($pass !== '' && $passLen !== 16) {
			$warnings[] = 'Gmail App Passwords are normally 16 characters (you entered ' . $passLen
				. '). Generate one at myaccount.google.com → Security → 2-Step Verification → App passwords.';
		}
		if ($user !== '' && $from !== '' && $user !== $from) {
			$warnings[] = 'For Gmail, smtp_username should equal from_email (' . $from . ').';
		}
		if (($enc === 'tls' && $port !== '587') || ($enc === 'ssl' && $port !== '465')) {
			$warnings[] = 'Gmail expects TLS on port 587 or SSL on port 465.';
		}
	}
	if ($user === '' && $modeOn) {
		$warnings[] = 'smtp_username is empty — most providers require authentication.';
	}
	return array('errors' => $errors, 'warnings' => $warnings);
}

/**
 * Persist SMTP settings to config.epc-smtp.php in the document root (not in git).
 * Blank smtp_password keeps the currently stored password.
 *
 * @param array<string, mixed> $in
 * @return array{ok:bool, message:string, path:string, warnings:string[]}
 */
function epc_auth_smtp_write_file_config(array $in): array
{
	$path = $_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php';
	$valid = epc_auth_smtp_validate_input($in);
	if ($valid['errors'] !== array()) {
		return array('ok' => false, 'message' => implode(' ', $valid['errors']), 'path' => $path, 'warnings' => $valid['warnings']);
	}

	$existing = epc_auth_smtp_file_config();
	$pass = (string) ($in['smtp_password'] ?? '');
	if ($pass === '' && isset($existing['smtp_password']) && (string) $existing['smtp_password'] !== '') {
		$pass = (string) $existing['smtp_password'];
	}

	$out = array(
		'smtp_mode' => !empty($in['smtp_mode']) ? '1' : '0',
		'smtp_host' => trim((string) ($in['smtp_host'] ?? '')),
		'smtp_port' => trim((string) ($in['smtp_port'] ?? '')),
		'smtp_encryption' => strtolower(trim((string) ($in['smtp_encryption'] ?? ''))),
		'smtp_username' => trim((string) ($in['smtp_username'] ?? '')),
		'smtp_password' => $pass,
		'from_email' => trim((string) ($in['from_email'] ?? '')),
		'from_name' => trim((string) ($in['from_name'] ?? '')),
		'allow_mail_fallback' => !empty($in['allow_mail_fallback']) ? '1' : '0',
		'disable_demo_otp_fallback' => !empty($in['disable_demo_otp_fallback']) ? '1' : '0',
	);

	$lines = array(
		'<?php',
		'/**',
		' * Platform SMTP for auth OTP — written by Super CP → Modern auth settings.',
		' * Do NOT commit to git. Overrides config.php / config.local.php for OTP mail.',
		' * Last saved: ' . date('c'),
		' */',
		'return array(',
	);
	foreach ($out as $key => $value) {
		$lines[] = "\t" . var_export((string) $key, true) . ' => ' . var_export((string) $value, true) . ',';
	}
	$lines[] = ');';
	$content = implode("\n", $lines) . "\n";

	if (@file_put_contents($path, $content, LOCK_EX) === false) {
		return array(
			'ok' => false,
			'message' => 'Could not write ' . $path . ' — check filesystem permissions on the document root.',
			'path' => $path,
			'warnings' => $valid['warnings'],
		);
	}
	@chmod($path, 0640);
	$msg = 'SMTP settings saved to config.epc-smtp.php.';
	if ($pass === '') {
		$msg .= ' Note: no password is stored — set one (Gmail App Password or mailbox password) unless using mail() fallback.';
	}
	return array('ok' => true, 'message' => $msg, 'path' => $path, 'warnings' => $valid['warnings']);
}

/**
 * @param array<string, mixed> $cfg
 */
function epc_auth_smtp_apply_to_mailer($mail, array $cfg): void
{
	$modeOn = (int) $cfg['smtp_mode'] === 1 || $cfg['smtp_mode'] === true || $cfg['smtp_mode'] === '1';
	if ($modeOn && trim((string) $cfg['smtp_host']) !== '') {
		$mail->isSMTP();
		$mail->Host = (string) $cfg['smtp_host'];
		$mail->Port = (int) $cfg['smtp_port'];
		$enc = strtolower(trim((string) $cfg['smtp_encryption']));
		if ($enc === 'ssl') {
			$mail->SMTPSecure = 'ssl';
		} elseif ($enc === 'tls') {
			$mail->SMTPSecure = 'tls';
		} else {
			$mail->SMTPSecure = '';
		}
		$user = trim((string) $cfg['smtp_username']);
		$pass = (string) $cfg['smtp_password'];
		if ($user !== '') {
			$mail->SMTPAuth = true;
			$mail->Username = $user;
			$mail->Password = $pass;
		}
	}
	$from = trim((string) $cfg['from_email']);
	if ($from !== '') {
		$mail->setFrom($from, (string) $cfg['from_name']);
		$mail->Sender = $from;
	}
}

/**
 * @return array{ok:bool, issues:string[], source:string, password_length:int, host:string, username:string, from_email:string, epc_smtp_file:bool}
 */
function epc_auth_smtp_diagnose(): array
{
	$cfg = epc_auth_smtp_effective_config();
	$issues = array();
	$modeOn = (int) $cfg['smtp_mode'] === 1 || $cfg['smtp_mode'] === true || $cfg['smtp_mode'] === '1';
	$passLen = strlen((string) $cfg['smtp_password']);
	if (!$modeOn) {
		$issues[] = 'SMTP mode is off in site config — enable smtp_mode=1 or deploy config.epc-smtp.php.';
	}
	if (trim((string) $cfg['smtp_host']) === '') {
		$issues[] = 'smtp_host is empty.';
	}
	if ($passLen < 8 && $modeOn) {
		$issues[] = 'smtp_password is missing or too short (' . $passLen . ' chars) — use a Gmail App Password (16 chars) or Hostinger mailbox password.';
	}
	if (trim((string) $cfg['from_email']) === '') {
		$issues[] = 'from_email is empty.';
	}
	$user = strtolower(trim((string) $cfg['smtp_username']));
	$from = strtolower(trim((string) $cfg['from_email']));
	if ($user !== '' && $from !== '' && $user !== $from && stripos((string) $cfg['smtp_host'], 'gmail') !== false) {
		$issues[] = 'smtp_username and from_email differ — Gmail often requires them to match.';
	}
	return array(
		'ok' => $issues === array(),
		'issues' => $issues,
		'source' => (string) ($cfg['_source'] ?? 'config.php'),
		'smtp_mode' => $modeOn,
		'password_length' => $passLen,
		'host' => (string) $cfg['smtp_host'],
		'port' => (string) $cfg['smtp_port'],
		'encryption' => (string) $cfg['smtp_encryption'],
		'username' => (string) $cfg['smtp_username'],
		'from_email' => (string) $cfg['from_email'],
		'from_name' => (string) $cfg['from_name'],
		'epc_smtp_file' => !empty($cfg['_epc_smtp_file']),
		'allow_mail_fallback' => !empty($cfg['allow_mail_fallback']),
	);
}

/**
 * @return array{ok:bool, message:string, detail:string, transport:string}
 */
function epc_auth_smtp_classify_error(string $errorInfo, array $debugLines = array()): array
{
	$blob = strtolower($errorInfo . "\n" . implode("\n", $debugLines));
	if (strpos($blob, 'could not connect') !== false || strpos($blob, 'connect() failed') !== false
		|| strpos($blob, 'connection refused') !== false || strpos($blob, 'connection timed out') !== false) {
		return array(
			'ok' => false,
			'message' => 'SMTP connection failed — check host, port, and firewall (try ssl/465 or tls/587).',
			'detail' => $errorInfo,
			'transport' => 'smtp',
		);
	}
	if (strpos($blob, '535') !== false || strpos($blob, 'authentication') !== false
		|| strpos($blob, 'username and password not accepted') !== false || strpos($blob, 'auth') !== false) {
		return array(
			'ok' => false,
			'message' => 'SMTP authentication failed — use an app password (Gmail) or correct mailbox password (Hostinger).',
			'detail' => $errorInfo,
			'transport' => 'smtp',
		);
	}
	if (strpos($blob, 'password') !== false && strpos($blob, 'empty') !== false) {
		return array(
			'ok' => false,
			'message' => 'SMTP password not configured — deploy config.epc-smtp.php or update CP mail settings.',
			'detail' => $errorInfo,
			'transport' => 'smtp',
		);
	}
	return array(
		'ok' => false,
		'message' => 'Could not send email — check SMTP settings in Control Panel',
		'detail' => $errorInfo,
		'transport' => 'smtp',
	);
}

/**
 * @return array{ok:bool, message:string, detail:string, transport:string}
 */
function epc_auth_smtp_send_html(string $to, string $subject, string $html, string $altBody = ''): array
{
	$diag = epc_auth_smtp_diagnose();
	if (!$diag['ok']) {
		return array(
			'ok' => false,
			'message' => implode(' ', $diag['issues']),
			'detail' => 'precheck',
			'transport' => 'none',
		);
	}

	$cfg = epc_auth_smtp_effective_config();
	$mailerFile = $_SERVER['DOCUMENT_ROOT'] . '/lib/DocpartMailer/docpart_mailer.php';
	if (!is_file($mailerFile)) {
		if (!empty($cfg['allow_mail_fallback'])) {
			$headers = "MIME-Version: 1.0\r\nContent-type: text/html; charset=utf-8\r\nFrom: "
				. (string) $cfg['from_name'] . ' <' . (string) $cfg['from_email'] . ">\r\n";
			$sent = @mail($to, $subject, $html, $headers);
			if ($sent) {
				return array('ok' => true, 'message' => 'Sent via PHP mail()', 'detail' => '', 'transport' => 'mail');
			}
		}
		return array(
			'ok' => false,
			'message' => 'Mailer library missing on server',
			'detail' => $mailerFile,
			'transport' => 'none',
		);
	}

	require_once $mailerFile;
	$debugLines = array();
	try {
		$mail = new DocpartMailer(true);
		epc_auth_smtp_apply_to_mailer($mail, $cfg);
		$mail->CharSet = 'UTF-8';
		$mail->isHTML(true);
		// Capture the real SMTP transcript so callers can surface the actual error.
		if (property_exists($mail, 'SMTPDebug')) {
			$mail->SMTPDebug = 2;
			$mail->Debugoutput = function ($line, $level) use (&$debugLines) {
				$debugLines[] = trim((string) $line);
			};
		}
		$mail->addAddress($to);
		$mail->Subject = $subject;
		$mail->Body = $html;
		$mail->AltBody = $altBody !== '' ? $altBody : strip_tags($html);
		if ($mail->send()) {
			return array('ok' => true, 'message' => 'Sent via SMTP', 'detail' => '', 'transport' => 'smtp');
		}
		$err = (string) $mail->ErrorInfo;
		error_log('epc_auth_smtp_send_html: ' . $err);
		$classified = epc_auth_smtp_classify_error($err, $debugLines);
		$tail = array_slice($debugLines, -12);
		$classified['detail'] = trim($err . ($tail ? "\n" . implode("\n", $tail) : ''));
		if (!empty($cfg['allow_mail_fallback'])) {
			$headers = "MIME-Version: 1.0\r\nContent-type: text/html; charset=utf-8\r\nFrom: "
				. (string) $cfg['from_name'] . ' <' . (string) $cfg['from_email'] . ">\r\n";
			if (@mail($to, $subject, $html, $headers)) {
				return array(
					'ok' => true,
					'message' => 'Sent via PHP mail() after SMTP failed',
					'detail' => $err,
					'transport' => 'mail',
				);
			}
		}
		return $classified;
	} catch (Throwable $e) {
		error_log('epc_auth_smtp_send_html: ' . $e->getMessage());
		return epc_auth_smtp_classify_error($e->getMessage());
	}
}

function epc_auth_otp_demo_fallback_allowed(string $tenantKey): bool
{
	$tenantKey = trim($tenantKey);
	if ($tenantKey === '' || strpos($tenantKey, 'demo_') !== 0) {
		return false;
	}
	$overlay = epc_auth_smtp_file_config();
	if (!empty($overlay['disable_demo_otp_fallback'])) {
		return false;
	}
	return true;
}

function epc_auth_otp_store_operator_code(PDO $platformPdo, int $otpId, string $code): void
{
	if ($otpId <= 0) {
		return;
	}
	$st = $platformPdo->prepare('SELECT `context_json` FROM `epc_auth_otp_requests` WHERE `id` = ? LIMIT 1');
	$st->execute(array($otpId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return;
	}
	$ctx = json_decode((string) ($row['context_json'] ?? '{}'), true);
	if (!is_array($ctx)) {
		$ctx = array();
	}
	$ctx['_operator_otp'] = $code;
	$ctx['_operator_otp_at'] = time();
	$platformPdo->prepare('UPDATE `epc_auth_otp_requests` SET `context_json` = ? WHERE `id` = ?')
		->execute(array(json_encode($ctx), $otpId));
}

/**
 * Super CP only — last demo-logged OTP for an email.
 *
 * @return array{ok:bool, email?:string, code?:string, logged_at?:int, message?:string}
 */
function epc_auth_otp_operator_lookup(PDO $platformPdo, string $email): array
{
	$email = strtolower(trim($email));
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'Valid email required');
	}
	epc_auth_otp_ensure_schema($platformPdo);
	$st = $platformPdo->prepare(
		'SELECT `context_json`, `created_at`, `tenant_key` FROM `epc_auth_otp_requests`
		 WHERE `email` = ? ORDER BY `id` DESC LIMIT 5'
	);
	$st->execute(array($email));
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$ctx = json_decode((string) ($row['context_json'] ?? '{}'), true);
		if (!is_array($ctx) || empty($ctx['_operator_otp'])) {
			continue;
		}
		return array(
			'ok' => true,
			'email' => $email,
			'code' => (string) $ctx['_operator_otp'],
			'logged_at' => (int) ($ctx['_operator_otp_at'] ?? (int) ($row['created_at'] ?? 0)),
			'tenant_key' => (string) ($row['tenant_key'] ?? ''),
		);
	}
	return array('ok' => false, 'message' => 'No operator-logged OTP for this email (SMTP may have succeeded or address not used on a demo tenant).');
}
