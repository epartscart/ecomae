<?php
/**
 * Restore production from server backups under /home/ecomae/backups/
 *
 *   mode=list              — available backup sessions
 *   mode=preview           — what apply would do (?session= optional)
 *   mode=apply&confirm=1   — import DBs + extract docroots + restart services
 *
 * https://www.ecomae.com/epc-production-restore.php?token=epartscart-deploy-2026&mode=list
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

@set_time_limit(0);
@ini_set('memory_limit', '2048M');
header('Content-Type: application/json; charset=utf-8');

function epc_pr_json($payload, int $code = 200): void
{
	http_response_code($code);
	echo json_encode($payload, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
	exit;
}

function epc_pr_backup_root(): string
{
	return '/home/ecomae/backups';
}

function epc_pr_platform_docroot(): string
{
	return '/home/ecomae/htdocs/www.ecomae.com';
}

function epc_pr_cp_docroot(): string
{
	return '/home/ecomae/htdocs/cp.ecomae.com';
}

/** @return list<array{session:string,dir:string,kind:string,modified:int,manifest:bool}> */
function epc_pr_list_sessions(): array
{
	$root = epc_pr_backup_root();
	$items = array();
	foreach (array('platform-full-*', 'epartscart-migration-*') as $glob) {
		foreach (glob($root . '/' . $glob, GLOB_ONLYDIR) ?: array() as $dir) {
			$name = basename($dir);
			$kind = strpos($name, 'platform-full-') === 0 ? 'platform-full' : 'migration';
			$items[] = array(
				'session' => $name,
				'dir' => $dir,
				'kind' => $kind,
				'modified' => (int) filemtime($dir),
				'manifest' => is_file($dir . '/MANIFEST.json'),
			);
		}
	}
	usort($items, static function ($a, $b) {
		return $b['modified'] <=> $a['modified'];
	});
	return $items;
}

function epc_pr_pick_session(?string $requested): ?array
{
	$sessions = epc_pr_list_sessions();
	if ($requested !== null && $requested !== '') {
		$requested = basename($requested);
		foreach ($sessions as $s) {
			if ($s['session'] === $requested) {
				return $s;
			}
		}
		return null;
	}
	return $sessions[0] ?? null;
}

/** @return array<string,string> logical => filename in session dir */
function epc_pr_artifact_map(string $dir): array
{
	$map = array();
	$candidates = array(
		'docpart_sql' => array('docpart-database.sql.gz', 'docpart_database.sql.gz'),
		'ecomae_sql' => array('ecomae-database.sql.gz', 'ecomae_database.sql.gz'),
		'platform_tar' => array(
			'www_ecomae_com-docroot.tar.gz',
			'ecomae_platform-docroot.tar.gz',
		),
		'cp_tar' => array('cp_ecomae_com-docroot.tar.gz', 'ecomae_cp-docroot.tar.gz'),
		'nginx_tar' => array('nginx-sites-enabled.tar.gz'),
	);
	foreach ($candidates as $key => $names) {
		foreach ($names as $name) {
			$path = $dir . '/' . $name;
			if (is_file($path)) {
				$map[$key] = $name;
				break;
			}
		}
	}
	return $map;
}

function epc_pr_load_db_creds(): array
{
	require_once __DIR__ . '/config.php';
	$cfg = new DP_Config();
	$host = $cfg->host ?: '127.0.0.1';
	$docpartUser = 'docpart';
	$docpartPass = $cfg->password;
	$docpartDb = 'docpart';
	$ecomaeUser = 'ecomae';
	$ecomaePass = $cfg->password;
	$ecomaeDb = 'ecomae';

	if (is_file(__DIR__ . '/config.local.php')) {
		$epc_config_local = null;
		require __DIR__ . '/config.local.php';
		if (isset($epc_config_local) && is_array($epc_config_local)) {
			if (!empty($epc_config_local['user'])) {
				$ecomaeUser = $epc_config_local['user'];
			}
			if (!empty($epc_config_local['password'])) {
				$ecomaePass = $epc_config_local['password'];
			}
			if (!empty($epc_config_local['db'])) {
				$ecomaeDb = $epc_config_local['db'];
			}
		}
	}
	if (strtolower((string) $cfg->db) === 'docpart') {
		$docpartDb = 'docpart';
		$docpartUser = $cfg->user;
		$docpartPass = $cfg->password;
	}

	return array(
		'host' => $host,
		'docpart' => array('db' => $docpartDb, 'user' => $docpartUser, 'pass' => $docpartPass),
		'ecomae' => array('db' => $ecomaeDb, 'user' => $ecomaeUser, 'pass' => $ecomaePass),
	);
}

