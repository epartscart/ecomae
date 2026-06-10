<?php
/**
 * Super CP main pane smoke test (logged-in markers in HTML).
 * GET https://www.ecomae.com/epc-cp-main-pane-verify.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);
define('_ASTEXE_', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	http_response_code(403);
	echo json_encode(array('ok' => false, 'error' => 'forbidden'), JSON_UNESCAPED_SLASHES);
	exit;
}

$base = 'https://www.ecomae.com';
$loginUrl = $base . '/cp/';
$pages = array(
	'tenant_hub' => $base . '/cp/shop/tenant_hub/tenant_hub?tab=onboard',
	'industry_settings' => $base . '/cp/control/portal/industry_settings',
	'cp_guideline' => $base . '/cp/control/cp-guideline',
	'tax_toolkit' => $base . '/cp/control/portal/epc_tax_toolkit_manage',
	'visual_editor' => $base . '/cp/control/portal/epc_visual_page_editor',
	'catalog' => $base . '/cp/shop/catalogue/products',
);

$cookieFile = sys_get_temp_dir() . '/epc_cp_verify_' . md5((string) getmypid()) . '.txt';
@unlink($cookieFile);

$loginBody = http_build_query(array(
	'authentication' => 'authentication',
	'auth_contact_select' => 'email',
	'auth_contact' => 'taxofin2025@gmail.com',
	'password' => '12345678',
));

$ch = curl_init($loginUrl);
curl_setopt_array($ch, array(
	CURLOPT_POST => true,
	CURLOPT_POSTFIELDS => $loginBody,
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_FOLLOWLOCATION => false,
	CURLOPT_COOKIEJAR => $cookieFile,
	CURLOPT_COOKIEFILE => $cookieFile,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_TIMEOUT => 60,
));
curl_exec($ch);
$loginCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);

$out = array(
	'ok' => true,
	'login_http' => $loginCode,
	'css_version' => null,
	'homer_js' => null,
	'pages' => array(),
	'homer_inline_reveal' => false,
);

if (is_file(__DIR__ . '/content/general_pages/epc_cp_professional_shell.php')) {
	require_once __DIR__ . '/content/general_pages/epc_cp_professional_shell.php';
	if (function_exists('epc_cp_shell_css_version')) {
		$out['css_version'] = epc_cp_shell_css_version();
	}
}

$homerUrl = $base . '/content/general_pages/epc_cp_homer.php?v='
	. (function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260604tenanthub4');
$ch = curl_init($homerUrl);
curl_setopt_array($ch, array(
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_TIMEOUT => 30,
));
$homerBody = (string) curl_exec($ch);
$homerCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);
$out['homer_js'] = array(
	'http' => $homerCode,
	'bytes' => strlen($homerBody),
	'skips_epc_cp_shell' => strpos($homerBody, 'epc-cp-shell') !== false,
	'has_opacity_add' => strpos($homerBody, "addClass('opacity-0')") !== false,
);

$markers = array(
	'tenant_hub' => array('epc-th-hero', 'epc-th-kpi', 'epc-cp-main-pane', 'Onboard client'),
	'industry_settings' => array('epc-portal-settings', 'Industry template', 'epc-cp-main-pane', 'epc-cp-page-frame'),
	'cp_guideline' => array('epc-cpg-quick', 'epc-cp-page-frame', 'Daily workflows', 'epc-cp-main-pane'),
	'tax_toolkit' => array('epc-ttk-hero', 'epc-cp-page-frame', 'Tax Toolkit', 'epc-cp-main-pane'),
	'visual_editor' => array('epc-vpe-app', 'epc-cp-page-frame', 'Visual page editor', 'epc-cp-main-pane'),
	'catalog' => array('epc-cp-main-pane', 'catalogue', 'category'),
);

foreach ($pages as $key => $url) {
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_COOKIEJAR => $cookieFile,
		CURLOPT_COOKIEFILE => $cookieFile,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 90,
	));
	$html = (string) curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);

	$found = array();
	foreach ($markers[$key] as $m) {
		$found[$m] = stripos($html, $m) !== false;
	}
	if (stripos($html, 'epcCpRevealMainPane') !== false) {
		$out['homer_inline_reveal'] = true;
	}
	$out['pages'][$key] = array(
		'url' => $url,
		'http' => $code,
		'bytes' => strlen($html),
		'logged_in' => stripos($html, 'login_form') === false && stripos($html, 'epc-cp-page-header__title') !== false,
		'markers' => $found,
		'main_ok' => !empty($found[$markers[$key][0]]),
		'css_supercpfix1' => stripos($html, 'supercpfix1') !== false,
		'css_tenanthub4' => stripos($html, 'tenanthub4') !== false || stripos($html, 'supercpfix1') !== false,
		'force_visible_js' => stripos($html, 'epc-cp-force-visible-js') !== false,
		'body_force_css' => stripos($html, 'epc-cp-body-force-visible') !== false,
		'build_comment' => preg_match('/epc-cp-build:([0-9a-z]+)/i', $html, $bm) ? $bm[1] : null,
		'mainpane5_marker' => stripos($html, 'mainpane5') !== false,
		'crossbrowser2_marker' => stripos($html, 'crossbrowser2') !== false,
		'cpframe1_marker' => stripos($html, 'cpframe1') !== false || stripos($html, 'epc-cp-page-frame') !== false,
		'raw_style_leak' => preg_match('/\.epc-[a-z0-9_-]+\s*\{[^}]+\}/i', strip_tags($html)) === 1 && stripos($html, '<style') === false,
		'style_css_proxy' => stripos($html, 'epc_cp_style_css.php') !== false,
		'flow_root_row' => stripos($html, 'display: flow-root') !== false,
		'min_height_400' => stripos($html, 'min-height: 400px') !== false,
	);
}

@unlink($cookieFile);

$out['tenant_hub_ok'] = !empty($out['pages']['tenant_hub']['main_ok']);
$out['industry_settings_ok'] = !empty($out['pages']['industry_settings']['main_ok']);
$out['catalog_ok'] = !empty($out['pages']['catalog']['logged_in']);
$out['supercpfix1_css'] = !empty($out['pages']['tenant_hub']['css_supercpfix1']);
$out['tenanthub4_css'] = !empty($out['pages']['tenant_hub']['css_tenanthub4']);
$out['crossbrowser2_css'] = !empty($out['pages']['tenant_hub']['crossbrowser2_marker'])
	|| in_array($out['css_version'] ?? '', array('20260606crossbrowser2', '20260606cpframe1'), true);
$out['cpframe1_css'] = ($out['css_version'] ?? '') === '20260606cpframe1'
	|| stripos(json_encode($out['pages']), 'cpframe1') !== false;
$out['cp_guideline_ok'] = !empty($out['pages']['cp_guideline']['main_ok']);
$out['tax_toolkit_ok'] = !empty($out['pages']['tax_toolkit']['main_ok']);
$out['visual_editor_ok'] = !empty($out['pages']['visual_editor']['main_ok']);
$out['ok'] = $out['tenant_hub_ok']
	&& $out['industry_settings_ok']
	&& $out['cp_guideline_ok']
	&& $out['tax_toolkit_ok']
	&& $out['visual_editor_ok']
	&& ($homerCode === 200 || !empty($out['homer_inline_reveal']))
	&& empty($out['homer_js']['has_opacity_add']);

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
