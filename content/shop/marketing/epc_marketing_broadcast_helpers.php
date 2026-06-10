<?php
/**
 * Marketing Broadcast — bulk email & WhatsApp for tenant CP.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/marketing/epc_marketing_broadcast_templates.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/customer_mgmt/epc_customer_mgmt_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_smtp.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php';

function epc_mb_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_mb_backend(): string
{
	global $DP_Config;
	return trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
}

function epc_mb_hub_url(string $tab = ''): string
{
	$base = '/' . epc_mb_backend() . '/control/portal/epc_marketing_broadcast';
	if ($tab !== '') {
		$base .= '?tab=' . rawurlencode($tab);
	}
	return $base;
}

function epc_mb_csrf_token(): string
{
	if (!isset($_SESSION)) {
		@session_start();
	}
	if (empty($_SESSION['epc_mb_csrf'])) {
		$_SESSION['epc_mb_csrf'] = bin2hex(random_bytes(16));
	}
	return (string) $_SESSION['epc_mb_csrf'];
}

function epc_mb_verify_csrf(): bool
{
	$token = (string) ($_POST['csrf_token'] ?? $_SERVER['HTTP_X_CSRF_TOKEN'] ?? '');
	if ($token === '') {
		return false;
	}
	if (!isset($_SESSION)) {
		@session_start();
	}
	return isset($_SESSION['epc_mb_csrf']) && hash_equals((string) $_SESSION['epc_mb_csrf'], $token);
}

function epc_mb_ensure_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_marketing_broadcast_campaigns` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`created_at` INT UNSIGNED NOT NULL,
			`channel` ENUM(\'email\',\'whatsapp\') NOT NULL,
			`template_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`subject` VARCHAR(255) NOT NULL DEFAULT \'\',
			`preview` VARCHAR(500) NOT NULL DEFAULT \'\',
			`body_html` MEDIUMTEXT,
			`body_text` MEDIUMTEXT,
			`audience_mode` VARCHAR(32) NOT NULL DEFAULT \'all\',
			`audience_meta` VARCHAR(255) NOT NULL DEFAULT \'\',
			`total_targets` INT UNSIGNED NOT NULL DEFAULT 0,
			`sent_ok` INT UNSIGNED NOT NULL DEFAULT 0,
			`sent_fail` INT UNSIGNED NOT NULL DEFAULT 0,
			`status` VARCHAR(24) NOT NULL DEFAULT \'draft\',
			`operator_id` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `created_at` (`created_at`),
			KEY `channel` (`channel`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_marketing_broadcast_log` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`campaign_id` INT UNSIGNED NOT NULL,
			`created_at` INT UNSIGNED NOT NULL,
			`recipient` VARCHAR(255) NOT NULL DEFAULT \'\',
			`user_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`status` TINYINT(1) NOT NULL DEFAULT 0,
			`detail` VARCHAR(500) NOT NULL DEFAULT \'\',
			`wa_link` VARCHAR(500) NOT NULL DEFAULT \'\',
			PRIMARY KEY (`id`),
			KEY `campaign_id` (`campaign_id`),
			KEY `created_at` (`created_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

function epc_mb_shop_context($DP_Config): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
	$name = function_exists('epc_brand_trade_name') ? epc_brand_trade_name() : '';
	if ($name === '') {
		$name = is_object($DP_Config) && !empty($DP_Config->from_name) ? (string) $DP_Config->from_name : 'Store';
	}
	$domain = rtrim((string) ($DP_Config->domain_path ?? ''), '/');
	if ($domain === '') {
		$domain = 'https://' . (string) ($_SERVER['HTTP_HOST'] ?? 'localhost');
	}
	if (strpos($domain, 'http') !== 0) {
		$domain = 'https://' . $domain;
	}
	return array('shop_name' => $name, 'shop_url' => $domain);
}

/**
 * @return array<int, array<string, mixed>>
 */
