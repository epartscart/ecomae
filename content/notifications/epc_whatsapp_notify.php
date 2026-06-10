<?php
/**
 * WhatsApp Cloud API — Phase 2 automated notifications (Meta Graph API).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php';

function epc_wa_cfg($DP_Config, string $key, $default = '')
{
	if (!is_object($DP_Config) || !property_exists($DP_Config, $key)) {
		return $default;
	}
	$v = $DP_Config->$key;
	return ($v === null || $v === '') ? $default : $v;
}

function epc_wa_api_enabled($DP_Config): bool
{
	return (string)epc_wa_cfg($DP_Config, 'epc_whatsapp_api_enabled', '0') === '1'
		&& epc_wa_cfg($DP_Config, 'epc_whatsapp_api_token', '') !== ''
		&& epc_wa_cfg($DP_Config, 'epc_whatsapp_phone_number_id', '') !== '';
}

function epc_wa_notify_names($DP_Config): array
{
	$raw = (string)epc_wa_cfg(
		$DP_Config,
		'epc_whatsapp_notify_names',
		'new_order_to_user,new_order_to_manager,order_status_to_customer,order_status_to_manager,order_message_to_customer,order_message_to_manager,epc_customer_login'
	);
	$parts = array_filter(array_map('trim', explode(',', $raw)));
	return $parts !== array() ? $parts : array('new_order_to_user', 'order_status_to_customer');
}

function epc_wa_notify_is_allowed(string $notifyName, $DP_Config): bool
{
	return in_array($notifyName, epc_wa_notify_names($DP_Config), true);
}

function epc_wa_plain_from_html(string $html): string
{
	$t = html_entity_decode(strip_tags($html), ENT_QUOTES | ENT_HTML5, 'UTF-8');
	$t = preg_replace('/[ \t]+/', ' ', $t);
	$t = preg_replace("/\n{3,}/", "\n\n", $t);
	return trim($t);
}

function epc_wa_build_notify_body(string $smsBody, string $emailBody, string $notifyName, array $vars, $DP_Config): string
{
	$body = trim($smsBody);
	if ($body === '') {
		$body = epc_wa_plain_from_html($emailBody);
	}
	if ($body === '' && !empty($vars['order_text'])) {
		$body = epc_wa_plain_from_html((string)$vars['order_text']);
	}
	if ($body === '' && !empty($vars['order_id'])) {
		$site = epc_wa_site_name($DP_Config);
		$body = $site . ' — order #' . (int)$vars['order_id'];
	}
	$max = 3500;
	if (strlen($body) > $max) {
		$body = substr($body, 0, $max - 1) . '…';
	}
	if ((string)epc_wa_cfg($DP_Config, 'epc_whatsapp_bilingual_notify', '1') === '1' && $body !== '') {
		$ar = epc_wa_notify_ar_snippet($notifyName, $vars, $DP_Config);
		if ($ar !== '') {
			$body = epc_wa_bilingual($body, $ar);
		}
	}
	return $body;
}

function epc_wa_notify_ar_snippet(string $notifyName, array $vars, $DP_Config): string
{
	$site = epc_wa_site_name($DP_Config);
	$oid = !empty($vars['order_id']) ? (int)$vars['order_id'] : 0;
	if (strpos($notifyName, 'order') !== false && $oid > 0) {
		return "مرحباً من {$site} — طلب #{$oid}. للاستفسار ردّوا على هذه الرسالة.";
	}
	if ($notifyName === 'epc_customer_login') {
		return 'تسجيل دخول عميل — ' . $site;
	}
	return "رسالة من {$site}.";
}

function epc_wa_status_allows_send(string $notifyName, array $vars, $DP_Config): bool
{
	if (!isset($DP_Config->orders_statuses_notifications_settings) || (string)$DP_Config->orders_statuses_notifications_settings !== '1') {
		return true;
	}
	$ref = isset($vars['status_ref']) && is_array($vars['status_ref']) ? $vars['status_ref'] : null;
	if (!$ref) {
		return true;
	}
	$map = array(
		'order_status_to_manager' => 'to_manager_sms',
		'order_status_to_customer' => 'to_customer_sms',
		'order_item_status_to_manager' => 'to_manager_sms',
		'order_item_status_to_customer' => 'to_customer_sms',
	);
	if (!isset($map[$notifyName])) {
		return true;
	}
	$key = $map[$notifyName];
	return !isset($ref[$key]) || (int)$ref[$key] === 1;
}

function epc_wa_should_send_for_person(array $notify, array $vars, $DP_Config, bool $hasPhone): bool
{
	if (!$hasPhone || !epc_wa_api_enabled($DP_Config)) {
		return false;
	}
	if ((int)($notify['email_on'] ?? 0) === 0 && (int)($notify['sms_on'] ?? 0) === 0) {
		return false;
	}
	if (!epc_wa_notify_is_allowed((string)$notify['name'], $DP_Config)) {
		return false;
	}
	return epc_wa_status_allows_send((string)$notify['name'], $vars, $DP_Config);
}

function epc_wa_log_attempt(PDO $db, string $notifyName, string $phone, bool $ok, string $preview, array $apiResponse = array()): void
{
	try {
		$db->exec(
			'CREATE TABLE IF NOT EXISTS `epc_whatsapp_notify_log` (
				`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
				`created_at` INT UNSIGNED NOT NULL,
				`notify_name` VARCHAR(64) NOT NULL,
				`phone` VARCHAR(32) NOT NULL,
				`status` TINYINT(1) NOT NULL DEFAULT 0,
				`message_preview` VARCHAR(500) NOT NULL DEFAULT \'\',
				`response` TEXT,
				PRIMARY KEY (`id`),
				KEY `created_at` (`created_at`),
				KEY `notify_name` (`notify_name`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8'
		);
		$st = $db->prepare(
			'INSERT INTO `epc_whatsapp_notify_log` (`created_at`, `notify_name`, `phone`, `status`, `message_preview`, `response`)
			 VALUES (?, ?, ?, ?, ?, ?)'
		);
		$st->execute(array(
			time(),
			$notifyName,
			$phone,
			$ok ? 1 : 0,
			substr($preview, 0, 500),
			json_encode($apiResponse, JSON_UNESCAPED_UNICODE),
		));
	} catch (Throwable $e) {
	}
}

/**
 * @return array{ok:bool,response:array,error:string}
 */
