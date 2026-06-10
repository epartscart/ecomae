<?php
/**
 * Replace WordPress on www.taxofinca.com with e-world portal (same VPS as epartscart).
 * https://www.epartscart.com/epc-taxofinca-replace-portal.php?token=epartscart-deploy-2026
 * Optional: &db_password=YOUR_TAXOFINCA_DB_PASSWORD
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$src = __DIR__;
$dbPassword = trim((string) ($_GET['db_password'] ?? ''));
$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: getenv('EPC_CLP_PASS') ?: ''));

function epc_tf_clp_request(string $url, array $opts, string &$cookieJar): string
{
	$method = isset($opts['method']) ? $opts['method'] : 'GET';
	$body = isset($opts['body']) ? $opts['body'] : '';
	$timeout = isset($opts['timeout']) ? (int) $opts['timeout'] : 60;
	$headers = $cookieJar !== '' ? ("Cookie: {$cookieJar}\r\n") : '';
	if ($body !== '') {
		$headers .= "Content-Type: application/x-www-form-urlencoded\r\n";
	}
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => $method,
			'header' => $headers,
			'content' => $body,
			'timeout' => $timeout,
			'ignore_errors' => true,
		),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$resp = @file_get_contents($url, false, $ctx);
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $line) {
			if (stripos($line, 'Set-Cookie:') !== 0) {
				continue;
			}
			$part = trim(substr($line, 11));
			$semi = strpos($part, ';');
			if ($semi !== false) {
				$part = substr($part, 0, $semi);
			}
			$name = strtok($part, '=');
			$cookieJar = trim(preg_replace('/(?:^|;\\s*)' . preg_quote($name, '/') . '=[^;]*/', '', $cookieJar));
			$cookieJar = trim($cookieJar . '; ' . $part, '; ');
		}
	}
	return $resp === false ? '' : $resp;
}

function epc_tf_clp_login(string $clpUser, string $clpPass, string &$cookie): bool
{
	$panel = 'https://127.0.0.1:8443';
	$cookie = '';
	$loginHtml = epc_tf_clp_request($panel . '/login', array(), $cookie);
	if ($loginHtml === '' || !preg_match('/name="_csrf_token" value="([^"]+)"/', $loginHtml, $m)) {
		return false;
	}
	epc_tf_clp_request($panel . '/login', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'userName' => $clpUser,
			'password' => $clpPass,
			'_csrf_token' => $m[1],
			'submit' => 'Log In',
		)),
	), $cookie);
	return $cookie !== '';
}

function epc_tf_clp_put_file(string &$cookie, string $remoteDir, string $name, string $content): void
{
	$panel = 'https://127.0.0.1:8443';
	epc_tf_clp_request($panel . '/file-manager/backend/makefile', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $remoteDir, 'name' => $name)),
	), $cookie);
	epc_tf_clp_request($panel . '/file-manager/backend/text', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $remoteDir . '/' . $name, 'content' => $content)),
	), $cookie);
}

function epc_tf_clp_detect_remote_dir(string $clpUser, string $clpPass): ?string
{
	$cookie = '';
	if (!epc_tf_clp_login($clpUser, $clpPass, $cookie)) {
		epc_tf_log('CloudPanel login failed during path detect.');
		return null;
	}
	$probe = 'epc-path-probe-' . substr(md5((string) time()), 0, 8) . '.txt';
	$probeBody = 'OK';
	$candidates = array(
		'/htdocs/www.taxofinca.com/public',
		'/htdocs/www.taxofinca.com',
		'/htdocs/taxofinca.com/public',
		'/htdocs/taxofinca.com',
		'/htdocs/taxofinca/public',
		'/htdocs/taxofinca',
		'/home/taxofinca/htdocs/www.taxofinca.com/public',
		'/home/taxofinca/htdocs/www.taxofinca.com',
		'/home/taxofinca/htdocs/taxofinca.com/public',
		'/home/taxofinca/htdocs/taxofinca.com',
	);
	foreach (array('www.taxofinca.com', 'taxofinca.com') as $site) {
		epc_tf_clp_request('https://127.0.0.1:8443/site/' . rawurlencode($site) . '/file-manager', array(), $cookie);
	}
	foreach ($candidates as $dir) {
		epc_tf_clp_put_file($cookie, $dir, $probe, $probeBody);
		$url = 'https://www.taxofinca.com/' . $probe;
		$ctx = stream_context_create(array(
			'http' => array('timeout' => 15, 'header' => "Host: www.taxofinca.com\r\n"),
			'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
		));
		$resp = @file_get_contents($url, false, $ctx);
		if (trim((string) $resp) === 'OK') {
			epc_tf_log('Detected docroot via CloudPanel: ' . $dir);
			return $dir;
		}
		$ctx2 = stream_context_create(array(
			'http' => array('timeout' => 10, 'header' => "Host: www.taxofinca.com\r\n"),
		));
		$resp2 = @file_get_contents('http://127.0.0.1/' . $probe, false, $ctx2);
		if (trim((string) $resp2) === 'OK') {
			epc_tf_log('Detected docroot (localhost): ' . $dir);
			return $dir;
		}
	}
	epc_tf_log('Could not detect taxofinca docroot path.');
	return null;
}

