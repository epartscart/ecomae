<?php
/**
 * BOS login (deployable under content/) — password-verified across DB sources.
 *
 * bos/ajax_epc_bos.php is often root-owned and not hot-deployable; bos/index.php
 * calls epc_bos_session_start() before requiring that ajax file, so the unified
 * kernel can intercept POST /bos/?action=login here.
 */
defined('_ASTEXE_') or die('No access');

function epc_bos_ajax_login_secure(): array
{
	$email = trim((string) ($_POST['email'] ?? ''));
	$password = (string) ($_POST['password'] ?? '');

	if ($email === '' || $password === '') {
		return array('ok' => false, 'error' => 'Email and password required');
	}

	$secretSuccession = '';
	$mainPdo = null;
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		$secretSuccession = (string) ($cfg->secret_succession ?? '');
		$mainPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		$mainPdo = null;
	}

	$platformPdo = null;
	try {
		$platformPdo = epc_portal_platform_operator_pdo();
	} catch (Exception $e) {
		$platformPdo = null;
	}

	if (!$mainPdo && !$platformPdo) {
		return array('ok' => false, 'error' => 'Platform database unavailable');
	}

	$userRow = null;
	$authPdo = null;
	$tables = array('users', 'admin', 'epc_cp_users');
	$pdoSources = array_filter(array($mainPdo, $platformPdo));
	$passOk = false;
	$storedPass = '';

	foreach ($pdoSources as $tryPdo) {
		foreach ($tables as $table) {
			try {
				$st = $tryPdo->prepare("SELECT * FROM `{$table}` WHERE `email` = ? LIMIT 1");
				$st->execute(array($email));
				$row = $st->fetch(PDO::FETCH_ASSOC);
				if (!$row) {
					continue;
				}
				$candidate = (string) ($row['password'] ?? $row['pass'] ?? '');
				if ($candidate === '') {
					continue;
				}
				$ok = false;
				if (password_verify($password, $candidate)) {
					$ok = true;
				} elseif ($secretSuccession !== '' && md5($password . $secretSuccession) === $candidate) {
					$ok = true;
				} elseif (md5($password) === $candidate) {
					$ok = true;
				}
				if ($ok) {
					$userRow = $row;
					$userRow['_table'] = $table;
					$authPdo = $tryPdo;
					$storedPass = $candidate;
					$passOk = true;
					break 2;
				}
			} catch (Exception $e) {
				continue;
			}
		}
	}

	if (!$userRow || !$passOk) {
		return array('ok' => false, 'error' => 'Invalid credentials');
	}

	require_once __DIR__ . '/epc_security_kernel.php';
	$roleInfo = epc_sec_bos_resolve_role($authPdo instanceof PDO ? $authPdo : $mainPdo, $userRow, $email);
	if (empty($roleInfo['allowed'])) {
		return array('ok' => false, 'error' => 'Access denied — operator credentials required');
	}

	$upgradeFile = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_password_upgrade.php';
	if (is_file($upgradeFile)) {
		require_once $upgradeFile;
		$userId_ = (int) ($userRow['id'] ?? $userRow['ID'] ?? $userRow['user_id'] ?? 0);
		if ($userId_ > 0 && $authPdo && function_exists('epc_password_is_legacy_md5') && epc_password_is_legacy_md5($storedPass)) {
			epc_password_upgrade_if_needed($authPdo, $userId_, $password, $storedPass);
		}
	}

	$userId = (int) ($userRow['id'] ?? $userRow['ID'] ?? $userRow['user_id'] ?? 0);
	$role = (string) $roleInfo['role'];
	$tenantSiteKey = (string) ($roleInfo['tenant_key'] ?? '');

	$sessionFile = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_session_security.php';
	if (is_file($sessionFile)) {
		require_once $sessionFile;
		if (function_exists('epc_session_regenerate')) {
			epc_session_regenerate();
		} else {
			session_regenerate_id(true);
		}
	} else {
		session_regenerate_id(true);
	}

	epc_bos_set_context(array(
		'role' => $role,
		'user_id' => $userId,
		'email' => $email,
		'tenant_key' => $tenantSiteKey,
	));
	$csrf = epc_sec_csrf_token('bos');

	$kernel = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
	if (is_file($kernel)) {
		require_once $kernel;
		if (function_exists('epc_boc_audit_log')) {
			try {
				$auditPdo = $platformPdo ?: $mainPdo;
				if ($auditPdo) {
					epc_boc_audit_log($auditPdo, $userId, 'bos', 'login', '', array('role' => $role), $email);
				}
			} catch (Exception $e) {
			}
		}
	}

	$redirect = '/bos/';
	if ($tenantSiteKey !== '') {
		$redirect = '/bos/?t=' . urlencode($tenantSiteKey);
	}
	return array(
		'ok' => true,
		'redirect' => $redirect,
		'role' => $role,
		'csrf' => $csrf,
	);
}
