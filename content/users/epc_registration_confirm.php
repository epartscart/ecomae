<?php
/**
 * Registration confirmation — professional frontend message + email.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Resolve a human brand name for emails / UI (never a multilang hash key).
 */
function epc_reg_confirm_brand_name($DP_Config = null): string
{
	$candidates = array();

	if (function_exists('epc_brand_trade_name')) {
		try {
			$candidates[] = trim((string) epc_brand_trade_name());
		} catch (Throwable $e) {
			// ignore
		}
	}
	if (function_exists('epc_site_trade_name')) {
		try {
			$candidates[] = trim((string) epc_site_trade_name());
		} catch (Throwable $e) {
			// ignore
		}
	}
	if (is_object($DP_Config) && !empty($DP_Config->site_name) && function_exists('translate_str_by_id')) {
		$translated = trim((string) translate_str_by_id($DP_Config->site_name));
		if ($translated !== '' && $translated !== (string) $DP_Config->site_name) {
			$candidates[] = $translated;
		}
	}

	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$host = preg_replace('/^www\./', '', $host);
	if ($host === 'epartscart.com' || substr($host, -strlen('.epartscart.com')) === '.epartscart.com') {
		$candidates[] = 'eParts Cart';
	}

	foreach ($candidates as $name) {
		if ($name === '') {
			continue;
		}
		// Reject multilang keys like 1760189439_1_<md5>
		if (preg_match('/^\d+_\d+_[a-f0-9]{20,}$/i', $name)) {
			continue;
		}
		if (stripos($name, 'Empty string') !== false || stripos($name, 'ERROR STR_KEY') !== false) {
			continue;
		}
		return $name;
	}

	return 'eParts Cart';
}

/**
 * Ensure reg_email_confirm notification uses a clean English template (not multilang hash keys).
 */
function epc_reg_confirm_ensure_notify_template(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;

	$vars = json_encode(array(
		array('name' => 'site_name', 'caption' => 'Site name', 'type' => 'text'),
		array('name' => 'email_confirm_href', 'caption' => 'Confirm link HTML', 'type' => 'text'),
		array('name' => 'confirm_html', 'caption' => 'Full confirm body HTML', 'type' => 'text'),
		array('name' => 'customer_email', 'caption' => 'Customer email', 'type' => 'text'),
	), JSON_UNESCAPED_UNICODE);

	// Plain English (not multilang keys) — dispatch falls back when translate_str_by_id is empty.
	$subject = 'Welcome to %site_name% — confirm your email';
	$body = '%confirm_html%';

	// Prefer a separate connection so this UPDATE is not held inside the registration transaction.
	$pdo = $db;
	global $DP_Config;
	if (is_object($DP_Config) && !empty($DP_Config->host) && !empty($DP_Config->db)) {
		try {
			$pdo = new PDO(
				'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
				(string) $DP_Config->user,
				(string) $DP_Config->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
		} catch (Throwable $e) {
			$pdo = $db;
		}
	}

	$st = $pdo->prepare('SELECT `id`, `email_subject`, `email_body`, `vars` FROM `notifications_settings` WHERE `name` = ? LIMIT 1');
	$st->execute(array('reg_email_confirm'));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return;
	}

	$needsUpdate = false;
	$subj = (string) ($row['email_subject'] ?? '');
	$bodyRaw = (string) ($row['email_body'] ?? '');
	// Multilang keys look like time_n_md5; also refresh if confirm_html placeholder missing.
	if (preg_match('/^\d+_\d+_/', $subj) || strpos($subj, 'Welcome to %site_name%') === false) {
		$needsUpdate = true;
	}
	if (strpos($bodyRaw, '%confirm_html%') === false) {
		$needsUpdate = true;
	}
	if (!$needsUpdate) {
		$decoded = json_decode((string) ($row['vars'] ?? ''), true);
		$names = array();
		if (is_array($decoded)) {
			foreach ($decoded as $v) {
				if (!empty($v['name'])) {
					$names[] = (string) $v['name'];
				}
			}
		}
		if (!in_array('confirm_html', $names, true)) {
			$needsUpdate = true;
		}
	}

	if (!$needsUpdate) {
		return;
	}

	$pdo->prepare(
		'UPDATE `notifications_settings`
		 SET `email_subject` = ?, `email_body` = ?, `vars` = ?, `email_on` = 1, `send_for_not_confirmed` = 1,
		     `default_email_subject` = ?, `default_email_body` = ?
		 WHERE `id` = ?'
	)->execute(array($subject, $body, $vars, $subject, $body, (int) $row['id']));
}

/**
 * CTA button for the confirmation link.
 */
