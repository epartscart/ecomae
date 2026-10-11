<?php
// PHP 8.3 goldens for plan Q1-boom (blockchain BOS proofs). Leftover ERP/shared/intro parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function boom_php_empty($v): bool
{
	return empty($v);
}

function boom_norm($row)
{
	if (!is_array($row)) {
		return $row;
	}
	foreach (array('created_at', 'updated_at', 'anchored_at') as $k) {
		if (array_key_exists($k, $row) && $row[$k] !== null && $row[$k] !== '') {
			$row[$k] = 'NOW';
		}
	}
	foreach ($row as $k => $v) {
		if (is_array($v)) {
			$row[$k] = boom_norm($v);
		}
	}
	return $row;
}

function boom_proof_pub(?array $row): ?array
{
	if (!$row) {
		return null;
	}
	return array(
		'proof_uid' => (string) ($row['proof_uid'] ?? ''),
		'tenant_key' => (string) ($row['tenant_key'] ?? ''),
		'record_type' => (string) ($row['record_type'] ?? ''),
		'record_id' => (string) ($row['record_id'] ?? ''),
		'payload_hash' => (string) ($row['payload_hash'] ?? ''),
		'status' => (string) ($row['status'] ?? ''),
		'batch_uid' => (string) ($row['batch_uid'] ?? ''),
		'merkle_root' => (string) ($row['merkle_root'] ?? ''),
		'merkle_index' => $row['merkle_index'] === null ? null : (int) $row['merkle_index'],
		'anchor_ref' => (string) ($row['anchor_ref'] ?? ''),
		'anchored_at' => (($row['anchored_at'] ?? '') === '' || ($row['anchored_at'] ?? null) === null) ? '' : 'NOW',
	);
}

function boom_reset_clock(): void
{
	$GLOBALS['BOOM_RANDS'] = array(
		'aaaaaaaaaaaaaaaa',
		'bbbbbbbbbbbbbbbb',
		'cccccccccccccccc',
		'dddddddddddddddd',
		'eeeeeeeeeeeeeeee',
		'ffffffffffffffff',
		'1111111111111111',
		'2222222222222222',
		'3333333333333333',
		'4444444444444444',
		'5555555555555555',
		'6666666666666666',
	);
	$GLOBALS['BOOM_RAND_I'] = 0;
	$GLOBALS['BOOM_MS'] = 1760083200000;
	$GLOBALS['BOOM_NOW'] = 1760083200;
	$GLOBALS['BOOM_JOBS'] = array();
	$GLOBALS['BOOM_HANDLERS'] = array();
	$GLOBALS['BOOM_TENANTS'] = array();
	$GLOBALS['BOOM_CLIENT_ERP'] = '';
	$GLOBALS['BOOM_SHARED'] = null;
	$GLOBALS['BOOM_COOKIE'] = '';
	$GLOBALS['BOOM_HOST'] = '';
	$GLOBALS['BOOM_BY_HOST'] = array();
	$GLOBALS['BOOM_BASE'] = '';
	$GLOBALS['DP_Config'] = null;
	$_SERVER = array();
	putenv('EPC_BC_ANCHOR_NETWORK');
}

