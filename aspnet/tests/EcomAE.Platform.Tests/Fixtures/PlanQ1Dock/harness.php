<?php
// PHP 8.3 goldens for plan Q1-dock (tenant onboard kernel). Leftover parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function dock_freeze($value)
{
	if (is_string($value)) {
		return preg_replace('/ecomae_cpw_[a-f0-9]+/', 'TENANT_DB', $value);
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = dock_freeze($v);
		}
		return $out;
	}
	return $value;
}

function dock_tpl_snap()
{
	$out = array();
	foreach (epc_portal_tenant_templates() as $k => $tpl) {
		$out[$k] = array(
			'industry' => $tpl['industry'] ?? '',
			'hostname' => $tpl['hostname'] ?? '',
			'access_mode' => $tpl['access_mode'] ?? '',
			'hosted_on' => $tpl['hosted_on'] ?? '',
			'erp_only_shared' => $tpl['erp_only_shared'] ?? 0,
		);
	}
	return $out;
}

function dock_row_snap(PDO $pdo, $key)
{
	$st = $pdo->prepare('SELECT `site_key`,`hostname`,`industry_code`,`status`,`trade_name`,`db_name`,`db_user`,`hosted_on`,`erp_only_shared`,`dedicated_db`,`scale_policy`,`blockchain_mode` FROM `epc_portal_tenants` WHERE `site_key`=?');
	$st->execute(array($key));
	return $st->fetch(PDO::FETCH_ASSOC) ?: array();
}

function dock_run_pure()
{
	putenv('ECOMAE_PLATFORM_IP=');
	$ipDef = epc_portal_platform_ip();
	putenv('ECOMAE_PLATFORM_IP=10.1.2.3');
	$ipEnv = epc_portal_platform_ip();
	$hosts = array(
		epc_portal_is_platform_hostname('www.ecomae.com'),
		epc_portal_is_platform_hostname('parts.ecomae.com'),
		epc_portal_is_platform_hostname('www.client.com'),
		epc_portal_is_epartscart_hostname('www.epartscart.com:443'),
		epc_portal_is_epartscart_hostname('shop.client.com'),
	);
	$dns = epc_portal_tenant_dns_instructions('www.shop.ae');
	$shared = array(
		epc_portal_tenant_is_shared_erp_row(array('erp_only_shared' => 1)),
		epc_portal_tenant_is_shared_erp_row(array('hosted_on' => 'platform')),
		epc_portal_tenant_is_shared_erp_row(array('erp_only_shared' => '0', 'hosted_on' => 'client')),
	);
	$profile = epc_portal_tenant_row_to_profile(array(
		'id' => 7,
		'status' => 'live',
		'site_key' => 'alpha',
		'hostname' => 'www.alpha.ae',
		'industry_code' => 'food',
		'db_name' => 'alpha',
		'db_user' => 'alpha',
		'db_password' => 'x',
		'dedicated_db' => 1,
		'scale_policy' => '',
		'erp_only_shared' => 0,
		'trade_name' => 'Alpha',
		'hub_name' => 'Hub',
		'from_email' => 'a@x.com',
	));
	unset($profile['password']);
	return dock_freeze(array(
		$ipDef, $ipEnv, epc_portal_platform_hostnames(), $hosts,
		epc_portal_tenant_statuses(), dock_tpl_snap(), $dns, $shared, $profile,
		epc_portal_client_may_share_docpart('www.epartscart.com'),
		epc_portal_client_may_share_docpart('www.taxofinca.com'),
	));
}

