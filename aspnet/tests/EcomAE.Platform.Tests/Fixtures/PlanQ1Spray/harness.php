<?php
// PHP 8.3 goldens for plan Q1-spray (CP professional shell). Branding / hub / animated-logo / leftover ERP routers stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function spray_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("defined('_ASTEXE_') or die('No access');", "// access gate stubbed", $code);
	$code = str_replace("require_once __DIR__ . '/epc_branding.php';", '// leftover branding injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_ecomae_hub_logo.php';", '// leftover hub logo injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_animated_epartscart_logo.php';", '// leftover animated logo injected', $code);
	file_put_contents($dest, $code);
}

function spray_stubs(): void
{
	if (!function_exists('epc_brand_cp_context')) {
		function epc_brand_cp_context(): array
		{
			return $GLOBALS['SPRAY_BRAND'] ?? array('company_name' => 'Acme Parts', 'product_name' => 'Control Panel', 'hub_tagline' => 'Finance & operations');
		}
	}
	if (!function_exists('epc_platform_erp_is_active')) {
		function epc_platform_erp_is_active(): bool
		{
			return !empty($GLOBALS['SPRAY_PLATFORM_ERP']);
		}
	}
	if (!function_exists('epc_portal_demo_is_cp_context')) {
		function epc_portal_demo_is_cp_context(): bool
		{
			return !empty($GLOBALS['SPRAY_DEMO_CP']);
		}
	}
	if (!function_exists('epc_portal_demo_cp_is_erp_only')) {
		function epc_portal_demo_cp_is_erp_only(): bool
		{
			return !empty($GLOBALS['SPRAY_DEMO_ERP_ONLY']);
		}
	}
	if (!function_exists('epc_portal_demo_is_autoparts_parity')) {
		function epc_portal_demo_is_autoparts_parity(): bool
		{
			return !empty($GLOBALS['SPRAY_AUTOPARTS']);
		}
	}
	if (!function_exists('epc_portal_demo_cp_site_key')) {
		function epc_portal_demo_cp_site_key(): string
		{
			return (string) ($GLOBALS['SPRAY_DEMO_KEY'] ?? '');
		}
	}
	if (!function_exists('epc_client_erp_is_active')) {
		function epc_client_erp_is_active(): bool
		{
			return !empty($GLOBALS['SPRAY_CLIENT_ERP']);
		}
	}
	if (!function_exists('epc_client_erp_tenant_row')) {
		function epc_client_erp_tenant_row()
		{
			return $GLOBALS['SPRAY_CLIENT_ROW'] ?? null;
		}
	}
	if (!function_exists('epc_client_erp_site_key')) {
		function epc_client_erp_site_key(): string
		{
			return (string) ($GLOBALS['SPRAY_CLIENT_KEY'] ?? '');
		}
	}
	if (!function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['SPRAY_SUPER']);
		}
	}
	if (!function_exists('epc_portal_is_platform_operator')) {
		function epc_portal_is_platform_operator(): bool
		{
			return !empty($GLOBALS['SPRAY_PLATFORM_OP']);
		}
	}
	if (!function_exists('epc_portal_is_platform_hostname')) {
		function epc_portal_is_platform_hostname(): bool
		{
			return !empty($GLOBALS['SPRAY_PLATFORM_HOST']);
		}
	}
	if (!function_exists('epc_portal_cp_active_industry')) {
		function epc_portal_cp_active_industry(): string
		{
			return (string) ($GLOBALS['SPRAY_INDUSTRY'] ?? '');
		}
	}
	if (!function_exists('epc_portal_industry')) {
		function epc_portal_industry($code = null): array
		{
			return $GLOBALS['SPRAY_INDUSTRY_ROW'] ?? array('name' => 'Auto parts', 'icon' => 'fa-cogs');
		}
	}
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id)
		{
			return 'T' . (int) $id;
		}
	}
	if (!function_exists('epc_ecomae_hub_logo_enqueue')) {
		function epc_ecomae_hub_logo_enqueue(): void
		{
			echo "<!--hub-logo-->\n";
		}
	}
	if (!function_exists('epc_cp_login_hero_enqueue')) {
		function epc_cp_login_hero_enqueue(): void
		{
			echo "<!--login-hero-->\n";
		}
	}
	if (!function_exists('epc_cp_login_enqueue')) {
		function epc_cp_login_enqueue(): void
		{
			echo "<!--login-css-->\n";
		}
	}
	if (!function_exists('epc_animated_epartscart_logo_applies')) {
		function epc_animated_epartscart_logo_applies(): bool
		{
			return !empty($GLOBALS['SPRAY_ANIMATED']);
		}
	}
	if (!function_exists('epc_animated_epartscart_logo_enqueue')) {
		function epc_animated_epartscart_logo_enqueue(): void
		{
		}
	}
	if (!function_exists('epc_animated_epartscart_logo_markup')) {
		function epc_animated_epartscart_logo_markup($slot = ''): string
		{
			return 'ANIMATED:' . $slot;
		}
	}
	if (!function_exists('epc_ecomae_hub_logo')) {
		function epc_ecomae_hub_logo($slot = '', array $opts = array()): string
		{
			return 'HUB:' . $slot . ':' . (!empty($opts['show_title']) ? '1' : '0');
		}
	}
	if (!function_exists('epc_ecomae_static_logo')) {
		function epc_ecomae_static_logo($slot = '', array $opts = array()): string
		{
			return 'STATIC:' . $slot . ':' . (!empty($opts['show_tagline']) ? '1' : '0');
		}
	}
}

