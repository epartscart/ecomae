<?php
// PHP 8.3 goldens for plan Q1-gulf (Super CP platform). Leftover portal.php stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function gulf_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace('/require_once\s+__DIR__\s*\.\s*[\'"]\/epc_portal\.php[\'"]\s*;/', '// leftover portal injected', $code);
	$code = preg_replace('/require_once\s+__DIR__\s*\.\s*[\'"]\/epc_portal_tenant_control\.php[\'"]\s*;/', '// leftover tenant control injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/users\/dp_user\.php[\'"]\s*;/', '// leftover user kernel injected', $code);
	file_put_contents($dest, $code);
}

function gulf_stubs(): void
{
	if (!function_exists('epc_portal_db_ensure')) {
		function epc_portal_db_ensure($pdo): void
		{
		}
	}
	if (!function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['GULF_SUPER']);
		}
	}
	if (!function_exists('epc_portal_tenant_control_list_all')) {
		function epc_portal_tenant_control_list_all($pdo): array
		{
			return (array) ($GLOBALS['GULF_TENANTS'] ?? array());
		}
	}
	if (!function_exists('epc_portal_tenant_control_tenant_pdo')) {
		function epc_portal_tenant_control_tenant_pdo($row)
		{
			$key = (string) ($row['site_key'] ?? '');
			return $GLOBALS['GULF_TENANT_PDO'][$key] ?? null;
		}
	}
	if (!function_exists('epc_portal_tenant_control_urls')) {
		function epc_portal_tenant_control_urls($row): array
		{
			$key = (string) ($row['site_key'] ?? '');
			return (array) ($GLOBALS['GULF_URLS'][$key] ?? array('cp' => '', 'client_erp' => ''));
		}
	}
	if (!class_exists('DP_User', false)) {
		class DP_User
		{
			public static function isAdmin(): bool
			{
				return !empty($GLOBALS['GULF_ADMIN']);
			}
		}
	}
}

function gulf_include(): void
{
	gulf_stubs();
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$cfg = new stdClass();
	$cfg->backend_dir = (string) ($GLOBALS['GULF_BACKEND'] ?? 'cp');
	$GLOBALS['DP_Config'] = $cfg;
	include_once $GLOBALS['GULF_PAGE'];
}

function gulf_admin(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function gulf_schema(PDO $admin, string $suffix): array
{
	$schema = 'ecomae_cpw_gulf_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$db = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $schema . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE `users` (
		`user_id` INT NOT NULL PRIMARY KEY,
		`email` VARCHAR(120) NOT NULL DEFAULT \'\',
		`phone` VARCHAR(64) NOT NULL DEFAULT \'\',
		`time_reg` INT NOT NULL DEFAULT 0
	)');
	$db->exec('CREATE TABLE `users_profiles` (
		`user_id` INT NOT NULL,
		`data_key` VARCHAR(64) NOT NULL,
		`data_value` VARCHAR(255) NOT NULL DEFAULT \'\',
		PRIMARY KEY (`user_id`, `data_key`)
	)');
	return array($schema, $db);
}

function gulf_seed_user(PDO $db, int $id, string $email, string $phone, int $reg, array $profile): void
{
	$db->prepare('INSERT INTO `users` (`user_id`,`email`,`phone`,`time_reg`) VALUES (?,?,?,?)')->execute(array($id, $email, $phone, $reg));
	$ins = $db->prepare('INSERT INTO `users_profiles` (`user_id`,`data_key`,`data_value`) VALUES (?,?,?)');
	foreach ($profile as $k => $v) {
		$ins->execute(array($id, $k, $v));
	}
}

function gulf_proj_price(array $r): array
{
	return array(
		'name' => (string) $r['name'],
		'scope' => (string) $r['scope'],
		'site_key' => (string) $r['site_key'],
		'client_type' => (string) $r['client_type'],
		'markup_percent' => (string) $r['markup_percent'],
		'currency' => (string) $r['currency'],
		'priority' => (int) $r['priority'],
		'active' => (int) $r['active'],
	);
}

