<?php
/**
 * Apply e-world Commerce System / Electronic World Group branding to DB.
 * https://www.epartscart.com/epc-eworld-branding-apply.php?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$replacements = array(
	3997 => array('en' => 'e-world Commerce System', 'ru' => 'e-world Commerce System'),
	3998 => array('en' => 'multi-industry commerce platform by Electronic World Group', 'ru' => 'мультиотраслевая commerce-платформа Electronic World Group'),
	3999 => array('en' => 'content management by Electronic World Group', 'ru' => 'система управления Electronic World Group'),
	4000 => array('en' => 'e-commerce by Electronic World Group', 'ru' => 'интернет-магазин Electronic World Group'),
	4035 => array('en' => 'e-world Commerce System', 'ru' => 'e-world Commerce System'),
);
$ins = $pdo->prepare(
	'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`)
	VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
);
foreach ($replacements as $strId => $langs) {
	$keyRow = $pdo->prepare('SELECT `str_key` FROM `lang_text_strings` WHERE `id` = ? LIMIT 1');
	$keyRow->execute(array((int) $strId));
	$strKey = (string) $keyRow->fetchColumn();
	if ($strKey === '') {
		$strKey = 'epc_brand_' . (int) $strId;
	}
	foreach ($langs as $lang => $value) {
		$ins->execute(array($strKey, $lang, $value));
	}
}
echo "Lang strings updated.\n";

epc_portal_db_ensure($pdo);
$hosts = array('www.epartscart.com', 'epartscart.com', 'www.taxofinca.com', 'taxofinca.com');
foreach ($hosts as $host) {
	$industry = (strpos($host, 'taxofinca') !== false) ? 'tax_advisory' : 'auto_parts';
	$ind = epc_portal_industry($industry);
	$pdo->prepare(
		'INSERT INTO `epc_portal_site_settings`
		(`host`, `industry_code`, `system_name`, `hub_name`, `tagline`, `enabled_packs_json`, `theme_json`, `updated_at`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE
		`system_name` = VALUES(`system_name`), `hub_name` = VALUES(`hub_name`), `tagline` = VALUES(`tagline`), `updated_at` = VALUES(`updated_at`)'
	)->execute(array(
		$host,
		$industry,
		'e-world Commerce System',
		'Electronic World Group',
		'Designed by Electronic World Group',
		json_encode(isset($ind['cp_packs']) ? $ind['cp_packs'] : array('core')),
		json_encode(isset($ind['theme']) ? $ind['theme'] : array()),
		time(),
	));
	echo "Portal settings: {$host}\n";
}

echo "CP name: e-world Commerce System\n";
echo "Design by: Electronic World Group\n";
echo "Done.\n";
