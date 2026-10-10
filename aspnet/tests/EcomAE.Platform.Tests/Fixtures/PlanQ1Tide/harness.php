<?php
// PHP 8.3 goldens for plan Q1-tide (platform failover helpers).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function tide_freeze($value)
{
	if (is_string($value)) {
		if (preg_match('/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/', $value)) {
			return '2026-10-10T00:00:00+00:00';
		}
		$value = str_replace($GLOBALS['TIDE_DOCROOT'] ?? '', 'DOCROOT', $value);
		return $value;
	}
	if (is_int($value) && $value >= 150 && $value <= 250) {
		return 200;
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = tide_freeze($v);
		}
		return $out;
	}
	return $value;
}

function tide_modes_meta()
{
	$modes = epc_failover_valid_modes();
	$health = array();
	$env = array();
	$splash = array();
	foreach (array_merge($modes, array('unknown')) as $m) {
		$health[$m] = epc_failover_primary_health_for_mode($m);
		$env[$m] = epc_failover_env_label($m);
		$splash[$m] = epc_failover_should_show_splash($m);
	}
	return array($modes, $health, $env, $splash);
}

function tide_run_pure()
{
	$modes = tide_modes_meta();
	$def = epc_failover_default_config();
	$cfg = epc_failover_read_config();
	$ttl = epc_failover_status_cache_ttl();
	putenv('EPC_FAILOVER_STATUS_TTL=15');
	$ttl15 = epc_failover_status_cache_ttl();
	putenv('EPC_FAILOVER_STATUS_TTL=12');
	$ttl12 = epc_failover_status_cache_ttl();
	putenv('EPC_FAILOVER_STATUS_TTL=abc');
	$ttlBad = epc_failover_status_cache_ttl();
	putenv('EPC_FAILOVER_STATUS_TTL');
	$url = epc_failover_primary_probe_url();
	putenv('EPC_FAILOVER_PRIMARY_URL= https://backup.example/ping ');
	$urlEnv = epc_failover_primary_probe_url();
	putenv('EPC_FAILOVER_PRIMARY_URL');
	$_SERVER['HTTP_HOST'] = 'shop.example:8443';
	$hostPort = epc_failover_host_label();
	unset($_SERVER['HTTP_HOST']);
	$hostDef = epc_failover_host_label();
	$_GET = array();
	$prev0 = epc_failover_splash_preview_requested();
	$_GET['preview'] = '0';
	$prevEmpty = epc_failover_splash_preview_requested();
	$_GET['epc_splash_preview'] = '1';
	$prevOk = epc_failover_splash_preview_requested();
	$fast = epc_failover_read_mode_fast();
	$mode = epc_failover_read_mode_file();
	$json = epc_failover_read_json_mirror();
	$age = epc_failover_json_mirror_age_sec();
	$resolved = epc_failover_resolve_mode(false);
	$splashNull = epc_failover_should_show_splash(null);
	$paths = array(
		epc_failover_mode_paths(),
		epc_failover_json_paths(),
		epc_failover_config_paths(),
	);
	$doc = epc_failover_docroot();
	return tide_freeze(array(
		$modes,
		$def,
		$cfg,
		$ttl,
		$ttl15,
		$ttl12,
		$ttlBad,
		$url,
		$urlEnv,
		$hostPort,
		$hostDef,
		$prev0,
		$prevEmpty,
		$prevOk,
		$fast,
		$mode,
		$json,
		$age,
		$resolved,
		$splashNull,
		$paths,
		$doc,
	));
}

function tide_run_files()
{
	$bad = epc_failover_write_mode_file('nope');
	$ok = epc_failover_write_mode_file('backup_active', array('note' => 'standby'));
	$read = epc_failover_read_mode_file();
	$fast = epc_failover_read_mode_fast();
	$mirror = epc_failover_read_json_mirror();
	$st = epc_failover_build_status('failback_redirect', array('redirect_seconds' => '9', 'ping' => true));
	$st2 = epc_failover_build_status('unknown-mode');
	$cfgW = epc_failover_write_config(array(
		'backup_base_url' => 'https://backup.local/',
		'primary_url' => 'https://cloud.example',
		'poll_interval_sec' => 5,
		'show_cloud_primary_badge' => '0',
		'extra' => 'keep',
	));
	$cfg = epc_failover_read_config();
	$cfgRawPoll = $cfg['poll_interval_sec'];
	$cfg2w = epc_failover_write_config(array(
		'poll_interval_sec' => 90,
		'show_cloud_primary_badge' => 1,
	));
	$cfg2 = epc_failover_read_config();
	$cur = epc_failover_current_status(false);
	$resolveFile = epc_failover_resolve_mode(true);
	return tide_freeze(array(
		$bad,
		$ok,
		$read,
		$fast,
		$mirror,
		$st,
		$st2,
		$cfgW,
		$cfg,
		$cfgRawPoll,
		$cfg2w,
		$cfg2,
		$cur,
		$resolveFile,
	));
}