function gulf_proj_block(array $r): array
{
	return array(
		'block_key' => (string) $r['block_key'],
		'title' => (string) $r['title'],
		'scope' => (string) $r['scope'],
		'site_key' => (string) $r['site_key'],
		'placement' => (string) $r['placement'],
		'locale' => (string) $r['locale'],
		'active' => (int) $r['active'],
		'sort_order' => (int) $r['sort_order'],
	);
}

function gulf_proj_task(array $r): array
{
	return array(
		'title' => (string) $r['title'],
		'assigned_email' => (string) $r['assigned_email'],
		'site_key' => (string) $r['site_key'],
		'category' => (string) $r['category'],
		'status' => (string) $r['status'],
		'priority' => (string) $r['priority'],
	);
}

function gulf_run_names()
{
	$GLOBALS['GULF_BACKEND'] = 'CP/';
	gulf_include();
	return array(
		epc_scp_h("O'Reilly & Co"),
		epc_scp_backend(),
		array_keys(epc_scp_price_client_types()),
		array_keys(epc_scp_info_placements()),
		array_keys(epc_scp_task_categories()),
		array_keys(epc_scp_task_statuses()),
		array_keys(epc_scp_task_priorities()),
		epc_scp_default_comm_settings(),
		epc_scp_operator_guide_url(),
		epc_scp_customer_name_from_row(array('fname' => 'Ada', 'sname' => 'Lovelace', 'company' => 'X', 'email' => 'ada@x.test')),
		epc_scp_customer_name_from_row(array('fname' => '', 'sname' => '', 'company' => "O'Reilly", 'email' => 'x@y.test')),
		epc_scp_customer_name_from_row(array('fname' => '', 'sname' => '', 'company' => '', 'email' => 'solo@y.test')),
	);
}

