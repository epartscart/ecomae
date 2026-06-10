<?php
/**
 * Smoke test: CP Price Management (/cp/shop/price-management)
 * GET: token=epartscart-deploy-2026&key=<tech_key>
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

$report = array(
	'ok' => true,
	'timestamp' => date('c'),
	'url' => 'https://www.epartscart.com/' . $DP_Config->backend_dir . '/shop/price-management',
	'checks' => array(),
);

function pm_check(array &$report, $id, $pass, $detail = '')
{
	$report['checks'][] = array(
		'id' => $id,
		'pass' => (bool)$pass,
		'detail' => $detail,
	);
	if (!$pass) {
		$report['ok'] = false;
	}
}

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
	);
} catch (Throwable $e) {
	pm_check($report, 'db_connect', false, $e->getMessage());
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

pm_check($report, 'db_connect', true, 'Connected');

$phpPath = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/pricing/price_management.php';
$pricingPath = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';
pm_check($report, 'file_price_management', is_file($phpPath), $phpPath);
pm_check($report, 'file_epc_pricing', is_file($pricingPath), $pricingPath);

	$tables = array('epc_price_profiles', 'epc_price_profile_brand_rules', 'epc_price_profile_article_rules', 'epc_price_settings');
foreach ($tables as $table) {
	$row = $pdo->query("SHOW TABLES LIKE " . $pdo->quote($table))->fetch(PDO::FETCH_NUM);
	$exists = !empty($row);
	pm_check($report, 'table_' . $table, $exists, $exists ? 'exists' : 'missing');
}

$route = $pdo->query("SELECT `id`, `url`, `published_flag`, `content_type`, `content` FROM `content` WHERE `is_frontend` = 0 AND `url` = 'shop/price-management' LIMIT 1")->fetch();
pm_check($report, 'cp_route', !empty($route), $route ? json_encode($route, JSON_UNESCAPED_UNICODE) : 'route not found');
if ($route) {
	pm_check($report, 'cp_route_published', (int)$route['published_flag'] === 1, 'published_flag=' . $route['published_flag']);
	pm_check($report, 'cp_route_php', (string)$route['content_type'] === 'php', 'content_type=' . $route['content_type']);
	pm_check($report, 'cp_route_content', strpos((string)$route['content'], 'price_management.php') !== false, (string)$route['content']);
}

$menu = $pdo->query("SELECT `id`, `caption`, `url`, `items_group`, `order` FROM `control_items` WHERE `url` LIKE '%/shop/price-management' LIMIT 1")->fetch();
pm_check($report, 'cp_menu', !empty($menu), $menu ? json_encode($menu, JSON_UNESCAPED_UNICODE) : 'menu item not found');

$profiles = $pdo->query("SELECT p.`code`, p.`group_id`, g.`value`, p.`vat_percent` FROM `epc_price_profiles` p INNER JOIN `groups` g ON g.`id` = p.`group_id` ORDER BY p.`id`")->fetchAll();
$profileCodes = array_column($profiles, 'code');
pm_check($report, 'profiles_count', count($profiles) >= 2, count($profiles) . ' profile(s)');
pm_check($report, 'profile_retail', in_array('retail', $profileCodes, true), in_array('retail', $profileCodes, true) ? 'ok' : 'missing retail');
pm_check($report, 'profile_wholesale', in_array('wholesale', $profileCodes, true), in_array('wholesale', $profileCodes, true) ? 'ok' : 'missing wholesale');

$settings = $pdo->query("SELECT `setting_key`, `setting_value` FROM `epc_price_settings`")->fetchAll(PDO::FETCH_KEY_PAIR);
pm_check($report, 'setting_vat', isset($settings['vat_percent']), 'vat_percent=' . ($settings['vat_percent'] ?? 'n/a'));
pm_check($report, 'setting_guest_margin', array_key_exists('guest_margin_percent', $settings), 'guest_margin_percent=' . ($settings['guest_margin_percent'] ?? 'n/a'));

$rulesCount = (int)$pdo->query("SELECT COUNT(*) FROM `epc_price_profile_brand_rules`")->fetchColumn();
$rulesSample = $pdo->query("SELECT r.`manufacturer`, r.`margin_percent`, r.`visible`, p.`code` AS profile_code FROM `epc_price_profile_brand_rules` r INNER JOIN `epc_price_profiles` p ON p.`group_id` = r.`group_id` ORDER BY r.`id` DESC LIMIT 10")->fetchAll();
pm_check($report, 'brand_rules_table', true, $rulesCount . ' rule(s)');

$brandsCount = 0;
try {
	$brandsCount = (int)$pdo->query("SELECT COUNT(*) FROM (SELECT DISTINCT `name` AS b FROM `shop_docpart_manufacturers` WHERE `name` != '' UNION SELECT DISTINCT `manufacturer` AS b FROM `shop_docpart_prices_data` WHERE `manufacturer` != '') x")->fetchColumn();
} catch (Throwable $e) {
	pm_check($report, 'brands_source', false, $e->getMessage());
}
pm_check($report, 'brands_available', $brandsCount > 0, $brandsCount . ' brand(s) in catalog');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';
pm_check($report, 'pricing_apply_price_rules', function_exists('epc_pricing_apply_price_rules'), function_exists('epc_pricing_apply_price_rules') ? 'ok' : 'missing');

$articleRulesCount = 0;
try {
	$articleRulesCount = (int)$pdo->query('SELECT COUNT(*) FROM `epc_price_profile_article_rules`')->fetchColumn();
	pm_check($report, 'table_epc_price_profile_article_rules', true, $articleRulesCount . ' article rule(s)');
} catch (Throwable $e) {
	pm_check($report, 'table_epc_price_profile_article_rules', false, $e->getMessage());
}

$retailGroup = 0;
foreach ($profiles as $p) {
	if ($p['code'] === 'retail') {
		$retailGroup = (int)$p['group_id'];
		break;
	}
}
if ($retailGroup > 0) {
	$mazdaDemo = epc_pricing_apply_price_rules($pdo, $retailGroup, 'MAZDA', 100.0, 0.0, '');
	pm_check(
		$report,
		'demo_retail_mazda_margin',
		!empty($mazdaDemo['visible']) && (float)$mazdaDemo['breakdown']['final_price'] >= 115.0,
		'final=' . ($mazdaDemo['breakdown']['final_price'] ?? 'n/a')
	);
}

$session = $pdo->query(
	"SELECT s.`session`, s.`2fa_session`, s.`user_id`, u.`email`
	 FROM `sessions` s INNER JOIN `users` u ON u.`user_id` = s.`user_id`
	 WHERE s.`type` = 1 ORDER BY s.`last_activiti_time` DESC LIMIT 1"
)->fetch();

if (!$session) {
	pm_check($report, 'http_authenticated', false, 'No active admin session in DB');
} else {
	$host = 'www.epartscart.com';
	$path = '/' . $DP_Config->backend_dir . '/shop/price-management';
	$cookie = 'admin_session=' . urlencode($session['session']) . '; admin_u_id=' . (int)$session['user_id'];
	if (!empty($session['2fa_session'])) {
		$cookie .= '; 2fa=' . urlencode($session['2fa_session']);
	}
	$ch = curl_init('https://' . $host . $path);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 45,
		CURLOPT_HTTPHEADER => array('Cookie: ' . $cookie),
		CURLOPT_SSL_VERIFYPEER => false,
	));
	$html = (string)curl_exec($ch);
	$http = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);

	$loginPage = stripos($html, 'Log in form') !== false;
	$fatal = preg_match('/Fatal error|Parse error|Uncaught/i', $html) === 1;
	$hasProfiles = stripos($html, 'Customer price profiles') !== false;
	$hasGuest = stripos($html, 'Guest / non-login customer margin') !== false;
	$hasBrandVis = stripos($html, 'Brand visibility by customer profile') !== false;
	$hasAssign = stripos($html, 'Assign customer to profile') !== false;
	$hasRules = stripos($html, 'Brand rules by profile') !== false;
	$hasGuide = stripos($html, 'Step-by-step guide') !== false;
	$hasDemo = stripos($html, 'Live demo') !== false;
	$hasArticleRules = stripos($html, 'Article rules by profile') !== false;
	$hasRetail = stripos($html, 'retail') !== false;
	$title = '';
	if (preg_match('/<title>([^<]+)</i', $html, $m)) {
		$title = trim($m[1]);
	}

	pm_check($report, 'http_status', $http === 200, 'HTTP ' . $http);
	pm_check($report, 'http_not_login', !$loginPage, $loginPage ? 'redirected to login' : 'authenticated');
	pm_check($report, 'http_no_php_fatal', !$fatal, $fatal ? 'PHP fatal in response' : 'clean');
	pm_check($report, 'http_title', stripos($title, 'Price') !== false || stripos($title, 'pricing') !== false, $title);
	pm_check($report, 'http_panel_profiles', $hasProfiles, $hasProfiles ? 'found' : 'missing');
	pm_check($report, 'http_panel_guest', $hasGuest, $hasGuest ? 'found' : 'missing');
	pm_check($report, 'http_panel_brand_visibility', $hasBrandVis, $hasBrandVis ? 'found' : 'missing');
	pm_check($report, 'http_panel_assign', $hasAssign, $hasAssign ? 'found' : 'missing');
	pm_check($report, 'http_panel_rules', $hasRules, $hasRules ? 'found' : 'missing');
	pm_check($report, 'http_panel_guide', $hasGuide, $hasGuide ? 'found' : 'missing');
	pm_check($report, 'http_panel_demo', $hasDemo, $hasDemo ? 'found' : 'missing');
	pm_check($report, 'http_panel_article_rules', $hasArticleRules, $hasArticleRules ? 'found' : 'missing');
	pm_check($report, 'http_profile_retail', $hasRetail, $hasRetail ? 'retail visible' : 'retail not in HTML');

	$report['http'] = array(
		'admin_email' => $session['email'],
		'bytes' => strlen($html),
		'title' => $title,
	);
}

$report['data'] = array(
	'profiles' => $profiles,
	'settings' => $settings,
	'brand_rules_count' => $rulesCount,
	'brand_rules_sample' => $rulesSample,
	'brands_in_catalog' => $brandsCount,
);

$passed = 0;
$failed = 0;
foreach ($report['checks'] as $c) {
	if ($c['pass']) {
		$passed++;
	} else {
		$failed++;
	}
}
$report['summary'] = array('passed' => $passed, 'failed' => $failed, 'total' => $passed + $failed);

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