function epc_mb_list_groups(PDO $pdo): array
{
	try {
		$st = $pdo->query('SELECT `id`, `value` AS `name` FROM `groups` ORDER BY `id` ASC');
		return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
	} catch (Throwable $e) {
		return array();
	}
}

/**
 * @return array<int, array<string, mixed>>
 */
function epc_mb_resolve_recipients(PDO $pdo, string $mode, string $meta = '', string $channel = 'email'): array
{
	$mode = strtolower(trim($mode));
	$channel = strtolower(trim($channel));
	$rows = array();

	if ($mode === 'manual') {
		$lines = preg_split('/[\r\n,;]+/', $meta);
		foreach ($lines as $line) {
			$line = trim($line);
			if ($line === '') {
				continue;
			}
			if ($channel === 'email' && strpos($line, '@') === false) {
				continue;
			}
			$rows[] = array(
				'user_id' => 0,
				'email' => $channel === 'email' ? $line : '',
				'phone' => $channel === 'whatsapp' ? $line : '',
				'name' => '',
			);
		}
		return $rows;
	}

	$sql = "SELECT u.`user_id`, u.`email`, u.`phone`,
		MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname,
		MAX(CASE WHEN up.`data_key` = 'surname' THEN up.`data_value` END) AS sname
		FROM `users` u
		LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`";
	$params = array();
	$where = ' WHERE u.`user_id` > 0';

	if ($mode === 'group' && (int) $meta > 0) {
		$sql .= ' INNER JOIN `users_groups_bind` b ON b.`user_id` = u.`user_id`';
		$where .= ' AND b.`group_id` = ?';
		$params[] = (int) $meta;
	} elseif ($mode === 'with_orders') {
		$where .= ' AND EXISTS (SELECT 1 FROM `shop_orders` o WHERE o.`user_id` = u.`user_id` AND o.`successfully_created` = 1)';
	}

	if ($channel === 'email') {
		$where .= " AND u.`email` != '' AND u.`email` LIKE '%@%'";
	} else {
		$where .= " AND u.`phone` != ''";
	}

	$sql .= $where . ' GROUP BY u.`user_id` ORDER BY u.`user_id` DESC LIMIT 2000';
	$st = $pdo->prepare($sql);
	$st->execute($params);
	$raw = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

	foreach ($raw as $r) {
		$name = trim((string) ($r['fname'] ?? '') . ' ' . (string) ($r['sname'] ?? ''));
		$rows[] = array(
			'user_id' => (int) ($r['user_id'] ?? 0),
			'email' => trim((string) ($r['email'] ?? '')),
			'phone' => trim((string) ($r['phone'] ?? '')),
			'name' => $name !== '' ? $name : 'Customer',
		);
	}
	return $rows;
}

function epc_mb_count_recipients(PDO $pdo, string $mode, string $meta = '', string $channel = 'email'): int
{
	return count(epc_mb_resolve_recipients($pdo, $mode, $meta, $channel));
}

function epc_mb_customer_vars(array $recipient, array $shop): array
{
	return array(
		'customer_name' => (string) ($recipient['name'] ?? 'Customer'),
		'shop_name' => (string) ($shop['shop_name'] ?? 'Store'),
		'shop_url' => (string) ($shop['shop_url'] ?? ''),
	);
}

function epc_mb_normalize_post(array $post): array
{
	$mode = (string) ($post['audience_mode'] ?? 'all');
	if ($mode === 'group') {
		$post['audience_meta'] = (string) ($post['audience_meta_group'] ?? $post['audience_meta'] ?? '');
	} elseif ($mode === 'manual') {
		$post['audience_meta'] = (string) ($post['audience_meta_manual'] ?? $post['audience_meta'] ?? '');
	}
	return $post;
}

function epc_mb_create_campaign(PDO $pdo, array $data): int
{
	epc_mb_ensure_schema($pdo);
	$st = $pdo->prepare(
		'INSERT INTO `epc_marketing_broadcast_campaigns`
		(`created_at`, `channel`, `template_key`, `subject`, `preview`, `body_html`, `body_text`,
		 `audience_mode`, `audience_meta`, `total_targets`, `status`, `operator_id`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	);
	$st->execute(array(
		time(),
		(string) ($data['channel'] ?? 'email'),
		(string) ($data['template_key'] ?? ''),
		substr((string) ($data['subject'] ?? ''), 0, 255),
		substr((string) ($data['preview'] ?? ''), 0, 500),
		(string) ($data['body_html'] ?? ''),
		(string) ($data['body_text'] ?? ''),
		(string) ($data['audience_mode'] ?? 'all'),
		substr((string) ($data['audience_meta'] ?? ''), 0, 255),
		(int) ($data['total_targets'] ?? 0),
		(string) ($data['status'] ?? 'sending'),
		(int) ($data['operator_id'] ?? 0),
	));
	return (int) $pdo->lastInsertId();
}

function epc_mb_update_campaign_counts(PDO $pdo, int $campaignId, int $ok, int $fail, string $status = 'completed'): void
{
	$pdo->prepare(
		'UPDATE `epc_marketing_broadcast_campaigns` SET `sent_ok` = ?, `sent_fail` = ?, `status` = ? WHERE `id` = ?'
	)->execute(array($ok, $fail, $status, $campaignId));
}

function epc_mb_log_recipient(PDO $pdo, int $campaignId, array $recipient, bool $ok, string $detail = '', string $waLink = ''): void
{
	$addr = (string) ($recipient['email'] ?? $recipient['phone'] ?? '');
	$pdo->prepare(
		'INSERT INTO `epc_marketing_broadcast_log`
		(`campaign_id`, `created_at`, `recipient`, `user_id`, `status`, `detail`, `wa_link`)
		 VALUES (?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		$campaignId,
		time(),
		substr($addr, 0, 255),
		(int) ($recipient['user_id'] ?? 0),
		$ok ? 1 : 0,
		substr($detail, 0, 500),
		substr($waLink, 0, 500),
	));
}

/**
 * @return array{ok:bool,message:string,campaign_id:int,sent_ok:int,sent_fail:int,wa_links:array}
 */
function epc_mb_send_email_campaign(PDO $pdo, array $post, $DP_Config, int $operatorId = 0): array
{
	$post = epc_mb_normalize_post($post);
	$mode = (string) ($post['audience_mode'] ?? 'all');
	$meta = (string) ($post['audience_meta'] ?? '');
	$templateKey = (string) ($post['template_key'] ?? 'blank');
	$subject = trim((string) ($post['subject'] ?? ''));
	$preview = trim((string) ($post['preview'] ?? ''));
	$html = (string) ($post['body_html'] ?? '');

	$templates = epc_mb_email_templates();
	if ($html === '' && isset($templates[$templateKey])) {
		$tpl = $templates[$templateKey];
		$subject = $subject !== '' ? $subject : (string) $tpl['subject'];
		$preview = $preview !== '' ? $preview : (string) ($tpl['preview'] ?? '');
		$html = (string) $tpl['html'];
	}
	if ($subject === '' || $html === '') {
		return array('ok' => false, 'message' => 'Subject and HTML body are required.', 'campaign_id' => 0, 'sent_ok' => 0, 'sent_fail' => 0, 'wa_links' => array());
	}

	$diag = epc_auth_smtp_diagnose();
	if (!$diag['ok']) {
		return array('ok' => false, 'message' => 'SMTP not configured: ' . implode(' ', $diag['issues']), 'campaign_id' => 0, 'sent_ok' => 0, 'sent_fail' => 0, 'wa_links' => array());
	}

	$recipients = epc_mb_resolve_recipients($pdo, $mode, $meta, 'email');
	if (!$recipients) {
		return array('ok' => false, 'message' => 'No recipients with valid email addresses.', 'campaign_id' => 0, 'sent_ok' => 0, 'sent_fail' => 0, 'wa_links' => array());
	}

	$maxBatch = min(100, max(1, (int) ($post['batch_limit'] ?? 50)));
	$recipients = array_slice($recipients, 0, $maxBatch);
	$shop = epc_mb_shop_context($DP_Config);

	$campaignId = epc_mb_create_campaign($pdo, array(
		'channel' => 'email',
		'template_key' => $templateKey,
		'subject' => $subject,
		'preview' => $preview,
		'body_html' => $html,
		'audience_mode' => $mode,
		'audience_meta' => $meta,
		'total_targets' => count($recipients),
		'operator_id' => $operatorId,
	));

	$ok = 0;
	$fail = 0;
	foreach ($recipients as $recipient) {
		$vars = epc_mb_customer_vars($recipient, $shop);
		$subj = epc_mb_apply_template_vars($subject, $vars);
		$body = epc_mb_apply_template_vars($html, $vars);
		$result = epc_auth_smtp_send_html((string) $recipient['email'], $subj, $body);
		if (!empty($result['ok'])) {
			$ok++;
			epc_mb_log_recipient($pdo, $campaignId, $recipient, true, (string) ($result['message'] ?? 'sent'));
		} else {
			$fail++;
			epc_mb_log_recipient($pdo, $campaignId, $recipient, false, (string) ($result['message'] ?? 'failed'));
		}
		usleep(200000);
	}

	epc_mb_update_campaign_counts($pdo, $campaignId, $ok, $fail);
	return array(
		'ok' => true,
		'message' => "Email campaign sent: {$ok} OK, {$fail} failed (batch limit {$maxBatch}).",
		'campaign_id' => $campaignId,
		'sent_ok' => $ok,
		'sent_fail' => $fail,
		'wa_links' => array(),
	);
}

/**
 * @return array{ok:bool,message:string,campaign_id:int,sent_ok:int,sent_fail:int,wa_links:array}
 */
function epc_mb_send_whatsapp_campaign(PDO $pdo, array $post, $DP_Config, int $operatorId = 0): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/epc_whatsapp_notify.php';

	$post = epc_mb_normalize_post($post);
	$mode = (string) ($post['audience_mode'] ?? 'all');
	$meta = (string) ($post['audience_meta'] ?? '');
	$templateKey = (string) ($post['template_key'] ?? 'blank');
	$bodyText = trim((string) ($post['body_text'] ?? ''));

	$templates = epc_mb_whatsapp_templates();
	if ($bodyText === '' && isset($templates[$templateKey])) {
		$bodyText = (string) $templates[$templateKey]['body'];
	}
	if ($bodyText === '') {
		return array('ok' => false, 'message' => 'WhatsApp message body is required.', 'campaign_id' => 0, 'sent_ok' => 0, 'sent_fail' => 0, 'wa_links' => array());
	}

	$recipients = epc_mb_resolve_recipients($pdo, $mode, $meta, 'whatsapp');
	if (!$recipients) {
		return array('ok' => false, 'message' => 'No recipients with phone numbers.', 'campaign_id' => 0, 'sent_ok' => 0, 'sent_fail' => 0, 'wa_links' => array());
	}

	$maxBatch = min(100, max(1, (int) ($post['batch_limit'] ?? 50)));
	$recipients = array_slice($recipients, 0, $maxBatch);
	$shop = epc_mb_shop_context($DP_Config);
	$apiEnabled = epc_wa_api_enabled($DP_Config);

	$campaignId = epc_mb_create_campaign($pdo, array(
		'channel' => 'whatsapp',
		'template_key' => $templateKey,
		'body_text' => $bodyText,
		'audience_mode' => $mode,
		'audience_meta' => $meta,
		'total_targets' => count($recipients),
		'operator_id' => $operatorId,
	));

	$ok = 0;
	$fail = 0;
	$waLinks = array();

	foreach ($recipients as $recipient) {
		$vars = epc_mb_customer_vars($recipient, $shop);
		$text = epc_mb_apply_template_vars($bodyText, $vars);
		$phone = (string) ($recipient['phone'] ?? '');
		$waLink = epc_wa_share_url($phone, $text);

		if ($apiEnabled) {
			$result = epc_wa_api_send_text($phone, $text, $DP_Config);
			if (!empty($result['ok'])) {
				$ok++;
				epc_mb_log_recipient($pdo, $campaignId, $recipient, true, 'API sent', $waLink);
				epc_wa_log_attempt($pdo, 'marketing_broadcast', $phone, true, substr($text, 0, 200), $result['response'] ?? array());
			} else {
				$fail++;
				$err = (string) ($result['error'] ?? 'API failed');
				epc_mb_log_recipient($pdo, $campaignId, $recipient, false, $err, $waLink);
				epc_wa_log_attempt($pdo, 'marketing_broadcast', $phone, false, substr($text, 0, 200), $result['response'] ?? array());
			}
			usleep(300000);
		} else {
			$ok++;
			epc_mb_log_recipient($pdo, $campaignId, $recipient, true, 'wa.me link prepared', $waLink);
			$waLinks[] = array(
				'name' => (string) ($recipient['name'] ?? ''),
				'phone' => $phone,
				'link' => $waLink,
			);
		}
	}

	epc_mb_update_campaign_counts($pdo, $campaignId, $ok, $fail);
	$msg = $apiEnabled
		? "WhatsApp API campaign: {$ok} OK, {$fail} failed."
		: "WhatsApp wa.me links prepared for {$ok} customers (open links to send manually).";

	return array(
		'ok' => true,
		'message' => $msg,
		'campaign_id' => $campaignId,
		'sent_ok' => $ok,
		'sent_fail' => $fail,
		'wa_links' => $waLinks,
	);
}

/**
 * @return array<int, array<string, mixed>>
 */
function epc_mb_list_campaigns(PDO $pdo, int $limit = 20): array
{
	epc_mb_ensure_schema($pdo);
	$limit = max(1, min(50, $limit));
	$st = $pdo->query(
		'SELECT * FROM `epc_marketing_broadcast_campaigns` ORDER BY `id` DESC LIMIT ' . $limit
	);
	return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
}

function epc_mb_dashboard_stats(PDO $pdo): array
{
	epc_mb_ensure_schema($pdo);
	$emailCustomers = epc_mb_count_recipients($pdo, 'all', '', 'email');
	$waCustomers = epc_mb_count_recipients($pdo, 'all', '', 'whatsapp');
	$sentEmail = (int) $pdo->query(
		"SELECT IFNULL(SUM(`sent_ok`),0) FROM `epc_marketing_broadcast_campaigns` WHERE `channel` = 'email'"
	)->fetchColumn();
	$sentWa = (int) $pdo->query(
		"SELECT IFNULL(SUM(`sent_ok`),0) FROM `epc_marketing_broadcast_campaigns` WHERE `channel` = 'whatsapp'"
	)->fetchColumn();
	$campaigns = (int) $pdo->query('SELECT COUNT(*) FROM `epc_marketing_broadcast_campaigns`')->fetchColumn();
	return array(
		'email_recipients' => $emailCustomers,
		'whatsapp_recipients' => $waCustomers,
		'emails_sent' => $sentEmail,
		'whatsapp_sent' => $sentWa,
		'campaigns' => $campaigns,
	);
}

function epc_cp_marketing_broadcast_menu_apply(PDO $pdo): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php';
	epc_cp_mm_lang($pdo, 'epc_marketing_broadcast_cp', 'Marketing broadcast', 'Рассылка — маркетинг');

	$portalGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_portal');
	if ($portalGroup <= 0) {
		$portalMenu = epc_cp_portal_menu_apply($pdo);
		$portalGroup = (int) ($portalMenu['portal_group'] ?? 0);
	}

	$itemId = epc_cp_mm_ensure_item(
		$pdo,
		$portalGroup,
		'epc_marketing_broadcast_cp',
		'/<backend>/control/portal/epc_marketing_broadcast',
		18,
		'#db2777',
		'fas fa-bullhorn',
		1
	);

	return array(
		'portal_group' => $portalGroup,
		'marketing_broadcast_item' => $itemId,
	);
}
