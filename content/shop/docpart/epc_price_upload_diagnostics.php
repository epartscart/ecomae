<?php
/**
 * Price upload system diagnostics: counts by channel, health checks, test helpers.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

require_once __DIR__ . '/docpart_price_upload_history.php';

/**
 * Canonical pyprices HTTP endpoint.
 *
 * nginx serves /pyprices/api.py as a static file (GET only → HTTP 405 on POST).
 * The PHP CGI bridge executes api.py reliably on Linux.
 */
function epc_pyprices_api_url(string $domainPath = ''): string
{
	$domain = trim($domainPath);
	if ($domain === '' && isset($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config'])) {
		$domain = (string) ($GLOBALS['DP_Config']->domain_path ?? '');
	}
	if ($domain === '') {
		return '/pyprices/pyprices-api.php';
	}
	return rtrim($domain, '/') . '/pyprices/pyprices-api.php';
}

/**
 * @return array<string,mixed>
 */
function epc_price_upload_diagnostics_snapshot(PDO $db, array $config)
{
	epc_price_history_ensure_schema($db);

	$loadModes = [];
	try {
		$q = $db->query('SELECT `id`, `name` FROM `shop_docpart_prices_load_modes` ORDER BY `id`');
		if ($q) {
			while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
				$loadModes[(int)$row['id']] = (string)$row['name'];
			}
		}
	} catch (Exception $e) {
		$loadModes = [1 => 'Manual', 2 => 'FTP', 3 => 'E-mail', 4 => 'URL'];
	}

	$byLoadMode = [];
	$priceLists = [];
	$q = $db->query(
		'SELECT p.`id`, p.`name`, p.`load_mode`, p.`last_updated`,
		 (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `records_count`
		 FROM `shop_docpart_prices` p ORDER BY p.`name`'
	);
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$lm = (int)$row['load_mode'];
		if (!isset($byLoadMode[$lm])) {
			$byLoadMode[$lm] = ['count' => 0, 'records' => 0, 'lists' => []];
		}
		$byLoadMode[$lm]['count']++;
		$byLoadMode[$lm]['records'] += (int)$row['records_count'];
		$byLoadMode[$lm]['lists'][] = [
			'id' => (int)$row['id'],
			'name' => (string)$row['name'],
			'last_updated' => (string)$row['last_updated'],
			'records_count' => (int)$row['records_count'],
		];
		$priceLists[] = $row;
	}

	$historyBySource = [];
	try {
		$h = $db->query(
			'SELECT `upload_source`, COUNT(*) AS `cnt`, MAX(`created_at`) AS `last_at`
			 FROM `epc_price_upload_history` GROUP BY `upload_source`'
		);
		while ($row = $h->fetch(PDO::FETCH_ASSOC)) {
			$historyBySource[(string)$row['upload_source']] = [
				'uploads' => (int)$row['cnt'],
				'last_at' => (string)$row['last_at'],
			];
		}
	} catch (Exception $e) {
		$historyBySource = [];
	}

	$cronTasks = 0;
	$cronPriceLinks = 0;
	try {
		$cronTasks = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab`')->fetchColumn();
		$cronPriceLinks = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab_prices`')->fetchColumn();
	} catch (Exception $e) {
		$cronTasks = -1;
	}

	$pendingTasks = 0;
	try {
		$pendingTasks = (int)$db->query(
			"SELECT COUNT(*) FROM `shop_docpart_pyprices_tasks` WHERE `status` IS NULL OR `status` = '' OR `status` NOT IN ('done','completed','error','failed')"
		)->fetchColumn();
	} catch (Exception $e) {
		$pendingTasks = -1;
	}

	$channels = epc_price_upload_channel_definitions($config);

	return [
		'generated_at' => date('Y-m-d H:i:s'),
		'load_modes' => $loadModes,
		'by_load_mode' => $byLoadMode,
		'price_lists_total' => count($priceLists),
		'history_by_source' => $historyBySource,
		'cron_tasks' => $cronTasks,
		'cron_price_links' => $cronPriceLinks,
		'pyprices_pending_tasks' => $pendingTasks,
		'channels' => $channels,
	];
}

/**
 * @return array<int,array<string,mixed>>
 */
