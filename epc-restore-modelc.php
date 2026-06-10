<?php
/**
 * Server-side Model C restore (upload ZIPs to .epc-restore-incoming/ then run).
 *
 *   mode=status   — list incoming zips
 *   mode=prepare  — extract SQL (+ client site/ to work dir)
 *   mode=apply&confirm=1 — import docpart + ecomae databases
 *   mode=apply_site&confirm=1 — copy prepared site/ over docroot (after prepare)
 *
 * Upload: docroot/.epc-restore-incoming/modelc-client-*.zip and modelc-platform-*.zip
 */
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

@set_time_limit(0);
@ini_set('memory_limit', '1536M');
header('Content-Type: application/json; charset=utf-8');

$incoming = __DIR__ . '/.epc-restore-incoming';
$work = __DIR__ . '/.epc-restore-work';

function epc_restore_json($payload, $code = 200)
{
	http_response_code($code);
	echo json_encode($payload, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
	exit;
}

function epc_restore_find_zip($dir, $prefix)
{
	if (!is_dir($dir)) {
		return null;
	}
	$found = null;
	foreach (glob($dir . '/' . $prefix . '*.zip') ?: array() as $path) {
		if ($found === null || filemtime($path) > filemtime($found)) {
			$found = $path;
		}
	}
	return $found;
}

function epc_restore_rmdir_tree($dir)
{
	if (!is_dir($dir)) {
		return;
	}
	$it = new RecursiveIteratorIterator(
		new RecursiveDirectoryIterator($dir, RecursiveDirectoryIterator::SKIP_DOTS),
		RecursiveIteratorIterator::CHILD_FIRST
	);
	foreach ($it as $f) {
		$f->isDir() ? @rmdir($f->getRealPath()) : @unlink($f->getRealPath());
	}
	@rmdir($dir);
}

function epc_restore_extract_sql($zipPath, $destDir)
{
	if (!class_exists('ZipArchive')) {
		throw new Exception('ZipArchive not available');
	}
	if (!is_dir($destDir) && !@mkdir($destDir, 0700, true)) {
		throw new Exception('Cannot create work directory');
	}
	$zip = new ZipArchive();
	if ($zip->open($zipPath) !== true) {
		throw new Exception('Cannot open zip: ' . basename($zipPath));
	}
	$idx = $zip->locateName('database.sql', ZipArchive::FL_NOCASE);
	if ($idx === false) {
		$zip->close();
		throw new Exception('database.sql not in archive');
	}
	$zip->extractTo($destDir, array('database.sql'));
	$zip->close();
	$sqlPath = $destDir . '/database.sql';
	if (!is_file($sqlPath)) {
		throw new Exception('Extract failed for database.sql');
	}
	return $sqlPath;
}

function epc_restore_copy_site_tree($src, $dest)
{
	if (!is_dir($src)) {
		throw new Exception('Site source missing: ' . $src);
	}
	$src = rtrim($src, '/\\');
	$dest = rtrim($dest, '/\\');
	$it = new RecursiveIteratorIterator(
		new RecursiveDirectoryIterator($src, RecursiveDirectoryIterator::SKIP_DOTS),
		RecursiveIteratorIterator::SELF_FIRST
	);
	foreach ($it as $item) {
		$rel = substr($item->getPathname(), strlen($src) + 1);
		$target = $dest . DIRECTORY_SEPARATOR . $rel;
		if ($item->isDir()) {
			if (!is_dir($target)) {
				@mkdir($target, 0755, true);
			}
		} else {
			@copy($item->getPathname(), $target);
		}
	}
}

function epc_restore_import_sql(PDO $pdo, $sqlPath)
{
	$fh = fopen($sqlPath, 'rb');
	if (!$fh) {
		throw new Exception('Cannot read SQL file');
	}
	$pdo->exec('SET FOREIGN_KEY_CHECKS=0');
	$pdo->exec('SET NAMES utf8mb4');
	$buffer = '';
	while (!feof($fh)) {
		$line = fgets($fh);
		if ($line === false) {
			break;
		}
		$trim = ltrim($line);
		if ($trim === '' || strpos($trim, '--') === 0) {
			continue;
		}
		$buffer .= $line;
		if (substr(rtrim($line), -1) === ';') {
			$stmt = trim($buffer);
			$buffer = '';
			if ($stmt !== '') {
				$pdo->exec($stmt);
			}
		}
	}
	fclose($fh);
	if (trim($buffer) !== '') {
		$pdo->exec(trim($buffer));
	}
	$pdo->exec('SET FOREIGN_KEY_CHECKS=1');
}

function epc_restore_db_credentials()
{
	require_once __DIR__ . '/config.php';
	$cfg = new DP_Config();
	$host = $cfg->host;
	$clientDb = 'docpart';
	$clientUser = $cfg->user;
	$clientPass = $cfg->password;
	$platformDb = 'ecomae';
	$platformUser = 'ecomae';
	$platformPass = $cfg->password;

	if (is_file(__DIR__ . '/config.local.php')) {
		$epc_config_local = null;
		require __DIR__ . '/config.local.php';
		if (isset($epc_config_local) && is_array($epc_config_local)) {
			if (!empty($epc_config_local['user'])) {
				$platformUser = $epc_config_local['user'];
				$clientUser = $epc_config_local['user'];
			}
			if (!empty($epc_config_local['password'])) {
				$platformPass = $epc_config_local['password'];
				$clientPass = $epc_config_local['password'];
			}
		}
	}

	if (strtolower((string) $cfg->db) === 'docpart') {
		$clientDb = 'docpart';
		$clientUser = $cfg->user;
		$clientPass = $cfg->password;
	}

	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_sites')) {
		foreach (epc_portal_sites() as $site) {
			if (!empty($site['db']) && $site['db'] === 'ecomae') {
				$platformDb = 'ecomae';
				if (!empty($site['user'])) {
					$platformUser = $site['user'];
				}
				break;
			}
		}
	}

	return array(
		'host' => $host,
		'client_db' => $clientDb,
		'client_user' => $clientUser,
		'client_pass' => $clientPass,
		'platform_db' => $platformDb,
		'platform_user' => $platformUser,
		'platform_pass' => $platformPass,
	);
}

