<?php
/**
 * ERP access — frontend ERP team group (no CP admin required).
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_group_value_key()
{
	return 'EPC_ERP_TEAM';
}

function epc_erp_frontend_urls()
{
	return array(
		'main' => 'shop/erp',
		'guide' => 'shop/erp/guide',
	);
}

function epc_erp_cp_urls()
{
	return array(
		'main' => 'shop/finance/erp',
		'guide' => 'shop/finance/erp/guide',
	);
}

function epc_erp_lang_href()
{
	if (isset($GLOBALS['multilang_params']['lang_href']) && $GLOBALS['multilang_params']['lang_href'] !== '') {
		return rtrim((string)$GLOBALS['multilang_params']['lang_href'], '/');
	}
	if (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') {
		return rtrim((string)$multilang_params['lang_href'], '/');
	}
	return '';
}

function epc_erp_team_group_id(PDO $db)
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$key = epc_erp_group_value_key();
	$st = $db->prepare('SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1');
	$st->execute(array($key));
	$cached = (int)$st->fetchColumn();
	return $cached;
}

function epc_erp_user_in_team(PDO $db, $userId = 0)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if ($userId <= 0) {
		$userId = (int)DP_User::getUserId();
	}
	if ($userId <= 0) {
		return false;
	}
	$gid = epc_erp_team_group_id($db);
	if ($gid <= 0) {
		return false;
	}
	$st = $db->prepare('SELECT 1 FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` = ? LIMIT 1');
	$st->execute(array($userId, $gid));
	return (bool)$st->fetchColumn();
}

function epc_erp_allowed_groups_for_content(PDO $db, $contentUrl, $isFrontend)
{
	$allowed = array();
	$st = $db->prepare(
		'SELECT `group_id` FROM `content_access`
		 WHERE `content_id` = (SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = ? LIMIT 1)'
	);
	$st->execute(array($contentUrl, (int)$isFrontend));
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$allowed[] = (int)$row['group_id'];
	}
	if (empty($allowed)) {
		return array();
	}
	$inserted = array();
	$collect = function ($parentId) use ($db, &$collect, &$inserted, &$allowed) {
		$ch = $db->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($parentId));
		while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
			$gid = (int)$row['id'];
			if (!in_array($gid, $allowed, true) && !in_array($gid, $inserted, true)) {
				$inserted[] = $gid;
			}
			if ((int)$row['count'] > 0) {
				$collect($gid);
			}
		}
	};
	foreach ($allowed as $gid) {
		$collect((int)$gid);
	}
	return array_values(array_unique(array_merge($allowed, $inserted)));
}

function epc_erp_profile_groups($isFrontend)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if ($isFrontend) {
		$profile = DP_User::getUserProfile();
	} else {
		$profile = DP_User::getAdminProfile();
	}
	if (!$profile || empty($profile['groups']) || !is_array($profile['groups'])) {
		return array();
	}
	return array_map('intval', $profile['groups']);
}

function epc_erp_has_content_access(PDO $db, $contentUrl, $isFrontend)
{
	$allowed = epc_erp_allowed_groups_for_content($db, $contentUrl, $isFrontend);
	if (empty($allowed)) {
		return true;
	}
	$userGroups = epc_erp_profile_groups($isFrontend);
	foreach ($userGroups as $gid) {
		if (in_array((int)$gid, $allowed, true)) {
			return true;
		}
	}
	return false;
}

function epc_erp_user_in_administrator_group(PDO $db, $userId = 0)
{
	if ($userId <= 0) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		$userId = (int)DP_User::getUserId();
	}
	if ($userId <= 0) {
		return false;
	}
	$st = $db->prepare(
		'SELECT COUNT(*) FROM `users_groups_bind` ugb
		 INNER JOIN `groups` g ON g.`id` = ugb.`group_id`
		 WHERE ugb.`user_id` = ? AND (g.`value` LIKE ? OR g.`value` LIKE ?)'
	);
	$st->execute(array($userId, '%Администратор%', '%Administrator%'));
	return (int)$st->fetchColumn() > 0;
}

function epc_erp_backend_group_ids(PDO $db)
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$ids = array();
	$st = $db->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = $st->fetch(PDO::FETCH_ASSOC);
	if (!$root) {
		$cached = array(1, 3);
		return $cached;
	}
	$collect = function ($parentId) use ($db, &$collect, &$ids) {
		$ids[(int)$parentId] = true;
		$ch = $db->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($parentId));
		while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
			$ids[(int)$row['id']] = true;
			if ((int)$row['count'] > 0) {
				$collect((int)$row['id']);
			}
		}
	};
	$collect((int)$root['id']);
	$cached = array_keys($ids);
	return $cached;
}

function epc_erp_user_in_backend_tree(PDO $db, $userId = 0)
{
	if ($userId <= 0) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		$userId = (int)DP_User::getUserId();
	}
	if ($userId <= 0) {
		return false;
	}
	$groups = epc_erp_backend_group_ids($db);
	if (empty($groups)) {
		return false;
	}
	$in = implode(',', array_map('intval', $groups));
	$st = $db->prepare('SELECT COUNT(*) FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN (' . $in . ')');
	$st->execute(array($userId));
	return (int)$st->fetchColumn() > 0;
}

function epc_erp_user_has_cp_erp_access(PDO $db, $userId = 0)
{
	if ($userId <= 0) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		$userId = (int)DP_User::getUserId();
	}
	if ($userId <= 0) {
		return false;
	}
	$allowed = epc_erp_allowed_groups_for_content($db, epc_erp_cp_urls()['main'], 0);
	if (empty($allowed)) {
		return true;
	}
	$st = $db->prepare('SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?');
	$st->execute(array($userId));
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		if (in_array((int)$row['group_id'], $allowed, true)) {
			return true;
		}
	}
	return false;
}

function epc_erp_user_can_access(PDO $db)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$cpUrl = epc_erp_cp_urls()['main'];

	if (DP_User::isAdmin() && epc_erp_has_content_access($db, $cpUrl, 0)) {
		return true;
	}

	$userId = (int)DP_User::getUserId();
	if ($userId <= 0) {
		return false;
	}

	if (
		DP_User::isBackendGroup()
		|| epc_erp_user_in_backend_tree($db, $userId)
		|| epc_erp_user_in_administrator_group($db, $userId)
		|| epc_erp_user_has_cp_erp_access($db, $userId)
		|| epc_erp_user_in_any_dept($db, $userId)
	) {
		return true;
	}

	return epc_erp_user_in_team($db, $userId);
}

function epc_erp_user_in_any_dept(PDO $db, $userId = 0)
{
	require_once __DIR__ . '/epc_erp_staff.php';
	$codes = epc_erp_staff_user_department_codes($db, $userId);
	return !empty($codes);
}

function epc_erp_user_allowed_tabs(PDO $db, $userId = 0)
{
	require_once __DIR__ . '/epc_erp_staff.php';
	return epc_erp_staff_allowed_tabs($db, $userId);
}

function epc_erp_user_can_access_tab(PDO $db, $tab, $userId = 0)
{
	$allowed = epc_erp_user_allowed_tabs($db, $userId);
	return in_array($tab, $allowed, true);
}

/**
 * ERP AJAX endpoint — use /content/ proxy on platform host (nginx-safe for client-erp shells).
 */