function epc_tf_clp_upload_bootstrap(string $clpUser, string $clpPass, string $remoteDir): bool
{
	$cookie = '';
	if (!epc_tf_clp_login($clpUser, $clpPass, $cookie)) {
		epc_tf_log('CloudPanel login failed.');
		return false;
	}
	epc_tf_clp_request('https://127.0.0.1:8443/site/www.taxofinca.com/file-manager', array(), $cookie);
	$files = array(
		'chunk-receiver.php' => __DIR__ . '/chunk-receiver.php',
		'extract-zip-taxofinca.php' => __DIR__ . '/extract-zip-taxofinca.php',
		'epc_deploy_auth.php' => __DIR__ . '/epc_deploy_auth.php',
	);
	foreach ($files as $name => $path) {
		if (!is_file($path)) {
			continue;
		}
		epc_tf_clp_put_file($cookie, $remoteDir, $name, file_get_contents($path));
		epc_tf_log('CloudPanel uploaded: ' . $name . ' → ' . $remoteDir);
	}
	return true;
}

function epc_tf_clp_write_config(string $clpUser, string $clpPass, string $remoteDir, string $dbPassword): void
{
	$cookie = '';
	if (!epc_tf_clp_login($clpUser, $clpPass, $cookie)) {
		return;
	}
	$template = is_file(__DIR__ . '/config.local.taxofinca.php') ? file_get_contents(__DIR__ . '/config.local.taxofinca.php') : "<?php\n\$epc_config_local = array('password' => '', 'from_name' => 'Taxofinca', 'from_email' => 'info@taxofinca.com');\n";
	if ($dbPassword !== '') {
		$template = preg_replace("/'password'\\s*=>\\s*'[^']*'/", "'password' => " . var_export($dbPassword, true), $template, 1);
	}
	epc_tf_clp_put_file($cookie, $remoteDir, 'config.local.php', $template);
	epc_tf_log('CloudPanel wrote config.local.php');
}

function epc_tf_log(string $msg): void
{
	echo $msg . "\n";
	@flush();
}

function epc_tf_find_taxofinca_root(): ?string
{
	$candidates = array();
	foreach (array(
		'/home/taxofinca/htdocs/www.taxofinca.com',
		'/home/taxofinca/htdocs/www.taxofinca.com/public',
		'/home/taxofinca/htdocs/taxofinca.com',
		'/home/taxofinca/htdocs/taxofinca.com/public',
	) as $path) {
		$candidates[] = $path;
	}
	foreach (glob('/home/*/htdocs/*taxofinca*') ?: array() as $path) {
		$candidates[] = $path;
		$candidates[] = rtrim($path, '/') . '/public';
	}
	foreach (glob('/home/*/htdocs/*/*taxofinca*') ?: array() as $path) {
		$candidates[] = $path;
	}
	if (function_exists('exec')) {
		exec('find /home -maxdepth 5 -name wp-config.php 2>/dev/null', $findOut, $findCode);
		if ($findCode === 0) {
			foreach ($findOut as $line) {
				$line = trim($line);
				if ($line !== '' && stripos($line, 'taxofinca') !== false) {
					$candidates[] = dirname($line);
				}
			}
		}
		exec('grep -r "server_name.*taxofinca" /etc/nginx/ 2>/dev/null | head -20', $nginxOut, $nginxCode);
		if ($nginxCode === 0) {
			foreach ($nginxOut as $line) {
				if (preg_match('/root\\s+([^;]+);/', $line, $m)) {
					$candidates[] = trim($m[1]);
				}
			}
		}
	}
	$seen = array();
	$best = null;
	foreach ($candidates as $path) {
		$path = rtrim($path, '/');
		if ($path === '' || isset($seen[$path]) || !is_dir($path)) {
			continue;
		}
		$seen[$path] = true;
		if (is_file($path . '/wp-config.php') || is_file($path . '/wp-login.php')) {
			return $path;
		}
		if (is_file($path . '/index.php') && $best === null) {
			$best = $path;
		}
	}
	return $best;
}

