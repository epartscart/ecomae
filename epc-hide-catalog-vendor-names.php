<?php
/**
 * Scrub third-party catalog vendor names from storefront-facing CMS/tab captions.
 *
 *   https://www.epartscart.com/epc-hide-catalog-vendor-names.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token(false);

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();

require_once __DIR__ . '/content/general_pages/epc_portal.php';
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($cfg);
}

$apply = !empty($_GET['apply']);
$report = array('ok' => true, 'apply' => $apply, 'changes' => array(), 'checks' => array());

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage())));
}

/** @var array<string,string> */
$replacements = array(
	'OEM Catalog (Laximo)' => 'OEM Catalog',
	'OEM Parts Catalog (Laximo)' => 'OEM Parts Catalog',
	'OEM Parts Catalog — Laximo' => 'OEM Parts Catalog',
	'OEM Parts Catalog - Laximo' => 'OEM Parts Catalog',
	'Laximo OEM Catalog' => 'OEM Catalog',
	'(Laximo OEM Catalog)' => '',
	'(Laximo)' => '',
	' — Laximo' => '',
	' - Laximo' => '',
	', Laximo,' => ',',
	' Laximo,' => ',',
	', Laximo' => '',
	'UMAPI Catalog' => 'Vehicle Catalog',
	'(UMAPI)' => '',
	'Carcat' => 'Vehicle catalog',
	'TecDoc' => 'parts cross-reference',
	'Docpart' => 'platform',
);

function epc_scrub_vendor_text(string $text, array $replacements): string
{
	$out = $text;
	foreach ($replacements as $from => $to) {
		$out = str_ireplace($from, $to, $out);
	}
	// Catch remaining bare vendor tokens in customer-facing CMS text.
	$out = preg_replace('/\bLaximo(?:\.(?:OEM|DOC|AM|CAT))?\b/iu', '', $out) ?? $out;
	$out = preg_replace('/\b(?:UMAPI|Umapi|Carcat|CARCAT|Lavimo|Levam|Carmod|CARMOD|Guayaquil)\b/u', '', $out) ?? $out;
	$out = preg_replace('/\bTecDocs?\b/iu', 'parts data', $out) ?? $out;
	$out = preg_replace('/\bDocpart\b/iu', 'platform', $out) ?? $out;
	$out = preg_replace('/\s{2,}/', ' ', $out) ?? $out;
	$out = preg_replace('/\s+,/', ',', $out) ?? $out;
	$out = preg_replace('/\s+([.,;:])/', '$1', $out) ?? $out;
	return trim($out, " \t\n\r\0\x0B,-—");
}

// CMS pages
$cols = array('value', 'title_tag', 'description_tag', 'keywords_tag');
$rows = $pdo->query(
	"SELECT `id`, `url`, `value`, `title_tag`, `description_tag`, `keywords_tag`
	 FROM `content` WHERE `is_frontend` = 1
	 AND (
		`value` LIKE '%Laximo%' OR `title_tag` LIKE '%Laximo%' OR `description_tag` LIKE '%Laximo%' OR `keywords_tag` LIKE '%Laximo%'
		OR `value` LIKE '%UMAPI%' OR `title_tag` LIKE '%UMAPI%'
		OR `value` LIKE '%Carcat%' OR `title_tag` LIKE '%Carcat%'
		OR `value` LIKE '%TecDoc%' OR `title_tag` LIKE '%TecDoc%'
		OR `value` LIKE '%Docpart%' OR `title_tag` LIKE '%Docpart%'
	 )"
)->fetchAll(PDO::FETCH_ASSOC);

$report['checks']['cms_hits'] = count($rows);
foreach ($rows as $row) {
	$id = (int) $row['id'];
	$sets = array();
	$params = array();
	$sample = array('id' => $id, 'url' => $row['url']);
	foreach ($cols as $col) {
		$before = (string) ($row[$col] ?? '');
		$sample[$col] = $before;
		$after = epc_scrub_vendor_text($before, $replacements);
		if ($after !== $before) {
			$sets[] = "`{$col}` = ?";
			$params[] = $after;
			$report['changes'][] = array(
				'table' => 'content',
				'id' => $id,
				'url' => $row['url'],
				'field' => $col,
				'from' => $before,
				'to' => $after,
			);
		}
	}
	$report['checks']['cms_samples'][] = $sample;
	if ($apply && $sets !== array()) {
		$params[] = $id;
		$pdo->prepare('UPDATE `content` SET ' . implode(', ', $sets) . ' WHERE `id` = ?')->execute($params);
	}
}

// Search tabs
try {
	$tabRows = $pdo->query(
		"SELECT `id`, `name`, `caption` FROM `shop_docpart_search_tabs`
		 WHERE `caption` LIKE '%Laximo%' OR `caption` LIKE '%UMAPI%' OR `caption` LIKE '%Carcat%'
		    OR `caption` LIKE '%TecDoc%' OR `caption` LIKE '%Docpart%' OR `name` = 'laximo_catalog'"
	)->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$tabRows = array();
}
$report['checks']['tab_hits'] = count($tabRows);
foreach ($tabRows as $row) {
	$id = (int) $row['id'];
	$before = (string) $row['caption'];
	$after = epc_scrub_vendor_text($before, $replacements);
	if ($after === '' || stripos($after, 'catalog') === false) {
		if ((string) $row['name'] === 'laximo_catalog') {
			$after = 'OEM Catalog';
		}
	}
	if ($after !== $before) {
		$report['changes'][] = array(
			'table' => 'shop_docpart_search_tabs',
			'id' => $id,
			'name' => $row['name'],
			'field' => 'caption',
			'from' => $before,
			'to' => $after,
		);
		if ($apply) {
			$pdo->prepare('UPDATE `shop_docpart_search_tabs` SET `caption` = ? WHERE `id` = ?')->execute(array($after, $id));
		}
	}
}