function spray_reset_flags(): void
{
	foreach (array('SPRAY_PLATFORM_ERP', 'SPRAY_DEMO_CP', 'SPRAY_DEMO_ERP_ONLY', 'SPRAY_AUTOPARTS', 'SPRAY_CLIENT_ERP', 'SPRAY_SUPER', 'SPRAY_PLATFORM_OP', 'SPRAY_PLATFORM_HOST', 'SPRAY_ANIMATED') as $k) {
		$GLOBALS[$k] = 0;
	}
	$GLOBALS['SPRAY_DEMO_KEY'] = '';
	$GLOBALS['SPRAY_CLIENT_KEY'] = '';
	$GLOBALS['SPRAY_CLIENT_ROW'] = null;
	$GLOBALS['SPRAY_INDUSTRY'] = '';
	$GLOBALS['SPRAY_INDUSTRY_ROW'] = array('name' => 'Auto parts', 'icon' => 'fa-cogs');
	$GLOBALS['SPRAY_BRAND'] = array('company_name' => 'Acme Parts', 'product_name' => 'Control Panel', 'hub_tagline' => 'Finance & operations');
	$GLOBALS['epc_demo_cp_tenant_row'] = null;
	$GLOBALS['DP_Config'] = (object) array('backend_dir' => 'cp');
	$_GET = array();
	$_COOKIE = array();
	$_SERVER['REQUEST_URI'] = '/cp/shop/orders';
}

function spray_include(): void
{
	spray_stubs();
	include_once $GLOBALS['SPRAY_PAGE'];
}

function spray_view($value)
{
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[(string) $k] = spray_view($v);
		}
		return $out;
	}
	return $value;
}

function spray_run_names()
{
	spray_include();
	$href = epc_cp_shell_asset_href('/cp/templates/bootstrap_admin/css/epc_cp_ui.css', '/content/general_pages/epc_cp_ui_css.php');
	$first = epc_cp_sidebar_first_paint_script();
	$nuclear = epc_cp_nuclear_critical_css();
	$force = epc_cp_force_visible_body_style();
	$early = epc_cp_menu_sections_early_style();
	return array(
		epc_cp_shell_css_version(),
		epc_cp_shell_use_asset_proxies() ? 1 : 0,
		$href,
		epc_cp_shell_body_classes(),
		substr($first, 0, 70),
		strpos($first, 'epc_cp_sidebar_collapsed') !== false ? 1 : 0,
		strpos($nuclear, 'epc-cp-main-pane-critical') !== false ? 1 : 0,
		strlen($nuclear),
		substr($force, 0, 40),
		strpos($force, '20260721aoCfg1') !== false ? 1 : 0,
		strpos($early, 'epc-cp-menu-sections-early') !== false ? 1 : 0,
		strlen($early),
	);
}