function epc_tf_rrmdir(string $dir): void
{
	if (!is_dir($dir)) {
		return;
	}
	$it = new RecursiveIteratorIterator(
		new RecursiveDirectoryIterator($dir, FilesystemIterator::SKIP_DOTS),
		RecursiveIteratorIterator::CHILD_FIRST
	);
	foreach ($it as $item) {
		if ($item->isDir()) {
			@rmdir($item->getPathname());
		} else {
			@unlink($item->getPathname());
		}
	}
	@rmdir($dir);
}

function epc_tf_wipe_wordpress(string $dir): array
{
	$removed = array();
	foreach (array('wp-admin', 'wp-content', 'wp-includes') as $sub) {
		$path = $dir . '/' . $sub;
		if (is_dir($path)) {
			epc_tf_rrmdir($path);
			$removed[] = $sub;
		}
	}
	foreach (glob($dir . '/wp-*.php') ?: array() as $file) {
		if (@unlink($file)) {
			$removed[] = basename($file);
		}
	}
	foreach (array('xmlrpc.php', 'license.txt', 'readme.html') as $file) {
		$f = $dir . '/' . $file;
		if (is_file($f) && @unlink($f)) {
			$removed[] = $file;
		}
	}
	return $removed;
}

function epc_tf_exclude_rel(string $rel): bool
{
	$rel = str_replace('\\', '/', $rel);
	if ($rel === 'config.local.php' || $rel === 'config.local.taxofinca.php') {
		return true;
	}
	if (strpos($rel, '/tmp/') !== false || strpos($rel, 'tmp/') === 0) {
		return true;
	}
	if (preg_match('/\.zip$/i', $rel)) {
		return true;
	}
	return false;
}

function epc_tf_mirror(string $from, string $to): array
{
	$stats = array('copied' => 0, 'skipped' => 0, 'errors' => 0);
	if (!is_dir($to) && !@mkdir($to, 0755, true)) {
		throw new RuntimeException('Cannot create destination: ' . $to);
	}
	$from = rtrim($from, '/\\');
	$to = rtrim($to, '/\\');
	$len = strlen($from) + 1;
	$it = new RecursiveIteratorIterator(
		new RecursiveDirectoryIterator($from, FilesystemIterator::SKIP_DOTS),
		RecursiveIteratorIterator::SELF_FIRST
	);
	foreach ($it as $item) {
		$rel = substr($item->getPathname(), $len);
		$rel = str_replace('\\', '/', $rel);
		if (epc_tf_exclude_rel($rel)) {
			$stats['skipped']++;
			continue;
		}
		$dest = $to . '/' . $rel;
		if ($item->isDir()) {
			if (!is_dir($dest) && !@mkdir($dest, 0755, true)) {
				$stats['errors']++;
			}
			continue;
		}
		$parent = dirname($dest);
		if (!is_dir($parent)) {
			@mkdir($parent, 0755, true);
		}
		if (@copy($item->getPathname(), $dest)) {
			@chmod($dest, 0644);
			$stats['copied']++;
		} else {
			$stats['errors']++;
		}
	}
	return $stats;
}

function epc_tf_write_config_local(string $dest, string $dbPassword): bool
{
	$template = __DIR__ . '/config.local.taxofinca.php';
	if (!is_file($template)) {
		$template = __DIR__ . '/stage/config.local.taxofinca.php';
	}
	$content = is_file($template)
		? file_get_contents($template)
		: "<?php\n\$epc_config_local = array('password' => '', 'from_name' => 'Taxofinca', 'from_email' => 'info@taxofinca.com');\n";
	if ($dbPassword !== '') {
		$content = preg_replace(
			"/'password'\\s*=>\\s*'[^']*'/",
			"'password' => " . var_export($dbPassword, true),
			$content,
			1
		);
	}
	return (bool) file_put_contents($dest . '/config.local.php', $content);
}