function epc_erp_resolve_ajax_endpoint(string $cpPathPrefix = '/cp/'): string
{
	$host = strtolower(trim((string) ($_SERVER['HTTP_HOST'] ?? $_SERVER['SERVER_NAME'] ?? '')));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	$platformHosts = array('www.ecomae.com', 'ecomae.com');
	if (function_exists('epc_portal_platform_hostnames')) {
		$platformHosts = array_unique(array_merge($platformHosts, epc_portal_platform_hostnames()));
	}
	if ($host !== '' && in_array($host, $platformHosts, true)) {
		return '/content/general_pages/ajax_epc_erp.php';
	}
	if (function_exists('epc_erp_shell_use_asset_proxies') && epc_erp_shell_use_asset_proxies()) {
		return '/content/general_pages/ajax_epc_erp.php';
	}
	$prefix = '/' . trim($cpPathPrefix, '/') . '/';
	if ($prefix === '//') {
		$prefix = '/cp/';
	}
	return $prefix . 'content/shop/finance/erp/ajax_erp_endpoint.php';
}

function epc_erp_configure_portal_urls($portal = 'cp')
{
	global $DP_Config;
	$epc_erp_portal = ($portal === 'frontend') ? 'frontend' : 'cp';
	$epc_erp_cp_links = ($epc_erp_portal === 'cp');
	$lang = epc_erp_lang_href();
	$prefix = ($lang !== '' ? $lang : '');

	if ($epc_erp_portal === 'frontend') {
		// Trailing slash is required on the marketing host: nginx 301-redirects
		// `/erp?query` -> `/erp/` and drops the query string, which would strip
		// the area/tab module params and bounce every navigation to the
		// dashboard. `/erp/?area=...` is the canonical form and is served
		// directly with the query intact.
		$erpUrl = $prefix . '/erp/';
		$erpAjaxUrl = $prefix . '/erp/ajax';
		$guideUrl = $prefix . '/erp/guide';
		$ordersUrl = '';
		$financeOpsUrl = '';
		$storagesUrl = '';
	} else {
		$backend = '/' . $DP_Config->backend_dir;
		$erpUrl = $backend . '/shop/finance/erp';
		$platformRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
		if (is_file($platformRouter)) {
			require_once $platformRouter;
			if (function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request()) {
				$erpUrl = epc_platform_erp_path_prefix() . 'shop/finance/erp';
			}
		}
		$clientRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
		if (is_file($clientRouter)) {
			require_once $clientRouter;
			if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()
				&& function_exists('epc_client_erp_site_key')) {
				$clientKey = epc_client_erp_site_key();
				if ($clientKey !== '') {
					$erpUrl = epc_client_erp_path_prefix() . $clientKey . '/shop/finance/erp';
				}
			}
		}
		$demoRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
		if (is_file($demoRouter)) {
			require_once $demoRouter;
			if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
				&& function_exists('epc_portal_demo_erp_module_url')) {
				$demoErp = epc_portal_demo_erp_module_url();
				if ($demoErp !== '') {
					$erpUrl = $demoErp;
				}
			}
		}
		$cpPathPrefix = $backend . '/';
		if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
			&& function_exists('epc_portal_demo_cp_tenant_base')) {
			$demoBase = epc_portal_demo_cp_tenant_base();
			if ($demoBase !== '') {
				$cpPathPrefix = $demoBase;
			}
		}
		$erpAjaxUrl = epc_erp_resolve_ajax_endpoint($cpPathPrefix);
		$guideUrl = $cpPathPrefix . 'shop/finance/erp/guide';
		$ordersUrl = $cpPathPrefix . 'shop/orders/orders';
		$financeOpsUrl = $cpPathPrefix . 'shop/finance/account_operations';
		$storagesUrl = $cpPathPrefix . 'shop/logistics/storages';
	}

	$erpAjaxEndpoint = $erpAjaxUrl;

	return compact(
		'epc_erp_portal',
		'epc_erp_cp_links',
		'erpUrl',
		'erpAjaxUrl',
		'erpAjaxEndpoint',
		'guideUrl',
		'ordersUrl',
		'financeOpsUrl',
		'storagesUrl'
	);
}

