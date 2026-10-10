<?php
// PHP 8.3 goldens for plan Q1-peak (commerce isolation helpers).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

class PeakPdo extends PDO
{
	public $lastSql = '';

	public function prepare(string $query, array $options = []): PDOStatement|false
	{
		$this->lastSql = $query;
		return parent::prepare($query, $options);
	}
}

function peak_freeze($value)
{
	if (is_string($value)) {
		if (preg_match('/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/', $value)) {
			return '2026-10-10 00:00:00';
		}
		return preg_replace('/ecomae_cpw_[a-f0-9]+/', 'TENANT_DB', $value);
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = peak_freeze($v);
		}
		return $out;
	}
	return $value;
}

function peak_sort_by_file($rows)
{
	if (!is_array($rows)) {
		return $rows;
	}
	usort($rows, function ($a, $b) {
		$fa = (string) ($a['file'] ?? '');
		$fb = (string) ($b['file'] ?? '');
		$c = strcmp($fa, $fb);
		if ($c !== 0) {
			return $c;
		}
		return strcmp((string) ($a['table'] ?? ''), (string) ($b['table'] ?? ''));
	});
	return $rows;
}

function peak_norm_check($check)
{
	if (!is_array($check)) {
		return $check;
	}
	if (isset($check['unscoped']) && is_array($check['unscoped'])) {
		$check['unscoped'] = peak_sort_by_file($check['unscoped']);
	}
	if (isset($check['orphan_price_ids']) && is_array($check['orphan_price_ids'])) {
		foreach ($check['orphan_price_ids'] as &$o) {
			$o['price_id'] = (int) $o['price_id'];
			$o['row_count'] = (int) $o['row_count'];
		}
		unset($o);
	}
	if (isset($check['total_orphan_rows'])) {
		$check['total_orphan_rows'] = (int) $check['total_orphan_rows'];
	}
	if (isset($check['total_price_ids'])) {
		$check['total_price_ids'] = (int) $check['total_price_ids'];
	}
	if (isset($check['high_risk_count'])) {
		$check['high_risk_count'] = (int) $check['high_risk_count'];
	}
	if (isset($check['total_files_scanned'])) {
		$check['total_files_scanned'] = (int) $check['total_files_scanned'];
	}
	if (isset($check['files_with_target_table'])) {
		$check['files_with_target_table'] = (int) $check['files_with_target_table'];
	}
	if (isset($check['properly_scoped'])) {
		$check['properly_scoped'] = (int) $check['properly_scoped'];
	}
	if (isset($check['details']) && is_array($check['details'])) {
		foreach ($check['details'] as &$d) {
			if (isset($d['dedicated_db'])) {
				$d['dedicated_db'] = (int) $d['dedicated_db'];
			}
		}
		unset($d);
	}
	return $check;
}

function peak_norm_audit($results)
{
	if (!is_array($results)) {
		return $results;
	}
	if (isset($results['checks']) && is_array($results['checks'])) {
		foreach ($results['checks'] as $k => $check) {
			$results['checks'][$k] = peak_norm_check($check);
		}
	}
	if (isset($results['summary']) && is_array($results['summary'])) {
		foreach (array('passed', 'failed', 'warnings') as $k) {
			if (isset($results['summary'][$k])) {
				$results['summary'][$k] = (int) $results['summary'][$k];
			}
		}
	}
	return peak_freeze($results);
}

function peak_norm_latest($row)
{
	if (!is_array($row)) {
		return $row;
	}
	unset($row['report_json']);
	foreach (array('id', 'total_tenants', 'passed', 'failed', 'warnings') as $k) {
		if (isset($row[$k])) {
			$row[$k] = (int) $row[$k];
		}
	}
	return peak_freeze($row);
}

function peak_rel_files($scan, $files)
{
	$out = array();
	foreach ($files as $f) {
		$out[] = ltrim(str_replace($scan, '', $f), '/');
	}
	sort($out);
	return $out;
}