function gulf_run_price()
{
	gulf_include();
	$admin = gulf_admin();
	[$schema, $db] = gulf_schema($admin, 'p');
	try {
		$emptyName = epc_scp_price_config_save($db, array('name' => '  '));
		$save = epc_scp_price_config_save($db, array(
			'name' => '  Gulf List  ',
			'scope' => 'nope',
			'site_key' => 'Acme-Parts!',
			'client_type' => 'nope',
			'client_ref' => ' ref1 ',
			'markup_percent' => '12.5',
			'markup_fixed' => '1.25',
			'currency' => 'aed',
			'priority' => 0,
			'active' => 0,
			'notes' => ' n ',
		));
		$upd = epc_scp_price_config_save($db, array(
			'name' => 'Gulf List',
			'scope' => 'tenant',
			'site_key' => 'acme_parts',
			'client_type' => 'api',
			'markup_percent' => 8,
			'currency' => 'USD',
			'priority' => 5,
			'active' => 1,
		), (int) $save['id']);
		$second = epc_scp_price_config_save($db, array(
			'name' => 'Zeta',
			'scope' => 'platform',
			'client_type' => 'catalog',
			'markup_percent' => 1,
			'active' => 1,
			'priority' => 50,
		));
		$list = array_map('gulf_proj_price', epc_scp_price_configs_list($db));
		$del = epc_scp_price_config_delete($db, (int) $second['id']) ? 1 : 0;
		$after = count(epc_scp_price_configs_list($db));
		$defaults = epc_scp_comm_settings_get($db);
		epc_scp_comm_settings_save($db, array(
			'notify_from_name' => 'Gulf Ops',
			'notify_tenant_onboard' => 0,
			'notify_daily_digest' => '1',
			'digest_hour_utc' => '9',
			'nope' => 'x',
		));
		$saved = epc_scp_comm_settings_get($db);
		return array(
			$emptyName,
			$save['ok'] ? 1 : 0,
			(int) $save['id'],
			$upd,
			$list,
			$del,
			$after,
			$defaults['notify_from_name'],
			$defaults['notify_daily_digest'],
			$saved['notify_from_name'],
			$saved['notify_tenant_onboard'],
			$saved['notify_daily_digest'],
			$saved['digest_hour_utc'],
			isset($saved['nope']) ? 1 : 0,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function gulf_run_info()
{
	gulf_include();
	$admin = gulf_admin();
	[$schema, $db] = gulf_schema($admin, 'i');
	try {
		$bad = epc_scp_info_block_save($db, array('block_key' => '', 'title' => 'X'));
		$save = epc_scp_info_block_save($db, array(
			'block_key' => 'Hero-Banner!',
			'title' => '  Hello  ',
			'scope' => 'tenant',
			'site_key' => 'Acme-Parts!',
			'placement' => 'nope',
			'content_html' => '<b>Hi</b>',
			'locale' => '',
			'active' => 1,
			'sort_order' => 3,
		));
		$dup = epc_scp_info_block_save($db, array(
			'block_key' => 'hero-banner',
			'title' => 'Again',
			'scope' => 'tenant',
			'site_key' => 'acmeparts',
			'locale' => 'en',
		));
		$footer = epc_scp_info_block_save($db, array(
			'block_key' => 'foot',
			'title' => 'Footer',
			'placement' => 'footer',
			'sort_order' => 1,
		));
		$all = array_map('gulf_proj_block', epc_scp_info_blocks_list($db));
		$home = array_map('gulf_proj_block', epc_scp_info_blocks_list($db, 'homepage'));
		$del = epc_scp_info_block_delete($db, (int) $footer['id']) ? 1 : 0;
		$tBad = epc_scp_task_save($db, array('title' => ' '));
		$t1 = epc_scp_task_save($db, array(
			'title' => '  Fix DNS  ',
			'description' => 'now',
			'assigned_to' => -3,
			'assigned_email' => 'Ops@Ecomae.COM',
			'site_key' => 'Acme-Parts!',
			'category' => 'nope',
			'status' => 'nope',
			'priority' => 'urgent',
			'due_at' => -5,
		), 0, 9);
		$t2 = epc_scp_task_save($db, array(
			'title' => 'Bill',
			'category' => 'billing',
			'status' => 'done',
			'priority' => 'low',
		));
		$open = array_map('gulf_proj_task', epc_scp_tasks_list($db, 'open'));
		$allTasks = array_map('gulf_proj_task', epc_scp_tasks_list($db));
		$upd = epc_scp_task_save($db, array(
			'title' => 'Fix DNS',
			'status' => 'in_progress',
			'priority' => 'high',
			'assigned_email' => 'ops@ecomae.com',
			'site_key' => 'acme_parts',
			'category' => 'support',
		), (int) $t1['id']);
		$tdel = epc_scp_task_delete($db, (int) $t2['id']) ? 1 : 0;
		return array(
			$bad,
			$save,
			$dup,
			$all,
			$home,
			$del,
			$tBad,
			$t1['ok'] ? 1 : 0,
			$open,
			$allTasks[0]['status'] ?? '',
			$upd,
			$tdel,
			count(epc_scp_tasks_list($db)),
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function gulf_run_board()
{
	gulf_include();
	$admin = gulf_admin();
	[$platSchema, $plat] = gulf_schema($admin, 'b');
	[$acmeSchema, $acme] = gulf_schema($admin, 'a');
	[$betaSchema, $beta] = gulf_schema($admin, 'z');
	try {
		gulf_seed_user($plat, 11, 'zana@ecomae.test', '1', 100, array('name' => 'Zana', 'surname' => 'Ops'));
		gulf_seed_user($plat, 10, 'ada@ecomae.test', '2', 90, array('name' => 'Ada'));
		gulf_seed_user($acme, 21, 'buyer@acme.test', '971', 50, array('name' => 'Buyer', 'company' => 'Acme'));
		gulf_seed_user($beta, 31, 'other@beta.test', '3', 40, array('name' => 'Other'));
		$GLOBALS['GULF_TENANTS'] = array(
			array('site_key' => 'acme_parts', 'trade_name' => 'Acme Parts', 'hostname' => 'www.acme.test', 'in_registry' => 1, 'access_blocked' => 0,
				'urls' => array('cp' => 'https://www.acme.test/cp', 'client_erp' => 'https://www.acme.test/cp')),
			array('site_key' => 'beta', 'trade_name' => '', 'hostname' => 'www.beta.test', 'in_registry' => 1, 'access_blocked' => 1),
			array('site_key' => '', 'trade_name' => 'Ghost', 'in_registry' => 1),
			array('site_key' => 'skip', 'trade_name' => 'Skip', 'in_registry' => 0),
		);
		$GLOBALS['GULF_TENANT_PDO'] = array(
			'acme_parts' => $acme,
			'beta' => $beta,
		);
		$opts = epc_scp_tenant_options($plat);
		$users = array();
		foreach (epc_scp_platform_users($plat) as $u) {
			$users[] = array((int) $u['user_id'], (string) $u['email'], (string) ($u['fname'] ?? ''));
		}
		$chunk = epc_scp_customers_from_pdo($acme, array(
			'site_key' => 'acme_parts',
			'label' => 'Acme Parts (acme_parts)',
			'hostname' => 'www.acme.test',
			'urls' => array('cp' => 'https://www.acme.test/cp', 'client_erp' => ''),
		), '', 10);
		$boardAll = epc_scp_customer_board_search($plat, '', '', 1, 10);
		$boardAcme = epc_scp_customer_board_search($plat, '', 'acme_parts', 1, 10);
		$boardPlat = epc_scp_customer_board_search($plat, 'ada', 'platform', 1, 10);
		$projBoard = static function (array $pack): array {
			$rows = array();
			foreach ($pack['rows'] as $r) {
				$rows[] = array(
					'source' => $r['source'],
					'email' => $r['email'],
					'name' => $r['name'],
					'crm' => $r['links']['crm'] ?? '',
					'erp' => $r['links']['erp'] ?? '',
				);
			}
			return array($rows, $pack['total'], $pack['stats']);
		};
		$GLOBALS['GULF_SUPER'] = 0;
		ob_start();
		$g1 = epc_scp_guard_super_admin() ? 1 : 0;
		$deny = ob_get_clean();
		$GLOBALS['GULF_SUPER'] = 1;
		$GLOBALS['GULF_ADMIN'] = 0;
		ob_start();
		$g2 = epc_scp_guard_super_admin() ? 1 : 0;
		$login = ob_get_clean();
		$GLOBALS['GULF_ADMIN'] = 1;
		ob_start();
		$g3 = epc_scp_guard_super_admin() ? 1 : 0;
		$ok = ob_get_clean();
		ob_start();
		epc_scp_render_hero('Badge', "O'Reilly", 'Sub & more', array(
			array('url' => '/cp/x?a=1&b=2', 'label' => "View", 'icon' => 'fa-eye', 'primary' => 1),
		));
		$hero = ob_get_clean();
		ob_start();
		epc_scp_render_workspace_intro('nope');
		$none = ob_get_clean();
		ob_start();
		epc_scp_render_workspace_intro('customer_board');
		$intro = ob_get_clean();
		ob_start();
		epc_scp_render_empty_state('Empty', 'Nothing', array());
		$empty = ob_get_clean();
		return array(
			$opts,
			$users,
			$chunk,
			$projBoard($boardAll),
			$projBoard($boardAcme),
			$projBoard($boardPlat),
			$g1,
			$deny,
			$g2,
			$login,
			$g3,
			$ok,
			$hero,
			$none,
			$intro,
			$empty,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $platSchema . '`');
		$admin->exec('DROP DATABASE IF EXISTS `' . $acmeSchema . '`');
		$admin->exec('DROP DATABASE IF EXISTS `' . $betaSchema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_gulf_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	gulf_patch($root . '/content/general_pages/epc_super_cp_platform.php', $tmp . '/page.php');
	$GLOBALS['GULF_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'gulf_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1gulf_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1gulf_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