function boom_run_pure()
{
	boom_reset_clock();
	$modes = epc_bc_bos_modes();
	$norm = array(
		epc_bc_bos_normalize_mode(''),
		epc_bc_bos_normalize_mode('ANCHOR'),
		epc_bc_bos_normalize_mode('network'),
		epc_bc_bos_normalize_mode('nope'),
		epc_bc_bos_normalize_mode('  Off  '),
	);
	$netDefault = epc_bc_bos_anchor_network();
	putenv('EPC_BC_ANCHOR_NETWORK=custom_net');
	$netCustom = epc_bc_bos_anchor_network();
	putenv('EPC_BC_ANCHOR_NETWORK=');
	$netBlank = epc_bc_bos_anchor_network();
	putenv('EPC_BC_ANCHOR_NETWORK');
	$canon = array(
		epc_bc_bos_canonical_json(array('z' => 1, 'a' => array('y' => 2, 'x' => 3))),
		epc_bc_bos_canonical_json(array('b', 'a')),
		epc_bc_bos_canonical_json(array()),
		epc_bc_bos_canonical_json(array(0 => 'a', 2 => 'b')),
		epc_bc_bos_canonical_json(array('url' => 'https://x.com/a', 'name' => 'café')),
	);
	$assoc = array(
		epc_bc_bos_is_assoc(array()),
		epc_bc_bos_is_assoc(array(1, 2)),
		epc_bc_bos_is_assoc(array('a' => 1)),
		epc_bc_bos_is_assoc(array(0 => 'a', 2 => 'b')),
	);
	$hash = array(
		epc_bc_bos_hash('plain'),
		epc_bc_bos_hash(array('z' => 1, 'a' => 2)),
	);
	$leaves3 = array(
		hash('sha256', 'a'),
		hash('sha256', 'b'),
		hash('sha256', 'c'),
	);
	$rootEmpty = epc_bc_bos_merkle_root(array());
	$rootSkip = epc_bc_bos_merkle_root(array('', '  '));
	$rootOne = epc_bc_bos_merkle_root(array($leaves3[0]));
	$rootTwo = epc_bc_bos_merkle_root(array($leaves3[0], $leaves3[1]));
	$rootOdd = epc_bc_bos_merkle_root($leaves3);
	$path0 = epc_bc_bos_merkle_proof_path($leaves3, 0);
	$path2 = epc_bc_bos_merkle_proof_path($leaves3, 2);
	$verOk = epc_bc_bos_verify_merkle_path($leaves3[0], $path0, $rootOdd);
	$verBad = epc_bc_bos_verify_merkle_path($leaves3[1], $path0, $rootOdd);
	$uid = epc_bc_bos_new_uid('prf');
	$uidBat = epc_bc_bos_new_uid('bat');
	$urls = array(
		epc_bc_bos_verify_url('prf abc/x'),
		epc_bc_bos_verify_url('  hash  '),
	);
	$baseNone = epc_bc_bos_public_base_url();
	$_SERVER['HTTP_HOST'] = 'shop.example';
	$_SERVER['HTTPS'] = '';
	$_SERVER['SERVER_PORT'] = '80';
	$baseHttp = epc_bc_bos_public_base_url();
	$_SERVER['HTTPS'] = 'on';
	$baseHttps = epc_bc_bos_public_base_url();
	$_SERVER['HTTPS'] = 'off';
	$_SERVER['SERVER_PORT'] = '443';
	$basePort = epc_bc_bos_public_base_url();
	$_SERVER['HTTPS'] = '0';
	$_SERVER['SERVER_PORT'] = '80';
	$baseHttps0 = epc_bc_bos_public_base_url();
	$GLOBALS['DP_Config'] = (object) array('domain_path' => 'https://brand.example/');
	$baseDomain = epc_bc_bos_public_base_url();
	$GLOBALS['DP_Config'] = null;
	$GLOBALS['BOOM_BASE'] = 'https://plat.example/app/';
	$basePlat = epc_bc_bos_public_base_url();
	$abs = epc_bc_bos_verify_url_absolute('prf_1');
	$GLOBALS['BOOM_BASE'] = '';
	$_SERVER = array();
	$einvoice = array(
		epc_bc_bos_einvoice_record_keys(array()),
		epc_bc_bos_einvoice_record_keys(array('doc_category' => 'tax_credit_note', 'id' => '9')),
		epc_bc_bos_einvoice_record_keys(array('invoice_type_code' => '381', 'invoice_number' => 'CN-1')),
		epc_bc_bos_einvoice_record_keys(array('invoice_number' => 'INV-7', 'id' => 3)),
		epc_bc_bos_einvoice_record_keys(array('id' => '12abc')),
	);
	$grn = array(
		epc_bc_bos_grn_record_id(array()),
		epc_bc_bos_grn_record_id(array('invoice_number' => '88', 'id' => 4)),
		epc_bc_bos_grn_record_id(array('id' => 4)),
	);
	$badge = array(
		epc_bc_bos_proof_badge_html(null),
		epc_bc_bos_proof_badge_html(null, array('show_missing' => 1)),
		epc_bc_bos_proof_badge_html(array('status' => 'pending', 'proof_uid' => "prf_o'brien")),
		epc_bc_bos_proof_badge_html(array('status' => 'anchored', 'proof_uid' => 'prf_ok'), array('show_uid' => 1)),
	);
	return array(
		$modes,
		$norm,
		$netDefault,
		$netCustom,
		$netBlank,
		epc_bc_bos_product_name(false),
		epc_bc_bos_product_name(true),
		epc_bc_bos_product_tagline(),
		$canon,
		$assoc,
		$hash,
		$rootEmpty,
		$rootSkip,
		$rootOne,
		$rootTwo,
		$rootOdd,
		$path0,
		$path2,
		$verOk,
		$verBad,
		$uid,
		$uidBat,
		$urls,
		$baseNone,
		$baseHttp,
		$baseHttps,
		$basePort,
		$baseHttps0,
		$baseDomain,
		$basePlat,
		$abs,
		$einvoice,
		$grn,
		$badge,
	);
}