function dock_run_save_types(PDO $pdo)
{
	$noKey = epc_portal_save_tenant($pdo, array('hostname' => 'www.a.com'));
	$noHost = epc_portal_save_tenant($pdo, array('site_key' => 'siteone'));
	$plat = epc_portal_save_tenant($pdo, array('site_key' => 'plat', 'hostname' => 'www.ecomae.com'));
	$site = epc_portal_save_tenant($pdo, array(
		'site_key' => 'siteone',
		'hostname' => 'www.siteone.ae',
		'industry_code' => 'food_beverage',
		'status' => 'dns_pending',
		'trade_name' => 'Site One',
		'scale_policy' => 'shared_docpart',
		'db_name' => 'docpart',
		'db_user' => 'docpart',
		'db_password' => 'shared',
	));
	$erp = epc_portal_save_tenant($pdo, array(
		'site_key' => 'erpone',
		'erp_only_shared' => 1,
		'industry_code' => 'erp_standalone',
		'status' => 'draft',
		'trade_name' => 'ERP One',
		'from_email' => 'erp@one.ae',
	));
	$mixed = epc_portal_save_tenant($pdo, array(
		'site_key' => 'mixedone',
		'hostname' => 'www.mixed.ae',
		'industry_code' => 'retail',
		'status' => 'live',
		'trade_name' => 'Mixed One',
		'dedicated_db' => 1,
		'db_password' => 'mixpass',
		'blockchain_mode' => 'nope',
	));
	$reserved = epc_portal_save_tenant($pdo, array(
		'site_key' => 'badone',
		'hostname' => 'www.bad.ae',
		'dedicated_db' => 1,
		'db_name' => 'ecomae',
		'db_user' => 'ecomae',
		'db_password' => 'x',
	));
	$dup = epc_portal_save_tenant($pdo, array(
		'site_key' => 'erptwo',
		'erp_only_shared' => 1,
		'db_name' => 'erpone',
		'db_user' => 'erpone',
		'db_password' => 'x',
	));
	$again = epc_portal_save_tenant($pdo, array(
		'site_key' => 'siteone',
		'hostname' => 'www.siteone.ae',
		'status' => 'live',
		'trade_name' => 'Site One Live',
		'db_password' => '',
	));
	return dock_freeze(array(
		$noKey, $noHost, $plat, $site, $erp, $mixed, $reserved, $dup, $again,
		dock_row_snap($pdo, 'siteone'),
		dock_row_snap($pdo, 'erpone'),
		dock_row_snap($pdo, 'mixedone'),
		$GLOBALS['DOCK_SETTINGS'],
	));
}

function dock_run_isolation(PDO $pdo)
{
	epc_portal_save_tenant($pdo, array(
		'site_key' => 'alpha',
		'hostname' => 'www.alpha.ae',
		'status' => 'live',
		'trade_name' => 'Alpha',
		'db_name' => 'alpha',
		'db_user' => 'alpha',
		'db_password' => 'a',
		'dedicated_db' => 1,
	));
	epc_portal_save_tenant($pdo, array(
		'site_key' => 'beta',
		'hostname' => 'www.beta.ae',
		'status' => 'dns_pending',
		'trade_name' => 'Beta',
		'db_name' => 'beta',
		'db_user' => 'beta',
		'db_password' => 'b',
		'dedicated_db' => 1,
	));
	$pdo->prepare('UPDATE `epc_portal_tenants` SET `is_active`=0 WHERE `site_key`=?')->execute(array('beta'));
	$alpha = epc_portal_load_tenant_by_host('www.alpha.ae', $pdo);
	unset($alpha['password']);
	$betaLive = epc_portal_load_tenant_by_host('www.beta.ae', $pdo);
	$plat = epc_portal_load_tenant_by_host('www.ecomae.com', $pdo);
	$epc = epc_portal_load_tenant_by_host('www.epartscart.com', $pdo);
	$regA = epc_portal_tenant_registry_row($pdo, 'Alpha!');
	$regMiss = epc_portal_tenant_registry_row($pdo, '!!!');
	$list = array();
	foreach (epc_portal_list_tenants($pdo) as $row) {
		$list[] = array('site_key' => $row['site_key'], 'hostname' => $row['hostname']);
	}
	return dock_freeze(array($alpha, $betaLive, $plat, $epc, $regA['site_key'] ?? '', $regMiss, $list));
}

