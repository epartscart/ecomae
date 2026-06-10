<?php
/**
 * Social Media Hub — post-deploy verification probe.
 * https://www.ecomae.com/epc-social-media-verify.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/social_media/epc_social_media_helpers.php';
require_once __DIR__ . '/content/social_media/epc_social_media_pack_data.php';

$cfg = new DP_Config();
$checks = array();

$files = array(
	'content/social_media/epc_social_media_helpers.php',
	'content/social_media/epc_social_media_pack_data.php',
	'content/general_pages/epc_social_media_hub_css.php',
	'content/general_pages/epc_social_media_hub_js.php',
	'content/general_pages/epc_social_media_hub_config.php',
	'content/general_pages/ajax_epc_social_media.php',
	'cp/content/control/portal/epc_social_media_hub.php',
	'cp/content/control/portal/epc_social_media_hub_config.php',
	'cp/content/control/portal/epc_social_media_hub_panel.php',
	'cp/content/control/portal/epc_social_media_hub.js',
	'cp/content/control/portal/ajax_epc_social_media.php',
	'epc-social-media-setup-all.php',
);
foreach ($files as $f) {
	$checks['file:' . $f] = is_file(__DIR__ . '/' . $f);
}

try {
	$host = trim((string) $cfg->host);
	if ($host === '' || strtolower($host) === 'localhost') {
		$host = '127.0.0.1';
	}
	$pdo = new PDO(
		'mysql:host=' . $host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_social_ensure_schema($pdo);
	$checks['schema_accounts'] = (bool) $pdo->query("SHOW TABLES LIKE 'epc_social_accounts'")->fetch();
	$checks['schema_drafts'] = (bool) $pdo->query("SHOW TABLES LIKE 'epc_social_post_drafts'")->fetch();
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array('control/portal/epc_social_media_hub'));
	$checks['cp_content_route'] = (int) $st->fetchColumn() > 0;
	$gst = $pdo->prepare('SELECT `id` FROM `control_groups` WHERE `caption` = ? LIMIT 1');
	$gst->execute(array('epc_cp_group_portal'));
	$portalGroupId = (int) $gst->fetchColumn();
	$menuSt = $pdo->prepare(
		'SELECT `id`, `items_group` FROM `control_items` WHERE `url` LIKE ? LIMIT 1'
	);
	$menuSt->execute(array('%/control/portal/epc_social_media_hub'));
	$menuRow = $menuSt->fetch(PDO::FETCH_ASSOC);
	$checks['portal_menu_item'] = is_array($menuRow) && (int) ($menuRow['id'] ?? 0) > 0;
	$checks['portal_menu_group'] = is_array($menuRow)
		&& $portalGroupId > 0
		&& (int) ($menuRow['items_group'] ?? 0) === $portalGroupId;
} catch (Throwable $e) {
	$checks['db'] = false;
	$checks['db_error'] = $e->getMessage();
}

$posts = epc_social_pack_posts('linkedin');
$checks['pack_posts_linkedin'] = count($posts) >= 4;
$checks['pack_posts_tiktok'] = count(epc_social_pack_posts('tiktok')) >= 4;

$enc = epc_social_encrypt('test-secret', 'platform');
$dec = epc_social_decrypt($enc, 'platform');
$checks['encryption_roundtrip'] = ($dec === 'test-secret');

$configPath = __DIR__ . '/content/general_pages/epc_social_media_hub_config.php';
$configSrc = (string) @file_get_contents($configPath);
$checks['config_bootstrap'] = strpos($configSrc, "define('_ASTEXE_', 1)") !== false
	&& strpos($configSrc, "die('No access')") === false
	&& strpos($configSrc, '/content/general_pages/ajax_epc_social_media.php') !== false;

$cpConfigPath = __DIR__ . '/cp/content/control/portal/epc_social_media_hub_config.php';
$cpConfigSrc = (string) @file_get_contents($cpConfigPath);
$checks['config_cp_proxy'] = strpos($cpConfigSrc, '/content/general_pages/epc_social_media_hub_config.php') !== false;

$jsProxyPath = __DIR__ . '/content/general_pages/epc_social_media_hub_js.php';
$checks['js_proxy_on_disk'] = is_file($jsProxyPath) && filesize($jsProxyPath) > 0;
$checks['config_proxy_on_disk'] = is_file($configPath) && filesize($configPath) > 0;
$checks['ajax_proxy_on_disk'] = is_file(__DIR__ . '/content/general_pages/ajax_epc_social_media.php')
	&& filesize(__DIR__ . '/content/general_pages/ajax_epc_social_media.php') > 0;

$host = function_exists('epc_portal_host') ? (string) epc_portal_host() : 'www.ecomae.com';
if ($host === '') {
	$host = 'www.ecomae.com';
}
$probeCtx = stream_context_create(array('http' => array('timeout' => 15, 'ignore_errors' => true)));
$jsProbeUrl = 'https://' . $host . '/content/general_pages/epc_social_media_hub_js.php';
$jsProbeRaw = @file_get_contents($jsProbeUrl, false, $probeCtx);
$checks['js_proxy_http'] = is_string($jsProbeRaw) && strlen($jsProbeRaw) > 200 && strpos($jsProbeRaw, 'epc-social-copy') !== false;

$configProbeUrl = 'https://' . $host . '/content/general_pages/epc_social_media_hub_config.php';
$configProbeRaw = @file_get_contents($configProbeUrl, false, $probeCtx);
$checks['config_proxy_http'] = is_string($configProbeRaw)
	&& strpos($configProbeRaw, 'window.EPC_SOCIAL_HUB') !== false
	&& strpos($configProbeRaw, 'No access') === false;

$ajaxProbeUrl = 'https://' . $host . '/content/general_pages/ajax_epc_social_media.php';
$ajaxProbeHeaders = @get_headers($ajaxProbeUrl);
$ajaxProbeHttp = 0;
if (is_array($ajaxProbeHeaders) && isset($ajaxProbeHeaders[0]) && preg_match('/\s(\d{3})\s/', (string) $ajaxProbeHeaders[0], $m)) {
	$ajaxProbeHttp = (int) $m[1];
}
$checks['ajax_proxy_http'] = in_array($ajaxProbeHttp, array(200, 403), true);

$ok = !in_array(false, array_filter($checks, static function ($v) {
	return is_bool($v);
}), true);

echo "=== Social Media Hub Verify ===\n";
echo 'status=' . ($ok ? 'OK' : 'FAIL') . "\n\n";
foreach ($checks as $k => $v) {
	if (is_bool($v)) {
		echo $k . '=' . ($v ? 'OK' : 'FAIL') . "\n";
	} else {
		echo $k . '=' . $v . "\n";
	}
}

$guideProbe = array();
foreach (array('platform', 'epartscart') as $sk) {
	$probeUrl = 'https://www.ecomae.com/epc-social-media-guide-verify.php?token=' . rawurlencode(epc_deploy_token()) . '&site_key=' . rawurlencode($sk);
	$raw = @file_get_contents($probeUrl);
	$guideProbe[$sk] = is_string($raw) ? json_decode($raw, true) : null;
}
echo "\nGuide tab probe:\n";
foreach ($guideProbe as $sk => $data) {
	if (!is_array($data)) {
		echo "  {$sk}: FAIL (no response)\n";
		continue;
	}
	echo '  ' . $sk . ': ' . (!empty($data['ok']) ? 'OK' : 'FAIL')
		. ' guide_tab_html_length=' . (int) ($data['guide_tab_html_length'] ?? 0);
	if (!empty($data['error'])) {
		echo ' error=' . $data['error'];
	}
	echo "\n";
}

$renderProbe = array();
foreach (array('www.ecomae.com', 'www.epartscart.com') as $probeHost) {
	$renderUrl = 'https://www.ecomae.com/epc-social-media-hub-render-probe.php?token='
		. rawurlencode(epc_deploy_token()) . '&host=' . rawurlencode($probeHost) . '&tab=guide';
	$raw = @file_get_contents($renderUrl);
	$renderProbe[$probeHost] = is_string($raw) ? json_decode($raw, true) : null;
}
echo "\nCP render probe (guide tab, admin cookie):\n";
foreach ($renderProbe as $probeHost => $data) {
	if (!is_array($data)) {
		echo "  {$probeHost}: FAIL (no response)\n";
		continue;
	}
	echo '  ' . $probeHost . ': ' . (!empty($data['ok']) ? 'OK' : 'FAIL')
		. ' hub_http=' . (int) ($data['hub']['http'] ?? 0)
		. ' guide=' . (!empty($data['hub']['has_guide_content']) ? 'yes' : 'no')
		. ' config=' . (!empty($data['config']['valid_js']) ? 'yes' : 'no');
	if (!empty($data['hub']['is_login_page'])) {
		echo ' login_page=yes';
	}
	echo "\n";
}

echo "\nGuide URL (Super CP):\n";
echo "  https://www.ecomae.com/cp/control/portal/epc_social_media_hub?tab=guide\n";
echo "Guide URL (Tenant — epartscart):\n";
echo "  https://www.epartscart.com/cp/control/portal/epc_social_media_hub?tab=guide\n";
echo "\nTenant hub tab:\n";
echo "  https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=social\n";
echo "\nCP menu path:\n";
echo "  Portal → Social media hub → /cp/control/portal/epc_social_media_hub\n";
