<?php
/**
 * GMS (Garage Management System) — register frontend + CP routes, schema, labour seed.
 * curl "https://www.epartscart.com/epc-gms-setup.php?token=epartscart-deploy-2026"
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

function epc_gms_lang(PDO $pdo, string $key, string $en, string $ru = ''): void
{
	if ($ru === '') {
		$ru = $en;
	}
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')
		->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')
		->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')
		->execute(array($key, 'ru', $ru));
}

function epc_gms_upsert_content(
	PDO $pdo,
	string $url,
	string $path,
	string $value,
	string $title,
	int $isFrontend,
	string $alias = '',
	string $description = ''
): int {
	$now = time();
	if ($alias === '') {
		$alias = preg_replace('/[^a-z0-9\-]+/i', '-', basename(str_replace('/', '-', $url))) ?: $url;
	}
	if ($description === '') {
		$description = $isFrontend ? 'Garage Management System' : 'Workshop garage desk';
	}
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = ? LIMIT 1');
	$st->execute(array($url, $isFrontend));
	$id = (int) $st->fetchColumn();
	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag`=1, `content_type`=\'php\', `content`=?, `value`=?, `title_tag`=?, `alias`=?, `description`=?, `time_edited`=? WHERE `id`=?'
		)->execute(array($path, $value, $title, $alias, $description, $now, $id));
		echo "Updated content id={$id} url={$url}\n";
		return $id;
	}

	$parent = 0;
	$level = 1;
	if ($isFrontend === 0) {
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
	} elseif (strpos($url, 'garage/') === 0) {
		// Nest under garage parent if present; otherwise top-level
		$gst = $pdo->prepare('SELECT `id`,`level` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
		$gst->execute(array('garage'));
		$grow = $gst->fetch(PDO::FETCH_ASSOC);
		if ($grow) {
			$parent = (int) $grow['id'];
			$level = (int) $grow['level'] + 1;
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
		$description,
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

echo "GMS setup " . date('c') . "\n";

epc_gms_lang($pdo, 'epc_menu_garage_login', 'Garage login', 'Вход в гараж');
epc_gms_lang($pdo, 'epc_menu_garage_manager', 'Garage Manager', 'Менеджер гаража');
epc_gms_lang($pdo, 'epc_title_garage_login', 'Garage Manager login', 'Вход менеджера гаража');
epc_gms_lang($pdo, 'epc_title_garage_manager', 'Garage Manager', 'Менеджер гаража');
epc_gms_lang($pdo, 'epc_menu_auto_workshop', 'Auto Workshop', 'Автосервис');
epc_gms_lang($pdo, 'epc_workshop_cp', 'Workshop & service', 'Цех и сервис');
epc_gms_lang($pdo, 'epc_portal_autoworkshop_guide', 'Auto Workshop Online', 'Auto Workshop Online');
echo "Lang keys: OK\n";

// Storefront GMS pages
epc_gms_upsert_content(
	$pdo,
	'garage/login',
	'/content/shop/workshop/garage_login.php',
	'epc_menu_garage_login',
	'epc_title_garage_login',
	1,
	'garage-login',
	'Garage staff login for GMS'
);

epc_gms_upsert_content(
	$pdo,
	'garage/manager',
	'/content/shop/workshop/garage_manager_portal.php',
	'epc_menu_garage_manager',
	'epc_title_garage_manager',
	1,
	'garage-manager',
	'Garage Manager System portal'
);

epc_gms_upsert_content(
	$pdo,
	'auto-workshop',
	'/content/general_pages/epc_autoworkshop_storefront_page.php',
	'epc_menu_auto_workshop',
	'epc_menu_auto_workshop',
	1,
	'auto-workshop',
	'Book and track workshop service'
);

// CP workshop desk
epc_gms_upsert_content(
	$pdo,
	'shop/workshop/workshop',
	'/<backend_dir>/content/shop/workshop/workshop_main_page.php',
	'epc_workshop_cp',
	'epc_workshop_cp',
	0,
	'workshop'
);

epc_gms_upsert_content(
	$pdo,
	'control/portal/epc_autoworkshop_guide',
	'/<backend_dir>/content/control/portal/epc_autoworkshop_guide.php',
	'epc_portal_autoworkshop_guide',
	'epc_portal_autoworkshop_guide',
	0,
	'epc_autoworkshop_guide'
);

require_once __DIR__ . '/content/shop/workshop/epc_workshop_helpers.php';
epc_ws_ensure_schema($pdo);
$labour = epc_ws_seed_labour_ops($pdo);
$seed = epc_ws_seed_demo($pdo);
echo 'Schema OK · labour_ops seeded=' . $labour . ' · bays=' . $seed['bays'] . ' techs=' . $seed['techs'] . ' jobs=' . $seed['jobs'] . "\n";

$jobCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_ws_jobs`')->fetchColumn();
$apptCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_ws_appointments`')->fetchColumn();
echo "Jobs={$jobCount} appointments={$apptCount}\n";

$root = rtrim(__DIR__, '/') . '/';
$files = array(
	'content/shop/workshop/epc_workshop_helpers.php',
	'content/shop/workshop/garage_login.php',
	'content/shop/workshop/garage_manager_portal.php',
	'content/shop/workshop/garage_manager.js',
	'content/shop/workshop/ajax_garage_manager.php',
	'content/shop/workshop/ajax_workshop_public.php',
	'content/shop/workshop/epc_garage_header_link.php',
	'content/general_pages/epc_gms_portal.css',
	'content/general_pages/epc_storefront_auth_links.php',
	'content/general_pages/epc_autoworkshop_storefront_page.php',
	'cp/content/shop/workshop/workshop_main_page.php',
	'cp/content/shop/workshop/ajax_workshop_endpoint.php',
	'cp/content/shop/workshop/epc_workshop.css',
	'cp/content/shop/workshop/epc_workshop.js',
	'templates/nero/desktop.php',
	'content/shop/docpart/garage/garage.php',
);
foreach ($files as $rel) {
	$abs = $root . $rel;
	echo $rel . ': ' . (is_file($abs) ? ('OK bytes=' . filesize($abs)) : 'MISSING') . "\n";
}

echo "\nDone.\n";
echo "Login:   /en/garage/login\n";
echo "Manager: /en/garage/manager\n";
echo "Book:    /en/auto-workshop\n";
echo "MyGarage:/en/garazh\n";
echo "CP:      /cp/shop/workshop/workshop\n";