function epc_wa_api_send_text(string $phone, string $text, $DP_Config): array
{
	$digits = epc_wa_digits($phone);
	if ($digits === '' || trim($text) === '') {
		return array('ok' => false, 'response' => array(), 'error' => 'empty phone or body');
	}

	$token = (string)epc_wa_cfg($DP_Config, 'epc_whatsapp_api_token', '');
	$phoneId = (string)epc_wa_cfg($DP_Config, 'epc_whatsapp_phone_number_id', '');
	$version = (string)epc_wa_cfg($DP_Config, 'epc_whatsapp_api_version', 'v21.0');
	$url = 'https://graph.facebook.com/' . rawurlencode($version) . '/' . rawurlencode($phoneId) . '/messages';

	$payload = array(
		'messaging_product' => 'whatsapp',
		'recipient_type' => 'individual',
		'to' => $digits,
		'type' => 'text',
		'text' => array(
			'preview_url' => false,
			'body' => $text,
		),
	);

	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_POST => true,
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_HTTPHEADER => array(
			'Authorization: Bearer ' . $token,
			'Content-Type: application/json',
		),
		CURLOPT_POSTFIELDS => json_encode($payload, JSON_UNESCAPED_UNICODE),
		CURLOPT_TIMEOUT => 30,
		CURLOPT_SSL_VERIFYPEER => true,
	));
	$raw = curl_exec($ch);
	$errno = curl_errno($ch);
	$err = curl_error($ch);
	$code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);

	if ($errno) {
		return array('ok' => false, 'response' => array('curl_error' => $err), 'error' => $err);
	}

	$decoded = json_decode((string)$raw, true);
	if (!is_array($decoded)) {
		$decoded = array('raw' => substr((string)$raw, 0, 500));
	}

	$ok = ($code >= 200 && $code < 300 && !empty($decoded['messages']));
	if (!$ok && empty($decoded['error']['message'])) {
		$decoded['error'] = array('message' => 'HTTP ' . $code);
	}

	return array(
		'ok' => $ok,
		'response' => $decoded,
		'error' => $ok ? '' : (string)($decoded['error']['message'] ?? 'send failed'),
	);
}

function epc_wa_dispatch_for_person(
	PDO $db,
	$DP_Config,
	array $notify,
	array $vars,
	string $smsBody,
	string $emailBody,
	string $phone,
	bool $hasPhone
): array {
	$out = array(
		'tried_to_send' => false,
		'status' => false,
		'error' => '',
	);

	if (!epc_wa_should_send_for_person($notify, $vars, $DP_Config, $hasPhone)) {
		return $out;
	}

	$body = epc_wa_build_notify_body($smsBody, $emailBody, (string)$notify['name'], $vars, $DP_Config);
	if ($body === '') {
		return $out;
	}

	$out['tried_to_send'] = true;
	$result = epc_wa_api_send_text($phone, $body, $DP_Config);
	$out['status'] = $result['ok'];
	$out['error'] = $result['error'];
	epc_wa_log_attempt($db, (string)$notify['name'], $phone, $result['ok'], $body, $result['response']);

	return $out;
}
