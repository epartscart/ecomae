<?php
/**
 * Tenant hub — Super CP helpers (DNS-only multi-tenant on ecomae).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/../../general_pages/epc_portal_db.php';
require_once __DIR__ . '/../../general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/../../general_pages/epc_portal_tenant_intro.php';

function epc_th_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

/**
 * Storefront / CP / ERP action links for Tenant hub (shared ERP uses client-erp URLs).
 *
 * @return array{storefront:string,cp:string,erp:string}
 */
function epc_th_tenant_action_urls(array $row): array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
	$host = trim((string) ($row['hostname'] ?? ''));
	$industry = (string) ($row['industry_code'] ?? '');
	$erpStandalone = $industry === 'erp_standalone' || $industry === 'erp_only';

	if (epc_portal_tenant_is_shared_erp_row($row) && $key !== '') {
		require_once __DIR__ . '/../../general_pages/epc_client_erp_router.php';
		return array(
			'storefront' => '',
			'cp' => '',
			'erp' => 'https://www.ecomae.com' . epc_client_erp_shell_url($key),
			'erp_login' => 'https://www.ecomae.com' . epc_client_erp_login_url($key),
		);
	}

	$commerceHost = function_exists('epc_portal_tenant_control_commerce_host')
		? epc_portal_tenant_control_commerce_host($host)
		: $host;
	if ($commerceHost === '') {
		$commerceHost = $host;
	}

	if ($erpStandalone) {
		return array(
			'storefront' => '',
			'cp' => 'https://' . $commerceHost . '/cp/',
			'erp' => 'https://' . $commerceHost . '/cp/shop/finance/erp?epc_erp_shell=1',
		);
	}

	return array(
		'storefront' => 'https://' . $commerceHost . '/en/',
		'cp' => 'https://' . $commerceHost . '/cp/',
		'erp' => 'https://' . $commerceHost . '/cp/shop/finance/erp?epc_erp_shell=1',
	);
}

function epc_th_tenant_db_connect_ok(array $row): bool
{
	$db = trim((string) ($row['db_name'] ?? ''));
	$user = trim((string) ($row['db_user'] ?? ''));
	if ($user === '') {
		$user = $db;
	}
	$pass = (string) ($row['db_password'] ?? '');
	if ($db === 'docpart' && $pass === '' && function_exists('epc_portal_resolve_tenant_db_credentials')) {
		$creds = epc_portal_resolve_tenant_db_credentials();
		if (!empty($creds['password'])) {
			$user = (string) ($creds['user'] ?? 'docpart');
			$pass = (string) $creds['password'];
		}
	}
	if ($db === '' || $user === '' || $pass === '') {
		return false;
	}
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8;connect_timeout=3',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
		);
		$pdo->query('SELECT 1');
		return true;
	} catch (Throwable $e) {
		return false;
	}
}

function epc_th_require_super_cp(): void
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (!epc_portal_is_platform_operator()) {
		throw new Exception('Super CP is only available on www.ecomae.com/cp');
	}
}

function epc_th_list_tenants(PDO $db): array
{
	$rows = epc_portal_list_tenants($db);
	$industries = epc_portal_industries();
	$ecosystems = epc_portal_ecosystems();
	$statuses = epc_portal_tenant_statuses();
	foreach ($rows as &$r) {
		$code = (string) $r['industry_code'];
		$r['industry_name'] = isset($industries[$code]['name']) ? $industries[$code]['name'] : $code;
		$ecoCode = isset($industries[$code]['ecosystem']) ? (string) $industries[$code]['ecosystem'] : '';
		$r['ecosystem_code'] = $ecoCode;
		$r['ecosystem_name'] = ($ecoCode !== '' && isset($ecosystems[$ecoCode]['name'])) ? $ecosystems[$ecoCode]['name'] : '';
		$st = (string) $r['status'];
		$r['status_label'] = isset($statuses[$st]) ? $statuses[$st] : $st;
		if (!empty($r['is_demo'])) {
			require_once __DIR__ . '/../../general_pages/epc_portal_demo.php';
			$demoKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $r['site_key']));
			$demoUrls = epc_portal_demo_urls($demoKey, $r);
			$cpAuto = epc_portal_demo_cp_autologin_url($demoKey);
			$r['storefront_url'] = (string) ($demoUrls['storefront'] ?? '');
			$r['cp_url'] = $cpAuto !== '' ? $cpAuto : (string) ($demoUrls['cp'] ?? ('https://www.ecomae.com' . epc_portal_demo_cp_login_url($demoKey)));
			$r['erp_url'] = 'https://www.ecomae.com' . epc_portal_demo_erp_shell_url($demoKey);
			$r['is_demo_tenant'] = true;
		} else {
			$urls = epc_th_tenant_action_urls($r);
			$r['storefront_url'] = (string) ($urls['storefront'] ?? '');
			$r['cp_url'] = (string) ($urls['cp'] ?? '');
			$r['erp_url'] = (string) ($urls['erp'] ?? '');
		}
		$r['intro'] = epc_portal_intro_decode((string) ($r['intro_json'] ?? ''));
		$r['intro_done'] = !empty($r['intro']['submitted_at']);
		$r['db_connect_ok'] = epc_th_tenant_db_connect_ok($r);
	}
	unset($r);
	return $rows;
}

