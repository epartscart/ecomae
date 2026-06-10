<?php
/**
 * Recover from MySQL "Too many connections" — show stats, kill idle sleeps, optional restart.
 * https://www.ecomae.com/epc-mysql-connections-fix.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$apply = !empty($_GET['apply']);
$restart = !empty($_GET['restart']);
$killSleepSec = max(30, (int) ($_GET['kill_sleep_sec'] ?? 120));
$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));

function epc_mysql_sudo(string $cmd, string $clpPass): string
{
	if ($clpPass === '') {
		return epc_mysql_run($cmd);
	}
	$escaped = str_replace("'", "'\\''", $clpPass);
	return epc_mysql_run("echo '" . $escaped . "' | sudo -S " . $cmd . ' 2>&1');
}

function epc_mysql_run(string $cmd): string
{
	if (!function_exists('epc_clp_run_cmd')) {
		return 'epc_clp_run_cmd missing';
	}
	$r = epc_clp_run_cmd($cmd);
	$out = isset($r['output']) ? trim((string) $r['output']) : '';
	$code = isset($r['code']) ? (int) $r['code'] : -1;
	return ($out !== '' ? $out : '(empty)') . " [exit={$code}]";
}

function epc_mysql_binaries(): array
{
	static $bins = null;
	if ($bins !== null) {
		return $bins;
	}
	$bins = array();
	$find = epc_mysql_run('command -v mysql 2>/dev/null; command -v mariadb 2>/dev/null; ls -1 /usr/bin/mysql /usr/bin/mariadb 2>/dev/null');
	foreach (preg_split('/\R/', $find) as $line) {
		$line = trim($line);
		if ($line !== '' && is_file($line)) {
			$bins[$line] = true;
		}
	}
	return array_keys($bins);
}

function epc_mysql_query(string $sql): string
{
	$escaped = str_replace("'", "'\\''", $sql);
	$cmds = array();
	foreach (epc_mysql_binaries() as $bin) {
		$cmds[] = escapeshellarg($bin) . " -N -B -e '{$escaped}' 2>&1";
	}
	$cmds[] = "runuser -u root -- /usr/bin/mysql -N -B -e '{$escaped}' 2>&1";
	$cmds[] = "runuser -u clp -- /usr/bin/mysql -N -B -e '{$escaped}' 2>&1";
	$cmds[] = "sudo -n mysql -N -B -e '{$escaped}' 2>&1";
	foreach ($cmds as $cmd) {
		$out = epc_mysql_run($cmd);
		if ($out !== '' && stripos($out, 'ERROR') === false && stripos($out, 'Access denied') === false && stripos($out, 'not found') === false) {
			return $out;
		}
	}
	return 'query_failed';
}

function epc_mysql_release_php_connections(string $clpPass = ''): array
{
	$log = array();
	foreach (array(
		'systemctl restart php8.4-fpm',
		'systemctl restart php8.3-fpm',
		'systemctl restart php8.2-fpm',
		'systemctl restart php-fpm',
	) as $svc) {
		$out = $clpPass !== ''
			? epc_mysql_sudo($svc, $clpPass)
			: epc_mysql_run('runuser -u root -- ' . $svc . ' 2>&1');
		$log[] = $svc . ' → ' . $out;
		if (stripos($out, 'exit=0') !== false) {
			break;
		}
		$out2 = epc_mysql_run('runuser -u clp -- ' . $svc . ' 2>&1');
		$log[] = 'runuser clp ' . $svc . ' → ' . $out2;
		if (stripos($out2, 'exit=0') !== false) {
			break;
		}
	}
	return $log;
}

echo "=== MySQL connections fix ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . ' restart=' . ($restart ? 'yes' : 'no') . "\n";
echo 'kill_sleep_sec=' . $killSleepSec . "\n";
echo 'clp_pass=' . ($clpPass !== '' ? 'set' : 'missing') . "\n\n";

if ($apply) {
	echo "=== Emergency: release PHP-FPM + optional MariaDB restart (before stats) ===\n";
	foreach (epc_mysql_release_php_connections($clpPass) as $line) {
		echo $line . "\n";
	}
	if ($restart) {
		foreach (array('systemctl restart mariadb', 'systemctl restart mysql') as $svc) {
			$out = $clpPass !== ''
				? epc_mysql_sudo($svc, $clpPass)
				: epc_mysql_run('runuser -u root -- ' . $svc . ' 2>&1');
			echo $svc . ' → ' . $out . "\n";
			if (stripos($out, 'exit=0') !== false) {
				break;
			}
			$out2 = epc_mysql_run('runuser -u clp -- ' . $svc . ' 2>&1');
			echo 'runuser clp ' . $svc . ' → ' . $out2 . "\n";
			if (stripos($out2, 'exit=0') !== false) {
				break;
			}
		}
	}
	sleep(3);
	echo "\n";
}

echo "=== Service ===\n";
echo epc_mysql_run('systemctl is-active mariadb 2>&1 || systemctl is-active mysql 2>&1') . "\n";
echo 'mysql_bins=' . implode(', ', epc_mysql_binaries()) . "\n\n";

if (!$apply) {
	echo "Dry run. Re-run with apply=1&clp_pass=... to release PHP-FPM pools + kill sleeping connections.\n";
	echo "Add restart=1 to restart mariadb (brief outage).\n";
	exit;
}

echo "=== Connection stats (after PHP-FPM release) ===\n";
foreach (array(
	"SHOW VARIABLES LIKE 'max_connections'",
	"SHOW STATUS LIKE 'Threads_connected'",
	"SHOW STATUS LIKE 'Max_used_connections'",
) as $q) {
	echo $q . ': ' . epc_mysql_query($q) . "\n";
}
echo "\n";

$killSql = "SELECT CONCAT('KILL ', id, ';') FROM information_schema.processlist"
	. " WHERE Command = 'Sleep' AND Time >= {$killSleepSec} AND User NOT IN ('root', 'system user')";
$killList = epc_mysql_query($killSql);
$killed = 0;
if ($killList !== 'query_failed') {
	foreach (preg_split('/\R/', $killList) as $line) {
		$line = trim($line);
		if ($line === '' || stripos($line, 'KILL') !== 0) {
			continue;
		}
		$res = epc_mysql_query(rtrim($line, ';'));
		echo $line . ' → ' . $res . "\n";
		$killed++;
	}
}
echo "killed_sleeping={$killed}\n\n";

echo "=== After ===\n";
echo "Threads_connected: " . epc_mysql_query("SHOW STATUS LIKE 'Threads_connected'") . "\n";

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
$cfg = new DP_Config();
epc_portal_apply_config($cfg);
try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8;connect_timeout=5',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 5)
	);
	$tpl = (int) $pdo->query('SELECT COUNT(*) FROM `templates` WHERE `current` = 1 AND `is_frontend` = 1')->fetchColumn();
	echo "docpart_connect=ok frontend_templates={$tpl}\n";
} catch (Exception $e) {
	echo 'docpart_connect=fail ' . $e->getMessage() . "\n";
}