function epc_erp_resolve_user_session()
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$session = DP_User::getAdminSession();
	if (!empty($session) && is_array($session)) {
		return $session;
	}
	$session = DP_User::getUserSession();
	return (is_array($session)) ? $session : array();
}

/**
 * Standalone /erp bypasses dp_core and the frontend auth plugin — ensure guest session + CSRF token.
 */
function epc_erp_portal_is_bot_request()
{
	if (!isset($_SERVER['HTTP_USER_AGENT']) || $_SERVER['HTTP_USER_AGENT'] === '') {
		return false;
	}
	$bots = array(
		'googlebot', 'bingbot', 'yandex', 'slurp', 'duckduckbot', 'baiduspider',
		'facebookexternalhit', 'twitterbot', 'rogerbot', 'linkedinbot', 'embedly',
		'quora link preview', 'showyoubot', 'outbrain', 'pinterest', 'slackbot',
		'vkShare', 'W3C_Validator', 'bot', 'spider', 'crawl',
	);
	$ua = strtolower((string) $_SERVER['HTTP_USER_AGENT']);
	foreach ($bots as $bot) {
		if (strpos($ua, strtolower($bot)) !== false) {
			return true;
		}
	}
	return false;
}

function epc_erp_portal_ensure_guest_session(PDO $db_link)
{
	global $DP_Config;
	$GLOBALS['db_link'] = $db_link;
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if (!isset($_SERVER['HTTP_USER_AGENT'])) {
		$_SERVER['HTTP_USER_AGENT'] = '';
	}
	if (epc_erp_portal_is_bot_request()) {
		return;
	}

	$userId = (int) DP_User::getUserId();
	if ($userId > 0) {
		if (!empty($_COOKIE['session']) && isset($_COOKIE['u_id'])) {
			$db_link->prepare('UPDATE `sessions` SET `last_activiti_time` = ? WHERE `session` = ? AND `user_id` = ?;')
				->execute(array(time(), $_COOKIE['session'], $_COOKIE['u_id']));
		}
		return;
	}

	$needsCreate = false;
	if (empty($_COOKIE['session']) || !isset($_COOKIE['u_id'])) {
		$needsCreate = true;
	} else {
		$check = $db_link->prepare('SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `user_id` = ?;');
		$check->execute(array($_COOKIE['session'], $_COOKIE['u_id']));
		if ((int) $check->fetchColumn() !== 1) {
			$needsCreate = true;
		} else {
			$row = DP_User::getUserSession();
			if (empty($row) || !is_array($row) || empty($row['csrf_guard_key'])) {
				$needsCreate = true;
			} else {
				$db_link->prepare('UPDATE `sessions` SET `last_activiti_time` = ? WHERE `session` = ? AND `user_id` = ?;')
					->execute(array(time(), $_COOKIE['session'], $_COOKIE['u_id']));
			}
		}
	}

	if (!$needsCreate) {
		return;
	}

	$cookietime = time() + 9999999;
	$sessionSuccession = md5(time() . rand(1000, 1000000000) . $DP_Config->secret_succession . $_SERVER['REMOTE_ADDR']);
	$csrfGuardKey = sha1($DP_Config->secret_succession . $sessionSuccession . $_SERVER['REMOTE_ADDR'] . $_SERVER['HTTP_USER_AGENT']);

	$insert = $db_link->prepare(
		'INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `last_activiti_time`, `csrf_guard_key`) VALUES (?, ?, ?, ?, ?, ?);'
	);
	if (!$insert->execute(array($sessionSuccession, 0, time(), '', time(), $csrfGuardKey))) {
		return;
	}

	setcookie('session', $sessionSuccession, $cookietime, '/', '', false, true);
	setcookie('u_id', '0', $cookietime, '/', '', false, true);
	$_COOKIE['session'] = $sessionSuccession;
	$_COOKIE['u_id'] = '0';
}