function epc_th_platform_stats(PDO $db): array
{
	$tenants = epc_th_list_tenants($db);
	$live = 0;
	$pending = 0;
	foreach ($tenants as $t) {
		if ($t['status'] === 'live') {
			$live++;
		}
		if ($t['status'] === 'dns_pending') {
			$pending++;
		}
	}
	return array(
		'tenants_total' => count($tenants),
		'tenants_live' => $live,
		'tenants_dns_pending' => $pending,
		'platform_ip' => epc_portal_platform_ip(),
		'platform_host' => epc_portal_host(),
	);
}

function epc_th_probe_url(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 8, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$start = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $start) * 1000);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	return array(
		'ok' => $body !== false && $code >= 200 && $code < 400,
		'http_code' => $code,
		'ms' => $ms,
		'snippet' => $body !== false ? substr(strip_tags((string) $body), 0, 120) : '',
	);
}

function epc_th_add_tenant(PDO $db, array $data): array
{
	return epc_portal_save_tenant($db, $data);
}

function epc_th_update_tenant_status(PDO $db, string $siteKey, string $status): array
{
	$statuses = epc_portal_tenant_statuses();
	if (!isset($statuses[$status])) {
		return array('ok' => false, 'message' => 'Invalid status');
	}
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$st = $db->prepare('UPDATE `epc_portal_tenants` SET `status` = ?, `updated_at` = ? WHERE `site_key` = ?');
	$st->execute(array($status, time(), $key));
	if ($st->rowCount() === 0) {
		return array('ok' => false, 'message' => 'Tenant not found');
	}
	$sync = array('ok' => true, 'message' => 'skipped');
	if ($status === 'live' && function_exists('epc_portal_sync_tenant_packs_to_client_db')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_cp_menu.php';
		$sync = epc_portal_sync_tenant_packs_to_client_db($db, $key);
	}
	$msg = 'Status updated to ' . $status;
	if ($status === 'live' && !empty($sync['ok']) && ($sync['message'] ?? '') !== 'skipped') {
		$msg .= ' — ' . $sync['message'];
	} elseif ($status === 'live' && empty($sync['ok'])) {
		$msg .= ' — pack sync: ' . ($sync['message'] ?? 'failed');
	}
	return array('ok' => true, 'message' => $msg, 'client_sync' => $sync);
}

/**
 * Update a tenant's Blockchain BOS mode from Super CP fleet controls.
 *
 * @return array{ok:bool,message:string,mode?:string}
 */
function epc_th_update_tenant_blockchain_mode(PDO $db, string $siteKey, string $mode): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
	$mode = epc_bc_bos_normalize_mode($mode);
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return array('ok' => false, 'message' => 'Invalid tenant');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	epc_portal_db_ensure($db);
	$st = $db->prepare('UPDATE `epc_portal_tenants` SET `blockchain_mode` = ?, `updated_at` = ? WHERE `site_key` = ?');
	$st->execute(array($mode, time(), $key));
	if ($st->rowCount() === 0) {
		// rowCount 0 can also mean "same value" — confirm tenant exists
		$chk = $db->prepare('SELECT `blockchain_mode` FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
		$chk->execute(array($key));
		$row = $chk->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			return array('ok' => false, 'message' => 'Tenant not found');
		}
	}
	epc_bc_bos_clear_tenant_mode_cache($key);
	$labels = epc_bc_bos_modes();
	$label = isset($labels[$mode]) ? $labels[$mode] : $mode;
	$note = '';
	if ($mode === 'network') {
		$note = ' (roadmap — currently records and anchors like Anchor)';
	}
	return array(
		'ok' => true,
		'message' => 'Blockchain mode for ' . $key . ' set to ' . $label . $note,
		'mode' => $mode,
	);
}

/**
 * Drain pending proofs immediately (operator override for cron).
 *
 * @return array{ok:bool,message:string}
 */
function epc_th_anchor_blockchain_pending_now(int $limit = 100): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
	$out = epc_bc_bos_anchor_pending_batch($limit);
	if (empty($out['ok'])) {
		return array('ok' => false, 'message' => 'Anchor failed: ' . (string) ($out['error'] ?? 'unknown'));
	}
	$count = (int) ($out['proof_count'] ?? 0);
	if ($count === 0) {
		return array('ok' => true, 'message' => 'No pending proofs to anchor.');
	}
	$net = (string) ($out['anchor_network'] ?? epc_bc_bos_anchor_network());
	$root = (string) ($out['merkle_root'] ?? '');
	$msg = 'Anchored ' . $count . ' proof(s) on ' . $net;
	if ($root !== '') {
		$msg .= ' · root ' . substr($root, 0, 12) . '…';
	}
	return array('ok' => true, 'message' => $msg, 'result' => $out);
}

function epc_th_onboard_client(PDO $db, array $post, string $submittedBy = ''): array
{
	return epc_portal_onboard_client($db, $post, $submittedBy);
}

function epc_th_launch_checklist(PDO $db, string $siteKey): array
{
	return epc_portal_tenant_launch_checklist($db, $siteKey);
}

/**
 * Super CP: apply matching industry visual style + storefront package to a tenant.
 *
 * @return array{ok:bool,message:string}
 */
function epc_th_apply_industry_theme(PDO $db, string $siteKey, array $opts = array()): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
	return epc_portal_apply_industry_theme_to_tenant($db, $siteKey, $opts);
}