function epc_reg_confirm_button_html(string $url, string $label = 'Confirm my email'): string
{
	$url = htmlspecialchars($url, ENT_QUOTES, 'UTF-8');
	$label = htmlspecialchars($label, ENT_QUOTES, 'UTF-8');
	return '<a href="' . $url . '" target="_blank" style="display:inline-block;background:#111827;color:#ffffff;text-decoration:none;font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:700;letter-spacing:0.02em;padding:14px 28px;border-radius:8px;border:1px solid #111827;">'
		. $label . '</a>';
}

/**
 * Professional HTML body for the registration confirmation email.
 *
 * @param array{brand?:string,email?:string,confirm_url?:string,customer_type?:string,home_url?:string} $opts
 */
function epc_reg_confirm_email_body_html(array $opts): string
{
	$brand = htmlspecialchars((string) ($opts['brand'] ?? 'eParts Cart'), ENT_QUOTES, 'UTF-8');
	$email = htmlspecialchars((string) ($opts['email'] ?? ''), ENT_QUOTES, 'UTF-8');
	$confirmUrl = (string) ($opts['confirm_url'] ?? '');
	$customerType = strtolower(trim((string) ($opts['customer_type'] ?? 'retail')));
	$homeUrl = htmlspecialchars((string) ($opts['home_url'] ?? '/'), ENT_QUOTES, 'UTF-8');
	$button = epc_reg_confirm_button_html($confirmUrl, 'Confirm my email');
	$safeUrl = htmlspecialchars($confirmUrl, ENT_QUOTES, 'UTF-8');

	$typeLine = 'Your retail account is ready to activate — confirm your email, then sign in to browse and checkout.';
	if ($customerType === 'wholesale') {
		$typeLine = 'You registered as a <strong>wholesale</strong> customer (subject to approval only). After you confirm your email, a manager will review your account before trade pricing unlocks.';
	}

	return ''
		. '<div style="font-family:Arial,Helvetica,sans-serif;color:#111827;font-size:15px;line-height:1.55;max-width:560px;">'
		. '<p style="margin:0 0 8px;font-size:22px;font-weight:700;color:#0f172a;">Welcome to ' . $brand . '</p>'
		. '<p style="margin:0 0 18px;color:#475569;">Thanks for creating your account' . ($email !== '' ? ' with <strong style="color:#0f172a;">' . $email . '</strong>' : '') . '.</p>'
		. '<p style="margin:0 0 18px;color:#334155;">' . $typeLine . '</p>'
		. '<p style="margin:0 0 10px;color:#0f172a;font-weight:600;">One quick step to finish:</p>'
		. '<p style="margin:0 0 22px;">' . $button . '</p>'
		. '<p style="margin:0 0 8px;font-size:13px;color:#64748b;">Or copy this link into your browser:</p>'
		. '<p style="margin:0 0 22px;font-size:12px;word-break:break-all;color:#475569;"><a href="' . $safeUrl . '" style="color:#1d4ed8;text-decoration:underline;">' . $safeUrl . '</a></p>'
		. '<p style="margin:0 0 18px;color:#64748b;font-size:13px;">If you did not create an account on ' . $brand . ', you can safely ignore this message — no changes will be made.</p>'
		. '<p style="margin:0;font-size:13px;"><a href="' . $homeUrl . '" style="color:#1d4ed8;text-decoration:none;font-weight:600;">Visit ' . $brand . ' →</a></p>'
		. '</div>';
}

/**
 * Professional on-page confirmation after successful registration.
 *
 * @param array{brand?:string,email?:string,customer_type?:string,email_failed?:bool,confirm_url?:string,login_url?:string} $opts
 */
