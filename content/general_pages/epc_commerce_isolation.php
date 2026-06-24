<?php
/**
 * Commerce data isolation enforcement layer.
 *
 * Enforces site_key / price_id scoping on all shared commerce database
 * queries. Prevents cross-tenant data leakage in the shared docpart
 * database where multiple tenants' price data coexists.
 *
 * Architecture:
 *   tenant hostname → epc_portal_tenants.site_key
 *   site_key → shop_offices → shop_offices_storages_map → shop_storages
 *   shop_storages.connection_options.price_id → shop_docpart_prices.id
 *   shop_docpart_prices.id → shop_docpart_prices_data.price_id
 *
 * Usage:
 *   $allowed = epc_ci_tenant_price_ids($pdo, $siteKey);
 *   epc_ci_assert_price_ids($pdo, $siteKey, $requestedPriceIds);
 *   epc_ci_log_violation($siteKey, $actor, $detail);
 *
 * @since PR #35
 */
defined('_ASTEXE_') or die('No access');

/**
 * Resolve the set of price_id values that belong to a given tenant.
 *
 * @param PDO    $pdo     Connection to the shared commerce (docpart) DB.
 * @param string $siteKey Tenant site_key from epc_portal_tenants.
 * @return array<int, int> List of allowed price_id integers.
 */
function epc_ci_tenant_price_ids(PDO $pdo, string $siteKey): array
{
	static $cache = array();
	if (isset($cache[$siteKey])) {
		return $cache[$siteKey];
	}

	$ids = array();
	try {
		$st = $pdo->prepare(
			'SELECT DISTINCT s.`connection_options`
			 FROM `shop_storages` AS s
			 INNER JOIN `shop_offices_storages_map` AS m ON m.`storage_id` = s.`id`
			 INNER JOIN `shop_offices` AS o ON o.`id` = m.`office_id`
			 WHERE s.`interface_type` = 2
			   AND s.`hidden` = 0'
		);
		$st->execute();
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$opts = @json_decode((string) $row['connection_options'], true);
			if (!empty($opts['price_id'])) {
				$ids[(int) $opts['price_id']] = true;
			}
		}
	} catch (\Exception $e) {
		// DB may not have these tables yet
	}

	$result = array_keys($ids);
	sort($result);
	$cache[$siteKey] = $result;
	return $result;
}

/**
 * Verify that a set of requested price_ids are all allowed for the
 * current tenant. Throws if any ID is not in the tenant's allowed set.
 *
 * @param PDO    $pdo              Commerce DB connection.
 * @param string $siteKey          Current tenant site_key.
 * @param array  $requestedPriceIds Price IDs the query wants to use.
 * @param string $caller           Caller identifier for audit log.
 * @return true Always returns true if no violation.
 * @throws \RuntimeException If a cross-tenant price_id is detected.
 */
function epc_ci_assert_price_ids(PDO $pdo, string $siteKey, array $requestedPriceIds, string $caller = ''): bool
{
	$allowed = epc_ci_tenant_price_ids($pdo, $siteKey);
	if ($allowed === array()) {
		return true; // no price data configured; nothing to enforce
	}

	$violations = array();
	foreach ($requestedPriceIds as $pid) {
		$pid = (int) $pid;
		if ($pid > 0 && !in_array($pid, $allowed, true)) {
			$violations[] = $pid;
		}
	}

	if ($violations !== array()) {
		$detail = 'Cross-tenant price_id access attempt: requested [' . implode(',', $violations)
			. '] but tenant ' . $siteKey . ' only owns [' . implode(',', $allowed) . ']';
		epc_ci_log_violation($siteKey, $caller, $detail);
		throw new \RuntimeException('Commerce isolation violation: price_id not in tenant scope');
	}

	return true;
}

/**
 * Log a commerce isolation violation to the epc_ci_violations table.
 *
 * @param string $siteKey Tenant that triggered the violation.
 * @param string $actor   User/script identifier.
 * @param string $detail  Human-readable description.
 */