// Frontend translation strings (page titles / descriptions)
try {
	$langRows = $pdo->query(
		"SELECT `id`, `str_key`, `lang_code`, `value`
		 FROM `lang_text_strings_translation`
		 WHERE `value` LIKE '%Laximo%'
		    OR `value` LIKE '%UMAPI%'
		    OR `value` LIKE '%Umapi%'
		    OR `value` LIKE '%Carcat%'
		    OR `value` LIKE '%TecDoc%'
		    OR `value` LIKE '%Docpart%'
		    OR `value` LIKE '%Levam%'
		    OR `value` LIKE '%Carmod%'
		    OR `value` LIKE '%Guayaquil%'
		    OR `str_key` LIKE 'epc_laximo%'
		    OR `str_key` LIKE 'epc_umapi_catalog%'"
	)->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$langRows = array();
}
$report['checks']['lang_hits'] = count($langRows);

/** Preferred clean captions for known keys (lang => text). */
$keyDefaults = array(
	'epc_laximo_catalog_title' => array(
		'en' => 'OEM Parts Catalog',
		'ru' => 'Каталог оригинальных запчастей',
	),
	'epc_laximo_catalog_desc' => array(
		'en' => 'Search original OEM parts by vehicle brand, VIN, or part name.',
		'ru' => 'Поиск оригинальных запчастей по марке, VIN или артикулу.',
	),
	'epc_laximo_aftermarket_title' => array(
		'en' => 'Aftermarket Catalog',
		'ru' => 'Каталог aftermarket',
	),
	'epc_laximo_aftermarket_desc' => array(
		'en' => 'Search aftermarket parts and cross-references.',
		'ru' => 'Поиск aftermarket-запчастей и кроссов.',
	),
	'epc_umapi_catalog_title' => array(
		'en' => 'Vehicle Parts Catalog',
		'ru' => 'Каталог запчастей',
	),
	'epc_umapi_catalog_desc' => array(
		'en' => 'Select spare parts by passenger car, commercial vehicle, motorbike, or article analogs.',
		'ru' => 'Подбор запчастей по легковым авто, коммерческому транспорту, мотоциклам или аналогам артикула.',
	),
	'epc_levam_oem_title' => array(
		'en' => 'OEM Catalog',
		'ru' => 'OEM-каталог',
	),
);

foreach ($langRows as $row) {
	$id = (int) $row['id'];
	$key = (string) $row['str_key'];
	$lang = (string) $row['lang_code'];
	$before = (string) $row['value'];
	if (isset($keyDefaults[$key]) && is_array($keyDefaults[$key])) {
		$after = $keyDefaults[$key][$lang] ?? ($keyDefaults[$key]['en'] ?? epc_scrub_vendor_text($before, $replacements));
	} else {
		$after = epc_scrub_vendor_text($before, $replacements);
	}
	if ($after === $before) {
		continue;
	}
	$report['changes'][] = array(
		'table' => 'lang_text_strings_translation',
		'id' => $id,
		'str_key' => $key,
		'lang_code' => $row['lang_code'],
		'field' => 'value',
		'from' => $before,
		'to' => $after,
	);
	if ($apply) {
		$pdo->prepare('UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `id` = ?')->execute(array($after, $id));
	}
}

// Also scrub text_for_url overlays if present
try {
	$urlRows = $pdo->query(
		"SELECT `id`, `url`, `content`, `title_tag`, `description_tag`, `keywords_tag`
		 FROM `text_for_url`
		 WHERE `content` LIKE '%Laximo%' OR `title_tag` LIKE '%Laximo%' OR `description_tag` LIKE '%Laximo%' OR `keywords_tag` LIKE '%Laximo%'
		    OR `content` LIKE '%UMAPI%' OR `title_tag` LIKE '%UMAPI%'
		    OR `content` LIKE '%TecDoc%' OR `title_tag` LIKE '%TecDoc%'
		    OR `content` LIKE '%Docpart%' OR `title_tag` LIKE '%Docpart%'"
	)->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$urlRows = array();
}
$report['checks']['text_for_url_hits'] = count($urlRows);
foreach ($urlRows as $row) {
	$id = (int) $row['id'];
	$sets = array();
	$params = array();
	foreach (array('content', 'title_tag', 'description_tag', 'keywords_tag') as $col) {
		$before = (string) ($row[$col] ?? '');
		$after = epc_scrub_vendor_text($before, $replacements);
		if ($after !== $before) {
			$sets[] = "`{$col}` = ?";
			$params[] = $after;
			$report['changes'][] = array(
				'table' => 'text_for_url',
				'id' => $id,
				'url' => $row['url'],
				'field' => $col,
				'from' => $before,
				'to' => $after,
			);
		}
	}
	if ($apply && $sets !== array()) {
		$params[] = $id;
		$pdo->prepare('UPDATE `text_for_url` SET ' . implode(', ', $sets) . ' WHERE `id` = ?')->execute($params);
	}
}

$report['change_count'] = count($report['changes']);
if (!$apply) {
	$report['hint'] = 'Dry run. Pass apply=1 to write changes.';
}

exit(json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
