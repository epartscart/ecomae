<?php
/**
 * Ensure Auto Workshop Online CMS pages + language strings.
 * curl "https://www.epartscart.com/epc-autoworkshop-setup.php?token=..."
 */
declare(strict_types=1);
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit('DB failed: ' . $e->getMessage() . "\n");
}

function epc_aw_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')
		->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')
		->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')
		->execute(array($key, 'ru', $ru));
}

epc_aw_lang($pdo, 'epc_portal_autoworkshop_guide', 'Auto Workshop Online', 'Auto Workshop Online');
epc_aw_lang($pdo, 'epc_cp_group_workshop', 'Workshop', 'Цех');
epc_aw_lang($pdo, 'epc_workshop_cp', 'Workshop & service', 'Цех и сервис');
epc_aw_lang($pdo, '4756', 'Page script missing', 'Скрипт страницы отсутствует');
echo "Lang keys: OK\n";

$pages = array(
	array(
		'url' => 'control/portal/epc_autoworkshop_guide',
		'path' => '/<backend_dir>/content/control/portal/epc_autoworkshop_guide.php',
		'value' => 'epc_portal_autoworkshop_guide',
		'title' => 'epc_portal_autoworkshop_guide',
	),
	array(
		'url' => 'auto-workshop',
		'path' => '/content/general_pages/epc_autoworkshop_storefront_page.php',
		'value' => 'Auto Workshop Online',
		'title' => 'Auto Workshop Online',
		'frontend' => 1,
	),
	array(
		'url' => 'shop/workshop/workshop',
		'path' => '/<backend_dir>/content/shop/workshop/workshop_main_page.php',
		'value' => 'epc_workshop_cp',
		'title' => 'epc_workshop_cp',
	),
);

foreach ($pages as $p) {
	$frontend = isset($p['frontend']) ? (int) $p['frontend'] : 0;
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = ? LIMIT 1');
	$st->execute(array($p['url'], $frontend));
	$id = (int) $st->fetchColumn();
	if ($id > 0) {
		$pdo->prepare('UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `value` = ?, `title_tag` = ? WHERE `id` = ?')
			->execute(array($p['path'], $p['value'], $p['title'], $id));
		echo "Updated content id={$id} url={$p['url']}\n";
	} else {
		echo "Missing content row for {$p['url']} (create via CMS if needed)\n";
	}
}

$root = rtrim(__DIR__, '/') . '/';
$files = array(
	'cp/content/control/portal/epc_autoworkshop_guide.php',
	'content/general_pages/epc_autoworkshop_storefront_page.php',
	'cp/content/shop/workshop/workshop_main_page.php',
);
foreach ($files as $rel) {
	$abs = $root . $rel;
	echo $rel . ': ' . (is_file($abs) ? ('OK bytes=' . filesize($abs)) : 'MISSING') . "\n";
}

echo "\nDone.\n";
echo "Guide: /cp/control/portal/epc_autoworkshop_guide\n";
echo "Site:  /auto-workshop\n";
echo "CP:    /cp/shop/workshop/workshop\n";