function epc_price_upload_channel_definitions(array $config)
{
	$backend = (string)(isset($config['backend_dir']) ? $config['backend_dir'] : 'cp');
	$domain = (string)(isset($config['domain_path']) ? $config['domain_path'] : '');
	$techKey = (string)(isset($config['tech_key']) ? $config['tech_key'] : '');

	return [
		[
			'id' => 'cp_wizard',
			'title' => 'CP manual upload wizard (file from PC)',
			'load_mode' => 1,
			'engine' => 'PHP ajax_1–ajax_6',
			'cp_url' => '/' . $backend . '/shop/prices/upload?price_id={id}',
			'config_url' => '/' . $backend . '/shop/prices/price?price_id={id}',
			'upload_source' => 'cp_wizard',
			'formats' => 'CSV, TXT, ZIP/RAR/7z (Excel converted in step 3)',
			'test' => 'Upload a small CSV via green upload button or wizard page; check Upload history.',
		],
		[
			'id' => 'pyprices_pc',
			'title' => 'Pyprices — file from PC (manager row)',
			'load_mode' => 1,
			'engine' => 'pyprices/pyprices-api.php + upload_file.php',
			'cp_url' => '/' . $backend . '/shop/prices',
			'config_url' => '/' . $backend . '/shop/prices/price?price_id={id}',
			'upload_source' => 'pyprices_upload',
			'formats' => 'CSV, XLSX, archives per pyprices',
			'test' => 'Use file input on price row → wait for task completion → refresh record count.',
		],
		[
			'id' => 'pyprices_ftp',
			'title' => 'Pyprices — FTP',
			'load_mode' => 2,
			'engine' => 'pyprices/pyprices-api.php (ftp)',
			'cp_url' => '/' . $backend . '/shop/prices',
			'config_url' => '/' . $backend . '/shop/prices/price?price_id={id}',
			'upload_source' => 'pyprices_ftp',
			'formats' => 'File on FTP matching file_name_substring',
			'test' => 'Configure FTP on price list → Manual update FTP icon → verify rows imported.',
		],
		[
			'id' => 'pyprices_email',
			'title' => 'Pyprices — E-mail',
			'load_mode' => 3,
			'engine' => 'pyprices/pyprices-api.php (email/IMAP)',
			'cp_url' => '/' . $backend . '/shop/prices',
			'config_url' => '/' . $backend . '/shop/prices/price?price_id={id}',
			'upload_source' => 'pyprices_email',
			'formats' => 'Attachment in mailbox (site mail config required)',
			'test' => 'Send test file from allowed sender → Manual update email icon.',
		],
		[
			'id' => 'pyprices_url',
			'title' => 'Pyprices — URL / link',
			'load_mode' => 4,
			'engine' => 'pyprices/pyprices-api.php (url) or wizard download',
			'cp_url' => '/' . $backend . '/shop/prices',
			'config_url' => '/' . $backend . '/shop/prices/price?price_id={id}',
			'upload_source' => 'pyprices_url',
			'formats' => 'File at URL in price list `link` field',
			'test' => 'Set link on price → Manual update URL icon or wizard with load_mode=4.',
		],
		[
			'id' => 'cron_scheduled',
			'title' => 'Scheduled automatic update (cron)',
			'load_mode' => null,
			'engine' => 'cron_crutch.php → cron_task_executor.php → pyprices',
			'cp_url' => '/' . $backend . '/shop/prices',
			'config_url' => '/' . $backend . '/shop/prices (schedule column)',
			'upload_source' => 'pyprices_ftp|pyprices_email|pyprices_url',
			'formats' => 'Same as FTP/email/URL per list in schedule',
			'test' => 'Create schedule for one list → wait for cron minute → check last_updated.',
			'cron_wget' => $domain . $backend . '/content/shop/prices_upload/for_pyprices/for_cron/cron_crutch.php?key=' . $techKey,
		],
		[
			'id' => 'deploy_api',
			'title' => 'Deploy API (epc-upload-uae-prices.php)',
			'load_mode' => null,
			'engine' => 'Direct PHP import',
			'cp_url' => '(external) POST multipart',
			'config_url' => '/epc-upload-uae-prices.php',
			'upload_source' => 'deploy_api',
			'formats' => 'CSV, TXT, XLSX',
			'test' => 'POST with token + tech_key + price_file; verify history_id in JSON.',
		],
		[
			'id' => 'api_upload_price',
			'title' => 'Legacy API (api/prices/upload_price.php)',
			'load_mode' => null,
			'engine' => 'Staged file → ajax_5 import',
			'cp_url' => '/api/prices/upload_price.php',
			'config_url' => 'tech_key + price id + document file',
			'upload_source' => 'cp_wizard',
			'formats' => 'CSV per Treelax API',
			'test' => 'POST tech_key and file; confirm ajax_5 runs for target price_id.',
		],
		[
			'id' => 'manual_edit',
			'title' => 'Manual row edit (not file import)',
			'load_mode' => null,
			'engine' => 'prices_edit/ajax_operations.php',
			'cp_url' => '/' . $backend . '/shop/prices/prices_edit',
			'config_url' => '',
			'upload_source' => '',
			'formats' => 'Single rows in grid',
			'test' => 'Add one row in prices edit → confirm in storefront search.',
		],
		[
			'id' => 'price_review',
			'title' => 'Price review (adjust prices, not bulk load)',
			'load_mode' => null,
			'engine' => 'price_review/ajax_price_review.php',
			'cp_url' => '/' . $backend . '/shop/prices/review?price_id={id}',
			'config_url' => '',
			'upload_source' => '',
			'formats' => 'Updates existing DB rows',
			'test' => 'Open review → change price → export CSV optional.',
		],
	];
}

/**
 * @return array<string,mixed>
 */