function epc_tf_clone_db(PDO $srcPdo, string $destDb, string $destUser, string $destPass): bool
{
	$cfg = new DP_Config();
	$destPdo = new PDO(
		'mysql:host=' . $cfg->host . ';charset=utf8',
		$destUser,
		$destPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$destPdo->exec('CREATE DATABASE IF NOT EXISTS `' . str_replace('`', '', $destDb) . '` CHARACTER SET utf8 COLLATE utf8_general_ci');
	$destPdo->exec('USE `' . str_replace('`', '', $destDb) . '`');
	$tables = $destPdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_COLUMN);
	if (count($tables) > 5) {
		epc_tf_log('DB already has ' . count($tables) . ' tables — skip clone.');
		return true;
	}
	$srcDb = $cfg->db;
	$srcTables = $srcPdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_COLUMN);
	epc_tf_log('Cloning ' . count($srcTables) . ' tables docpart → ' . $destDb . ' ...');
	foreach ($srcTables as $table) {
		$destPdo->exec('DROP TABLE IF EXISTS `' . $table . '`');
		$row = $srcPdo->query('SHOW CREATE TABLE `' . $table . '`')->fetch(PDO::FETCH_NUM);
		if (!$row || empty($row[1])) {
			continue;
		}
		$destPdo->exec($row[1]);
		$destPdo->exec('INSERT INTO `' . $table . '` SELECT * FROM `' . $srcDb . '`.`' . $table . '`');
	}
	epc_tf_log('DB clone complete.');
	return true;
}

function epc_tf_http_setup(string $host, string $path): string
{
	$url = 'https://' . $host . $path;
	if (strpos($path, '?') === false) {
		$url .= '?token=' . urlencode(epc_deploy_token());
	} else {
		$url .= '&token=' . urlencode(epc_deploy_token());
	}
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 120, 'header' => "Host: {$host}\r\n"),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$resp = @file_get_contents($url, false, $ctx);
	return $resp === false ? '' : substr($resp, 0, 2000);
}