function dock_run_creds(PDO $pdo)
{
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['DOCK_STUB'];
	file_put_contents($GLOBALS['DOCK_STUB'] . '/config.tenant-db.php', "<?php\n\$epc_tenant_db=array('db'=>'docpart','user'=>'docpart','password'=>'dp-secret');\n");
	file_put_contents($GLOBALS['DOCK_STUB'] . '/config.tenant-host-db.php', "<?php\n\$epc_tenant_host_db=array('www.taxofinca.com'=>array('db'=>'taxo','user'=>'taxo','password'=>'tax-secret'));\n");
	$resolved = epc_portal_resolve_tenant_db_credentials();
	$runtime = epc_portal_runtime_host_db('www.taxofinca.com');
	$setupDed = epc_portal_tenant_setup_credentials(array(
		'hostname' => 'www.alpha.ae',
		'db_name' => 'alpha',
		'db_user' => 'alpha',
		'db_password' => 'p',
		'dedicated_db' => 1,
	));
	$setupRun = epc_portal_tenant_setup_credentials(array(
		'hostname' => 'www.taxofinca.com',
		'db_name' => 'x',
		'db_user' => 'x',
		'db_password' => 'x',
	));
	$_SERVER['HTTP_HOST'] = 'www.taxofinca.com';
	$cfg = (object) array('db' => 'ecomae', 'user' => 'ecomae', 'password' => 'plat', 'host' => '127.0.0.1', 'epc_tenant_db_isolation_error' => '');
	epc_portal_resolve_tenant_db($cfg);
	return dock_freeze(array($resolved, $runtime, $setupDed, $setupRun, (array) $cfg, epc_portal_tenant_db_is_degraded_shared()));
}