/**
 * Password / logout POST on /erp (auth plugin is not loaded on standalone portal).
 */
function epc_erp_portal_handle_auth_post(PDO $db_link)
{
	global $DP_Config;
	if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
		return;
	}
	if (empty($_POST['authentication']) && empty($_POST['logout'])) {
		return;
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	epc_erp_portal_ensure_guest_session($db_link);

	$redirectBase = function_exists('epc_erp_portal_canonical_base')
		? epc_erp_portal_canonical_base(function_exists('epc_erp_lang_href') ? epc_erp_lang_href() : '')
		: '/erp';

	if (!empty($_POST['logout']) && $_POST['logout'] === 'true') {
		$csrf_check_admin = false;
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
		if ((int) DP_User::getUserId() > 0 && !empty($_COOKIE['session'])) {
			$db_link->prepare('DELETE FROM `sessions` WHERE `session` = ? AND `user_id` = ?;')
				->execute(array($_COOKIE['session'], DP_User::getUserId()));
			setcookie('session', '', time() - 10000, '/', '', false, true);
			setcookie('u_id', '', time() - 10000, '/', '', false, true);
		}
		header('Location: ' . $redirectBase);
		exit;
	}

	if (empty($_POST['authentication']) || empty($_POST['auth_contact']) || empty($_POST['auth_contact_type'])) {
		return;
	}

	$csrf_check_admin = false;
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

	$authContact = (string) $_POST['auth_contact'];
	$authContactType = (string) $_POST['auth_contact_type'];
	if ($authContactType !== 'email' && $authContactType !== 'phone') {
		return;
	}

	$authRecord = false;
	if (isset($_POST['code'])) {
		$userSession = DP_User::getUserSession();
		if (!empty($userSession) && is_array($userSession)) {
			$sessionData = json_decode((string) $userSession['data'], true);
			$faCode = $userSession['2fa_code'];
			$faAttempts = (int) $userSession['2fa_attempts'];
			if ($faAttempts >= 1 && $faCode === $_POST['code'] && is_array($sessionData) && $sessionData['expireFaCode'] >= time()) {
				$authQuery = $db_link->prepare(
					'SELECT * FROM `users` WHERE `' . $authContactType . '` = ? AND `' . $authContactType . '_confirmed` = ? AND `unlocked` = ?;'
				);
				$authQuery->execute(array($authContact, 1, 1));
				$authRecord = $authQuery->fetch();
			}
		}
	} else {
		$password = isset($_POST['password']) ? (string) $_POST['password'] : '';
		$authQuery = $db_link->prepare(
			'SELECT * FROM `users` WHERE `' . $authContactType . '` = ? AND `' . $authContactType . '_confirmed` = ? AND `unlocked` = ? AND `password` = ?;'
		);
		$authQuery->execute(array($authContact, 1, 1, md5($password . $DP_Config->secret_succession)));
		$authRecord = $authQuery->fetch();
	}

	if ($authRecord === false) {
		header('Location: ' . $redirectBase . '?auth_failed=1#sign-in');
		exit;
	}

	$userId = (int) $authRecord['user_id'];
	$time = time();
	$lastActivitiTimeToDel = time() - 2592000;
	$db_link->prepare(
		'DELETE FROM `users_options` WHERE `session_id` IN (SELECT `id` FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?);'
	)->execute(array($userId, $lastActivitiTimeToDel));
	$db_link->prepare('DELETE FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?;')
		->execute(array($userId, $lastActivitiTimeToDel));

	$sessionSuccession = md5($authContact . $userId . $time . $DP_Config->secret_succession);
	$csrfGuardKey = sha1($DP_Config->secret_succession . $sessionSuccession . $_SERVER['REMOTE_ADDR'] . $_SERVER['HTTP_USER_AGENT']);
	$db_link->prepare('INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `csrf_guard_key`) VALUES (?, ?, ?, ?, ?);')
		->execute(array($sessionSuccession, $userId, $time, '', $csrfGuardKey));

	$cookietime = !empty($_POST['rememberme']) ? (time() + 9999999) : 0;
	setcookie('session', $sessionSuccession, $cookietime, '/', '', false, true);
	setcookie('u_id', (string) $userId, $cookietime, '/', '', false, true);

	header('Location: ' . $redirectBase);
	exit;
}