function epc_price_upload_run_health_checks(array $config)
{
	$domain = rtrim((string)(isset($config['domain_path']) ? $config['domain_path'] : ''), '/');
	$backend = (string)(isset($config['backend_dir']) ? $config['backend_dir'] : 'cp');
	$docRoot = (string)(isset($_SERVER['DOCUMENT_ROOT']) ? $_SERVER['DOCUMENT_ROOT'] : '');
	$checks = [];

	$pyUrl = epc_pyprices_api_url($domain);
	$pyDb = epc_price_upload_curl_json(
		$pyUrl,
		array('key' => isset($config['tech_key']) ? $config['tech_key'] : '', 'just_test_db' => 'yes'),
		10
	);
	$checks['pyprices_api_reachable'] = [
		'ok' => isset($pyDb['status']) && array_key_exists('list_to_handle', $pyDb),
		'detail' => !empty($pyDb['status'])
			? ('API responded via ' . $pyUrl)
			: ('Response: ' . substr(json_encode($pyDb), 0, 200)),
	];

	$checks['pyprices_db'] = [
		'ok' => !empty($pyDb['status']),
		'detail' => !empty($pyDb['status']) ? 'DB connection from pyprices OK' : (string)(isset($pyDb['message']) ? $pyDb['message'] : json_encode($pyDb)),
	];

	$cronUrl = $domain . '/' . $backend . '/content/shop/prices_upload/for_pyprices/for_cron/cron_crutch.php?key=' . urlencode((string)(isset($config['tech_key']) ? $config['tech_key'] : ''));
	$cronHit = epc_price_upload_curl_raw($cronUrl, 12);
	$checks['cron_crutch'] = [
		'ok' => $cronHit['http_code'] >= 200 && $cronHit['http_code'] < 500,
		'detail' => 'HTTP ' . $cronHit['http_code'] . (strlen($cronHit['body']) ? ' — ' . substr(trim($cronHit['body']), 0, 120) : ''),
	];

	$tmpRel = (string)(isset($config['tmp_dir_prices_upload']) ? $config['tmp_dir_prices_upload'] : '/tmp/prices_upload_files');
	$tmpPath = $docRoot . '/' . $backend . $tmpRel;
	$tmpWritable = is_dir($tmpPath) && is_writable($tmpPath);
	if (!$tmpWritable && !is_dir($tmpPath)) {
		$tmpWritable = @mkdir($tmpPath, 0755, true) && is_writable($tmpPath);
	}
	$checks['tmp_upload_dir'] = [
		'ok' => $tmpWritable,
		'detail' => $tmpPath,
	];

	$histRoot = $docRoot . '/content/files/price_upload_history';
	$checks['history_archive_dir'] = [
		'ok' => is_dir($histRoot) && is_writable($histRoot),
		'detail' => $histRoot,
	];

	$checks['deploy_upload_endpoint'] = [
		'ok' => is_file($docRoot . '/epc-upload-uae-prices.php'),
		'detail' => '/epc-upload-uae-prices.php',
	];

	$wizardSteps = ['ajax_1_prepare_tmp_dir.php', 'ajax_5_import_csv_to_db.php', 'ajax_6_complete_session.php'];
	$wizardOk = true;
	$missing = [];
	foreach ($wizardSteps as $f) {
		$p = $docRoot . '/' . $backend . '/content/shop/prices_upload/' . $f;
		if (!is_file($p)) {
			$wizardOk = false;
			$missing[] = $f;
		}
	}
	$checks['cp_wizard_scripts'] = [
		'ok' => $wizardOk,
		'detail' => $wizardOk ? 'Steps 1, 5, 6 present' : ('Missing: ' . implode(', ', $missing)),
	];

	$allOk = true;
	foreach ($checks as $c) {
		if (empty($c['ok'])) {
			$allOk = false;
		}
	}

	return [
		'all_ok' => $allOk,
		'checks' => $checks,
		'pyprices_url' => $pyUrl,
		'cron_wget_example' => "wget -O /dev/null -q '" . $cronUrl . "'",
	];
}

/**
 * @param array<string,string>|null $postFields
 * @return array<string,mixed>
 */
function epc_price_upload_curl_json($url, $postFields = null, $timeout = 15)
{
	$raw = epc_price_upload_curl_raw($url, $timeout, $postFields);
	$decoded = json_decode(trim($raw['body']), true);
	return is_array($decoded) ? $decoded : ['_raw' => substr($raw['body'], 0, 500), '_http' => $raw['http_code']];
}

/**
 * @param array<string,string>|null $postFields
 * @return array{http_code:int,body:string}
 */
function epc_price_upload_curl_raw($url, $timeout = 15, $postFields = null)
{
	if (!function_exists('curl_init')) {
		return ['http_code' => 0, 'body' => 'curl not available'];
	}
	$ch = curl_init($url);
	curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
	curl_setopt($ch, CURLOPT_TIMEOUT, $timeout);
	curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);
	curl_setopt($ch, CURLOPT_FOLLOWLOCATION, true);
	if ($postFields !== null) {
		curl_setopt($ch, CURLOPT_POST, true);
		curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($postFields));
	}
	$body = (string)curl_exec($ch);
	$code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	return ['http_code' => $code, 'body' => $body];
}