function peak_write_scan($scan)
{
	@mkdir($scan . '/vendor', 0777, true);
	@mkdir($scan . '/node_modules', 0777, true);
	@mkdir($scan . '/.git', 0777, true);
	@mkdir($scan . '/.agents', 0777, true);
	@mkdir($scan . '/content/files', 0777, true);
	@mkdir($scan . '/admin', 0777, true);
	file_put_contents($scan . '/vendor/skip.php', "<?php echo 'vendor';\n");
	file_put_contents($scan . '/node_modules/skip.php', "<?php echo 'nm';\n");
	file_put_contents($scan . '/.git/skip.php', "<?php echo 'git';\n");
	file_put_contents($scan . '/.agents/skip.php', "<?php echo 'agents';\n");
	file_put_contents($scan . '/content/files/leak.php', "<?php SELECT * FROM shop_docpart_prices_data;\n");
	file_put_contents($scan . '/shop.php', "<?php SELECT * FROM shop_docpart_prices_data;\n");
	file_put_contents($scan . '/ok.php', "<?php SELECT * FROM shop_docpart_prices_data WHERE price_id IN (1);\n");
	file_put_contents($scan . '/mention.php', "<?php // shop_docpart_prices_data catalog note\n");
	file_put_contents($scan . '/other.php', "<?php SELECT 1;\n");
	file_put_contents($scan . '/admin/epc-site-health.php', "<?php SELECT * FROM shop_docpart_prices_data;\n");
	file_put_contents($scan . '/sitekey.php', "<?php SELECT * FROM shop_docpart_prices_data; SELECT * FROM shop_docpart_prices WHERE site_key = 1;\n");
	file_put_contents($scan . '/prices_only.php', "<?php SELECT * FROM shop_docpart_prices;\n");
}