function boom_run_record(?PDO $pdo)
{
	boom_reset_clock();
	$noDb = null;
	$prev = $GLOBALS['db_link'];
	$GLOBALS['db_link'] = null;
	$missDb = epc_bc_bos_record_proof('acme', 'invoice', '1', array('n' => 1));
	$GLOBALS['db_link'] = $prev;
	$badType = epc_bc_bos_record_proof('acme', '!!!', '1', array('n' => 1));
	$first = epc_bc_bos_record_proof('Acme-1!', 'Invoice', 'INV-1', array('total' => 10, 'note' => 'café'), array('ts' => '2026-10-10T08:00:00+00:00'));
	$dup = epc_bc_bos_record_proof('acme1', 'invoice', 'INV-1', array('note' => 'café', 'total' => 10), array('ts' => '2026-10-10T08:00:00+00:00'));
	$second = epc_bc_bos_record_proof('acme1', 'invoice', 'INV-1', array('total' => 11), array('ts' => '2026-10-10T08:00:00+00:00', 'enqueue_anchor' => 1));
	$enq0 = epc_bc_bos_record_proof('acme1', 'invoice', 'INV-2', array('x' => 1), array('ts' => '2026-10-10T08:00:00+00:00', 'enqueue_anchor' => 0));
	$enqStr0 = epc_bc_bos_record_proof('acme1', 'invoice', 'INV-3', array('x' => 1), array('ts' => '2026-10-10T08:00:00+00:00', 'enqueue_anchor' => '0'));
	$enqMissing = epc_bc_bos_record_proof('acme1', 'invoice', 'INV-4', array('x' => 1), array('ts' => '2026-10-10T08:00:00+00:00'));
	$maybeNoTenant = epc_bc_bos_maybe_record_document('invoice', 'M1', array('a' => 1));
	$GLOBALS['BOOM_CLIENT_ERP'] = 'beta';
	$GLOBALS['BOOM_TENANTS']['beta'] = array('blockchain_mode' => 'off');
	$maybeOff = epc_bc_bos_maybe_record_document('invoice', 'M1', array('a' => 1));
	$maybeIds = epc_bc_bos_maybe_record_document('!!!', '', array('a' => 1), array('tenant_key' => 'beta'));
	$GLOBALS['BOOM_TENANTS']['beta'] = array('blockchain_mode' => 'anchor');
	epc_bc_bos_clear_tenant_mode_cache('beta');
	$maybeOk = epc_bc_bos_maybe_record_document('invoice', 'M1', array('a' => 1), array('ts' => '2026-10-10T08:00:00+00:00'));
	$maybeNoEnq = epc_bc_bos_maybe_record_document('invoice', 'M2', array('a' => 2), array('ts' => '2026-10-10T08:00:00+00:00', 'enqueue_anchor' => 0));
	$GLOBALS['BOOM_CLIENT_ERP'] = '';
	$resolve = array(
		epc_bc_bos_resolve_site_key(array('tenant_key' => 'Acme-9!')),
		epc_bc_bos_resolve_site_key(array()),
	);
	$GLOBALS['BOOM_CLIENT_ERP'] = 'From_ERP';
	$resolve[] = epc_bc_bos_resolve_site_key(array());
	$GLOBALS['BOOM_CLIENT_ERP'] = '';
	$GLOBALS['DP_Config'] = (object) array('epc_shared_erp_site_key' => 'Shared_1', 'site_key' => 'site_x');
	$resolve[] = epc_bc_bos_resolve_site_key(array());
	$GLOBALS['DP_Config'] = (object) array('site_key' => 'site_x');
	$resolve[] = epc_bc_bos_resolve_site_key(array());
	$GLOBALS['DP_Config'] = null;
	$GLOBALS['BOOM_SHARED'] = array('site_key' => 'shared_row');
	$resolve[] = epc_bc_bos_resolve_site_key(array());
	$GLOBALS['BOOM_SHARED'] = null;
	$GLOBALS['BOOM_COOKIE'] = 'cookie_tn';
	$resolve[] = epc_bc_bos_resolve_site_key(array());
	$GLOBALS['BOOM_COOKIE'] = '';
	$GLOBALS['BOOM_HOST'] = 'acme.example';
	$GLOBALS['BOOM_BY_HOST']['acme.example'] = array('site_key' => 'host_tn');
	$resolve[] = epc_bc_bos_resolve_site_key(array());
	$GLOBALS['BOOM_HOST'] = '';
	$GLOBALS['BOOM_BY_HOST'] = array();
	epc_bc_bos_clear_tenant_mode_cache(null);
	$modeEmpty = epc_bc_bos_tenant_mode('');
	$modeMiss = epc_bc_bos_tenant_mode('ghost');
	$GLOBALS['BOOM_TENANTS']['ghost'] = array('blockchain_mode' => 'network');
	epc_bc_bos_clear_tenant_mode_cache('ghost');
	$modeNet = epc_bc_bos_tenant_mode('ghost');
	$modeCached = epc_bc_bos_tenant_mode('ghost');
	$GLOBALS['BOOM_TENANTS']['ghost'] = array('blockchain_mode' => 'off');
	$modeStill = epc_bc_bos_tenant_mode('ghost');
	epc_bc_bos_clear_tenant_mode_cache('ghost');
	$modeOff = epc_bc_bos_tenant_mode('ghost');
	return array(
		$missDb,
		$badType,
		boom_norm($first),
		boom_norm($dup),
		boom_norm($second),
		boom_norm($enq0),
		boom_norm($enqStr0),
		boom_norm($enqMissing),
		$maybeNoTenant,
		$maybeOff,
		$maybeIds,
		boom_norm($maybeOk),
		boom_norm($maybeNoEnq),
		$resolve,
		$modeEmpty,
		$modeMiss,
		$modeNet,
		$modeCached,
		$modeStill,
		$modeOff,
		$GLOBALS['BOOM_JOBS'],
		$GLOBALS['BOOM_HANDLERS'],
	);
}