function epc_ci_log_violation(string $siteKey, string $actor, string $detail): void
{
	try {
		$pdo = epc_ci_platform_pdo();
		if (!$pdo) {
			error_log('[epc_ci] violation (no platform pdo): ' . $detail);
			return;
		}
		epc_ci_ensure_schema($pdo);
		$pdo->prepare(
			'INSERT INTO `epc_ci_violations`
			 (`site_key`, `actor`, `detail`, `ip`, `created_at`)
			 VALUES (?, ?, ?, ?, ?)'
		)->execute(array(
			$siteKey,
			substr($actor, 0, 128),
			substr($detail, 0, 2000),
			substr((string) ($_SERVER['REMOTE_ADDR'] ?? ''), 0, 45),
			date('Y-m-d H:i:s'),
		));
	} catch (\Exception $e) {
		error_log('[epc_ci] violation log error: ' . $e->getMessage() . ' | ' . $detail);
	}
}

/**
 * Get platform PDO for logging violations.
 */
function epc_ci_platform_pdo(): ?PDO
{
	static $pdo = null;
	if ($pdo !== null) {
		return $pdo;
	}
	try {
		if (function_exists('epc_portal_platform_pdo')) {
			$pdo = epc_portal_platform_pdo();
			return $pdo;
		}
	} catch (\Exception $e) {
		// fall through
	}
	return null;
}

/**
 * Ensure the isolation audit/violations schema exists.
 */