function tide_run_probe()
{
	$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
	$local = epc_failover_probe_primary(4);
	$_SERVER['HTTP_HOST'] = 'ecomae.com:443';
	$www = epc_failover_probe_primary(4);
	$_SERVER['HTTP_HOST'] = 'other.test';
	putenv('EPC_FAILOVER_PRIMARY_URL=https://www.ecomae.com/epc-platform-status.php?ping=1');
	// remote path is not executed against a live host; only the local short-circuit is golden.
	putenv('EPC_FAILOVER_PRIMARY_URL');
	$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
	$auto = epc_failover_resolve_mode(true);
	$_GET = array();
	$_POST = array();
	$_SESSION = array();
	$no = epc_failover_probe_authorized();
	$_GET['token'] = '0';
	$zero = epc_failover_probe_authorized();
	if (!function_exists('epc_deploy_token')) {
		function epc_deploy_token(): string
		{
			return 'tide-secret';
		}
	}
	$_GET['token'] = 'tide-secret';
	$tokOk = epc_failover_probe_authorized();
	$_GET = array();
	$_POST['token'] = 'nope';
	$tokBad = epc_failover_probe_authorized();
	$_POST = array();
	$_SESSION['user_id'] = 0;
	$uid0 = epc_failover_probe_authorized();
	$_SESSION['user_id'] = 7;
	$noPortal = epc_failover_probe_authorized();
	$portalDir = epc_failover_docroot() . '/content/general_pages';
	if (!is_dir($portalDir)) {
		mkdir($portalDir, 0755, true);
	}
	file_put_contents($portalDir . '/epc_portal.php', "<?php\nfunction epc_portal_is_super_cp_host(){return true;}\n");
	$super = epc_failover_probe_authorized();
	return tide_freeze(array($local, $www, $auto, $no, $zero, $tokOk, $tokBad, $uid0, $noPortal, $super));
}

function tide_run_status()
{
	epc_failover_write_mode_file('primary_down');
	$fresh = epc_failover_current_status(false);
	$ageFresh = epc_failover_json_mirror_age_sec();
	$json = epc_failover_json_paths()[0];
	$staleMirror = epc_failover_build_status('primary_ok');
	$staleMirror['updated_at'] = 'OLD';
	file_put_contents($json, json_encode($staleMirror, JSON_UNESCAPED_SLASHES) . "\n");
	clearstatcache(true, $json);
	$cached = epc_failover_current_status(false);
	touch($json, time() - 200);
	clearstatcache(true, $json);
	$ageStale = epc_failover_json_mirror_age_sec();
	$rebuilt = epc_failover_current_status(false);
	file_put_contents($json, json_encode(array('mode' => 'backup_active', 'label' => 'cached-auto'), JSON_UNESCAPED_SLASHES) . "\n");
	clearstatcache(true, $json);
	$autoHit = epc_failover_current_status(true);
	file_put_contents($json, json_encode(array('mode' => '0'), JSON_UNESCAPED_SLASHES) . "\n");
	clearstatcache(true, $json);
	$emptyMode = epc_failover_read_json_mirror();
	return tide_freeze(array($fresh, $ageFresh < 5 ? 0 : $ageFresh, $cached, $ageStale, $rebuilt, $autoHit, $emptyMode));
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_tide_' . substr(md5(uniqid('', true)), 0, 12);
	mkdir($doc, 0755, true);
	$GLOBALS['TIDE_DOCROOT'] = $doc;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	if (!defined('EPC_FAILOVER_DOCROOT')) {
		define('EPC_FAILOVER_DOCROOT', $doc);
	}
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_GET = array();
	$_POST = array();
	$_SESSION = array();
	require $root . '/content/general_pages/epc_platform_failover.php';
	$cleanup = function () use ($doc) {
		$it = new RecursiveIteratorIterator(
			new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS),
			RecursiveIteratorIterator::CHILD_FIRST
		);
		foreach ($it as $f) {
			$f->isDir() ? @rmdir($f->getPathname()) : @unlink($f->getPathname());
		}
		@rmdir($doc);
	};
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1t_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1t_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