function epc_pr_shell(string $cmd): array
{
	$out = array();
	$code = 0;
	@exec($cmd . ' 2>&1', $out, $code);
	return array('cmd' => $cmd, 'code' => $code, 'output' => implode("\n", $out));
}

function epc_pr_import_sql_gz(string $gzPath, array $cred): array
{
	$db = $cred['db'];
	$user = $cred['user'];
	$pass = $cred['pass'];
	$host = $cred['host'] ?? '127.0.0.1';
	$passEsc = escapeshellarg($pass);
	$userEsc = escapeshellarg($user);
	$dbEsc = escapeshellarg($db);
	$hostEsc = escapeshellarg($host);
	$gzEsc = escapeshellarg($gzPath);

	$cmd = "gunzip -c {$gzEsc} | mysql -h {$hostEsc} -u {$userEsc} -p{$passEsc} {$dbEsc}";
	$result = epc_pr_shell($cmd);
	if ($result['code'] !== 0) {
		$cmd2 = "gunzip -c {$gzEsc} | mysql -h {$hostEsc} -u {$userEsc} -p{$passEsc} {$dbEsc} 2>/dev/null";
		$result = epc_pr_shell($cmd2);
	}
	return $result;
}

function epc_pr_extract_tar(string $tarPath, string $dest): array
{
	if (!is_dir($dest)) {
		@mkdir($dest, 0755, true);
	}
	$tarEsc = escapeshellarg($tarPath);
	$destEsc = escapeshellarg($dest);
	return epc_pr_shell("tar -xzf {$tarEsc} -C {$destEsc}");
}

function epc_pr_restart_services(): array
{
	$steps = array();
	foreach (array(
		'systemctl restart mariadb 2>/dev/null || systemctl restart mysql 2>/dev/null',
		'systemctl restart php8.3-fpm 2>/dev/null || systemctl restart php8.2-fpm 2>/dev/null || systemctl restart php-fpm 2>/dev/null',
		'systemctl restart nginx 2>/dev/null',
	) as $cmd) {
		$steps[] = epc_pr_shell($cmd);
	}
	return $steps;
}

function epc_pr_probe_urls(): array
{
	$urls = array(
		'https://www.ecomae.com/',
		'https://www.ecomae.com/cp/',
		'https://www.epartscart.com/cp/',
		'https://www.taxofinca.com/cp/',
	);
	$out = array();
	foreach ($urls as $url) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_NOBODY => true,
			CURLOPT_TIMEOUT => 15,
			CURLOPT_SSL_VERIFYPEER => false,
			CURLOPT_FOLLOWLOCATION => true,
		));
		curl_exec($ch);
		$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
		curl_close($ch);
		$out[$url] = $code;
	}
	return $out;
}

$mode = isset($_GET['mode']) ? (string) $_GET['mode'] : 'list';
$sessionReq = isset($_GET['session']) ? (string) $_GET['session'] : null;

if ($mode === 'list') {
	epc_pr_json(array(
		'ok' => true,
		'backup_root' => epc_pr_backup_root(),
		'platform_docroot' => epc_pr_platform_docroot(),
		'sessions' => epc_pr_list_sessions(),
		'usage' => array(
			'preview' => '?token=...&mode=preview&session=SESSION',
			'apply' => '?token=...&mode=apply&confirm=1&session=SESSION',
		),
	));
}