function boom_run_anchor(PDO $pdo)
{
	boom_reset_clock();
	putenv('EPC_BC_ANCHOR_NETWORK=local_merkle');
	$empty = epc_bc_bos_anchor_pending_batch(0);
	$a = epc_bc_bos_record_proof('acme', 'invoice', 'A', array('n' => 1), array('ts' => '2026-10-10T08:00:00+00:00'));
	$b = epc_bc_bos_record_proof('acme', 'invoice', 'B', array('n' => 2), array('ts' => '2026-10-10T08:00:00+00:00'));
	$c = epc_bc_bos_record_proof('beta', 'grn', 'C', array('n' => 3), array('ts' => '2026-10-10T08:00:00+00:00'));
	$pendingVerify = boom_norm(epc_bc_bos_verify((string) $a['proof_uid']));
	$firstBatch = boom_norm(epc_bc_bos_anchor_pending_batch(2));
	$anchoredA = boom_norm(epc_bc_bos_verify((string) $a['proof_uid']));
	$byHash = boom_norm(epc_bc_bos_verify(strtoupper((string) $a['payload_hash'])));
	$rest = boom_norm(epc_bc_bos_job_anchor_batch('acme', array('limit' => 50)));
	$missing = boom_norm(epc_bc_bos_verify('nope'));
	$blank = boom_norm(epc_bc_bos_verify('  '));
	epc_bc_bos_register_job_handlers();
	epc_bc_bos_register_job_handlers();
	$lookA = boom_proof_pub(epc_bc_bos_lookup_proof('acme', 'invoice', 'A'));
	$failBatch = $anchoredA;
	if (!empty($failBatch['proof']['payload_hash'])) {
		$tamper = $failBatch;
		$tamper['valid'] = epc_bc_bos_verify_merkle_path('00' . substr((string) $failBatch['proof']['payload_hash'], 2), array(), (string) ($failBatch['batch']['merkle_root'] ?? ''));
	} else {
		$tamper = false;
	}
	return array(
		$empty,
		boom_norm($a),
		boom_norm($b),
		boom_norm($c),
		$pendingVerify,
		$firstBatch,
		$anchoredA,
		$byHash,
		$rest,
		$missing,
		$blank,
		$lookA,
		$tamper,
		$GLOBALS['BOOM_HANDLERS'],
		$GLOBALS['BOOM_JOBS'],
	);
}

