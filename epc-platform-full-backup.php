<?php
/**
 * Full platform backup (Model C) — ecomae Super CP + docpart tenants + docroots + nginx + CloudPanel.
 *
 * Preview:  ?token=epartscart-deploy-2026
 * Run:      ?token=epartscart-deploy-2026&apply=1
 * List:     ?token=...&mode=list
 * Download: ?token=...&mode=download&session=platform-full-...&file=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

@set_time_limit(0);
@ini_set('memory_limit', '2048M');

header('Content-Type: application/json; charset=utf-8');

$mode = strtolower(trim((string) ($_GET['mode'] ?? '')));
$apply = isset($_GET['apply']) && (string) $_GET['apply'] === '1';
if ($mode === '' && $apply) {
	$mode = 'create';
}
if ($mode === '') {
	$mode = 'status';
}

function epc_pfb_json($payload, int $status = 200): void
{
	http_response_code($status);
	echo json_encode($payload, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

function epc_pfb_backup_root(): string
{
	$root = '/home/ecomae/backups';
	if (!is_dir($root)) {
		@mkdir($root, 0700, true);
	}
	if (!is_dir($root) || !is_writable($root)) {
		$root = __DIR__ . '/.epc-backups';
		if (!is_dir($root)) {
			@mkdir($root, 0700, true);
		}
	}
	return $root;
}

function epc_pfb_session_dir(): string
{
	return epc_pfb_backup_root() . '/platform-full-' . gmdate('Ymd-His');
}

function epc_pfb_platform_db_credentials(): array
{
	$defaults = array(
		'host' => '127.0.0.1',
		'db' => 'ecomae',
		'user' => 'ecomae',
		'password' => '',
	);
	$paths = array(
		'/home/ecomae/htdocs/www.ecomae.com/config.local.php',
		__DIR__ . '/config.local.php',
	);
	foreach ($paths as $path) {
		if (!is_file($path)) {
			continue;
		}
		$epc_config_local = null;
		require $path;
		if (isset($epc_config_local) && is_array($epc_config_local) && !empty($epc_config_local['password'])) {
			$defaults['password'] = (string) $epc_config_local['password'];
			if (!empty($epc_config_local['host'])) {
				$defaults['host'] = (string) $epc_config_local['host'];
			}
			if (!empty($epc_config_local['db'])) {
				$defaults['db'] = (string) $epc_config_local['db'];
			}
			if (!empty($epc_config_local['user'])) {
				$defaults['user'] = (string) $epc_config_local['user'];
			}
			return $defaults;
		}
	}
	return $defaults;
}

function epc_pfb_tenant_db_credentials(): array
{
	$defaults = array(
		'host' => '127.0.0.1',
		'db' => 'docpart',
		'user' => 'docpart',
		'password' => '',
	);
	$platform = epc_pfb_platform_db_credentials();
	if ($platform['password'] !== '') {
		try {
			require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
			$pdo = new PDO(
				'mysql:host=' . $platform['host'] . ';dbname=' . $platform['db'] . ';charset=utf8',
				$platform['user'],
				$platform['password'],
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			epc_portal_db_ensure($pdo);
			$st = $pdo->query('SELECT site_key, db_name, db_user, db_password FROM `epc_portal_tenants` ORDER BY site_key');
			$tenants = array();
			while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
				$tenants[] = $row;
			}
			foreach ($tenants as $row) {
				if ((string) ($row['db_name'] ?? '') === 'docpart' && (string) ($row['db_password'] ?? '') !== '') {
					return array(
						'host' => '127.0.0.1',
						'db' => (string) $row['db_name'],
						'user' => (string) $row['db_user'],
						'password' => (string) $row['db_password'],
					);
				}
			}
		} catch (Exception $e) {
			// fall through
		}
	}
	if (is_file(__DIR__ . '/config.php')) {
		if (!defined('_ASTEXE_')) {
			define('_ASTEXE_', 1);
		}
		$cfg = null;
		require __DIR__ . '/config.php';
		if (isset($cfg) && is_object($cfg) && (string) $cfg->db === 'docpart') {
			return array(
				'host' => (string) $cfg->host,
				'db' => (string) $cfg->db,
				'user' => (string) $cfg->user,
				'password' => (string) $cfg->password,
			);
		}
	}
	return $defaults;
}

function epc_pfb_docroots(): array
{
	return array(
		'www_ecomae_com' => '/home/ecomae/htdocs/www.ecomae.com',
		'cp_ecomae_com' => '/home/ecomae/htdocs/cp.ecomae.com',
	);
}

function epc_pfb_gzip_file(string $src, string $dest): bool
{
	$in = @fopen($src, 'rb');
	$out = @gzopen($dest, 'wb9');
	if (!$in || !$out) {
		return false;
	}
	while (!feof($in)) {
		gzwrite($out, (string) fread($in, 1024 * 512));
	}
	fclose($in);
	gzclose($out);
	return is_file($dest);
}

function epc_pfb_sql_value(PDO $pdo, $value): string
{
	return $value === null ? 'NULL' : $pdo->quote((string) $value);
}

function epc_pfb_dump_sql_php(PDO $pdo, string $sqlPath): int
{
	$fh = fopen($sqlPath, 'wb');
	if (!$fh) {
		throw new RuntimeException('Cannot create SQL dump');
	}
	fwrite($fh, "-- platform-full backup PHP dump\n-- Created: " . gmdate('c') . "\n");
	fwrite($fh, "SET FOREIGN_KEY_CHECKS = 0;\n\n");
	$tables = $pdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_NUM);
	foreach ($tables as $tableRow) {
		$table = $tableRow[0];
		$quoted = '`' . str_replace('`', '``', $table) . '`';
		$createRow = $pdo->query('SHOW CREATE TABLE ' . $quoted)->fetch(PDO::FETCH_ASSOC);
		$createSql = $createRow['Create Table'] ?? array_values($createRow)[1];
		fwrite($fh, "DROP TABLE IF EXISTS {$quoted};\n{$createSql};\n\n");
		$stmt = $pdo->query('SELECT * FROM ' . $quoted);
		$columns = array();
		for ($i = 0; $i < $stmt->columnCount(); $i++) {
			$meta = $stmt->getColumnMeta($i);
			$columns[] = '`' . str_replace('`', '``', $meta['name']) . '`';
		}
		$columnSql = implode(',', $columns);
		$batch = array();
		while ($row = $stmt->fetch(PDO::FETCH_NUM)) {
			$values = array();
			foreach ($row as $value) {
				$values[] = epc_pfb_sql_value($pdo, $value);
			}
			$batch[] = '(' . implode(',', $values) . ')';
			if (count($batch) >= 50) {
				fwrite($fh, "INSERT INTO {$quoted} ({$columnSql}) VALUES\n" . implode(",\n", $batch) . ";\n");
				$batch = array();
			}
		}
		if ($batch !== array()) {
			fwrite($fh, "INSERT INTO {$quoted} ({$columnSql}) VALUES\n" . implode(",\n", $batch) . ";\n");
		}
		fwrite($fh, "\n");
	}
	fwrite($fh, "SET FOREIGN_KEY_CHECKS = 1;\n");
	fclose($fh);
	return (int) filesize($sqlPath);
}

function epc_pfb_mysqldump(array $creds, string $outSql): array
{
	$method = 'php';
	$log = array();
	if ($creds['password'] === '') {
		return array('ok' => false, 'method' => $method, 'error' => 'empty password', 'log' => $log);
	}
	$which = trim((string) shell_exec('command -v mysqldump 2>/dev/null') ?: '');
	if ($which === '') {
		return array('ok' => false, 'method' => $method, 'error' => 'mysqldump not found', 'log' => $log);
	}
	$defaults = tempnam(sys_get_temp_dir(), 'epc-my-');
	$cnf = "[client]\nhost=" . $creds['host'] . "\nuser=" . $creds['user'] . "\npassword=" . $creds['password'] . "\n";
	file_put_contents($defaults, $cnf);
	@chmod($defaults, 0600);
	$cmd = escapeshellarg($which)
		. ' --defaults-extra-file=' . escapeshellarg($defaults)
		. ' --single-transaction --routines --triggers --events'
		. ' ' . escapeshellarg($creds['db'])
		. ' > ' . escapeshellarg($outSql) . ' 2>&1';
	@exec($cmd, $out, $code);
	@unlink($defaults);
	$log[] = 'mysqldump exit ' . $code;
	if ($code === 0 && is_file($outSql) && filesize($outSql) > 100) {
		return array('ok' => true, 'method' => 'mysqldump', 'log' => $log);
	}
	return array('ok' => false, 'method' => $method, 'error' => implode("\n", array_slice($out, 0, 10)), 'log' => $log);
}

function epc_pfb_dump_database(array $creds, string $label, string $sessionDir): array
{
	$sqlPlain = $sessionDir . '/' . $label . '-database.sql';
	$sqlGz = $sessionDir . '/' . $label . '-database.sql.gz';
	$dumpMeta = array('db' => $creds['db'], 'user' => $creds['user'], 'host' => $creds['host']);

	$md = epc_pfb_mysqldump($creds, $sqlPlain);
	$dumpMeta['dump_method'] = $md['method'];
	if (empty($md['ok'])) {
		if ($creds['password'] === '') {
			throw new RuntimeException("Cannot dump {$label}: missing DB password");
		}
		$pdo = new PDO(
			'mysql:host=' . $creds['host'] . ';dbname=' . $creds['db'] . ';charset=utf8',
			$creds['user'],
			$creds['password'],
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$dumpMeta['dump_method'] = 'php';
		$dumpMeta['source_sql_bytes'] = epc_pfb_dump_sql_php($pdo, $sqlPlain);
	} else {
		$dumpMeta['source_sql_bytes'] = (int) filesize($sqlPlain);
	}

	if (!epc_pfb_gzip_file($sqlPlain, $sqlGz)) {
		throw new RuntimeException("Cannot gzip {$label} database dump");
	}
	@unlink($sqlPlain);

	return array(
		'file' => basename($sqlGz),
		'size' => filesize($sqlGz),
		'sha256' => hash_file('sha256', $sqlGz),
		'dump_method' => $dumpMeta['dump_method'],
		'db_name' => $creds['db'],
		'db_user' => $creds['user'],
		'source_sql_bytes' => $dumpMeta['source_sql_bytes'] ?? 0,
	);
}

function epc_pfb_tar_docroot(string $docroot, string $tarGzPath, array $excludePrefixes): array
{
	if (!is_dir($docroot)) {
		return array('ok' => false, 'error' => 'docroot missing', 'path' => $docroot);
	}
	$excludeArgs = array();
	foreach ($excludePrefixes as $prefix) {
		$excludeArgs[] = '--exclude=' . $prefix;
	}
	$cmd = 'tar -czf ' . escapeshellarg($tarGzPath)
		. ' -C ' . escapeshellarg($docroot) . ' '
		. implode(' ', $excludeArgs) . ' . 2>&1';
	@exec($cmd, $out, $code);
	return array(
		'ok' => ($code === 0 && is_file($tarGzPath)),
		'code' => $code,
		'path' => $docroot,
		'output' => implode("\n", array_slice($out, 0, 20)),
		'file' => basename($tarGzPath),
		'size' => is_file($tarGzPath) ? filesize($tarGzPath) : 0,
		'sha256' => is_file($tarGzPath) ? hash_file('sha256', $tarGzPath) : '',
	);
}

function epc_pfb_nginx_backup(string $sessionDir): ?array
{
	$sources = array('/etc/nginx/sites-enabled', '/etc/nginx/conf.d');
	$nginxDir = $sessionDir . '/nginx-config';
	@mkdir($nginxDir, 0700, true);
	$copied = 0;
	foreach ($sources as $src) {
		if (!is_dir($src) || !is_readable($src)) {
			continue;
		}
		$dest = $nginxDir . '/' . basename($src);
		@mkdir($dest, 0700, true);
		$items = @scandir($src);
		if ($items === false) {
			continue;
		}
		foreach ($items as $item) {
			if ($item === '.' || $item === '..') {
				continue;
			}
			$from = $src . '/' . $item;
			if (!is_file($from) || !is_readable($from)) {
				continue;
			}
			if (@copy($from, $dest . '/' . $item)) {
				$copied++;
			}
		}
	}
	if ($copied === 0) {
		$grepFile = $nginxDir . '/grep-server-names.txt';
		@exec('grep -R "server_name" /etc/nginx/sites-enabled/ /etc/nginx/conf.d/ 2>/dev/null | head -80', $lines);
		if (!empty($lines)) {
			file_put_contents($grepFile, implode("\n", $lines) . "\n");
			$copied = 1;
		}
	}
	if ($copied === 0) {
		return null;
	}
	$tarPath = $sessionDir . '/nginx-sites-enabled.tar.gz';
	$cmd = 'tar -czf ' . escapeshellarg($tarPath) . ' -C ' . escapeshellarg($sessionDir) . ' nginx-config 2>&1';
	@exec($cmd, $out, $code);
	if ($code !== 0 || !is_file($tarPath)) {
		return null;
	}
	return array(
		'file' => basename($tarPath),
		'size' => filesize($tarPath),
		'sha256' => hash_file('sha256', $tarPath),
		'files_copied' => $copied,
	);
}

function epc_pfb_cloudpanel_export(string $sessionDir): array
{
	require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
	$out = array();
	foreach (array('site:list' => 'cloudpanel-site-list.txt', 'app:list' => 'cloudpanel-app-list.txt') as $sub => $file) {
		$r = epc_clp_run($sub);
		$path = $sessionDir . '/' . $file;
		$body = "command: {$r['cmd']}\nexit: {$r['code']}\n\n{$r['output']}\n";
		file_put_contents($path, $body);
		$out[$sub] = array(
			'file' => $file,
			'size' => filesize($path),
			'sha256' => hash_file('sha256', $path),
			'exit_code' => $r['code'],
		);
	}
	return $out;
}

function epc_pfb_tenant_registry_export(string $sessionDir): ?array
{
	$platform = epc_pfb_platform_db_credentials();
	if ($platform['password'] === '') {
		return null;
	}
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	$pdo = new PDO(
		'mysql:host=' . $platform['host'] . ';dbname=' . $platform['db'] . ';charset=utf8',
		$platform['user'],
		$platform['password'],
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_portal_db_ensure($pdo);
	$tenants = $pdo->query('SELECT * FROM `epc_portal_tenants` ORDER BY site_key')->fetchAll(PDO::FETCH_ASSOC);
	$safe = array();
	foreach ($tenants as $row) {
		if (isset($row['db_password'])) {
			$row['db_password'] = '[REDACTED]';
		}
		$safe[] = $row;
	}
	$path = $sessionDir . '/tenant-registry-export.json';
	file_put_contents($path, json_encode($safe, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
	return array(
		'file' => basename($path),
		'size' => filesize($path),
		'sha256' => hash_file('sha256', $path),
		'tenant_count' => count($safe),
	);
}

function epc_pfb_status(): array
{
	$platform = epc_pfb_platform_db_credentials();
	$tenant = epc_pfb_tenant_db_credentials();
	$docroots = array();
	foreach (epc_pfb_docroots() as $key => $path) {
		$docroots[$key] = array(
			'path' => $path,
			'exists' => is_dir($path),
			'size_estimate' => is_dir($path) ? epc_pfb_dir_size($path) : 0,
		);
	}
	$serverIp = '';
	@exec('curl -4 -s --max-time 5 ifconfig.me 2>/dev/null', $ipOut, $ipCode);
	if ($ipCode === 0 && !empty($ipOut[0])) {
		$serverIp = trim($ipOut[0]);
	}
	return array(
		'ok' => true,
		'mode' => 'status',
		'generated_utc' => gmdate('c'),
		'http_host' => (string) ($_SERVER['HTTP_HOST'] ?? ''),
		'php_docroot' => __DIR__,
		'server_public_ip' => $serverIp,
		'backup_root' => epc_pfb_backup_root(),
		'session_name_pattern' => 'platform-full-YYYYMMDD-HHMMSS',
		'platform_db' => array('db' => $platform['db'], 'user' => $platform['user'], 'password_set' => $platform['password'] !== ''),
		'tenant_db' => array('db' => $tenant['db'], 'user' => $tenant['user'], 'password_set' => $tenant['password'] !== ''),
		'docroots' => $docroots,
		'nginx_readable' => is_readable('/etc/nginx/sites-enabled'),
		'mysqldump' => trim((string) shell_exec('command -v mysqldump 2>/dev/null') ?: '') !== '',
		'run_url' => 'epc-platform-full-backup.php?token=REDACTED&apply=1',
		'note' => 'Nothing is deleted on the server. Backups accumulate under backup_root.',
	);
}

function epc_pfb_dir_size(string $dir): int
{
	$total = 0;
	$iter = new RecursiveIteratorIterator(
		new RecursiveDirectoryIterator($dir, FilesystemIterator::SKIP_DOTS),
		RecursiveIteratorIterator::CHILD_FIRST
	);
	$limit = 50000;
	$n = 0;
	foreach ($iter as $file) {
		if ($file->isFile()) {
			$total += (int) $file->getSize();
		}
		if (++$n > $limit) {
			break;
		}
	}
	return $total;
}

function epc_pfb_restore_readme(array $manifest): string
{
	$ip = $manifest['inventory']['server_public_ip'] ?? '31.97.216.247';
	$session = $manifest['session'] ?? '';
	$lines = array();
	$lines[] = 'ECOM AE — Platform full backup (Model C)';
	$lines[] = 'Session: ' . $session;
	$lines[] = 'Created UTC: ' . ($manifest['created_utc'] ?? '');
	$lines[] = 'Server path: ' . ($manifest['session_dir'] ?? '');
	$lines[] = '';
	$lines[] = 'KEEP PRIVATE — database dumps contain secrets.';
	$lines[] = '';
	$lines[] = 'Restore order (full disaster recovery):';
	$lines[] = '1. Provision Ubuntu + CloudPanel (or match PHP/MySQL versions from MANIFEST.json).';
	$lines[] = '2. Import ecomae-database.sql.gz → MySQL database ecomae (Super CP registry).';
	$lines[] = '   gunzip -c ecomae-database.sql.gz | mysql -u ecomae -p ecomae';
	$lines[] = '3. Import docpart-database.sql.gz → MySQL database docpart (tenant storefronts).';
	$lines[] = '   gunzip -c docpart-database.sql.gz | mysql -u docpart -p docpart';
	$lines[] = '4. Extract www_ecomae_com-docroot.tar.gz → /home/ecomae/htdocs/www.ecomae.com';
	$lines[] = '   tar -xzf www_ecomae_com-docroot.tar.gz -C /home/ecomae/htdocs/www.ecomae.com';
	$lines[] = '5. If present: extract cp_ecomae_com-docroot.tar.gz → /home/ecomae/htdocs/cp.ecomae.com';
	$lines[] = '6. Restore nginx-sites-enabled.tar.gz or recreate vhosts in CloudPanel (see cloudpanel-site-list.txt).';
	$lines[] = '7. Verify config.local.php passwords; restart PHP-FPM + nginx.';
	$lines[] = '8. Test: https://www.ecomae.com/ (Super CP), tenant sites, /cp/, /erp.';
	$lines[] = '';
	$lines[] = 'Server IP (current): ' . $ip;
	$lines[] = 'Docroot: /home/ecomae/htdocs/www.ecomae.com';
	$lines[] = '';
	$lines[] = 'Re-run backup: epc-platform-full-backup.php?token=...&apply=1';
	$lines[] = 'Download artifacts: mode=download&session=' . $session . '&file=FILENAME';
	$lines[] = '';
	$lines[] = 'DO NOT delete server backups until local copy verified (sha256 in MANIFEST.json).';
	return implode("\n", $lines) . "\n";
}

$backupRoot = epc_pfb_backup_root();

if ($mode === 'list') {
	$dirs = glob($backupRoot . '/platform-full-*', GLOB_ONLYDIR) ?: array();
	$items = array();
	foreach ($dirs as $dir) {
		$manifest = $dir . '/MANIFEST.json';
		$files = glob($dir . '/*') ?: array();
		$total = 0;
		foreach ($files as $f) {
			if (is_file($f)) {
				$total += filesize($f);
			}
		}
		$items[] = array(
			'session' => basename($dir),
			'dir' => $dir,
			'manifest' => is_file($manifest),
			'total_bytes' => $total,
			'modified_utc' => gmdate('c', filemtime($dir)),
		);
	}
	usort($items, static function ($a, $b) {
		return strcmp($b['session'], $a['session']);
	});
	epc_pfb_json(array('ok' => true, 'backup_root' => $backupRoot, 'sessions' => $items));
}

if ($mode === 'download') {
	$file = basename((string) ($_GET['file'] ?? ''));
	$session = basename((string) ($_GET['session'] ?? ''));
	if ($file === '' || $session === '' || preg_match('/[^a-zA-Z0-9_.-]/', $file) || preg_match('/[^a-zA-Z0-9_.-]/', $session)) {
		epc_pfb_json(array('ok' => false, 'error' => 'Invalid file or session'), 400);
	}
	$path = $backupRoot . '/' . $session . '/' . $file;
	if (!is_file($path)) {
		epc_pfb_json(array('ok' => false, 'error' => 'Not found'), 404);
	}
	header('Content-Type: application/octet-stream');
	header('Content-Length: ' . filesize($path));
	header('Content-Disposition: attachment; filename="' . $file . '"');
	readfile($path);
	exit;
}

if ($mode === 'manifest') {
	$session = basename((string) ($_GET['session'] ?? ''));
	$path = $backupRoot . '/' . $session . '/MANIFEST.json';
	if (!is_file($path)) {
		epc_pfb_json(array('ok' => false, 'error' => 'Manifest not found'), 404);
	}
	readfile($path);
	exit;
}

if ($mode === 'status') {
	epc_pfb_json(epc_pfb_status());
}

if ($mode !== 'create') {
	epc_pfb_json(array('ok' => false, 'error' => 'Unknown mode'), 400);
}

if (!$apply) {
	epc_pfb_json(array(
		'ok' => false,
		'error' => 'Refusing create without apply=1',
		'preview' => epc_pfb_status(),
	), 400);
}

try {
	$inventory = epc_pfb_status();
	$sessionDir = epc_pfb_session_dir();
	if (!is_dir($sessionDir)) {
		@mkdir($sessionDir, 0700, true);
	}
	$sessionName = basename($sessionDir);
	$artifacts = array();

	$platformCreds = epc_pfb_platform_db_credentials();
	$tenantCreds = epc_pfb_tenant_db_credentials();
	$artifacts['ecomae_database'] = epc_pfb_dump_database($platformCreds, 'ecomae', $sessionDir);
	$artifacts['docpart_database'] = epc_pfb_dump_database($tenantCreds, 'docpart', $sessionDir);

	$exclude = array('.epc-backups', 'cp/tmp', 'content/files/cache', 'content/files/tmp');
	foreach (epc_pfb_docroots() as $key => $path) {
		if (!is_dir($path)) {
			continue;
		}
		$tarName = $key . '-docroot.tar.gz';
		$tarPath = $sessionDir . '/' . $tarName;
		$tar = epc_pfb_tar_docroot($path, $tarPath, $exclude);
		if (!empty($tar['ok'])) {
			$artifacts[$key . '_files'] = array(
				'file' => $tar['file'],
				'path' => $path,
				'size' => $tar['size'],
				'sha256' => $tar['sha256'],
			);
		} else {
			$artifacts[$key . '_files'] = array('ok' => false, 'path' => $path, 'error' => $tar['error'] ?? $tar['output'] ?? 'tar failed');
		}
	}

	$nginx = epc_pfb_nginx_backup($sessionDir);
	if ($nginx !== null) {
		$artifacts['nginx'] = $nginx;
	}

	$clp = epc_pfb_cloudpanel_export($sessionDir);
	foreach ($clp as $k => $v) {
		$artifacts['cloudpanel_' . str_replace(':', '_', $k)] = $v;
	}

	$registry = epc_pfb_tenant_registry_export($sessionDir);
	if ($registry !== null) {
		$artifacts['tenant_registry'] = $registry;
	}

	$readmePath = $sessionDir . '/RESTORE-README.txt';
	$manifest = array(
		'ok' => true,
		'session' => $sessionName,
		'session_dir' => $sessionDir,
		'created_utc' => gmdate('c'),
		'inventory' => $inventory,
		'artifacts' => $artifacts,
		'restore_steps' => array(
			'import_ecomae' => 'gunzip -c ecomae-database.sql.gz | mysql -u ecomae -p ecomae',
			'import_docpart' => 'gunzip -c docpart-database.sql.gz | mysql -u docpart -p docpart',
			'extract_platform_docroot' => 'tar -xzf www_ecomae_com-docroot.tar.gz -C /home/ecomae/htdocs/www.ecomae.com',
			'extract_cp_docroot' => 'tar -xzf cp_ecomae_com-docroot.tar.gz -C /home/ecomae/htdocs/cp.ecomae.com',
			'nginx' => 'Extract nginx-sites-enabled.tar.gz or rebuild vhosts from cloudpanel-site-list.txt',
		),
	);
	file_put_contents($readmePath, epc_pfb_restore_readme($manifest));
	$artifacts['restore_readme'] = array(
		'file' => basename($readmePath),
		'size' => filesize($readmePath),
	);

	$totalBytes = 0;
	foreach (glob($sessionDir . '/*') ?: array() as $f) {
		if (is_file($f)) {
			$totalBytes += filesize($f);
		}
	}
	$manifest['total_bytes'] = $totalBytes;
	$manifest['restore_readme_text'] = file_get_contents($readmePath);
	file_put_contents($sessionDir . '/MANIFEST.json', json_encode($manifest, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));

	epc_pfb_json($manifest);
} catch (Exception $e) {
	epc_pfb_json(array('ok' => false, 'error' => $e->getMessage()), 500);
}
