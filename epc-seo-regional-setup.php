<?php
/**
 * One-time regional SEO setup: shipping-export page + translation keys.
 * Run: https://www.epartscart.com/epc-seo-regional-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$epcTenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? 'www.epartscart.com'));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($cfg, $epcTk)) {
				$cfg->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}

$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_reg_tr($pdo, $key, $en, $ru, $ar = '')
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
	if ($ar !== '') {
		$ins->execute(array($key, 'ar', $ar));
	}
}

epc_reg_tr($pdo, 'epc_shipping_export_title', 'Shipping & export', 'Доставка и экспорт', 'الشحن والتصدير');
epc_reg_tr($pdo, 'epc_shipping_export_desc', 'UAE-Oman-KSA warehouse — Fast ship to GCC and worldwide. Couriers: DHL, FedEx, Aramex, UPS.', 'Склады ОАЭ–Оман–КСА — быстрая доставка в GCC и мир. Курьеры: DHL, FedEx, Aramex, UPS.', 'مستودعات الإمارات وعُمان والسعودية — شحن سريع للخليج والعالم. شركات الشحن: DHL وFedEx وAramex وUPS.');
epc_reg_tr($pdo, 'epc_lang_ar_caption', 'Arabic', 'Арабский', 'العربية');

$langSteps = array();
foreach (array('ar', 'ru', 'en') as $langCode) {
	$stmt = $pdo->prepare('SELECT `id`, `active` FROM `lang_languages` WHERE `lang_code` = ? LIMIT 1');
	$stmt->execute(array($langCode));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		if ((int) $row['active'] !== 1) {
			$pdo->prepare('UPDATE `lang_languages` SET `active` = 1 WHERE `id` = ?')->execute(array((int) $row['id']));
			$langSteps[] = $langCode . ': activated';
		} else {
			$langSteps[] = $langCode . ': already active';
		}
	} elseif ($langCode === 'ar') {
		$pdo->prepare(
			'INSERT INTO `lang_languages` (`lang_code`, `active`, `is_default`, `caption_str_key`, `restrict_edit`) VALUES (?, 1, 0, ?, 0)'
		)->execute(array('ar', 'epc_lang_ar_caption'));
		$langSteps[] = 'ar: inserted and activated';
	} else {
		$langSteps[] = $langCode . ': missing (not inserted)';
	}
}

$now = time();
$modules = '[1,22,32,34]';
$url = 'shipping-export';
$path = '/content/general_pages/epc_seo_shipping_export.php';

$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
$stmt->execute(array($url));
$contentId = $stmt->fetchColumn();

if ($contentId) {
	$pdo->prepare(
		'UPDATE `content` SET `alias`=?, `value`=?, `description`=?, `content_type`="php", `content`=?, `title_tag`=?, `description_tag`=?, `keywords_tag`=?, `modules_array`=?, `robots_tag`=?, `published_flag`=1, `time_edited`=? WHERE `id`=?'
	)->execute(array('shipping_export', 'epc_shipping_export_title', 'epc_shipping_export_desc', $path, 'epc_shipping_export_title', 'epc_shipping_export_desc', 'epc_shipping_export_desc', $modules, 'index, follow', $now, $contentId));
} else {
	$maxOrder = (int) $pdo->query('SELECT COALESCE(MAX(`order`), 0) FROM `content` WHERE `is_frontend` = 1')->fetchColumn();
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (0, ?, 1, ?, ?, 0, ?, 1, "php", ?, ?, ?, ?, "0", 0, ?, "", "index, follow", 0, 1, 0, ?, ?, ?)'
	)->execute(array($url, 'shipping_export', 'epc_shipping_export_title', 'epc_shipping_export_desc', $path, 'epc_shipping_export_title', 'epc_shipping_export_desc', 'epc_shipping_export_desc', $modules, $now, $now, $maxOrder + 1));
	$contentId = (int) $pdo->lastInsertId();
}

$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
$groups = $pdo->query('SELECT `id` FROM `groups`');
while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
	$pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)')->execute(array($contentId, (int) $g['id']));
}

echo "Regional SEO setup OK\n";
echo "languages: " . implode(', ', $langSteps) . "\n";
echo "shipping-export content_id={$contentId}\n";
echo "URL: /en/shipping-export (also /ar/shipping-export)\n";
echo "Page: {$path}\n";
echo "db=" . $cfg->db . "\n";
echo "Next: submit sitemap-index.php in Google Search Console\n";