function boom_run_fleet(PDO $pdo)
{
	boom_reset_clock();
	epc_bc_bos_record_proof('acme', 'invoice', 'I1', array('n' => 1), array('ts' => '2026-10-10T08:00:00+00:00'));
	epc_bc_bos_record_proof('acme', 'grn', 'G1', array('n' => 2), array('ts' => '2026-10-10T08:00:00+00:00'));
	epc_bc_bos_record_proof('beta', 'invoice', 'I9', array('n' => 3), array('ts' => '2026-10-10T08:00:00+00:00'));
	epc_bc_bos_anchor_pending_batch(1);
	$acme = array_map('boom_proof_pub', epc_bc_bos_list_proofs('acme', array('limit' => 50)));
	$acmeInv = array_map('boom_proof_pub', epc_bc_bos_list_proofs('acme', array('record_type' => 'invoice')));
	$acmePend = array_map('boom_proof_pub', epc_bc_bos_list_proofs('acme', array('status' => 'pending')));
	$blankTenant = epc_bc_bos_list_proofs('');
	$fleetAll = array_map('boom_proof_pub', epc_bc_bos_list_proofs_fleet(array('limit' => 10)));
	$fleetBeta = array_map('boom_proof_pub', epc_bc_bos_list_proofs_fleet(array('site_key' => 'beta')));
	$stats = epc_bc_bos_fleet_stats();
	$GLOBALS['BOOM_TENANTS']['acme'] = array('blockchain_mode' => 'anchor');
	$GLOBALS['BOOM_TENANTS']['beta'] = array('blockchain_mode' => 'off');
	$GLOBALS['BOOM_CLIENT_ERP'] = 'acme';
	$tAcme = epc_bc_bos_tenant_proof_stats();
	$tBeta = epc_bc_bos_tenant_proof_stats('beta');
	$tGhost = epc_bc_bos_tenant_proof_stats('!!!');
	$doc = epc_bc_bos_document_badge_html('invoice', 'I1', array('show_uid' => 1));
	$GLOBALS['BOOM_CLIENT_ERP'] = 'beta';
	epc_bc_bos_clear_tenant_mode_cache(null);
	$docOff = epc_bc_bos_document_badge_html('invoice', 'I9');
	$GLOBALS['BOOM_CLIENT_ERP'] = 'acme';
	epc_bc_bos_clear_tenant_mode_cache(null);
	$grnEmpty = epc_bc_bos_grn_badge_html(array('id' => 4, 'invoice_number' => 'G1'));
	$grnPosted = epc_bc_bos_grn_badge_html(array('id' => 4, 'invoice_number' => 'G1', 'inv_receipt_posted' => '1'), array('show_uid' => 1));
	$grnZero = epc_bc_bos_grn_badge_html(array('id' => 4, 'invoice_number' => 'G1', 'inv_receipt_posted' => '0'));
	$flashEmpty = epc_bc_bos_grn_flash_for_purchase(array('id' => 4, 'invoice_number' => 'G1'));
	$flash = epc_bc_bos_grn_flash_for_purchase(array('id' => 4, 'invoice_number' => 'G1', 'inventory_receipt_posted' => 1));
	return array(
		$acme,
		$acmeInv,
		$acmePend,
		$blankTenant,
		$fleetAll,
		$fleetBeta,
		$stats,
		$tAcme,
		$tBeta,
		$tGhost,
		$doc,
		$docOff,
		$grnEmpty,
		$grnPosted,
		$grnZero,
		$flashEmpty,
		$flash,
	);
}