function dock_write_stubs($stub, $srcTenant)
{
	file_put_contents($stub . '/epc_portal.php', "<?php\nfunction epc_portal_host(){\n\t\$h=(string)(\$_SERVER['HTTP_HOST']??'');\n\tif(\$h!==''&&strpos(\$h,':')!==false){\$h=explode(':',\$h,2)[0];}\n\treturn strtolower(\$h);\n}\nfunction epc_portal_is_client_hostname(\$host=null){\n\tif(\$host===null){\$host=epc_portal_host();}\n\t\$host=strtolower(trim(\$host));\n\tif(\$host===''||in_array(\$host,array('www.ecomae.com','ecomae.com','cp.ecomae.com'),true)){return false;}\n\tif(preg_match('/^[a-z0-9][a-z0-9_-]*\\.ecomae\\.com$/',\$host)){return false;}\n\treturn true;\n}\n");
	file_put_contents($stub . '/epc_portal_db.php', "<?php\nfunction epc_portal_db_ensure(PDO \$pdo){\nstatic \$done=array();\n\$oid=spl_object_id(\$pdo);\nif(isset(\$done[\$oid]))return;\n\$done[\$oid]=true;\n\$pdo->exec(\"CREATE TABLE IF NOT EXISTS `epc_portal_tenants` (`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY, `site_key` VARCHAR(64) NOT NULL, `hostname` VARCHAR(120) NOT NULL, `industry_code` VARCHAR(32) NOT NULL DEFAULT 'auto_parts', `status` VARCHAR(24) NOT NULL DEFAULT 'draft', `trade_name` VARCHAR(120) NOT NULL DEFAULT '', `hub_name` VARCHAR(120) NOT NULL DEFAULT '', `from_email` VARCHAR(120) NOT NULL DEFAULT '', `db_name` VARCHAR(64) NOT NULL DEFAULT '', `db_user` VARCHAR(64) NOT NULL DEFAULT '', `db_password` VARCHAR(255) NOT NULL DEFAULT '', `notes` VARCHAR(500) NOT NULL DEFAULT '', `intro_json` TEXT NULL, `hosted_on` VARCHAR(24) NOT NULL DEFAULT 'client', `erp_only_shared` TINYINT(1) NOT NULL DEFAULT 0, `dedicated_db` TINYINT(1) NOT NULL DEFAULT 0, `scale_policy` VARCHAR(32) NOT NULL DEFAULT 'shared_docpart', `blockchain_mode` VARCHAR(24) NOT NULL DEFAULT 'anchor', `is_active` TINYINT(1) NOT NULL DEFAULT 1, `created_at` INT NOT NULL DEFAULT 0, `updated_at` INT NOT NULL DEFAULT 0, UNIQUE KEY `site_key` (`site_key`), KEY `hostname_lookup` (`hostname`)) ENGINE=InnoDB DEFAULT CHARSET=utf8\");\n\$pdo->exec(\"CREATE TABLE IF NOT EXISTS `epc_portal_site_settings` (`host` VARCHAR(120) NOT NULL PRIMARY KEY, `industry_code` VARCHAR(32) NOT NULL DEFAULT 'auto_parts', `hub_name` VARCHAR(120) NOT NULL DEFAULT '', `domain_path` VARCHAR(255) NOT NULL DEFAULT '', `updated_at` INT NOT NULL DEFAULT 0) ENGINE=InnoDB DEFAULT CHARSET=utf8\");\n}\nfunction epc_portal_default_site_settings(\$host){return array('host'=>\$host,'industry_code'=>'auto_parts','hub_name'=>'Electronic World Group');}\nfunction epc_portal_save_site_settings(PDO \$pdo,array \$data){\$GLOBALS['DOCK_SETTINGS'][]=\$data;return true;}\n");
	file_put_contents($stub . '/epc_portal_tenant_intro.php', "<?php\nfunction epc_portal_intro_decode(\$json){\nif(\$json===null||\$json===''){return array();}\n\$d=json_decode(\$json,true);return is_array(\$d)?\$d:array();}\nfunction epc_portal_apply_intro_to_site_settings(\$pdo,\$host,\$row,\$intro){\$GLOBALS['DOCK_APPLY'][]=array(\$host,\$row['site_key']??'',\$intro);}\n");
	file_put_contents($stub . '/epc_portal_tenant_control.php', "<?php\nfunction epc_portal_tenant_control_generate_password(){return 'abOp12!';}\nfunction epc_portal_tenant_control_row_is_active(\$row){if(!is_array(\$row))return true;if(array_key_exists('is_active',\$row)&&(int)\$row['is_active']===0)return false;return true;}\n");
	file_put_contents($stub . '/epc_portal_demo.php', "<?php\nfunction epc_portal_demo_provision_database_raw(\$db,\$user,\$pass){return array('ok'=>true,'db_name'=>\$db);}\n");
	file_put_contents($stub . '/epc_blockchain_bos.php', "<?php\nfunction epc_bc_bos_modes(){return array('off'=>1,'anchor'=>1,'network'=>1);}\nfunction epc_bc_bos_normalize_mode(\$m){\$m=strtolower(trim(\$m));return isset(epc_bc_bos_modes()[\$m])?\$m:'off';}\n");
	file_put_contents($stub . '/epc_portal_cp_menu.php', "<?php\nfunction epc_portal_sync_tenant_packs_to_client_db(\$pdo,\$host){return array('ok'=>true,'message'=>'packs synced');}\n");
	file_put_contents($stub . '/epc_tenant_pdo.php', "<?php\nfunction epc_tenant_pdo(\$h,\$d,\$u,\$p,\$o=array()){return array(null);}\n");
	copy($srcTenant, $stub . '/epc_portal_tenant.php');
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$cleanup = function () use ($admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$stub = sys_get_temp_dir() . '/dock_' . substr(md5(uniqid('', true)), 0, 8);
	mkdir($stub, 0777, true);
	dock_write_stubs($stub, $root . '/content/general_pages/epc_portal_tenant.php');
	$_SERVER['DOCUMENT_ROOT'] = $stub;
	$GLOBALS['DOCK_STUB'] = $stub;
	$GLOBALS['DOCK_SETTINGS'] = array();
	require $stub . '/epc_portal_tenant.php';
	$_GET = array();
	$GLOBALS['db_link'] = $pdo;
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1dock_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1dock_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