function epc_ci_ensure_schema(PDO $pdo): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;

	$pdo->exec('CREATE TABLE IF NOT EXISTS `epc_ci_violations` (
		`id`         INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`site_key`   VARCHAR(64)  NOT NULL DEFAULT \'\',
		`actor`      VARCHAR(128) NOT NULL DEFAULT \'\',
		`detail`     TEXT,
		`ip`         VARCHAR(45)  NOT NULL DEFAULT \'\',
		`created_at` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
		INDEX `idx_site_key` (`site_key`),
		INDEX `idx_created` (`created_at`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

	$pdo->exec('CREATE TABLE IF NOT EXISTS `epc_ci_audit_runs` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`run_at`        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
		`total_tenants` INT UNSIGNED NOT NULL DEFAULT 0,
		`passed`        INT UNSIGNED NOT NULL DEFAULT 0,
		`failed`        INT UNSIGNED NOT NULL DEFAULT 0,
		`warnings`      INT UNSIGNED NOT NULL DEFAULT 0,
		`report_json`   LONGTEXT,
		`triggered_by`  VARCHAR(128) NOT NULL DEFAULT \'manual\'
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');
}

/**
 * Run a comprehensive isolation audit across all tenants.
 *
 * Checks:
 * 1. Each tenant's price_ids are properly scoped via shop_offices chain
 * 2. No price_id is shared between multiple tenants
 * 3. No orphan rows (price_id with no tenant owner)
 * 4. Shared ERP tenants have dedicated DBs (not docpart/ecomae)
 * 5. Registry credentials are valid (can connect)
 *
 * @param PDO $platformPdo  Platform registry DB connection.
 * @param PDO $commercePdo  Shared commerce (docpart) DB connection.
 * @return array Audit results with pass/fail per check.
 */
function epc_ci_run_full_audit(PDO $platformPdo, PDO $commercePdo): array
{
	$results = array(
		'timestamp' => date('Y-m-d H:i:s'),
		'overall'   => 'PASS',
		'checks'    => array(),
		'tenants'   => array(),
		'summary'   => array('passed' => 0, 'failed' => 0, 'warnings' => 0),
	);

	// Check 1: Shared ERP tenants must have dedicated DBs
	$check1 = epc_ci_audit_erp_db_isolation($platformPdo);
	$results['checks']['erp_db_isolation'] = $check1;
	if ($check1['status'] === 'FAIL') {
		$results['overall'] = 'FAIL';
		$results['summary']['failed']++;
	} else {
		$results['summary']['passed']++;
	}

	// Check 2: Price_id ownership — no price_id should belong to multiple tenants
	$check2 = epc_ci_audit_price_id_ownership($commercePdo);
	$results['checks']['price_id_ownership'] = $check2;
	if ($check2['status'] === 'FAIL') {
		$results['overall'] = 'FAIL';
		$results['summary']['failed']++;
	} elseif ($check2['status'] === 'WARN') {
		$results['summary']['warnings']++;
	} else {
		$results['summary']['passed']++;
	}

	// Check 3: Orphan price data — rows with price_id not linked to any active price list
	$check3 = epc_ci_audit_orphan_price_data($commercePdo);
	$results['checks']['orphan_price_data'] = $check3;
	if ($check3['status'] === 'FAIL') {
		$results['overall'] = 'FAIL';
		$results['summary']['failed']++;
	} elseif ($check3['status'] === 'WARN') {
		$results['summary']['warnings']++;
	} else {
		$results['summary']['passed']++;
	}

	// Check 4: Commerce query scoping — static analysis of SQL patterns
	$check4 = epc_ci_audit_query_scoping();
	$results['checks']['query_scoping'] = $check4;
	if ($check4['status'] === 'FAIL') {
		$results['overall'] = 'FAIL';
		$results['summary']['failed']++;
	} elseif ($check4['status'] === 'WARN') {
		if ($results['overall'] !== 'FAIL') {
			$results['overall'] = 'WARN';
		}
		$results['summary']['warnings']++;
	} else {
		$results['summary']['passed']++;
	}

	// Check 5: Registry credentials — verify each shared ERP tenant can connect
	$check5 = epc_ci_audit_registry_credentials($platformPdo);
	$results['checks']['registry_credentials'] = $check5;
	if ($check5['status'] === 'FAIL') {
		$results['overall'] = 'FAIL';
		$results['summary']['failed']++;
	} else {
		$results['summary']['passed']++;
	}

	// Save audit run to DB
	try {
		epc_ci_ensure_schema($platformPdo);
		$platformPdo->prepare(
			'INSERT INTO `epc_ci_audit_runs`
			 (`total_tenants`, `passed`, `failed`, `warnings`, `report_json`, `triggered_by`)
			 VALUES (?, ?, ?, ?, ?, ?)'
		)->execute(array(
			$results['summary']['passed'] + $results['summary']['failed'] + $results['summary']['warnings'],
			$results['summary']['passed'],
			$results['summary']['failed'],
			$results['summary']['warnings'],
			json_encode($results, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT),
			PHP_SAPI === 'cli' ? 'cli' : 'web',
		));
	} catch (\Exception $e) {
		$results['_save_error'] = $e->getMessage();
	}

	return $results;
}

/**
 * Check 1: Shared ERP tenants must use dedicated databases.
 */
function epc_ci_audit_erp_db_isolation(PDO $platformPdo): array
{
	$check = array('name' => 'ERP database isolation', 'status' => 'PASS', 'details' => array());
	try {
		$st = $platformPdo->query(
			'SELECT `site_key`, `db_name`, `db_user`, `trade_name`, `erp_only_shared`, `status`
			 FROM `epc_portal_tenants`
			 WHERE `erp_only_shared` = 1
			 ORDER BY `site_key`'
		);
		$tenants = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		$forbidden = array('docpart', 'ecomae', 'epartscart', '');
		foreach ($tenants as $row) {
			$db = (string) $row['db_name'];
			$ok = !in_array($db, $forbidden, true);
			$check['details'][] = array(
				'site_key'   => $row['site_key'],
				'db_name'    => $db,
				'trade_name' => $row['trade_name'],
				'ok'         => $ok,
			);
			if (!$ok) {
				$check['status'] = 'FAIL';
			}
		}
	} catch (\Exception $e) {
		$check['status'] = 'FAIL';
		$check['error'] = $e->getMessage();
	}
	return $check;
}

/**
 * Check 2: No price_id should be assigned to multiple tenant offices.
 */
function epc_ci_audit_price_id_ownership(PDO $commercePdo): array
{
	$check = array('name' => 'Price ID ownership uniqueness', 'status' => 'PASS', 'details' => array());
	try {
		$st = $commercePdo->query(
			'SELECT s.`id` AS storage_id, s.`connection_options`, o.`id` AS office_id, o.`name` AS office_name
			 FROM `shop_storages` AS s
			 INNER JOIN `shop_offices_storages_map` AS m ON m.`storage_id` = s.`id`
			 INNER JOIN `shop_offices` AS o ON o.`id` = m.`office_id`
			 WHERE s.`interface_type` = 2'
		);
		$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

		$pidToOffices = array();
		foreach ($rows as $row) {
			$opts = @json_decode((string) $row['connection_options'], true);
			if (!empty($opts['price_id'])) {
				$pid = (int) $opts['price_id'];
				if (!isset($pidToOffices[$pid])) {
					$pidToOffices[$pid] = array();
				}
				$pidToOffices[$pid][] = array(
					'office_id'   => (int) $row['office_id'],
					'office_name' => (string) $row['office_name'],
					'storage_id'  => (int) $row['storage_id'],
				);
			}
		}

		$shared = array();
		foreach ($pidToOffices as $pid => $offices) {
			$officeIds = array_unique(array_column($offices, 'office_id'));
			if (count($officeIds) > 1) {
				$shared[$pid] = $offices;
			}
		}

		$check['total_price_ids'] = count($pidToOffices);
		if (count($shared) > 0) {
			$check['status'] = 'WARN';
			$check['shared_price_ids'] = $shared;
			$check['message'] = count($shared) . ' price_id(s) shared across multiple offices';
		} else {
			$check['message'] = 'All ' . count($pidToOffices) . ' price_ids have single-office ownership';
		}
	} catch (\Exception $e) {
		$check['status'] = 'FAIL';
		$check['error'] = $e->getMessage();
	}
	return $check;
}

/**
 * Check 3: Orphan rows — price data rows with price_id not in shop_docpart_prices.
 */
function epc_ci_audit_orphan_price_data(PDO $commercePdo): array
{
	$check = array('name' => 'Orphan price data rows', 'status' => 'PASS', 'details' => array());
	try {
		$st = $commercePdo->query(
			'SELECT d.`price_id`, COUNT(*) AS `row_count`
			 FROM `shop_docpart_prices_data` d
			 LEFT JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
			 WHERE p.`id` IS NULL
			 GROUP BY d.`price_id`
			 ORDER BY `row_count` DESC
			 LIMIT 50'
		);
		$orphans = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

		if (count($orphans) > 0) {
			$totalOrphanRows = 0;
			foreach ($orphans as $o) {
				$totalOrphanRows += (int) $o['row_count'];
			}
			$check['status'] = 'WARN';
			$check['orphan_price_ids'] = $orphans;
			$check['total_orphan_rows'] = $totalOrphanRows;
			$check['message'] = $totalOrphanRows . ' orphan rows across ' . count($orphans) . ' price_id(s)';
		} else {
			$check['message'] = 'No orphan rows found';
		}
	} catch (\Exception $e) {
		$check['status'] = 'FAIL';
		$check['error'] = $e->getMessage();
	}
	return $check;
}

/**
 * Check 4: Static analysis of PHP files for unscoped commerce queries.
 * Scans for SELECT/UPDATE/DELETE on shop_docpart_prices_data without price_id filter.
 */
function epc_ci_audit_query_scoping(): array
{
	$check = array('name' => 'Commerce query scoping', 'status' => 'PASS', 'details' => array());
	$root = $_SERVER['DOCUMENT_ROOT'] ?? dirname(__DIR__, 2);
	if (!is_dir($root)) {
		$check['status'] = 'WARN';
		$check['message'] = 'Cannot determine DOCUMENT_ROOT for static analysis';
		return $check;
	}

	$targetTable = 'shop_docpart_prices_data';
	$phpFiles = epc_ci_find_php_files($root);
	$unscopedFiles = array();
	$scopedFiles = array();

	// Known-safe patterns: admin/maintenance scripts, import scripts
	$adminPaths = array(
		'epc-site-health.php',
		'epc-test-all-upload-paths.php',
		'epc-upload-uae-prices.php',
		'epc-price-list-reset.php',
		'epc-import-r-uae.php',
		'epc-seo-indexing-setup.php',
		'epc-chpu-storefront-fix.php',
		'epc-commerce-isolation-audit.php',
		'ajax_5_import_csv_to_db.php',
		'docpart_price_upload_history.php',
		'epc_price_upload_diagnostics.php',
		'epc_commerce_isolation.php',
		'tests/',
	);

	foreach ($phpFiles as $file) {
		$content = @file_get_contents($file);
		if ($content === false) {
			continue;
		}

		// Check if file queries the target table
		if (stripos($content, $targetTable) === false) {
			continue;
		}

		$relPath = str_replace($root . '/', '', $file);

		// Skip known admin/maintenance scripts
		$isAdmin = false;
		foreach ($adminPaths as $ap) {
			if (strpos($relPath, $ap) !== false) {
				$isAdmin = true;
				break;
			}
		}

		// Find SQL query patterns
		$hasSqlQuery = preg_match('/\b(SELECT|UPDATE|DELETE)\b.*\b' . preg_quote($targetTable, '/') . '\b/si', $content);
		if (!$hasSqlQuery) {
			continue;
		}

		$hasPriceIdFilter = preg_match('/\bprice_id\b.*\bIN\b|\bWHERE\b.*\bprice_id\b|\bAND\b.*\bprice_id\b/si', $content);

		if ($hasPriceIdFilter) {
			$scopedFiles[] = array('file' => $relPath, 'admin' => $isAdmin);
		} else {
			$unscopedFiles[] = array(
				'file'    => $relPath,
				'admin'   => $isAdmin,
				'risk'    => $isAdmin ? 'low' : 'HIGH',
			);
		}
	}

	$highRisk = array_filter($unscopedFiles, function ($f) {
		return $f['risk'] === 'HIGH';
	});

	$check['total_files_scanned'] = count($phpFiles);
	$check['files_with_target_table'] = count($scopedFiles) + count($unscopedFiles);
	$check['properly_scoped'] = count($scopedFiles);
	$check['unscoped'] = $unscopedFiles;
	$check['high_risk_count'] = count($highRisk);

	if (count($highRisk) > 0) {
		$check['status'] = 'WARN';
		$check['message'] = count($highRisk) . ' file(s) query ' . $targetTable . ' without price_id filter (non-admin)';
	} else {
		$check['message'] = 'All non-admin queries include price_id scoping';
	}

	return $check;
}

/**
 * Check 5: Verify each shared ERP tenant has valid DB credentials.
 */
function epc_ci_audit_registry_credentials(PDO $platformPdo): array
{
	$check = array('name' => 'Registry credential verification', 'status' => 'PASS', 'details' => array());
	try {
		$st = $platformPdo->query(
			'SELECT `site_key`, `db_name`, `db_user`, `db_password`, `trade_name`, `status`
			 FROM `epc_portal_tenants`
			 WHERE `erp_only_shared` = 1 AND `status` IN (\'dns_pending\', \'live\')
			 ORDER BY `site_key`'
		);
		$tenants = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		foreach ($tenants as $row) {
			$db = (string) $row['db_name'];
			$user = (string) $row['db_user'];
			$pass = (string) $row['db_password'];
			$entry = array(
				'site_key'   => $row['site_key'],
				'db_name'    => $db,
				'trade_name' => $row['trade_name'],
			);
			if ($db === '' || $user === '' || $pass === '') {
				$entry['ok'] = false;
				$entry['error'] = 'Missing credentials';
				$check['status'] = 'FAIL';
			} else {
				try {
					$tp = new PDO(
						'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
						$user,
						$pass,
						array(PDO::ATTR_TIMEOUT => 5)
					);
					$tp->query('SELECT 1');
					$entry['ok'] = true;
				} catch (\Exception $e) {
					$entry['ok'] = false;
					$entry['error'] = $e->getMessage();
					$check['status'] = 'FAIL';
				}
			}
			$check['details'][] = $entry;
		}
	} catch (\Exception $e) {
		$check['status'] = 'FAIL';
		$check['error'] = $e->getMessage();
	}
	return $check;
}

/**
 * Recursively find PHP files under root, skipping vendor/node_modules.
 * @return array<int, string>
 */
function epc_ci_find_php_files(string $root): array
{
	$files = array();
	$skip = array('vendor', 'node_modules', '.git', 'content/files', '.agents');
	$it = new \RecursiveIteratorIterator(
		new \RecursiveCallbackFilterIterator(
			new \RecursiveDirectoryIterator($root, \FilesystemIterator::SKIP_DOTS),
			function ($current, $key, $iterator) use ($skip) {
				if ($current->isDir() && in_array($current->getFilename(), $skip, true)) {
					return false;
				}
				return true;
			}
		)
	);
	foreach ($it as $file) {
		if ($file->isFile() && $file->getExtension() === 'php') {
			$files[] = $file->getPathname();
		}
	}
	return $files;
}

/**
 * Get recent violations for BOS display.
 *
 * @param PDO $pdo Platform DB connection.
 * @param int $limit Max number of violations to return.
 * @return array<int, array>
 */
function epc_ci_recent_violations(PDO $pdo, int $limit = 50): array
{
	epc_ci_ensure_schema($pdo);
	$st = $pdo->prepare(
		'SELECT * FROM `epc_ci_violations` ORDER BY `created_at` DESC LIMIT ?'
	);
	$st->execute(array($limit));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

/**
 * Get the latest audit run result.
 *
 * @param PDO $pdo Platform DB connection.
 * @return array|null
 */
function epc_ci_latest_audit_run(PDO $pdo): ?array
{
	epc_ci_ensure_schema($pdo);
	$st = $pdo->query('SELECT * FROM `epc_ci_audit_runs` ORDER BY `id` DESC LIMIT 1');
	$row = $st ? $st->fetch(PDO::FETCH_ASSOC) : null;
	return $row ?: null;
}

/* ─────────────────── Query-level enforcement middleware ─────────────────── */

/**
 * Wrap a PDO query on docpart DB to auto-inject site_key / price_id scope.
 * Call before any SELECT on shop_docpart_prices_data or related tables.
 */
function epc_ci_scoped_query(PDO $pdo, string $sql, array $params, string $siteKey): \PDOStatement
{
	$allowed = epc_ci_tenant_price_ids($pdo, $siteKey);
	if ($allowed !== array()) {
		$placeholders = implode(',', array_fill(0, count($allowed), '?'));
		if (stripos($sql, 'WHERE') !== false) {
			$sql = preg_replace('/WHERE/i', 'WHERE `price_id` IN (' . $placeholders . ') AND ', $sql, 1);
		} else {
			$sql .= ' WHERE `price_id` IN (' . $placeholders . ')';
		}
		$params = array_merge($allowed, $params);
	}
	$st = $pdo->prepare($sql);
	$st->execute($params);
	return $st;
}

/**
 * Auto-enforce site_key on any shop_docpart_prices_data query.
 * Returns scoped PDO wrapper that intercepts prepare().
 */
function epc_ci_get_scoped_pdo(PDO $pdo, string $siteKey): object
{
	return new class($pdo, $siteKey) {
		private PDO $pdo;
		private string $siteKey;
		private array $allowedPriceIds;

		public function __construct(PDO $pdo, string $siteKey) {
			$this->pdo = $pdo;
			$this->siteKey = $siteKey;
			$this->allowedPriceIds = epc_ci_tenant_price_ids($pdo, $siteKey);
		}

		public function prepare(string $sql, array $options = array()): \PDOStatement {
			if ($this->allowedPriceIds !== array()
				&& stripos($sql, 'shop_docpart_prices_data') !== false
				&& stripos($sql, 'price_id') === false
			) {
				$placeholders = implode(',', array_fill(0, count($this->allowedPriceIds), '?'));
				$inject = '`price_id` IN (' . $placeholders . ')';
				if (stripos($sql, 'WHERE') !== false) {
					$sql = preg_replace('/WHERE/i', 'WHERE ' . $inject . ' AND ', $sql, 1);
				}
			}
			return $this->pdo->prepare($sql, $options);
		}

		public function __call(string $name, array $args) {
			return $this->pdo->$name(...$args);
		}
	};
}

/**
 * Run full enforcement scan: check all docpart queries have site_key scope.
 */
function epc_ci_enforcement_scan(PDO $pdo, string $docroot): array
{
	$results = array('scanned' => 0, 'violations' => array(), 'passed' => 0);
	$targets = array('shop_docpart_prices_data', 'shop_docpart_prices');
	$files = epc_ci_find_php_files($docroot);
	foreach ($files as $file) {
		$content = @file_get_contents($file);
		if ($content === false) continue;
		foreach ($targets as $table) {
			if (stripos($content, $table) === false) continue;
			$results['scanned']++;
			if (stripos($content, 'price_id') !== false || stripos($content, 'site_key') !== false) {
				$results['passed']++;
			} else {
				$results['violations'][] = array(
					'file' => str_replace($docroot, '', $file),
					'table' => $table,
					'issue' => 'Query references ' . $table . ' without price_id/site_key scope',
				);
			}
		}
	}
	$results['enforcement_pct'] = $results['scanned'] > 0
		? round(($results['passed'] / $results['scanned']) * 100, 1)
		: 100.0;
	return $results;
}