function boom_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace(
		'/function epc_bc_bos_platform_pdo\(\): \?PDO\s*\{.*?\n\}/s',
		"function epc_bc_bos_platform_pdo(): ?PDO\n{\n    return (isset(\$GLOBALS['db_link']) && \$GLOBALS['db_link'] instanceof PDO) ? \$GLOBALS['db_link'] : null;\n}",
		$code,
		1
	);
	$code = preg_replace(
		'/function epc_bc_bos_new_uid\(string \$prefix = \'prf\'\): string\s*\{.*?\n\}/s',
		"function epc_bc_bos_new_uid(string \$prefix = 'prf'): string\n{\n    \$rands = \$GLOBALS['BOOM_RANDS'] ?? array();\n    \$i = (int) (\$GLOBALS['BOOM_RAND_I'] ?? 0);\n    \$rand = isset(\$rands[\$i]) ? (string) \$rands[\$i] : str_repeat('a', 16);\n    \$GLOBALS['BOOM_RAND_I'] = \$i + 1;\n    \$ms = (int) (\$GLOBALS['BOOM_MS'] ?? (int) (microtime(true) * 1000));\n    \$GLOBALS['BOOM_MS'] = \$ms + 1;\n    return \$prefix . '_' . \$rand . '_' . dechex(\$ms);\n}",
		$code,
		1
	);
	$code = str_replace("gmdate('c')", "gmdate('c', (int) (\$GLOBALS['BOOM_NOW'] ?? time()))", $code);
	file_put_contents($dest, $code);
}

function boom_write_stubs(string $dir): void
{
	file_put_contents($dir . '/epc_platform_jobs.php', <<<'PHP'
<?php
function epc_platform_jobs_enqueue($type, $tenant = '', $payload = array(), $opts = array())
{
	$GLOBALS['BOOM_JOBS'][] = array(
		'type' => (string) $type,
		'tenant' => (string) $tenant,
		'payload' => $payload,
		'opts' => $opts,
	);
	return 1;
}
function epc_platform_jobs_register_handler($type, $fn)
{
	$GLOBALS['BOOM_HANDLERS'][] = (string) $type;
}
PHP
	);
	file_put_contents($dir . '/epc_portal_tenant_intro.php', <<<'PHP'
<?php
function epc_portal_tenant_get($pdo, $siteKey)
{
	$key = strtolower((string) $siteKey);
	return $GLOBALS['BOOM_TENANTS'][$key] ?? null;
}
PHP
	);
	file_put_contents($dir . '/epc_portal_shared_erp.php', <<<'PHP'
<?php
function epc_portal_shared_erp_active_tenant()
{
	return $GLOBALS['BOOM_SHARED'] ?? null;
}
function epc_portal_shared_erp_cookie_site_key()
{
	return (string) ($GLOBALS['BOOM_COOKIE'] ?? '');
}
PHP
	);
	file_put_contents($dir . '/epc_portal_tenant.php', <<<'PHP'
<?php
function epc_portal_host()
{
	return (string) ($GLOBALS['BOOM_HOST'] ?? '');
}
function epc_portal_load_tenant_by_host($host)
{
	return $GLOBALS['BOOM_BY_HOST'][$host] ?? null;
}
function epc_ecomae_platform_base_url()
{
	return (string) ($GLOBALS['BOOM_BASE'] ?? '');
}
PHP
	);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1b_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	boom_patch($root . '/content/general_pages/epc_blockchain_bos.php', $doc . '/epc_blockchain_bos.php');
	boom_write_stubs($doc);
	$cleanup = function () use ($admin, $dbName, $doc) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		foreach (glob($doc . '/*') ?: array() as $file) { @unlink($file); }
		@rmdir($doc);
	};
	if (!function_exists('epc_client_erp_site_key')) {
		eval('function epc_client_erp_site_key() { return (string) ($GLOBALS["BOOM_CLIENT_ERP"] ?? ""); }');
	}
	boom_reset_clock();
	$GLOBALS['db_link'] = $pdo;
	require $doc . '/epc_blockchain_bos.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1boom_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1boom_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
