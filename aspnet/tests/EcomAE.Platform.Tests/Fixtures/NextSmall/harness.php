<?php
// Runs the next-small includes listed in cases.json (one process per case) and records PHP 8.3 output.
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_next_' . substr(md5(uniqid('', true)), 0, 12);
	foreach (array(
		'/content/users',
		'/content/general_pages',
		'/content/shop/marketing',
		'/content/shop/pricing',
		'/content/shop/catalogue/tree_lists',
		'/content/shop/docpart',
		'/content/originalnye-katalogi',
		'/modules/login/epc_social',
		'/cp/content/control/portal',
		'/cp/content/shop/prices_upload',
		'/cp/content/shop/data_transfer/pages',
		'/cp/content/shop/document_control',
		'/cp/content/control/check_admin_access',
		'/cp/content/control/version_control',
		'/cp/content/users',
		'/cp/content/lang',
		'/cp/modules/lang',
		'/modules/lang',
		'/lang',
	) as $dir) {
		@mkdir($doc . $dir, 0777, true);
	}

	$isAdmin = !empty($case['is_admin']);
	$adminGroups = $case['admin_groups'] ?? array(1);
	file_put_contents(
		$doc . '/content/users/dp_user.php',
		'<?php if (!class_exists("DP_User")) { class DP_User { public static function isAdmin() { return !empty($GLOBALS["__case_is_admin"]); } public static function getAdminSession() { return $GLOBALS["__case_admin_session"] ?? false; } public static function getAdminProfile() { return array("groups" => $GLOBALS["__case_admin_groups"] ?? array()); } public static function getUserId() { return 1; } } }'
	);
	$forceNodb = !empty($case['force_nodb']);
	file_put_contents(
		$doc . '/config.php',
		'<?php if (!class_exists("DP_Config")) { class DP_Config { public $backend_dir = "cp"; public $host = "127.0.0.1"; public $db = "x"; public $user = "x"; public $password = "x"; public $secret_succession = "sec"; public $multilang = 0; public function __construct() { foreach (($GLOBALS["__case_config"] ?? array()) as $k => $v) { $this->$k = $v; } } } }'
	);
	file_put_contents(
		$doc . '/content/general_pages/epc_portal.php',
		'<?php function epc_portal_apply_config($c) {} function epc_portal_is_super_cp_host() { return !empty($GLOBALS["__case_is_super"]); } function epc_portal_is_epartscart_hostname($h = null) { $h = strtolower(trim((string)($h ?? ($_SERVER["HTTP_HOST"] ?? "")))); if (strpos($h, ":") !== false) { $h = explode(":", $h, 2)[0]; } $h = preg_replace("/^www\\./", "", $h); return $h === "epartscart.com"; } function epc_portal_host() { return $_SERVER["HTTP_HOST"] ?? ""; } function epc_deploy_token() { return "epartscart-deploy-2026"; }'
	);
	file_put_contents($doc . '/content/general_pages/epc_portal_tenant.php', '<?php require_once $_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal.php";');
	file_put_contents($doc . '/content/general_pages/epc_cp_page_frame.php', '<?php function epc_cp_page_frame_open($a = array()) {} function epc_cp_page_frame_close() {}');
	file_put_contents($doc . '/content/general_pages/epc_cp_page_assets.php', '<?php function epc_cp_page_asset_version() { return "1"; } function epc_cp_register_page_assets($a, $b) {}');
	file_put_contents($doc . '/content/shop/marketing/epc_marketing_broadcast_helpers.php', '<?php function epc_mb_backend() { return "cp"; } function epc_mb_shop_context($c) { return array("shop_name" => "Your shop", "shop_url" => "/"); } function epc_mb_render_hub() {}');
	file_put_contents($doc . '/content/users/stop_csrf.php', '<?php');
	file_put_contents($doc . '/lang/dp_lang.php', '<?php function multilang_init() { return array("lang" => "en", "lang_href" => "/en/", "lang_href_no_slash" => "en"); }');
	file_put_contents($doc . '/epc_deploy_auth.php', '<?php function epc_deploy_token() { return "epartscart-deploy-2026"; }');
	$social = $case['social'] ?? array('enabled' => false, 'tenant_key' => '', 'google_start_url' => '/epc-auth-google-start.php');
	file_put_contents(
		$doc . '/content/general_pages/epc_auth_social.php',
		'<?php function epc_auth_login_context_for_ui($c) { return array("google_start_url" => ' . var_export($social['google_start_url'] ?? '/epc-auth-google-start.php', true) . ', "tenant_key" => ' . var_export((string)($social['tenant_key'] ?? ''), true) . '); } function epc_auth_social_providers() { return array("google" => array("enabled" => ' . (!empty($social['enabled']) ? 'true' : 'false') . ')); }'
	);
	$profile = array_key_exists('industry_profile', $case) ? $case['industry_profile'] : null;
	$title = $case['industry_title'] ?? '';
	$items = $case['industry_items'] ?? array();
	$render = $case['industry_render'] ?? '';
	file_put_contents(
		$doc . '/content/general_pages/epc_portal_industry_catalog.php',
		'<?php function epc_portal_industry_catalog_profile() { return $GLOBALS["__ind_profile"]; } function epc_portal_industry_catalog_section_title($p) { return $GLOBALS["__ind_title"]; } function epc_portal_industry_catalog_categories($p) { return $GLOBALS["__ind_items"]; } function epc_portal_industry_catalog_render($p) { echo $GLOBALS["__ind_render"]; }'
	);

	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE epc_price_profiles (group_id INT, moq_multiplier DECIMAL(6,2), code VARCHAR(32), margin_percent DECIMAL(6,2))');
	$pdo->exec('CREATE TABLE users_groups_bind (record_id INT, user_id INT, group_id INT)');
	$pdo->exec("INSERT INTO epc_price_profiles VALUES (1, 1.00, 'retail', 0), (2, 1.50, 'fleet', 0)");
	$pdo->exec('INSERT INTO users_groups_bind VALUES (1, 9, 1), (2, 3, 2)');
	$pdo->exec('CREATE TABLE content_access (content_id INT, group_id INT)');
	foreach (($case['content_access'] ?? array()) as $cid => $groups) {
		foreach ($groups as $gid) {
			$pdo->prepare('INSERT INTO content_access VALUES (?, ?)')->execute(array($cid, $gid));
		}
	}
	$pdo->exec('CREATE TABLE shop_docpart_cars (id INT, image VARCHAR(64), caption VARCHAR(64))');
	$pdo->exec('CREATE TABLE shop_docpart_cars_catalogue_links (car_id INT)');
	foreach (($case['cars'] ?? array()) as $car) {
		$pdo->prepare('INSERT INTO shop_docpart_cars VALUES (?, ?, ?)')->execute(array($car['id'], $car['image'], $car['caption']));
		if ($car['link_id'] !== null) {
			$pdo->prepare('INSERT INTO shop_docpart_cars_catalogue_links VALUES (?)')->execute(array($car['link_id']));
		}
	}
	$pdo->exec('CREATE TABLE version_control (id INT, version VARCHAR(32), time INT)');
	foreach (($case['versions'] ?? array()) as $row) {
		$pdo->prepare('INSERT INTO version_control VALUES (?, ?, ?)')->execute(array($row['id'], $row['version'], $row['time']));
	}
	$pdo->exec('CREATE TABLE lang_languages (lang_code VARCHAR(8), caption_str_key INT, active INT)');
	foreach (($case['langs'] ?? array()) as $lang) {
		$pdo->prepare('INSERT INTO lang_languages VALUES (?, ?, 1)')->execute(array($lang['lang_code'], $lang['caption_str_key']));
	}
	$pdo->exec('CREATE TABLE `groups` (id INT PRIMARY KEY, parent INT, `count` INT)');
	foreach (($case['groups_tree'] ?? array()) as $id => $node) {
		$pdo->prepare('INSERT IGNORE INTO `groups` VALUES (?, 0, ?)')->execute(array($id, $node['count']));
	}

	foreach (($case['stubs'] ?? array()) as $rel => $body) {
		$path = $doc . '/' . ltrim($rel, '/');
		@mkdir(dirname($path), 0777, true);
		file_put_contents($path, $body);
	}

	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		if (is_dir($doc)) {
			$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
			foreach ($it as $file) {
				$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
			}
			@rmdir($doc);
		}
	};

	$GLOBALS['__root'] = $root;
	$GLOBALS['__case_is_admin'] = $isAdmin;
	$GLOBALS['__case_is_super'] = !empty($case['is_super']);
	$GLOBALS['__case_admin_session'] = array_key_exists('admin_session', $case) ? $case['admin_session'] : array('id' => 1);
	$GLOBALS['__case_admin_groups'] = $adminGroups;
	$GLOBALS['__case_config'] = $case['config'] ?? array('backend_dir' => 'cp');
	$GLOBALS['__ind_profile'] = $profile;
	$GLOBALS['__ind_title'] = $title;
	$GLOBALS['__ind_items'] = $items;
	$GLOBALS['__ind_render'] = $render;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['HTTP_HOST'] = $case['host'] ?? 'localhost';
	$_SERVER['REMOTE_ADDR'] = $case['remote_addr'] ?? '127.0.0.1';
	$_SERVER['REQUEST_URI'] = $case['uri'] ?? '/';
	$_GET = $case['get'] ?? array();
	$_POST = $case['post'] ?? array();
	$_COOKIE = array();
	$_REQUEST = array_merge($_GET, $_POST);
	$DP_Config = new stdClass();
	foreach (($case['config'] ?? array('backend_dir' => 'cp')) as $key => $value) {
		$DP_Config->$key = $value;
	}
	$GLOBALS['DP_Config'] = $DP_Config;
	$DP_Template = new stdClass();
	$DP_Template->name = $case['template'] ?? 'bootstrap_admin';
	$GLOBALS['DP_Template'] = $DP_Template;
	foreach (($case['vars'] ?? array()) as $key => $value) {
		$$key = $value;
	}
	function translate_str_by_id($key, $lang = null) { return '{' . $key . '}'; }
	function print_backend_button($button_params)
	{
		$onclick = isset($button_params['onclick']) ? 'onclick="' . $button_params['onclick'] . '"' : '';
		$target = '';
		echo "\t\t<a class=\"panel_a\" href=\"{$button_params['url']}\" {$onclick} {$target}>\n";
		echo "\t\t\t<div class=\"panel_a_img\" style=\"background-color: {$button_params['background_color']};width:96px;height:96px;display:table-cell;vertical-align:middle;\"><i class=\"{$button_params['fontawesome_class']}\" style=\"color:#FFF;font-size:45px\"></i></div>\n";
		echo "\t\t\t<div class=\"panel_a_caption\">{$button_params['caption']}</div>\n\t\t</a>\n\t\t";
	}
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	if (!defined('_PYPRICES_CRONTAB_')) {
		define('_PYPRICES_CRONTAB_', 1);
	}
	$db_link = $pdo;
	$GLOBALS['db'] = $db_link;
	$GLOBALS['db_link'] = $db_link;

	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$headers = array();
		foreach (headers_list() as $header) {
			$headers[] = $header;
		}
		$code = http_response_code();
		$cleanup();
		echo json_encode(
			array(
				'name' => $case['name'],
				'output' => $html,
				'result' => $GLOBALS['__result'] ?? null,
				'headers' => $headers,
				'status' => $code,
			),
			JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR
		);
	});
	ob_start();
	if (isset($case['prelude'])) {
		eval($case['prelude']);
	}
	if (isset($case['file'])) {
		$include = $root . '/' . $case['file'];
		if (!empty($case['copy_file'])) {
			$copy = $doc . '/' . $case['file'];
			@mkdir(dirname($copy), 0777, true);
			copy($include, $copy);
			$include = $copy;
		}
		if ($case['file'] === 'content/general_pages/epc_mobile_app_landing.php' || str_contains($case['eval'] ?? '', 'epc_mobile') || str_contains($case['eval'] ?? '', 'epc_portal_alias') || str_contains($case['eval'] ?? '', 'epc_moq') || str_contains($case['eval'] ?? '', 'epc_fashion') || str_contains($case['eval'] ?? '', 'addItemToDump') || str_contains($case['eval'] ?? '', 'DocpartManufacturer') || str_contains($case['eval'] ?? '', 'getInsertedGroups')) {
			// function files are included via eval below
		} else {
			include $include;
		}
	}
	if (in_array($case['name'], array('alias_path_null', 'alias_path_empty', 'alias_path_query', 'alias_canonical_cp', 'alias_canonical_same', 'alias_canonical_home', 'alias_redirect_cli', 'alias_bos_tenant', 'alias_bos_super'), true)) {
		require_once $root . '/content/general_pages/epc_portal_route_aliases.php';
	}
	if (str_starts_with($case['name'], 'moq_')) {
		require_once $root . '/content/shop/pricing/epc_moq_helpers.php';
	}
	if ($case['name'] === 'fashion_tree') {
		require_once $root . '/content/shop/price_engine/epc_fashion_taxonomy.php';
	}
	if (str_starts_with($case['name'], 'mobile_')) {
		require_once $root . '/content/general_pages/epc_mobile_app_landing.php';
	}
	if (str_starts_with($case['name'], 'add_item')) {
		require_once $root . '/content/shop/catalogue/tree_lists/helper.php';
	}
	if (str_starts_with($case['name'], 'manufacturer_')) {
		require_once $root . '/content/shop/docpart/DocpartManufacturer.php';
	}
	if ($case['name'] === 'inserted_groups') {
		require_once $root . '/cp/content/users/helper.php';
	}
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	if (isset($case['eval_after'])) {
		$GLOBALS['__result'] = eval($case['eval_after']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/next_small_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/next_small_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