$mode = isset($_GET['mode']) ? (string) $_GET['mode'] : 'status';

if ($mode === 'status') {
	epc_restore_json(array(
		'ok' => true,
		'incoming_dir' => $incoming,
		'work_dir' => $work,
		'client_zip' => epc_restore_find_zip($incoming, 'modelc-client-'),
		'platform_zip' => epc_restore_find_zip($incoming, 'modelc-platform-'),
		'steps' => array(
			'1' => 'Upload both ZIPs to .epc-restore-incoming/',
			'2' => 'mode=prepare',
			'3' => 'mode=apply&confirm=1 (databases)',
			'4' => 'mode=apply_site&confirm=1 (optional site files)',
		),
	));
}

if ($mode === 'prepare') {
	try {
		if (!is_dir($incoming)) {
			@mkdir($incoming, 0700, true);
		}
		$clientZip = epc_restore_find_zip($incoming, 'modelc-client-');
		$platformZip = epc_restore_find_zip($incoming, 'modelc-platform-');
		if (!$clientZip || !$platformZip) {
			throw new Exception('Upload modelc-client-*.zip and modelc-platform-*.zip to ' . $incoming);
		}
		if (is_dir($work)) {
			epc_restore_rmdir_tree($work);
		}
		@mkdir($work, 0700, true);
		@mkdir($work . '/client', 0700, true);
		@mkdir($work . '/platform', 0700, true);
		@mkdir($work . '/client-site', 0700, true);

		$clientSql = epc_restore_extract_sql($clientZip, $work . '/client');
		$platformSql = epc_restore_extract_sql($platformZip, $work . '/platform');

		if (class_exists('ZipArchive')) {
			$zip = new ZipArchive();
			if ($zip->open($clientZip) === true) {
				$zip->extractTo($work . '/client-site', array('site'));
				$zip->close();
			}
		}

		$creds = epc_restore_db_credentials();
		epc_restore_json(array(
			'ok' => true,
			'client_sql' => $clientSql,
			'platform_sql' => $platformSql,
			'client_site' => $work . '/client-site/site',
			'client_zip' => basename($clientZip),
			'platform_zip' => basename($platformZip),
			'will_import' => array(
				'client' => $creds['client_db'],
				'platform' => $creds['platform_db'],
			),
			'next_db' => '?token=REDACTED&mode=apply&confirm=1',
			'next_site' => '?token=REDACTED&mode=apply_site&confirm=1',
		));
	} catch (Exception $e) {
		epc_restore_json(array('ok' => false, 'error' => $e->getMessage()), 500);
	}
}

if ($mode === 'apply' || $mode === 'apply_site') {
	if (empty($_GET['confirm'])) {
		epc_restore_json(array('ok' => false, 'error' => 'Add confirm=1 to proceed'), 400);
	}
	try {
		$clientSql = $work . '/client/database.sql';
		$platformSql = $work . '/platform/database.sql';
		$siteSrc = $work . '/client-site/site';
		if ($mode === 'apply' && (!is_file($clientSql) || !is_file($platformSql))) {
			throw new Exception('Run mode=prepare first');
		}
		if ($mode === 'apply_site' && !is_dir($siteSrc)) {
			throw new Exception('Run mode=prepare first (site/ missing)');
		}

		$creds = epc_restore_db_credentials();
		$results = array();

		if ($mode === 'apply') {
			$clientPdo = new PDO(
				'mysql:host=' . $creds['host'] . ';dbname=' . $creds['client_db'] . ';charset=utf8mb4',
				$creds['client_user'],
				$creds['client_pass'],
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			epc_restore_import_sql($clientPdo, $clientSql);
			$results['client_db'] = $creds['client_db'];

			$platformPdo = new PDO(
				'mysql:host=' . $creds['host'] . ';dbname=' . $creds['platform_db'] . ';charset=utf8mb4',
				$creds['platform_user'],
				$creds['platform_pass'],
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			epc_restore_import_sql($platformPdo, $platformSql);
			$results['platform_db'] = $creds['platform_db'];
			$results['message'] = 'Databases imported. Optionally run mode=apply_site&confirm=1';
		}

		if ($mode === 'apply_site') {
			epc_restore_copy_site_tree($siteSrc, __DIR__);
			$results['site'] = 'copied to ' . __DIR__;
			$results['message'] = 'Site files restored over docroot';
		}

		epc_restore_json(array('ok' => true, 'imported' => $results));
	} catch (Exception $e) {
		epc_restore_json(array('ok' => false, 'error' => $e->getMessage()), 500);
	}
}

epc_restore_json(array('ok' => false, 'error' => 'Unknown mode'), 400);