function peak_schema($pdo)
{
	$pdo->exec('CREATE TABLE `shop_storages` (
		`id` int NOT NULL AUTO_INCREMENT,
		`interface_type` int NOT NULL DEFAULT 0,
		`hidden` int NOT NULL DEFAULT 0,
		`connection_options` text,
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `shop_offices_storages_map` (
		`storage_id` int NOT NULL,
		`office_id` int NOT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `shop_offices` (
		`id` int NOT NULL AUTO_INCREMENT,
		`caption` varchar(255) DEFAULT \'\',
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `shop_docpart_prices` (
		`id` int NOT NULL,
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `shop_docpart_prices_data` (
		`price_id` int NOT NULL,
		`sku` varchar(64) NOT NULL DEFAULT \'\'
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `epc_portal_tenants` (
		`site_key` varchar(64) NOT NULL DEFAULT \'\',
		`hostname` varchar(255) NOT NULL DEFAULT \'\',
		`db_name` varchar(128) NOT NULL DEFAULT \'\',
		`db_user` varchar(128) NOT NULL DEFAULT \'\',
		`db_password` varchar(128) NOT NULL DEFAULT \'\',
		`trade_name` varchar(255) NOT NULL DEFAULT \'\',
		`industry_code` varchar(64) NOT NULL DEFAULT \'\',
		`dedicated_db` int NOT NULL DEFAULT 0,
		`scale_policy` varchar(64) NOT NULL DEFAULT \'\',
		`erp_only_shared` int NOT NULL DEFAULT 0,
		`hosted_on` varchar(64) NOT NULL DEFAULT \'\',
		`status` varchar(32) NOT NULL DEFAULT \'\'
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function peak_seed_price($pdo)
{
	$pdo->exec("INSERT INTO `shop_offices` (`caption`) VALUES ('Alpha'), ('Beta')");
	$st = $pdo->prepare('INSERT INTO `shop_storages` (`interface_type`, `hidden`, `connection_options`) VALUES (?, ?, ?)');
	$st->execute(array(2, 0, '{"price_id": 5}'));
	$st->execute(array(2, 0, '{"price_id": "7"}'));
	$st->execute(array(2, 0, '{"price_id": "0"}'));
	$st->execute(array(2, 0, '{"price_id": 0}'));
	$st->execute(array(2, 1, '{"price_id": 9}'));
	$st->execute(array(1, 0, '{"price_id": 11}'));
	$st->execute(array(2, 0, 'not-json'));
	$st->execute(array(2, 0, '{"price_id": 13}'));
	$st->execute(array(2, 0, '{"foo": 1}'));
	$map = $pdo->prepare('INSERT INTO `shop_offices_storages_map` (`storage_id`, `office_id`) VALUES (?, ?)');
	foreach (array(1, 2, 3, 4, 5, 6, 7, 9) as $sid) {
		$map->execute(array($sid, 1));
	}
	$pdo->exec('INSERT INTO `shop_docpart_prices` (`id`) VALUES (5), (7)');
	$pdo->exec("INSERT INTO `shop_docpart_prices_data` (`price_id`, `sku`) VALUES (5, 'ABC'), (5, 'ZZZ'), (7, 'ABC'), (99, 'ABC')");
}

function peak_seed_audit($pdo, $tenantDb, $password)
{
	$ins = $pdo->prepare(
		'INSERT INTO `epc_portal_tenants`
		 (`site_key`, `hostname`, `db_name`, `db_user`, `db_password`, `trade_name`, `industry_code`,
		  `dedicated_db`, `scale_policy`, `erp_only_shared`, `hosted_on`, `status`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	);
	$ins->execute(array('epartscart', 'www.epartscart.com', 'docpart', 'u', 'p', 'eParts', 'auto', 0, '', 0, '', 'live'));
	$ins->execute(array('taxo', 'taxo.example.com', 'docpart', 'u', 'p', 'Taxo', 'tax', 0, 'shared', 0, '', 'live'));
	$ins->execute(array('boutique', 'Boutique.Example.com', 'boutique_db', 'u', 'p', 'Boutique', 'retail', 1, 'dedicated', 0, '', 'live'));
	$ins->execute(array('platformy', 'plat.example.com', 'ecomae', 'u', 'p', 'Plat', 'x', 0, '', 0, 'platform', 'live'));
	$ins->execute(array('nohost', '', 'x', 'u', 'p', 'NoHost', 'x', 0, '', 0, '', 'live'));
	$ins->execute(array('erpbads', 'erp-bad.example.com', 'docpart', 'ecomae', '', 'ERP Bad', 'erp', 0, '', 1, '', 'live'));
	$ins->execute(array('erpgood', 'erp-good.example.com', $tenantDb, 'ecomae', $password, 'ERP Good', 'erp', 1, '', 1, '', 'live'));
	$ins->execute(array('erpempty', 'erp-empty.example.com', '', '', '', 'ERP Empty', 'erp', 0, '', 1, '', 'live'));
	$ins->execute(array('pending', 'pend.example.com', 'ecomae', 'u', 'p', 'Pending', 'x', 0, '', 0, '', 'dns_pending'));
	$ins->execute(array('erpold', 'old.example.com', 'okdb', 'ecomae', $password, 'Old', 'erp', 0, '', 1, '', 'paused'));

	$pdo->exec("INSERT INTO `shop_offices` (`caption`) VALUES ('Alpha'), ('Beta')");
	$st = $pdo->prepare('INSERT INTO `shop_storages` (`interface_type`, `hidden`, `connection_options`) VALUES (?, ?, ?)');
	$st->execute(array(2, 0, '{"price_id": 5}'));
	$st->execute(array(2, 0, '{"price_id": 5}'));
	$st->execute(array(2, 0, '{"price_id": 8}'));
	$st->execute(array(2, 1, '{"price_id": 9}'));
	$map = $pdo->prepare('INSERT INTO `shop_offices_storages_map` (`storage_id`, `office_id`) VALUES (?, ?)');
	$map->execute(array(1, 1));
	$map->execute(array(2, 2));
	$map->execute(array(3, 1));
	$map->execute(array(4, 1));
	$pdo->exec('INSERT INTO `shop_docpart_prices` (`id`) VALUES (1), (2)');
	$pdo->exec("INSERT INTO `shop_docpart_prices_data` (`price_id`, `sku`) VALUES (1, 'A'), (1, 'B'), (99, 'X'), (99, 'Y'), (99, 'Z'), (100, 'Q')");
}

function peak_run_pure($pdo, $scan)
{
	$ids = epc_ci_tenant_price_ids($pdo, 'acme');
	$ok = epc_ci_assert_price_ids($pdo, 'acme', array(99, 5), 'pure');
	$plat = epc_ci_platform_pdo();
	$logFile = $scan . '/err.log';
	ini_set('error_log', $logFile);
	epc_ci_log_violation('acme', 'cli', 'no platform pdo detail');
	$err = is_file($logFile) ? trim((string) file_get_contents($logFile)) : '';
	$err = preg_replace('/^\[[^\]]+\]\s*/', '', $err);
	$latest = peak_norm_latest(epc_ci_latest_audit_run($pdo));
	$found = peak_rel_files($scan, epc_ci_find_php_files($scan));
	$scanRes = epc_ci_enforcement_scan($pdo, $scan);
	$scanRes['violations'] = peak_sort_by_file($scanRes['violations']);
	$scanRes['scanned'] = (int) $scanRes['scanned'];
	$scanRes['passed'] = (int) $scanRes['passed'];
	return peak_freeze(array(
		$ids,
		$ok,
		$plat === null,
		$err,
		$latest,
		$found,
		$scanRes,
	));
}

function peak_run_price($pdo)
{
	$before = epc_ci_tenant_price_ids($pdo, 'before');
	peak_seed_price($pdo);
	$ids = epc_ci_tenant_price_ids($pdo, 'acme');
	$again = epc_ci_tenant_price_ids($pdo, 'acme');
	$other = epc_ci_tenant_price_ids($pdo, 'other');
	$pass = epc_ci_assert_price_ids($pdo, 'acme', array(5, 7, 0, -1, '0'), 'price-ok');
	$zeroOk = epc_ci_assert_price_ids($pdo, 'acme', array('0'), 'price-zero');
	$caught = '';
	try {
		epc_ci_assert_price_ids($pdo, 'acme', array(5, 99), 'price-bad');
	} catch (Throwable $e) {
		$caught = $e->getMessage();
	}
	$st = epc_ci_scoped_query($pdo, 'SELECT price_id, sku FROM shop_docpart_prices_data WHERE sku = ?', array('ABC'), 'acme');
	$scopedWhere = array(
		'sql' => $pdo->lastSql,
		'rows' => $st->fetchAll(PDO::FETCH_ASSOC),
	);
	foreach ($scopedWhere['rows'] as &$r) {
		$r['price_id'] = (int) $r['price_id'];
	}
	unset($r);
	$st2 = epc_ci_scoped_query($pdo, 'SELECT price_id, sku FROM shop_docpart_prices_data', array(), 'acme');
	$scopedAll = array(
		'sql' => $pdo->lastSql,
		'rows' => $st2->fetchAll(PDO::FETCH_ASSOC),
	);
	foreach ($scopedAll['rows'] as &$r) {
		$r['price_id'] = (int) $r['price_id'];
	}
	unset($r);
	$wrapEmpty = epc_ci_get_scoped_pdo($pdo, 'before');
	$st3 = $wrapEmpty->prepare('SELECT price_id, sku FROM shop_docpart_prices_data WHERE sku = ?');
	$st3->execute(array('ABC'));
	$emptyWrap = array(
		'sql' => $pdo->lastSql,
		'rows' => $st3->fetchAll(PDO::FETCH_ASSOC),
	);
	foreach ($emptyWrap['rows'] as &$r) {
		$r['price_id'] = (int) $r['price_id'];
	}
	unset($r);
	$wrap = epc_ci_get_scoped_pdo($pdo, 'acme');
	$st4 = $wrap->prepare('SELECT price_id, sku FROM shop_docpart_prices_data WHERE sku = ?');
	$st4->execute(array('ABC'));
	$scopedWrap = array(
		'sql' => $pdo->lastSql,
		'rows' => $st4->fetchAll(PDO::FETCH_ASSOC),
	);
	foreach ($scopedWrap['rows'] as &$r) {
		$r['price_id'] = (int) $r['price_id'];
	}
	unset($r);
	$st5 = $wrap->prepare('SELECT price_id, sku FROM shop_docpart_prices_data WHERE price_id = 99');
	$already = $pdo->lastSql;
	$wrap->prepare('SELECT sku FROM shop_docpart_prices_data WHERE sku = ?');
	$injected = $pdo->lastSql;
	$one = $wrap->query('SELECT 1 AS n')->fetch(PDO::FETCH_ASSOC);
	$one['n'] = (int) $one['n'];
	$viol = $pdo->query('SELECT `site_key`, `actor`, `detail`, `ip` FROM `epc_ci_violations` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	return peak_freeze(array(
		$before,
		$ids,
		$again,
		$other,
		$pass,
		$zeroOk,
		$caught,
		$scopedWhere,
		$scopedAll,
		$emptyWrap,
		$scopedWrap,
		$already,
		$injected,
		$one,
		$viol,
	));
}

function peak_run_audit($pdo, $scan, $tenantDb, $password)
{
	peak_seed_audit($pdo, $tenantDb, $password);
	$erp = peak_freeze(peak_norm_check(epc_ci_audit_erp_db_isolation($pdo)));
	$client = peak_freeze(peak_norm_check(epc_ci_audit_client_docpart_isolation($pdo)));
	$own = peak_freeze(peak_norm_check(epc_ci_audit_price_id_ownership($pdo)));
	$orph = peak_freeze(peak_norm_check(epc_ci_audit_orphan_price_data($pdo)));
	$scope = peak_freeze(peak_norm_check(epc_ci_audit_query_scoping()));
	$cred = peak_freeze(peak_norm_check(epc_ci_audit_registry_credentials($pdo)));
	$full = peak_norm_audit(epc_ci_run_full_audit($pdo, $pdo));
	$latest = peak_norm_latest(epc_ci_latest_audit_run($pdo));
	return array($erp, $client, $own, $orph, $scope, $cred, $full, $latest);
}

function peak_run_scan($pdo, $scan)
{
	$found = peak_rel_files($scan, epc_ci_find_php_files($scan));
	$en = epc_ci_enforcement_scan($pdo, $scan);
	$en['violations'] = peak_sort_by_file($en['violations']);
	$en['scanned'] = (int) $en['scanned'];
	$en['passed'] = (int) $en['passed'];
	$_SERVER['REMOTE_ADDR'] = '203.0.113.9';
	epc_ci_log_violation('acme', str_repeat('A', 140), str_repeat('D', 2010));
	epc_ci_log_violation('beta', 'ops', 'second');
	$viol = $pdo->query('SELECT `site_key`, `actor`, LENGTH(`detail`) AS dlen, `ip` FROM `epc_ci_violations` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($viol as &$v) {
		$v['dlen'] = (int) $v['dlen'];
	}
	unset($v);
	$scope = peak_norm_check(epc_ci_audit_query_scoping());
	return peak_freeze(array($found, $en, $viol, $scope));
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1p_' . substr(md5(uniqid('', true)), 0, 12);
	$scan = $doc . '/scan';
	@mkdir($scan, 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$tenantDb = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$admin->exec('CREATE DATABASE `' . $tenantDb . '`');
	$pdo = new PeakPdo('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	peak_schema($pdo);
	peak_write_scan($scan);
	$cleanup = function () use ($doc, $admin, $dbName, $tenantDb) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $tenantDb . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};
	$_SERVER['DOCUMENT_ROOT'] = $scan;
	$_SERVER['REMOTE_ADDR'] = '198.51.100.10';
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	require $root . '/content/general_pages/epc_commerce_isolation.php';
	if ($case['name'] !== 'pure') {
		$GLOBALS['platform_pdo'] = $pdo;
		if (!function_exists('epc_portal_platform_pdo')) {
			function epc_portal_platform_pdo()
			{
				return $GLOBALS['platform_pdo'] ?? null;
			}
		}
	}
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['__doc'] = $doc;
	$GLOBALS['__scan'] = $scan;
	$GLOBALS['__root'] = $root;
	$GLOBALS['__tenant_db'] = $tenantDb;
	$GLOBALS['__db_pass'] = $password;
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1p_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1p_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
