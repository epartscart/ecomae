<?php
/**
 * Replace Docpart / INTASK UI strings in language table.
 * Run: https://www.epartscart.com/epc-branding-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

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

$updated = array();
$ins = $pdo->prepare(
	'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`)
	VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
);

foreach ($replacements as $strId => $langs) {
	$keyRow = $pdo->prepare('SELECT `str_key` FROM `lang_text_strings` WHERE `id` = ? LIMIT 1');
	$keyRow->execute(array((int)$strId));
	$strKey = (string)$keyRow->fetchColumn();
	if ($strKey === '') {
		$strKey = 'epc_brand_' . (int)$strId;
		$pdo->prepare(
			'INSERT IGNORE INTO `lang_text_strings` (`id`, `str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`)
			VALUES (?, ?, ?, NULL, 0, 1, 1)'
		)->execute(array((int)$strId, $strKey, $langs['en']));
	}
	foreach ($langs as $lang => $val) {
		$ins->execute(array($strKey, $lang, $val));
	}
	$updated[] = array('id' => (int)$strId, 'str_key' => $strKey, 'en' => $langs['en']);
}

echo json_encode(array(
	'status' => true,
	'message' => 'Branding strings updated — Eparts System / Eparts Hub',
	'updated' => $updated,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
