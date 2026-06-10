<?php
/**
 * Auto-parts demo storefront bootstrap — clone epartscart/docpart header data into tenant DB.
 * Preset: content/general_pages/epc_theme_presets/automotive_spareparts_pro.json
 *
 * Used by Layla provision, epc-demo-theme-fix.php, and epc_portal_demo_repair_header_parity().
 */
declare(strict_types=1);

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_demo_autoparts_bootstrap_preset_path(): string
{
	return __DIR__ . '/content/general_pages/epc_theme_presets/automotive_spareparts_pro.json';
}

/** @return array<string, mixed> */
function epc_demo_autoparts_bootstrap_preset(): array
{
	$path = epc_demo_autoparts_bootstrap_preset_path();
	if (!is_file($path)) {
		return array();
	}
	$raw = file_get_contents($path);
	if ($raw === false || $raw === '') {
		return array();
	}
	$data = json_decode($raw, true);
	return is_array($data) ? $data : array();
}

/** Tables cloned read-only from docpart for header location, hours, nav, catalog. */
function epc_demo_autoparts_bootstrap_clone_tables(): array
{
	$preset = epc_demo_autoparts_bootstrap_preset();
	$fromJson = $preset['docpart_clone_tables'] ?? null;
	if (is_array($fromJson) && $fromJson !== array()) {
		return array_values(array_filter(array_map('strval', $fromJson)));
	}
	return array(
		'lang_languages', 'lang_text_strings', 'lang_text_strings_translation',
		'groups', 'users_groups_bind',
		'shop_offices', 'shop_geo', 'shop_offices_geo_map',
		'menu', 'shop_catalogue_categories',
		'templates', 'content', 'modules', 'plugins',
		'shop_storages', 'shop_storages_data',
	);
}

function epc_demo_autoparts_bootstrap_docpart_pdo(): ?PDO
{
	try {
		require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
		$srcCreds = epc_portal_resolve_tenant_db_credentials();
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		return new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $srcCreds['db'] . ';charset=utf8',
			$srcCreds['user'],
			$srcCreds['password'],
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return null;
	}
}

/**
 * Clone header-related rows from docpart and verify Dubai + business hours markers in DB.
 */
function epc_demo_autoparts_bootstrap_apply(PDO $tenantPdo, bool $force = false): array
{
	if (!function_exists('epc_portal_demo_php_clone_tables')) {
		require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
	}

	$out = array(
		'ok' => true,
		'force' => $force,
		'preset' => basename(epc_demo_autoparts_bootstrap_preset_path()),
		'cloned' => array(),
		'home_modules' => false,
		'root_categories' => 0,
		'geo_nodes' => 0,
		'offices' => 0,
		'verify' => array(),
	);

	$docPdo = epc_demo_autoparts_bootstrap_docpart_pdo();
	if (!$docPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Storefront database PDO unavailable');
	}

	$tables = epc_demo_autoparts_bootstrap_clone_tables();
	if ($force) {
		$clone = epc_portal_demo_php_clone_tables($docPdo, $tenantPdo, $tables);
	} else {
		$clone = array('ok' => true, 'tables' => array(), 'errors' => array());
		foreach ($tables as $tbl) {
			$tblEsc = str_replace('`', '', $tbl);
			try {
				$cnt = (int) $tenantPdo->query('SELECT COUNT(*) FROM `' . $tblEsc . '`')->fetchColumn();
			} catch (Exception $e) {
				$cnt = 0;
			}
			if ($cnt === 0) {
				$part = epc_portal_demo_php_clone_tables($docPdo, $tenantPdo, array($tbl));
				$clone['tables'] = array_merge($clone['tables'] ?? array(), $part['tables'] ?? array());
				if (!empty($part['errors'])) {
					$clone['errors'] = array_merge($clone['errors'] ?? array(), $part['errors']);
				}
			}
		}
	}
	$out['cloned'] = $clone;
	if (!empty($clone['errors'])) {
		$out['ok'] = false;
	}

	try {
		$srcHome = $docPdo->query(
			'SELECT `modules_array` FROM `content` WHERE `main_flag` = 1 AND `published_flag` = 1 AND `is_frontend` = 1 LIMIT 1'
		)->fetch(PDO::FETCH_ASSOC);
		if ($srcHome && !empty($srcHome['modules_array'])) {
			$st = $tenantPdo->prepare(
				'UPDATE `content` SET `modules_array` = ? WHERE `main_flag` = 1 AND `published_flag` = 1 AND `is_frontend` = 1'
			);
			$st->execute(array($srcHome['modules_array']));
			$out['home_modules'] = $st->rowCount() > 0;
		}
	} catch (Exception $e) {
		$out['home_modules_error'] = $e->getMessage();
	}

	try {
		$out['root_categories'] = (int) $tenantPdo->query(
			'SELECT COUNT(`id`) FROM `shop_catalogue_categories` WHERE `published_flag` = 1 AND `parent` = 0'
		)->fetchColumn();
	} catch (Exception $e) {
		$out['root_categories'] = 0;
	}

	try {
		$out['geo_nodes'] = (int) $tenantPdo->query('SELECT COUNT(*) FROM `shop_geo`')->fetchColumn();
	} catch (Exception $e) {
		$out['geo_nodes'] = 0;
	}

	try {
		$out['offices'] = (int) $tenantPdo->query('SELECT COUNT(*) FROM `shop_offices`')->fetchColumn();
	} catch (Exception $e) {
		$out['offices'] = 0;
	}

	$preset = epc_demo_autoparts_bootstrap_preset();
	$markers = $preset['header_verify'] ?? array(
		'location' => 'Dubai',
		'hours' => 'Mon-Fri from 9:00',
	);
	$out['verify'] = epc_demo_autoparts_bootstrap_verify_db($tenantPdo, $markers);
	if (empty($out['verify']['ok'])) {
		$out['ok'] = false;
	}

	$out['message'] = $out['ok'] ? 'Auto-parts header bootstrap complete' : 'Auto-parts header bootstrap incomplete';
	return $out;
}

