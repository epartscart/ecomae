<?php
/**
 * Pre-VPS-removal inventory + backup for epartscart.com migration to ecomae platform.
 * https://www.ecomae.com/epc-epartscart-vps-backup.php?token=epartscart-deploy-2026
 * https://www.epartscart.com/epc-epartscart-vps-backup.php?token=epartscart-deploy-2026&mode=create
 *
 * Modes: inventory (default), create, list, download, manifest
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

@set_time_limit(0);
@ini_set('memory_limit', '2048M');

header('Content-Type: application/json; charset=utf-8');

$mode = strtolower(trim((string) ($_GET['mode'] ?? 'inventory')));
$stamp = gmdate('Ymd-His');
$dateDir = gmdate('Ymd');

function epc_mig_json($payload, int $status = 200): void
{
	http_response_code($status);
	echo json_encode($payload, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

function epc_mig_backup_roots(): array
{
	$candidates = array(
		'/home/ecomae/backups',
		'/home/epartscart/backups',
		__DIR__ . '/.epc-backups',
	);
	foreach ($candidates as $root) {
		if (is_dir($root) && is_writable($root)) {
			return array($root, $root . '/epartscart-migration-' . gmdate('Ymd-His'));
		}
	}
	$fallback = __DIR__ . '/.epc-backups';
	if (!is_dir($fallback)) {
		@mkdir($fallback, 0700, true);
	}
	return array($fallback, $fallback . '/epartscart-migration-' . gmdate('Ymd-His'));
}

function epc_mig_tenant_db_credentials(): array
{
	$defaults = array(
		'host' => '127.0.0.1',
		'db' => 'docpart',
		'user' => 'docpart',
		'password' => '',
	);

	if (is_file('/home/ecomae/htdocs/www.ecomae.com/config.local.php')) {
		$epc_config_local = null;
		require '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
		if (!empty($epc_config_local['password'])) {
			try {
				require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
				$pdo = new PDO(
					'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
					'ecomae',
					(string) $epc_config_local['password'],
					array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
				);
				epc_portal_db_ensure($pdo);
				$st = $pdo->prepare('SELECT db_name, db_user, db_password FROM `epc_portal_tenants` WHERE site_key = ? LIMIT 1');
				$st->execute(array('epartscart'));
				$row = $st->fetch(PDO::FETCH_ASSOC);
				if ($row && (string) $row['db_password'] !== '') {
					return array(
						'host' => '127.0.0.1',
						'db' => (string) $row['db_name'],
						'user' => (string) $row['db_user'],
						'password' => (string) $row['db_password'],
					);
				}
			} catch (Exception $e) {
				// fall through
			}
		}
	}

	foreach (array(
		'/home/epartscart/htdocs/www.epartscart.com/config.local.php',
		'/home/epartscart/htdocs/www.epartscart.com/config.php',
	) as $path) {
		if (!is_file($path)) {
			continue;
		}
		$epc_config_local = null;
		$cfg = null;
		require $path;
		if (isset($epc_config_local) && is_array($epc_config_local)) {
			foreach (array('host', 'db', 'user', 'password') as $key) {
				if (!empty($epc_config_local[$key])) {
					$defaults[$key] = (string) $epc_config_local[$key];
				}
			}
		}
		if (isset($cfg) && is_object($cfg) && (string) $cfg->db === 'docpart') {
			$defaults['host'] = (string) $cfg->host;
			$defaults['db'] = (string) $cfg->db;
			$defaults['user'] = (string) $cfg->user;
			if ((string) $cfg->password !== '') {
				$defaults['password'] = (string) $cfg->password;
			}
		}
	}

	if ($defaults['password'] === '' && is_file(__DIR__ . '/config.php')) {
		if (!defined('_ASTEXE_')) {
			define('_ASTEXE_', 1);
		}
		$cfg = null;
		require __DIR__ . '/config.php';
		if (isset($cfg) && is_object($cfg) && (string) $cfg->db === 'docpart') {
			$defaults['host'] = (string) $cfg->host;
			$defaults['db'] = (string) $cfg->db;
			$defaults['user'] = (string) $cfg->user;
			$defaults['password'] = (string) $cfg->password;
		}
	}

	return $defaults;
}

function epc_mig_tenant_pdo(): PDO
{
	$creds = epc_mig_tenant_db_credentials();
	if ($creds['password'] === '') {
		throw new RuntimeException('Cannot resolve docpart DB credentials');
	}
	return new PDO(
		'mysql:host=' . $creds['host'] . ';dbname=' . $creds['db'] . ';charset=utf8',
		$creds['user'],
		$creds['password'],
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

function epc_mig_load_cfg(): array
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	require_once __DIR__ . '/config.php';
	$cfg = new DP_Config();
	if (is_file(__DIR__ . '/config.local.php')) {
		$epc_config_local = null;
		require __DIR__ . '/config.local.php';
		if (isset($epc_config_local) && is_array($epc_config_local)) {
			foreach ($epc_config_local as $key => $value) {
				if (property_exists($cfg, $key)) {
					$cfg->$key = $value;
				}
			}
		}
	}
	return array($cfg, array(
		'host' => (string) $cfg->host,
		'db' => (string) $cfg->db,
		'user' => (string) $cfg->user,
		'password' => (string) $cfg->password,
		'domain_path' => (string) $cfg->domain_path,
	));
}

function epc_mig_docroots(): array
{
	$paths = array(
		'epartscart_vps' => '/home/epartscart/htdocs/www.epartscart.com',
		'ecomae_platform' => '/home/ecomae/htdocs/www.ecomae.com',
		'ecomae_cp' => '/home/ecomae/htdocs/cp.ecomae.com',
	);
	$out = array();
	foreach ($paths as $key => $path) {
		$out[$key] = array(
			'path' => $path,
			'exists' => is_dir($path),
			'writable' => is_dir($path) && is_writable($path),
			'index_php' => is_file($path . '/index.php'),
		);
	}
	return $out;
}

function epc_mig_nginx_inventory(): array
{
	$hosts = array('www.epartscart.com', 'epartscart.com', 'www.ecomae.com', 'ecomae.com', 'www.taxofinca.com');
	$lines = array();
	@exec('grep -R "server_name" /etc/nginx/sites-enabled/ /etc/nginx/conf.d/ 2>/dev/null | grep -Ei "epartscart|ecomae|taxofinca" | head -40', $lines);
	return array(
		'grep_hits' => $lines,
		'ssl_dirs' => array(
			'/etc/nginx/ssl-certificates/www.epartscart.com.crt' => is_file('/etc/nginx/ssl-certificates/www.epartscart.com.crt'),
			'/etc/nginx/ssl-certificates/www.ecomae.com.crt' => is_file('/etc/nginx/ssl-certificates/www.ecomae.com.crt'),
		),
	);
}

function epc_mig_dns_hints(): array
{
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
	$platformIp = epc_portal_platform_ip();
	$resolve = array();
	foreach (array('www.epartscart.com', 'epartscart.com', 'www.ecomae.com') as $host) {
		$ip = @gethostbyname($host);
		$resolve[$host] = ($ip === $host) ? 'unresolved' : $ip;
	}
	return array(
		'platform_ip_target' => $platformIp,
		'public_resolve' => $resolve,
		'cloudflare_proxied' => (strpos((string) ($resolve['www.epartscart.com'] ?? ''), '104.') === 0
			|| strpos((string) ($resolve['www.epartscart.com'] ?? ''), '172.') === 0),
	);
}

function epc_mig_tenant_export(?PDO $platformPdo, PDO $tenantPdo): array
{
	$tenant = null;
	$siteSettings = array();
	if ($platformPdo instanceof PDO) {
		require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
		epc_portal_db_ensure($platformPdo);
		$st = $platformPdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? OR `hostname` LIKE ? LIMIT 1');
		$st->execute(array('epartscart', '%epartscart.com'));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			$safe = $row;
			if (isset($safe['db_password'])) {
				$safe['db_password'] = '[REDACTED]';
			}
			$tenant = $safe;
		}
	}
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	epc_portal_db_ensure($tenantPdo);
	$st2 = $tenantPdo->prepare('SELECT * FROM `epc_portal_site_settings` WHERE `host` LIKE ? LIMIT 5');
	$st2->execute(array('%epartscart.com%'));
	$siteSettings = $st2->fetchAll(PDO::FETCH_ASSOC);
	return array('tenant_registry' => $tenant, 'site_settings_rows' => $siteSettings);
}

function epc_mig_sql_value(PDO $pdo, $value): string
{
	return $value === null ? 'NULL' : $pdo->quote((string) $value);
}

function epc_mig_dump_sql(PDO $pdo, string $sqlPath): int
{
	$fh = fopen($sqlPath, 'wb');
	if (!$fh) {
		throw new RuntimeException('Cannot create SQL dump');
	}
	fwrite($fh, "-- epartscart migration backup\n-- Created: " . gmdate('c') . "\n");
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
				$values[] = epc_mig_sql_value($pdo, $value);
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

function epc_mig_gzip_file(string $src, string $dest): bool
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

function epc_mig_tar_docroot(string $docroot, string $tarGzPath, array $excludePrefixes): array
{
	if (!is_dir($docroot)) {
		return array('ok' => false, 'error' => 'docroot missing');
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
		'output' => implode("\n", array_slice($out, 0, 20)),
		'size' => is_file($tarGzPath) ? filesize($tarGzPath) : 0,
		'sha256' => is_file($tarGzPath) ? hash_file('sha256', $tarGzPath) : '',
	);
}

function epc_mig_inventory(): array
{
	list($cfg,) = epc_mig_load_cfg();
	$docroots = epc_mig_docroots();
	$nginx = epc_mig_nginx_inventory();
	$dns = epc_mig_dns_hints();

	$tenantCreds = epc_mig_tenant_db_credentials();
	$tenantPdo = null;
	$platformPdo = null;
	$tenantDbError = null;
	$platformDbError = null;

	try {
		$tenantPdo = epc_mig_tenant_pdo();
	} catch (Exception $e) {
		$tenantDbError = $e->getMessage();
	}

	if (is_file('/home/ecomae/htdocs/www.ecomae.com/config.local.php')) {
		$epc_config_local = null;
		require '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
		if (!empty($epc_config_local['password'])) {
			try {
				$platformPdo = new PDO(
					'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
					'ecomae',
					(string) $epc_config_local['password'],
					array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
				);
			} catch (Exception $e) {
				$platformDbError = $e->getMessage();
			}
		}
	}

	$tenantExport = ($tenantPdo instanceof PDO)
		? epc_mig_tenant_export($platformPdo, $tenantPdo)
		: array();

	$serverIp = '';
	@exec('curl -4 -s --max-time 5 ifconfig.me 2>/dev/null', $ipOut, $ipCode);
	if ($ipCode === 0 && !empty($ipOut[0]) && filter_var(trim($ipOut[0]), FILTER_VALIDATE_IP)) {
		$serverIp = trim($ipOut[0]);
	}

	return array(
		'ok' => true,
		'generated_utc' => gmdate('c'),
		'http_host' => (string) ($_SERVER['HTTP_HOST'] ?? ''),
		'php_docroot' => __DIR__,
		'server_public_ip' => $serverIp,
		'config' => array(
			'domain_path' => (string) $cfg->domain_path,
			'db_host' => (string) $cfg->host,
			'db_name' => (string) $cfg->db,
			'db_user' => (string) $cfg->user,
		),
		'docroots' => $docroots,
		'nginx' => $nginx,
		'dns' => $dns,
		'tenant_db' => array(
			'host' => $tenantCreds['host'],
			'db' => $tenantCreds['db'],
			'user' => $tenantCreds['user'],
		),
		'tenant_export' => $tenantExport,
		'tenant_db_error' => $tenantDbError ?? null,
		'platform_db_error' => $platformDbError ?? null,
	);
}

list($backupRoot,) = epc_mig_backup_roots();

if ($mode === 'list') {
	$dirs = glob($backupRoot . '/epartscart-migration-*', GLOB_ONLYDIR) ?: array();
	$items = array();
	foreach ($dirs as $dir) {
		$manifest = $dir . '/MANIFEST.json';
		$items[] = array(
			'dir' => $dir,
			'manifest' => is_file($manifest),
			'size_bytes' => array_sum(array_map('filesize', glob($dir . '/*') ?: array())),
			'modified_utc' => gmdate('c', filemtime($dir)),
		);
	}
	epc_mig_json(array('ok' => true, 'backup_root' => $backupRoot, 'sessions' => $items));
}

if ($mode === 'download') {
	$file = basename((string) ($_GET['file'] ?? ''));
	$session = basename((string) ($_GET['session'] ?? ''));
	if ($file === '' || $session === '' || preg_match('/[^a-zA-Z0-9_.-]/', $file) || preg_match('/[^a-zA-Z0-9_.-]/', $session)) {
		epc_mig_json(array('ok' => false, 'error' => 'Invalid file or session'), 400);
	}
	$path = $backupRoot . '/' . $session . '/' . $file;
	if (!is_file($path)) {
		epc_mig_json(array('ok' => false, 'error' => 'Not found'), 404);
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
		epc_mig_json(array('ok' => false, 'error' => 'Manifest not found'), 404);
	}
	readfile($path);
	exit;
}

if ($mode === 'inventory') {
	epc_mig_json(epc_mig_inventory());
}

if ($mode !== 'create') {
	epc_mig_json(array('ok' => false, 'error' => 'Unknown mode'), 400);
}

try {
	$inventory = epc_mig_inventory();
	list(, $sessionDir) = epc_mig_backup_roots();
	if (!is_dir($sessionDir)) {
		@mkdir($sessionDir, 0700, true);
	}
	$sessionName = basename($sessionDir);

	list($cfg,) = epc_mig_load_cfg();
	$tenantCreds = epc_mig_tenant_db_credentials();
	$pdo = epc_mig_tenant_pdo();

	$artifacts = array();
	$sqlPlain = $sessionDir . '/docpart-database.sql';
	$sqlGz = $sessionDir . '/docpart-database.sql.gz';
	$sqlBytes = epc_mig_dump_sql($pdo, $sqlPlain);
	if (epc_mig_gzip_file($sqlPlain, $sqlGz)) {
		@unlink($sqlPlain);
	$artifacts['database'] = array(
			'file' => basename($sqlGz),
			'size' => filesize($sqlGz),
			'sha256' => hash_file('sha256', $sqlGz),
			'source_sql_bytes' => $sqlBytes,
			'db_name' => $tenantCreds['db'],
			'db_user' => $tenantCreds['user'],
		);
	}

	$exclude = array(
		'.epc-backups',
		'cp/tmp',
		'content/files/cache',
		'content/files/tmp',
		'lib/vendor',
	);
	foreach ($inventory['docroots'] as $key => $info) {
		if (empty($info['exists'])) {
			continue;
		}
		$tarName = $key . '-docroot.tar.gz';
		$tarPath = $sessionDir . '/' . $tarName;
		$tar = epc_mig_tar_docroot($info['path'], $tarPath, $exclude);
		if (!empty($tar['ok'])) {
			$artifacts[$key . '_files'] = array(
				'file' => $tarName,
				'path' => $info['path'],
				'size' => $tar['size'],
				'sha256' => $tar['sha256'],
			);
		}
	}

	$tenantJsonPath = $sessionDir . '/tenant-config-export.json';
	file_put_contents($tenantJsonPath, json_encode($inventory['tenant_export'], JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
	$artifacts['tenant_config'] = array(
		'file' => basename($tenantJsonPath),
		'size' => filesize($tenantJsonPath),
		'sha256' => hash_file('sha256', $tenantJsonPath),
	);

	$restoreTxt = $sessionDir . '/RESTORE-INSTRUCTIONS.txt';
	$restore = "epartscart.com — VPS removal migration backup\n";
	$restore .= 'Created UTC: ' . gmdate('c') . "\n\n";
	$restore .= "Restore order:\n";
	$restore .= "1. Import docpart-database.sql.gz: gunzip -c docpart-database.sql.gz | mysql -u USER -p docpart\n";
	$restore .= "2. Extract *-docroot.tar.gz to /home/ecomae/htdocs/www.ecomae.com (platform Model C)\n";
	$restore .= "3. Verify config.php + config.local.php (db=docpart, domain_path=https://www.epartscart.com/)\n";
	$restore .= "4. Run epc-epartscart-supercp-cutover.php?apply=1 on ecomae server\n";
	$restore .= "5. Point GoDaddy A @ and www → " . ($inventory['dns']['platform_ip_target'] ?? '31.97.216.247') . "\n";
	$restore .= "6. Probe https://www.epartscart.com/ /cp/ /erp before deleting old VPS\n\n";
	$restore .= "DO NOT delete VPS until all probes PASS and DNS propagated.\n";
	file_put_contents($restoreTxt, $restore);
	$artifacts['restore_instructions'] = array('file' => basename($restoreTxt), 'size' => filesize($restoreTxt));

	$manifest = array(
		'ok' => true,
		'session' => $sessionName,
		'session_dir' => $sessionDir,
		'created_utc' => gmdate('c'),
		'inventory' => $inventory,
		'artifacts' => $artifacts,
		'restore' => file_get_contents($restoreTxt),
	);
	file_put_contents($sessionDir . '/MANIFEST.json', json_encode($manifest, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));

	epc_mig_json($manifest);
} catch (Exception $e) {
	epc_mig_json(array('ok' => false, 'error' => $e->getMessage()), 500);
}