function epc_reg_confirm_frontend_html(array $opts): string
{
	$brand = htmlspecialchars((string) ($opts['brand'] ?? 'eParts Cart'), ENT_QUOTES, 'UTF-8');
	$email = htmlspecialchars((string) ($opts['email'] ?? ''), ENT_QUOTES, 'UTF-8');
	$customerType = strtolower(trim((string) ($opts['customer_type'] ?? 'retail')));
	$emailFailed = !empty($opts['email_failed']);
	$confirmUrl = (string) ($opts['confirm_url'] ?? '');
	$loginUrl = htmlspecialchars((string) ($opts['login_url'] ?? '/'), ENT_QUOTES, 'UTF-8');
	$isWholesale = ($customerType === 'wholesale');

	$badge = $isWholesale ? 'Wholesale · subject to approval' : 'Retail account';
	$title = 'Your account has been created';
	$lead = $email !== ''
		? 'We sent a confirmation email to <strong>' . $email . '</strong>.'
		: 'We sent a confirmation email to the address you registered with.';

	$steps = '';
	if ($emailFailed) {
		$lead = 'Your account was created, but the confirmation email could not be sent right now.';
		$safeConfirm = htmlspecialchars($confirmUrl, ENT_QUOTES, 'UTF-8');
		$steps = '<div class="epc-reg-success-box epc-reg-success-box--warn">'
			. '<p style="margin:0 0 10px;">Use this secure link to confirm your email:</p>'
			. '<p style="margin:0;word-break:break-all;"><a href="' . $safeConfirm . '">' . $safeConfirm . '</a></p>'
			. '</div>';
	} else {
		$steps = '<ol class="epc-reg-success-steps">'
			. '<li><strong>Open your inbox</strong> — look for a message from ' . $brand . ' (check Spam / Promotions if needed).</li>'
			. '<li><strong>Confirm your email</strong> — click the button in that message to activate your account.</li>'
			. ($isWholesale
				? '<li><strong>Wait for approval</strong> — wholesale accounts are subject to approval only. A manager will unlock trade pricing after review.</li>'
				: '<li><strong>Sign in &amp; shop</strong> — once confirmed, sign in to browse and checkout immediately.</li>')
			. '</ol>';
	}

	$typeNote = $isWholesale
		? '<p class="epc-reg-success-note">Wholesale registration is <em>subject to approval only</em>. You can confirm your email now; checkout unlocks after manager approval.</p>'
		: '<p class="epc-reg-success-note">Your retail account will be ready to use as soon as you confirm your email.</p>';

	return ''
		. '<style>'
		. '.epc-reg-success{max-width:640px;margin:24px auto;padding:28px 28px 24px;border:1px solid #e2e8f0;border-radius:14px;background:linear-gradient(180deg,#ffffff 0%,#f8fafc 100%);box-shadow:0 10px 30px rgba(15,23,42,.06);font-family:Arial,Helvetica,sans-serif;color:#0f172a}'
		. '.epc-reg-success-badge{display:inline-block;padding:5px 10px;border-radius:999px;background:#ecfdf5;color:#047857;font-size:12px;font-weight:700;letter-spacing:.03em;text-transform:uppercase;margin:0 0 12px}'
		. '.epc-reg-success-badge.is-wholesale{background:#eff6ff;color:#1d4ed8}'
		. '.epc-reg-success h2{margin:0 0 10px;font-size:26px;line-height:1.25;font-weight:800}'
		. '.epc-reg-success-lead{margin:0 0 18px;font-size:16px;line-height:1.55;color:#334155}'
		. '.epc-reg-success-steps{margin:0 0 18px;padding-left:20px;color:#334155;line-height:1.55}'
		. '.epc-reg-success-steps li{margin:0 0 10px}'
		. '.epc-reg-success-note{margin:0 0 18px;padding:12px 14px;background:#f1f5f9;border-radius:10px;color:#475569;font-size:14px;line-height:1.5}'
		. '.epc-reg-success-box{margin:0 0 18px;padding:14px;border-radius:10px;background:#fff7ed;border:1px solid #fdba74;color:#9a3412;font-size:14px}'
		. '.epc-reg-success-actions{display:flex;flex-wrap:wrap;gap:10px;margin-top:6px}'
		. '.epc-reg-success-actions a{display:inline-block;padding:11px 18px;border-radius:8px;font-weight:700;text-decoration:none;font-size:14px}'
		. '.epc-reg-success-actions .primary{background:#111827;color:#fff}'
		. '.epc-reg-success-actions .secondary{background:#fff;color:#111827;border:1px solid #cbd5e1}'
		. '</style>'
		. '<div class="epc-reg-success" role="status" aria-live="polite">'
		. '<div class="epc-reg-success-badge' . ($isWholesale ? ' is-wholesale' : '') . '">' . htmlspecialchars($badge, ENT_QUOTES, 'UTF-8') . '</div>'
		. '<h2>' . $title . '</h2>'
		. '<p class="epc-reg-success-lead">' . $lead . '</p>'
		. $steps
		. $typeNote
		. '<div class="epc-reg-success-actions">'
		. '<a class="primary" href="' . $loginUrl . '">Continue shopping</a>'
		. ($email !== '' ? '<a class="secondary" href="#epc-reg-success-inbox">I will check my inbox</a>' : '')
		. '</div>'
		. '<p id="epc-reg-success-inbox" style="margin:16px 0 0;font-size:13px;color:#64748b;">Tip: search your inbox for “' . $brand . '” or “confirm your email”.</p>'
		. '</div>';
}