/**
 * @param array<string, string> $markers
 * @return array<string, mixed>
 */
function epc_demo_autoparts_bootstrap_verify_db(PDO $tenantPdo, array $markers): array
{
	$locationNeedle = (string) ($markers['location'] ?? 'Dubai');
	$hoursNeedle = (string) ($markers['hours'] ?? 'Mon-Fri from 9:00');
	$verify = array(
		'ok' => true,
		'location' => false,
		'hours' => false,
		'geo_dubai' => false,
	);

	try {
		$dubaiGeo = (int) $tenantPdo->query('SELECT COUNT(*) FROM `shop_geo` WHERE `id` = 3')->fetchColumn();
		$verify['geo_dubai'] = $dubaiGeo > 0;
		$geoTotal = (int) $tenantPdo->query('SELECT COUNT(*) FROM `shop_geo`')->fetchColumn();
		$verify['location'] = $verify['geo_dubai'] || $geoTotal > 1;
		if (!$verify['location']) {
			$st2 = $tenantPdo->query('SELECT `city` FROM `shop_offices` ORDER BY `id` ASC LIMIT 3');
			while ($office = $st2->fetch(PDO::FETCH_ASSOC)) {
				if (stripos((string) ($office['city'] ?? ''), $locationNeedle) !== false) {
					$verify['location'] = true;
					break;
				}
			}
		}
	} catch (Exception $e) {
		$verify['geo_error'] = $e->getMessage();
	}

	try {
		$like = '%' . str_replace(array('%', '_'), array('\\%', '\\_'), $hoursNeedle) . '%';
		$hoursCnt = (int) $tenantPdo->query(
			'SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `value` LIKE ' . $tenantPdo->quote($like)
		)->fetchColumn();
		$verify['hours'] = $hoursCnt > 0;
		if (!$verify['hours']) {
			$office = $tenantPdo->query('SELECT `timetable` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1')->fetch(PDO::FETCH_ASSOC);
			$verify['hours'] = is_array($office) && trim((string) ($office['timetable'] ?? '')) !== '';
		}
	} catch (Exception $e) {
		$verify['office_error'] = $e->getMessage();
	}

	if (!$verify['location'] || !$verify['hours']) {
		$verify['ok'] = false;
	}
	return $verify;
}
