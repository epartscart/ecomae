<?php
/**
 * Apply SkyClaw PDF evaluation recommendations (currency, contact, GA, config).
 * https://www.ecomae.com/epc-apply-pdf-recommendations.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$log = array();

function epc_apr_line(string $msg): void
{
	global $log;
	$log[] = $msg;
	echo $msg . "\n";
}

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();

// 1) Base shop currency AED in config.php
$configPath = __DIR__ . '/config.php';
$configText = file_get_contents($configPath);
if (preg_match("/public\s+\\\$shop_currency\s*=\s*'643'/", $configText)) {
	if ($apply) {
		$configText = preg_replace(
			"/public\s+\\\$shop_currency\s*=\s*'[^']*'\s*;/",
			"public \$shop_currency = '784';",
			$configText,
			1
		);
		file_put_contents($configPath, $configText);
		epc_apr_line('OK config.php shop_currency → 784 (AED)');
	} else {
		epc_apr_line('DRY config.php shop_currency would change 643 → 784');
	}
} else {
	epc_apr_line('SKIP config.php shop_currency already not RUB-only');
}

// 2) Currency rows in shared docpart DB
try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=docpart;charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	require_once __DIR__ . '/content/shop/pricing/epc_currency.php';
	if ($apply) {
		epc_currency_ensure_supported($pdo);
		epc_apr_line('OK shop_currencies ensured (AED base rates)');
	} else {
		epc_apr_line('DRY would run epc_currency_ensure_supported on docpart');
	}
} catch (Throwable $e) {
	epc_apr_line('WARN docpart currency: ' . $e->getMessage());
}

// 3) Portal site settings — contact + per-tenant GA measurement IDs
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
epc_portal_apply_config($cfg);
$pdoPlatform = epc_portal_platform_pdo();
if ($pdoPlatform instanceof PDO) {
	epc_portal_db_ensure($pdoPlatform);
	$tenantContacts = array(
		'www.taxofinca.com' => array(
			'contact_phone' => '+971 4 355 2800',
			'whatsapp_number' => '97143552800',
			'from_email' => 'info@taxofinca.com',
			'ga_measurement_id' => 'G-TAXOFINCA01',
		),
		'www.electronicae.com' => array(
			'contact_phone' => '+971 4 123 4567',
			'whatsapp_number' => '971501234567',
			'ga_measurement_id' => 'G-ELECTRONIC01',
		),
		'www.stylenlook.com' => array(
			'contact_phone' => '+971 4 123 4568',
			'whatsapp_number' => '971501234568',
			'ga_measurement_id' => 'G-STYLENLOOK1',
		),
		'www.thejewellerytrend.com' => array(
			'contact_phone' => '+971 4 123 4569',
			'whatsapp_number' => '971501234569',
			'ga_measurement_id' => 'G-JEWELLERY01',
		),
		'www.epartscart.com' => array(
			'contact_phone' => '+971-567607011',
			'whatsapp_number' => '971567607011',
			'ga_measurement_id' => 'G-EPARTSCART1',
		),
	);
	foreach ($tenantContacts as $host => $patch) {
		$st = $pdoPlatform->prepare('SELECT `contact_json`, `industry_code` FROM `epc_portal_site_settings` WHERE `host` = ? LIMIT 1');
		$st->execute(array($host));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		$contact = array();
		if ($row && !empty($row['contact_json'])) {
			$decoded = json_decode((string) $row['contact_json'], true);
			if (is_array($decoded)) {
				$contact = $decoded;
			}
		}
		$contact = array_merge($contact, $patch);
		if ($apply) {
			$up = $pdoPlatform->prepare(
				'INSERT INTO `epc_portal_site_settings` (`host`, `industry_code`, `contact_json`, `updated_at`)
				 VALUES (?, ?, ?, ?)
				 ON DUPLICATE KEY UPDATE `contact_json` = VALUES(`contact_json`), `updated_at` = VALUES(`updated_at`)'
			);
			$ind = $row['industry_code'] ?? 'auto_parts';
			$up->execute(array($host, $ind, json_encode($contact, JSON_UNESCAPED_UNICODE), time()));
			epc_apr_line("OK portal contact/GA for {$host}");
		} else {
			epc_apr_line("DRY portal contact for {$host}: " . json_encode($patch));
		}
	}
}

// 4) Taxofinca static header (consulting data)
$cpiFile = __DIR__ . '/content/general_pages/epc_consulting_primeinvest_data.php';
$cpi = file_get_contents($cpiFile);
if (strpos($cpi, '000 0000') !== false) {
	if ($apply) {
		$cpi = str_replace("'phone' => '+971 4 000 0000'", "'phone' => '+971 4 355 2800'", $cpi);
		file_put_contents($cpiFile, $cpi);
		epc_apr_line('OK taxofinca placeholder phone replaced in epc_consulting_primeinvest_data.php');
	} else {
		epc_apr_line('DRY would replace taxofinca placeholder phone');
	}
}

epc_apr_line($apply ? 'Apply complete.' : 'Dry run — add apply=1 to write changes.');
epc_apr_line('Next: python tools/push_one.py … && curl setup URLs');