$session = epc_pr_pick_session($sessionReq);
if ($session === null) {
	epc_pr_json(array(
		'ok' => false,
		'error' => 'No backup session found under ' . epc_pr_backup_root(),
		'sessions' => epc_pr_list_sessions(),
	), 404);
}

$dir = $session['dir'];
$artifacts = epc_pr_artifact_map($dir);
$creds = epc_pr_load_db_creds();

$preview = array(
	'session' => $session['session'],
	'kind' => $session['kind'],
	'dir' => $dir,
	'artifacts' => $artifacts,
	'will_import' => array(
		'docpart' => isset($artifacts['docpart_sql']) ? $artifacts['docpart_sql'] : null,
		'ecomae' => isset($artifacts['ecomae_sql']) ? $artifacts['ecomae_sql'] : null,
	),
	'will_extract' => array(
		'platform' => isset($artifacts['platform_tar'])
			? $artifacts['platform_tar'] . ' → ' . epc_pr_platform_docroot()
			: null,
		'cp' => isset($artifacts['cp_tar'])
			? $artifacts['cp_tar'] . ' → ' . epc_pr_cp_docroot()
			: null,
	),
);

if ($mode === 'preview') {
	epc_pr_json(array('ok' => true, 'preview' => $preview));
}

if ($mode !== 'apply') {
	epc_pr_json(array('ok' => false, 'error' => 'Unknown mode', 'modes' => array('list', 'preview', 'apply')), 400);
}

if (empty($_GET['confirm'])) {
	epc_pr_json(array('ok' => false, 'error' => 'Add confirm=1 to run restore', 'preview' => $preview), 400);
}

$log = array('preview' => $preview, 'steps' => array());

try {
	if (isset($artifacts['ecomae_sql'])) {
		$path = $dir . '/' . $artifacts['ecomae_sql'];
		$c = $creds['ecomae'];
		$c['host'] = $creds['host'];
		$log['steps']['import_ecomae'] = epc_pr_import_sql_gz($path, $c);
	}
	if (isset($artifacts['docpart_sql'])) {
		$path = $dir . '/' . $artifacts['docpart_sql'];
		$c = $creds['docpart'];
		$c['host'] = $creds['host'];
		$log['steps']['import_docpart'] = epc_pr_import_sql_gz($path, $c);
	}

	if (isset($artifacts['platform_tar'])) {
		$log['steps']['extract_platform'] = epc_pr_extract_tar(
			$dir . '/' . $artifacts['platform_tar'],
			epc_pr_platform_docroot()
		);
	}
	if (isset($artifacts['cp_tar'])) {
		$log['steps']['extract_cp'] = epc_pr_extract_tar(
			$dir . '/' . $artifacts['cp_tar'],
			epc_pr_cp_docroot()
		);
	}
	if (isset($artifacts['nginx_tar'])) {
		$log['steps']['extract_nginx'] = epc_pr_extract_tar(
			$dir . '/' . $artifacts['nginx_tar'],
			'/etc/nginx'
		);
	}

	$log['steps']['restart_services'] = epc_pr_restart_services();
	$log['probes'] = epc_pr_probe_urls();

	$failed = false;
	foreach ($log['steps'] as $step) {
		if (is_array($step) && isset($step['code']) && $step['code'] !== 0) {
			$failed = true;
			break;
		}
		if (is_array($step) && !isset($step['code'])) {
			foreach ($step as $sub) {
				if (is_array($sub) && isset($sub['code']) && $sub['code'] !== 0) {
					$failed = true;
					break 2;
				}
			}
		}
	}

	$log['ok'] = !$failed;
	$log['next'] = array(
		'docpart_fix' => '/epc-docpart-db-fix.php?token=REDACTED&apply=1&clp_pass=...',
		'tenants' => '/epc-tenants-connectivity-fix.php?token=REDACTED&apply=1&clp_pass=...',
		'marketing' => '/epc-ecomae-force-marketing-home.php?token=REDACTED&apply=1',
	);

	epc_pr_json($log, $failed ? 500 : 200);
} catch (Throwable $e) {
	epc_pr_json(array('ok' => false, 'error' => $e->getMessage(), 'preview' => $preview), 500);
}
