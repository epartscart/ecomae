<?php
/**
 * Auto Workshop / Garage — CMS pages, schema, demo seed.
 * curl "https://www.epartscart.com/epc-autoworkshop-setup.php?token=epartscart-deploy-2026"
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

function epc_aw_upsert_content(
	PDO $pdo,
	string $url,
	string $path,
	string $value,
	string $title,
	int $isFrontend,
	string $alias = ''
): int {
	$now = time();
	if ($alias === '') {
		$alias = preg_replace('/[^a-z0-9\-]+/i', '-', basename(str_replace('/', '-', $url))) ?: $url;
	}
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = ? LIMIT 1');
	$st->execute(array($url, $isFrontend));
	$id = (int) $st->fetchColumn();
	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag`=1, `content_type`=\'php\', `content`=?, `value`=?, `title_tag`=?, `alias`=?, `time_edited`=? WHERE `id`=?'
		)->execute(array($path, $value, $title, $alias, $now, $id));
		echo "Updated content id={$id} url={$url}\n";
		return $id;
	}

	$parent = 0;
	$level = 1;
	if ($isFrontend === 0) {
		// Nest CP pages under SHOP or SYSTEM when possible
		$parentUrl = (strpos($url, 'shop/') === 0) ? 'shop' : 'control';
		$pst = $pdo->prepare('SELECT `id`,`level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
		$pst->execute(array($parentUrl));
		$prow = $pst->fetch(PDO::FETCH_ASSOC);
		if ($prow) {
			$parent = (int) $prow['id'];
			$level = (int) $prow['level'] + 1;
		}
		if (strpos($url, 'shop/workshop/') === 0) {
			$wst = $pdo->prepare('SELECT `id`,`level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
			$wst->execute(array('shop/workshop'));
			$wrow = $wst->fetch(PDO::FETCH_ASSOC);
			if ($wrow) {
				$parent = (int) $wrow['id'];
				$level = (int) $wrow['level'] + 1;
			}
		}
	}

	$maxOrder = (int) $pdo->query('SELECT COALESCE(MAX(`order`),0) FROM `content`')->fetchColumn();
	$pdo->prepare(
		'INSERT INTO `content`
		(`count`,`url`,`level`,`alias`,`value`,`parent`,`description`,`is_frontend`,`content_type`,`content`,
		 `title_tag`,`description_tag`,`keywords_tag`,`author_tag`,`main_flag`,`modules_array`,`css_js`,`robots_tag`,
		 `system_flag`,`published_flag`,`open`,`time_created`,`time_edited`,`order`)
		 VALUES (0,?,?,?,?,?,?,?,\'php\',?,?, \'0\',\'0\',\'0\',0,\'[]\',\'\',\'\',0,1,0,?,?,?)'
	)->execute(array(
		$url,
		$level,
		$alias,
		$value,
		$parent,
		$isFrontend ? 'Auto Workshop Online' : 'Workshop garage desk',
		$isFrontend,
		$path,
		$title,
		$now,
		$now,
		$maxOrder + 1,
	));
	$id = (int) $pdo->lastInsertId();
	echo "Inserted content id={$id} url={$url}\n";
	return $id;
}

epc_aw_lang($pdo, 'epc_portal_autoworkshop_guide', 'Auto Workshop Online', 'Auto Workshop Online');
epc_aw_lang($pdo, 'epc_cp_group_workshop', 'Workshop', 'Цех');
epc_aw_lang($pdo, 'epc_workshop_cp', 'Workshop & service', 'Цех и сервис');
epc_aw_lang($pdo, 'epc_menu_auto_workshop', 'Auto Workshop', 'Автосервис');
epc_aw_lang($pdo, '4756', 'Page script missing', 'Скрипт страницы отсутствует');
echo "Lang keys: OK\n";

epc_aw_upsert_content(
	$pdo,
	'control/portal/epc_autoworkshop_guide',
	'/<backend_dir>/content/control/portal/epc_autoworkshop_guide.php',
	'epc_portal_autoworkshop_guide',
	'epc_portal_autoworkshop_guide',
	0,
	'epc_autoworkshop_guide'
);

epc_aw_upsert_content(
	$pdo,
	'shop/workshop/workshop',
	'/<backend_dir>/content/shop/workshop/workshop_main_page.php',
	'epc_workshop_cp',
	'epc_workshop_cp',
	0,
	'workshop'
);

$frontId = epc_aw_upsert_content(
	$pdo,
	'auto-workshop',
	'/content/general_pages/epc_autoworkshop_storefront_page.php',
	'epc_menu_auto_workshop',
	'epc_menu_auto_workshop',
	1,
	'auto-workshop'
);

// Ensure frontend published + main modules empty array
$pdo->prepare('UPDATE `content` SET `published_flag`=1, `is_frontend`=1, `content_type`=\'php\', `level`=1 WHERE `id`=?')
	->execute(array($frontId));
echo "Storefront content id={$frontId} published\n";

require_once __DIR__ . '/content/shop/workshop/epc_workshop_helpers.php';
epc_ws_ensure_schema($pdo);
$seed = epc_ws_seed_demo($pdo);
echo 'Schema OK · seed bays=' . $seed['bays'] . ' techs=' . $seed['techs'] . ' new_jobs=' . $seed['jobs'] . "\n";

$jobCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_ws_jobs`')->fetchColumn();
echo "Total jobs in DB: {$jobCount}\n";

$root = rtrim(__DIR__, '/') . '/';
$files = array(
	'cp/content/control/portal/epc_autoworkshop_guide.php',
	'content/general_pages/epc_autoworkshop_storefront_page.php',
	'cp/content/shop/workshop/workshop_main_page.php',
	'content/shop/workshop/epc_workshop_helpers.php',
	'cp/content/shop/workshop/ajax_workshop_endpoint.php',
	'content/shop/workshop/ajax_workshop_public.php',
	'cp/content/shop/workshop/epc_workshop.css',
	'cp/content/shop/workshop/epc_workshop.js',
	'content/general_pages/epc_workshop_css.php',
);
foreach ($files as $rel) {
	$abs = $root . $rel;
	echo $rel . ': ' . (is_file($abs) ? ('OK bytes=' . filesize($abs)) : 'MISSING') . "\n";
}

echo "\nDone.\n";
echo "Guide: /cp/control/portal/epc_autoworkshop_guide\n";
echo "Site:  /auto-workshop\n";
echo "CP:    /cp/shop/workshop/workshop\n";