function spray_run_context()
{
	spray_include();
	spray_reset_flags();
	$tenant = spray_view(epc_cp_login_context());
	$tenantShell = spray_view(epc_cp_shell_context());
	$GLOBALS['SPRAY_SUPER'] = 1;
	$super = spray_view(epc_cp_login_context());
	$superShell = spray_view(epc_cp_shell_context());
	$GLOBALS['SPRAY_SUPER'] = 0;
	$GLOBALS['SPRAY_PLATFORM_ERP'] = 1;
	$plat = spray_view(epc_cp_login_context());
	$platShell = spray_view(epc_cp_shell_context());
	$GLOBALS['SPRAY_PLATFORM_ERP'] = 0;
	$GLOBALS['SPRAY_DEMO_CP'] = 1;
	$GLOBALS['SPRAY_DEMO_KEY'] = 'acme_demo';
	$GLOBALS['epc_demo_cp_tenant_row'] = array('trade_name' => 'Acme Demo');
	$demo = spray_view(epc_cp_login_context());
	$demoShell = spray_view(epc_cp_shell_context());
	$GLOBALS['SPRAY_DEMO_ERP_ONLY'] = 1;
	$erpOnly = spray_view(epc_cp_login_context());
	$erpOnlyShell = spray_view(epc_cp_shell_context());
	$GLOBALS['SPRAY_DEMO_CP'] = 0;
	$GLOBALS['SPRAY_DEMO_ERP_ONLY'] = 0;
	$GLOBALS['SPRAY_CLIENT_ERP'] = 1;
	$GLOBALS['SPRAY_CLIENT_KEY'] = 'beta';
	$GLOBALS['SPRAY_CLIENT_ROW'] = array('trade_name' => 'Beta Trading');
	$client = spray_view(epc_cp_login_context());
	$clientShell = spray_view(epc_cp_shell_context());
	$GLOBALS['SPRAY_CLIENT_ERP'] = 0;
	$GLOBALS['SPRAY_SUPER'] = 1;
	$GLOBALS['SPRAY_PLATFORM_OP'] = 1;
	$GLOBALS['SPRAY_PLATFORM_HOST'] = 1;
	$GLOBALS['SPRAY_INDUSTRY'] = '';
	$_SERVER['REQUEST_URI'] = '/cp/control/config';
	$header = spray_view(epc_cp_page_header_context());
	return array($tenant, $tenantShell, $super, $superShell, $plat, $platShell, $demo, $demoShell, $erpOnly, $erpOnlyShell, $client, $clientShell, $header);
}

function spray_run_markup()
{
	spray_include();
	spray_reset_flags();
	$empty = epc_cp_page_header_actions_html(array());
	$pills = epc_cp_page_header_actions_html(array(
		array('url' => '/erp/', 'label' => 'Platform ERP', 'icon' => 'fa-chart-line', 'primary' => 1),
		array('url' => 'https://www.ecomae.com/', 'label' => "O'Reilly", 'icon' => 'fa-globe', 'target' => '_blank'),
		array('url' => '', 'label' => 'skip'),
	));
	ob_start();
	epc_cp_shell_enqueue_assets(false);
	$enq1 = ob_get_clean();
	ob_start();
	epc_cp_shell_enqueue_assets(true);
	$enq2 = ob_get_clean();
	$GLOBALS['SPRAY_ANIMATED'] = 1;
	$heroAnim = epc_cp_login_hero_markup();
	$GLOBALS['SPRAY_ANIMATED'] = 0;
	$heroHub = epc_cp_login_hero_markup();
	$_COOKIE['epc_cp_login_static'] = '1';
	$heroStatic = epc_cp_login_hero_markup();
	$_COOKIE = array();
	$noInline = epc_cp_shell_inline_style_block();
	$_GET['epc_cp_inline_css'] = '0';
	$zeroInline = epc_cp_shell_inline_style_block();
	$_GET['epc_cp_inline_css'] = '1';
	$tmp = $GLOBALS['SPRAY_DOC'];
	file_put_contents($tmp . '/cp/templates/bootstrap_admin/css/epc_cp_ui.css', "body{color:red}\n");
	$inline = epc_cp_shell_inline_style_block();
	return array(
		$empty,
		$pills,
		$enq1,
		$enq2,
		$heroAnim,
		$heroHub,
		$heroStatic,
		$noInline,
		$zeroInline,
		$inline,
	);
}

function spray_run_scripts()
{
	spray_include();
	$blobs = array(
		epc_cp_sidebar_first_paint_script(),
		epc_cp_nuclear_critical_css(),
		epc_cp_force_visible_script(),
		epc_cp_sidebar_early_init_script(),
		epc_erp_sidebar_early_init_script(),
		epc_erp_sidebar_accordion_script(),
		epc_cp_hide_menu_vanilla_script(),
		epc_cp_menu_sections_script(),
		epc_cp_sidebar_collapse_script(),
		epc_cp_modern_reveal_script(),
	);
	$out = array();
	foreach ($blobs as $blob) {
		$out[] = array(strlen($blob), md5($blob), substr($blob, 0, 48), substr($blob, -32));
	}
	return $out;
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_spray_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp . '/cp/templates/bootstrap_admin/css', 0777, true);
	@mkdir($tmp . '/content/general_pages', 0777, true);
	spray_patch($root . '/content/general_pages/epc_cp_professional_shell.php', $tmp . '/page.php');
	$GLOBALS['SPRAY_PAGE'] = $tmp . '/page.php';
	$GLOBALS['SPRAY_DOC'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	spray_reset_flags();
	$fn = 'spray_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1spray_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1spray_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