function epc_tf_deploy_via_chunks(string $host): bool
{
	$zip = '/tmp/docpart-epartscart-site.zip';
	if (!is_file($zip)) {
		return false;
	}
	$token = epc_deploy_token();
	$data = file_get_contents($zip);
	$chunkSize = 150000;
	$header = "Host: {$host}\r\n";
	$idx = 0;
	for ($off = 0; $off < strlen($data); $off += $chunkSize) {
		$part = substr($data, $off, $chunkSize);
		$body = http_build_query(array(
			'token' => $token,
			'index' => (string) $idx,
			'data' => base64_encode($part),
			'final' => ($off + $chunkSize >= strlen($data)) ? '1' : '0',
		));
		$ctx = stream_context_create(array(
			'http' => array(
				'method' => 'POST',
				'header' => $header . "Content-Type: application/x-www-form-urlencoded\r\n",
				'content' => $body,
				'timeout' => 300,
			),
			'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
		));
		$resp = @file_get_contents('http://127.0.0.1/chunk-receiver.php', false, $ctx);
		if (trim((string) $resp) === '') {
			$resp = @file_get_contents('https://' . $host . '/chunk-receiver.php', false, $ctx);
		}
		epc_tf_log("chunk {$idx}: " . trim((string) $resp));
		$idx++;
	}
	$extractCtx = stream_context_create(array(
		'http' => array('timeout' => 300, 'header' => $header),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$extract = @file_get_contents(
		'http://127.0.0.1/extract-zip-taxofinca.php?token=' . urlencode($token),
		false,
		$extractCtx
	);
	if (!is_string($extract) || strpos($extract, 'exit=0') === false) {
		$extract = @file_get_contents(
			'https://' . $host . '/extract-zip-taxofinca.php?token=' . urlencode($token),
			false,
			$extractCtx
		);
	}
	epc_tf_log("extract:\n" . substr((string) $extract, 0, 2500));
	return is_string($extract) && strpos($extract, 'exit=0') !== false;
}

epc_tf_log('=== Taxofinca portal replace ===');
epc_tf_log('Source: ' . $src);

$dest = epc_tf_find_taxofinca_root();
if ($dest === null) {
	epc_tf_log('Docroot not visible from epartscart user — using CloudPanel + HTTPS deploy.');
	$dest = '/home/taxofinca/htdocs/www.taxofinca.com';
}

epc_tf_log('Destination: ' . $dest);

$wiped = is_dir($dest) ? epc_tf_wipe_wordpress($dest) : array();
epc_tf_log('WordPress wipe: ' . (count($wiped) ? implode(', ', $wiped) : '(skipped — no access)'));

$canWrite = is_dir($dest) && (is_writable($dest) || (@touch($dest . '/.epc-write-test') && @unlink($dest . '/.epc-write-test')));
$deployed = false;

if ($canWrite) {
	epc_tf_log('Copying portal files (direct)...');
	try {
		$stats = epc_tf_mirror($src, $dest);
		epc_tf_log('Copy stats: copied=' . $stats['copied'] . ' skipped=' . $stats['skipped'] . ' errors=' . $stats['errors']);
		epc_tf_write_config_local($dest, $dbPassword);
		$deployed = $stats['errors'] === 0 || $stats['copied'] > 100;
	} catch (Throwable $e) {
		epc_tf_log('Direct copy failed: ' . $e->getMessage());
	}
} else {
	epc_tf_log('Direct write not allowed — bootstrap via CloudPanel localhost...');
	$clpUser = getenv('CLP_USER') ?: 'admin';
	if ($clpPass === '') {
		epc_tf_log('ERROR: set CLP_PASS on server or pass clp_pass=... in URL.');
		exit(1);
	}
	$remoteDir = epc_tf_clp_detect_remote_dir($clpUser, $clpPass);
	if ($remoteDir === null) {
		$remoteDir = '/htdocs/www.taxofinca.com';
		epc_tf_log('Using fallback remote dir: ' . $remoteDir);
	}
	if (epc_tf_clp_upload_bootstrap($clpUser, $clpPass, $remoteDir)) {
		epc_tf_log('Chunk deploy to www.taxofinca.com ...');
		$deployed = epc_tf_deploy_via_chunks('www.taxofinca.com');
		if ($deployed) {
			epc_tf_clp_write_config($clpUser, $clpPass, $remoteDir, $dbPassword);
		}
	}
}

if (!$deployed) {
	epc_tf_log('ERROR: Could not deploy portal files. Set CloudPanel file permissions or run from server as root.');
	exit(1);
}

epc_tf_log('Portal files deployed.');

$cfg = new DP_Config();
$srcPdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

if ($dbPassword !== '') {
	try {
		epc_tf_clone_db($srcPdo, 'taxofinca', 'taxofinca', $dbPassword);
	} catch (Throwable $e) {
		epc_tf_log('DB clone failed: ' . $e->getMessage());
	}
} else {
	epc_tf_log('Skip DB clone — pass db_password=... (create DB taxofinca in CloudPanel first).');
}

$host = 'www.taxofinca.com';
epc_tf_log("\n--- Portal setup ---");
epc_tf_log(epc_tf_http_setup($host, '/epc-portal-setup.php'));
epc_tf_log("\n--- CP portal menu ---");
epc_tf_log(epc_tf_http_setup($host, '/epc-portal-cp-setup.php'));
epc_tf_log("\n--- Branding ---");
epc_tf_log(epc_tf_http_setup($host, '/epc-eworld-branding-apply.php'));

$pdo = $srcPdo;
epc_portal_db_ensure($pdo);
$now = time();
foreach (array('www.taxofinca.com', 'taxofinca.com') as $h) {
	$ind = epc_portal_industry('tax_advisory');
	$pdo->prepare(
		'INSERT INTO `epc_portal_site_settings`
		(`host`, `industry_code`, `system_name`, `hub_name`, `tagline`, `enabled_packs_json`, `theme_json`, `updated_at`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE
		`industry_code` = VALUES(`industry_code`), `system_name` = VALUES(`system_name`),
		`hub_name` = VALUES(`hub_name`), `tagline` = VALUES(`tagline`), `updated_at` = VALUES(`updated_at`)'
	)->execute(array(
		$h,
		'tax_advisory',
		'e-world Commerce System',
		'Electronic World Group',
		'Designed by Electronic World Group',
		json_encode(isset($ind['cp_packs']) ? $ind['cp_packs'] : array('core')),
		json_encode(isset($ind['theme']) ? $ind['theme'] : array()),
		$now,
	));
}
$pdo->prepare(
	'UPDATE `epc_portal_deploy_targets` SET `chunk_url` = ?, `extract_url` = ?, `setup_url` = ? WHERE `site_key` = ?'
)->execute(array(
	'https://www.taxofinca.com/chunk-receiver.php',
	'https://www.taxofinca.com/extract-zip-taxofinca.php?token=epartscart-deploy-2026',
	'https://www.taxofinca.com/epc-portal-setup.php',
	'taxofinca',
));

epc_tf_log("\nDone. Open https://www.taxofinca.com/ and https://www.taxofinca.com/cp/");
