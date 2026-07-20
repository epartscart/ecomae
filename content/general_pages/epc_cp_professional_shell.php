<?php
/**
 * Professional CP / ERP shell — shared branding, login hero, CSS enqueue, inline fallbacks.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_branding.php';
require_once __DIR__ . '/epc_ecomae_hub_logo.php';

function epc_cp_shell_css_version()
{
	return '20260720topnavclick2';
}

/** True on www.ecomae.com where nginx often 404s static /cp/templates/*.css. */
function epc_cp_shell_use_asset_proxies(): bool
{
	return function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname();
}

function epc_cp_shell_asset_href(string $staticPath, string $phpProxyPath): string
{
	$ver = epc_cp_shell_css_version();
	$path = epc_cp_shell_use_asset_proxies() ? $phpProxyPath : $staticPath;
	return $path . '?v=' . rawurlencode($ver);
}

/**
 * Synchronous head script — expanded sidebar before first CSS paint (Opera/cache).
 */
function epc_cp_sidebar_first_paint_script(): string
{
	return '<script id="epc-cp-sidebar-first-paint">(function(){try{document.documentElement.classList.add("epc-cp-html-lock");var KEY="epc_cp_sidebar_collapsed";document.documentElement.classList.remove("epc-cp-sidebar-collapsed");var p=location.pathname||"";if(p.indexOf("/tenant_hub/")!==-1||p.indexOf("/control/")!==-1||p.indexOf("/platform-erp/")!==-1){localStorage.setItem(KEY,"0");}else if(p.indexOf("/cp/")!==-1||p.indexOf("/control/")!==-1){var s=localStorage.getItem(KEY);if(s==="1"||s==="true"){localStorage.setItem(KEY,"0");}}}catch(e){}})();</script>' . "\n";
}

/**
 * Inline critical main-pane CSS — no external /cp/templates/ dependency.
 */
function epc_cp_nuclear_critical_css(): string
{
	return <<<'CSS'
	<style id="epc-cp-main-pane-critical">
		/* crossbrowser1: block layout — no flex/flow-root collapse (Opera/Firefox/Edge) */
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper {
			display: block !important;
			flex: none !important;
		}
		body.epc-cp-shell .content,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content.epc-cp-main-pane {
			display: block !important;
			width: 100% !important;
			min-height: 1px !important;
			flex: none !important;
			float: none !important;
			opacity: 1 !important;
			visibility: visible !important;
		}
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content > .row,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content .epc-cp-content-inner,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content .epc-cp-content-inner > .row,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content .col-lg-12,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content .col-lg-9,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content .col-lg-10,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content .col-lg-11,
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content [class*="col-"],
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content > .row > div {
			display: block !important;
			flex: none !important;
			float: none !important;
			width: 100% !important;
			max-width: 100% !important;
			min-height: 1px !important;
			height: auto !important;
			max-height: none !important;
			overflow: visible !important;
			opacity: 1 !important;
			visibility: visible !important;
			animation: none !important;
			transform: none !important;
		}
		body.epc-cp-shell:not(.epc-erp-standalone) #wrapper .content > .row {
			display: block !important;
			overflow: visible !important;
			width: 100% !important;
			min-height: 1px !important;
		}
		body.epc-cp-shell.hide-sidebar #wrapper .content,
		body.epc-cp-shell.page-small #wrapper .content,
		body.epc-cp-shell.page-small.show-sidebar #wrapper .content {
			display: block !important;
			opacity: 1 !important;
			visibility: visible !important;
			min-height: 500px !important;
		}
		body.epc-cp-shell .content.animate-panel,
		body.epc-cp-shell .content .opacity-0,
		body.epc-cp-shell .content .zoomIn,
		body.epc-cp-shell .content .animated-panel,
		body.epc-cp-shell .content .stagger {
			opacity: 1 !important;
			visibility: visible !important;
			animation: none !important;
			transform: none !important;
		}
		body.epc-cp-shell,
		body.epc-cp-shell.translated-ltr,
		body.epc-cp-shell.translated-rtl,
		html.translated-ltr body.epc-cp-shell,
		html.translated-rtl body.epc-cp-shell {
			top: 0 !important;
		}
		body.epc-cp-shell.fixed-sidebar #wrapper {
			display: block !important;
			min-height: calc(100vh - 56px) !important;
		}
		body.epc-cp-shell .content .hpanel,
		body.epc-cp-shell .content .panel-body,
		body.epc-cp-shell .content .epc-erp-shell,
		body.epc-cp-shell .content .epc-th-hero {
			display: block !important;
			opacity: 1 !important;
			visibility: visible !important;
			min-height: 1px !important;
		}
		body.epc-cp-shell .content .epc-cp-content-inner,
		body.epc-cp-shell .content .epc-portal-settings,
		body.epc-cp-shell .content .epc-cp-content-inner .row,
		body.epc-cp-shell .content .epc-cp-content-inner .row > *,
		body.epc-cp-shell .content .epc-cp-content-inner [class*="col-"] {
			display: block !important;
			opacity: 1 !important;
			visibility: visible !important;
			min-height: 1px !important;
			height: auto !important;
			max-height: none !important;
			overflow: visible !important;
			animation: none !important;
			transform: none !important;
		}
		body.epc-cp-shell .content .epc-th-kpi {
			display: flex !important;
			flex-wrap: wrap !important;
			opacity: 1 !important;
			visibility: visible !important;
			min-height: 0 !important;
			gap: 10px !important;
		}
		body.epc-cp-shell .content .epc-th-tabs {
			display: flex !important;
			flex-wrap: wrap !important;
			align-items: center !important;
			gap: 4px !important;
			opacity: 1 !important;
			visibility: visible !important;
			min-height: 0 !important;
		}
		/* density1: Actions tiles + nested grids — row wrap (nuclear forces block/100%) */
		body.epc-cp-shell .content .panel-body:has(> .panel_a),
		body.epc-cp-shell .content .panel-body:has(> a.panel_a),
		body.epc-cp-shell .content .epc-prices-toolbar .panel-body,
		body.epc-cp-shell .content .epc-cp-actions,
		body.epc-cp-shell .content .epc-cp-action-grid {
			display: flex !important;
			flex-wrap: wrap !important;
			align-items: stretch !important;
			align-content: flex-start !important;
			gap: 10px 12px !important;
			width: 100% !important;
			float: none !important;
			min-height: 0 !important;
		}
		body.epc-cp-shell .content .panel_a,
		body.epc-cp-shell .content a.panel_a {
			display: inline-flex !important;
			flex: 0 1 auto !important;
			float: none !important;
			width: auto !important;
			max-width: 220px !important;
			min-width: 148px !important;
			height: auto !important;
			min-height: 0 !important;
			max-height: none !important;
			align-items: center !important;
			gap: 10px !important;
			margin: 0 !important;
			padding: 10px 12px !important;
			box-sizing: border-box !important;
			text-align: left !important;
			vertical-align: top !important;
		}
		body.epc-cp-shell .content .panel_a .panel_a_img,
		body.epc-cp-shell .content a.panel_a .panel_a_img {
			display: block !important;
			width: 28px !important;
			height: 28px !important;
			min-width: 28px !important;
			min-height: 28px !important;
			max-width: 28px !important;
			max-height: 28px !important;
			flex: 0 0 28px !important;
			background-size: contain !important;
			margin: 0 !important;
		}
		body.epc-cp-shell .content .panel_a .panel_a_caption,
		body.epc-cp-shell .content a.panel_a .panel_a_caption {
			display: block !important;
			height: auto !important;
			max-width: 160px !important;
			margin: 0 !important;
			line-height: 1.25 !important;
			white-space: normal !important;
			text-align: left !important;
		}
		/* Nested Bootstrap rows inside panels — side-by-side again */
		body.epc-cp-shell .content .panel-body > .row,
		body.epc-cp-shell .content .hpanel .panel-body .row,
		body.epc-cp-shell .content .epc-cp-content-inner .panel-body .row {
			display: flex !important;
			flex-wrap: wrap !important;
			width: 100% !important;
			float: none !important;
			margin-left: -8px !important;
			margin-right: -8px !important;
		}
		body.epc-cp-shell .content .panel-body > .row > [class*="col-"],
		body.epc-cp-shell .content .hpanel .panel-body .row > [class*="col-"],
		body.epc-cp-shell .content .epc-cp-content-inner .panel-body .row > [class*="col-"] {
			display: block !important;
			float: none !important;
			box-sizing: border-box !important;
			padding-left: 8px !important;
			padding-right: 8px !important;
			min-height: 0 !important;
		}
		@media (min-width: 768px) {
			body.epc-cp-shell .content .panel-body .row > .col-sm-6,
			body.epc-cp-shell .content .panel-body .row > .col-md-6,
			body.epc-cp-shell .content .panel-body .row > .col-lg-6 { width: 50% !important; max-width: 50% !important; flex: 0 0 50% !important; }
			body.epc-cp-shell .content .panel-body .row > .col-sm-4,
			body.epc-cp-shell .content .panel-body .row > .col-md-4,
			body.epc-cp-shell .content .panel-body .row > .col-lg-4 { width: 33.3333% !important; max-width: 33.3333% !important; flex: 0 0 33.3333% !important; }
			body.epc-cp-shell .content .panel-body .row > .col-sm-3,
			body.epc-cp-shell .content .panel-body .row > .col-md-3,
			body.epc-cp-shell .content .panel-body .row > .col-lg-3 { width: 25% !important; max-width: 25% !important; flex: 0 0 25% !important; }
			body.epc-cp-shell .content .panel-body .row > .col-sm-8,
			body.epc-cp-shell .content .panel-body .row > .col-md-8,
			body.epc-cp-shell .content .panel-body .row > .col-lg-8 { width: 66.6667% !important; max-width: 66.6667% !important; flex: 0 0 66.6667% !important; }
			body.epc-cp-shell .content .panel-body .row > .col-sm-9,
			body.epc-cp-shell .content .panel-body .row > .col-md-9,
			body.epc-cp-shell .content .panel-body .row > .col-lg-9 { width: 75% !important; max-width: 75% !important; flex: 0 0 75% !important; }
			body.epc-cp-shell .content .panel-body .row > .col-sm-5,
			body.epc-cp-shell .content .panel-body .row > .col-md-5,
			body.epc-cp-shell .content .panel-body .row > .col-lg-5 { width: 41.6667% !important; max-width: 41.6667% !important; flex: 0 0 41.6667% !important; }
			body.epc-cp-shell .content .panel-body .row > .col-sm-7,
			body.epc-cp-shell .content .panel-body .row > .col-md-7,
			body.epc-cp-shell .content .panel-body .row > .col-lg-7 { width: 58.3333% !important; max-width: 58.3333% !important; flex: 0 0 58.3333% !important; }
		}
		body.epc-cp-shell .content .form-inline,
		body.epc-cp-shell .content .filters.form-inline,
		body.epc-cp-shell .content .filters {
			display: flex !important;
			flex-wrap: wrap !important;
			align-items: center !important;
			gap: 8px 10px !important;
			width: 100% !important;
		}
		body.epc-cp-shell .content .form-inline .form-control,
		body.epc-cp-shell .content .filters .form-control {
			width: auto !important;
			max-width: 100% !important;
			display: inline-block !important;
			flex: 0 1 auto !important;
		}
		html.epc-cp-sidebar-collapsed .epc-cp-shell #menu {
			width: 0 !important;
			min-width: 0 !important;
			overflow: hidden !important;
			visibility: hidden !important;
			pointer-events: none !important;
		}
		html.epc-cp-sidebar-collapsed .epc-cp-shell #wrapper {
			margin-left: 0 !important;
		}
		/* mainpane5: style.css 404 — pin sidebar fixed + wrapper beside menu (not stacked below) */
		body.fixed-sidebar.epc-cp-shell #menu,
		.epc-cp-shell.fixed-sidebar #menu {
			position: fixed !important;
			left: 0 !important;
			z-index: 100 !important;
			float: none !important;
		}
		html:not(.epc-cp-sidebar-collapsed) body.fixed-sidebar.epc-cp-shell #wrapper,
		html:not(.epc-cp-sidebar-collapsed) .epc-cp-shell.fixed-sidebar #wrapper {
			margin-left: var(--epc-cp-sidebar-w-expanded, 240px) !important;
			width: calc(100% - var(--epc-cp-sidebar-w-expanded, 240px)) !important;
			max-width: calc(100% - var(--epc-cp-sidebar-w-expanded, 240px)) !important;
			box-sizing: border-box !important;
			float: none !important;
			position: relative !important;
		}
		html.epc-cp-sidebar-collapsed body.fixed-sidebar.epc-cp-shell #wrapper,
		html.epc-cp-sidebar-collapsed .epc-cp-shell.fixed-sidebar #wrapper {
			margin-left: 0 !important;
			width: 100% !important;
			max-width: 100% !important;
		}
		body.epc-cp-shell #wrapper .epc-cp-page-header,
		body.epc-cp-shell #wrapper .epc-cp-page-header__card {
			display: block !important;
			opacity: 1 !important;
			visibility: visible !important;
			min-height: 1px !important;
			height: auto !important;
			animation: none !important;
			transform: none !important;
		}
	</style>
CSS;
}

/**
 * First lines of body — wins over late-loaded homer/style.css conflicts.
 */
function epc_cp_force_visible_body_style(): string
{
	$ver = epc_cp_shell_css_version();
	return '<!-- epc-cp-build:' . htmlspecialchars($ver, ENT_QUOTES, 'UTF-8') . " -->\n"
		. '<style id="epc-cp-body-force-visible">'
		. 'body.epc-cp-shell .content{display:block!important;width:100%!important;min-height:1px!important;opacity:1!important;visibility:visible!important}'
		. '#wrapper .content,#wrapper .content.epc-cp-main-pane,#wrapper .content .epc-cp-content-inner,#wrapper .content .epc-cp-content-inner>.row,#wrapper .content>.row,#wrapper .content .epc-portal-settings,#wrapper .epc-cp-page-header,#wrapper .epc-cp-page-header__card{display:block!important;opacity:1!important;visibility:visible!important;min-height:1px!important;height:auto!important;max-height:none!important;overflow:visible!important;float:none!important;width:100%!important;flex:none!important;animation:none!important;transform:none!important;position:relative!important;left:auto!important;top:auto!important;clip:auto!important;clip-path:none!important}'
		. '#wrapper .content .col-lg-12,#wrapper .content .col-lg-9,#wrapper .content [class*="col-"]{display:block!important;opacity:1!important;visibility:visible!important;min-height:0!important;height:auto!important;max-height:none!important;overflow:visible!important;float:none!important;flex:none!important;animation:none!important;transform:none!important}'
		. 'body.epc-cp-shell .splash{display:none!important}'
		. '</style>' . "\n";
}

/**
 * DOMContentLoaded nuclear reveal + sidebar localStorage recovery.
 */
function epc_cp_force_visible_script(): string
{
	return <<<'HTML'
<script id="epc-cp-force-visible-js">
(function(){
	var KEY='epc_cp_sidebar_collapsed';
	function clearUiBlockers(){
		document.querySelectorAll('.modal-backdrop').forEach(function(el){el.parentNode&&el.parentNode.removeChild(el);});
		if(document.body){
			document.body.classList.remove('modal-open');
			document.body.style.overflow='';
			document.body.style.paddingRight='';
			document.body.style.top='0';
		}
		document.querySelectorAll('body > .skiptranslate,iframe.goog-te-banner-frame,.goog-te-banner-frame').forEach(function(el){
			el.style.setProperty('pointer-events','none','important');
			el.style.setProperty('display','none','important');
		});
		var splash=document.querySelector('.splash');
		if(splash){splash.style.display='none';}
	}
	window.epcCpClearUiBlockers=clearUiBlockers;
	function forceEl(el){
		if(!el)return;
		el.style.setProperty('display','block','important');
		el.style.setProperty('opacity','1','important');
		el.style.setProperty('visibility','visible','important');
		el.style.setProperty('min-height','1px','important');
		el.style.setProperty('height','auto','important');
		el.style.setProperty('max-height','none','important');
		el.style.setProperty('overflow','visible','important');
		el.classList.remove('opacity-0','zoomIn','animated-panel','stagger','animate-panel');
	}
	function nuclear(){
		clearUiBlockers();
		var splash=document.querySelector('.splash');
		if(splash){splash.style.display='none';}
		if(document.body){
			document.body.classList.remove('hide-sidebar','opacity-0');
			document.body.style.top='0';
		}
		document.querySelectorAll('.content.animate-panel,.animate-panel').forEach(function(el){
			el.classList.remove('animate-panel','opacity-0','zoomIn','animated-panel','stagger');
		});
		document.querySelectorAll('#wrapper .content,#wrapper .content.epc-cp-main-pane,#wrapper .content .epc-cp-content-inner,#wrapper .content .epc-cp-content-inner>.row,#wrapper .content>.row,#wrapper .content .epc-portal-settings,#wrapper .content .epc-scp-dashboard,#wrapper .content .epc-th-hero,#wrapper .epc-cp-page-header,#wrapper .epc-cp-page-header__card').forEach(forceEl);
		document.querySelectorAll('#wrapper .content [class*="col-"],#wrapper .content .epc-cp-content-inner [class*="col-"],#wrapper .content .opacity-0,#wrapper .content .zoomIn,#wrapper .content .animate-panel').forEach(function(el){
			el.style.setProperty('opacity','1','important');
			el.style.setProperty('visibility','visible','important');
			el.style.setProperty('min-height','0','important');
			el.style.setProperty('height','auto','important');
			el.classList.remove('opacity-0','zoomIn','animated-panel','stagger','animate-panel');
		});
		var pane=document.querySelector('#wrapper .content.epc-cp-main-pane')||document.querySelector('#wrapper .content');
		if(!pane){return;}
		var row=pane.querySelector('.epc-cp-content-inner .row')||pane.querySelector('.row');
		if(row){forceEl(row);row.style.setProperty('min-height','1px','important');}
		var h=pane.offsetHeight||pane.scrollHeight||(pane.getBoundingClientRect?pane.getBoundingClientRect().height:0);
		var inner=pane.innerText?pane.innerText.replace(/\s+/g,'').length:0;
		if(h<100||inner<20){
			try{localStorage.removeItem(KEY);localStorage.setItem(KEY,'0');}catch(e){}
			document.documentElement.classList.remove('epc-cp-sidebar-collapsed');
			if(document.body){document.body.classList.remove('epc-cp-sidebar-collapsed','hide-sidebar');}
			if(typeof window.epcCpSidebarApply==='function'){window.epcCpSidebarApply(false,true);}
		}
	}
	window.epcCpNuclearForceVisible=nuclear;
	function boot(){
		nuclear();
		window.setTimeout(nuclear,50);
		window.setTimeout(nuclear,400);
	}
	if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',boot);}else{boot();}
	window.addEventListener('load',nuclear);
})();
</script>
HTML;
}

/**
 * Body classes for all authenticated CP / ERP shells (unified corporate blue).
 */
function epc_cp_shell_body_classes(): string
{
	return 'epc-cp--blue-theme epc-cp-modern';
}

/**
 * Page header context: eyebrow, icon, role, quick-link pills.
 *
 * @return array{eyebrow:string,icon:string,role_label:string,role_type:string,actions:array<int,array<string,mixed>>}
 */
function epc_cp_page_header_context(): array
{
	global $DP_Config;

	$shell = epc_cp_shell_context();
	$industryCode = function_exists('epc_portal_cp_active_industry') ? epc_portal_cp_active_industry() : '';
	if ($industryCode === '' && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()
		&& !(function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context())) {
		$industryCode = 'platform_host';
	}
	$industry = function_exists('epc_portal_industry') ? epc_portal_industry($industryCode) : array('name' => '', 'icon' => 'fa-cog');

	$isDemoCp = function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context();
	$eyebrow = trim((string) ($industry['name'] ?? ''));
	if ($isDemoCp) {
		$demoTrade = function_exists('epc_portal_demo_cp_site_key')
			? trim((string) ($GLOBALS['epc_demo_cp_tenant_row']['trade_name'] ?? ''))
			: '';
		if ($demoTrade !== '') {
			$eyebrow = $demoTrade;
		} elseif ($industryCode === 'auto_parts') {
			$eyebrow = 'eParts Cart Demo';
		}
	} elseif ($shell['type'] === 'super' && trim((string) ($shell['company'] ?? '')) !== '') {
		$eyebrow = trim((string) $shell['company']);
	} elseif ($eyebrow === '' && trim((string) ($shell['company'] ?? '')) !== '') {
		$eyebrow = trim((string) $shell['company']);
	}

	$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	$actions = array();
	$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
	$isPlatformOp = !$isDemoCp && function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
	$here = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';

	if ($isPlatformOp) {
		$erpUrl = (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname())
			? '/erp/'
			: '/' . $backend . '/platform-erp/';
		$actions[] = array(
			'url' => $erpUrl,
			'label' => 'Platform ERP',
			'icon' => 'fa-chart-line',
			'primary' => true,
		);
	}
	if ($isSuper && !$isDemoCp && strpos($here, '/tenant_hub/') === false) {
		$actions[] = array(
			'url' => '/' . $backend . '/shop/tenant_hub/tenant_hub',
			'label' => 'Tenant hub',
			'icon' => 'fa-cloud',
		);
	}
	if ($isSuper && !$isDemoCp) {
		$actions[] = array(
			'url' => 'https://www.ecomae.com/',
			'label' => 'Marketing site',
			'icon' => 'fa-globe',
			'target' => '_blank',
		);
	} elseif (!empty($DP_Config->domain_path) && !(function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only())) {
		$actions[] = array(
			'url' => (string) $DP_Config->domain_path,
			'label' => 'View site',
			'icon' => 'fa-external-link',
			'target' => '_blank',
		);
	} elseif (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
		$key = function_exists('epc_portal_demo_cp_site_key') ? epc_portal_demo_cp_site_key() : '';
		if ($key !== '') {
			$actions[] = array(
				'url' => 'https://www.ecomae.com/demo/' . preg_replace('/[^a-z0-9_]/', '', strtolower($key)) . '/',
				'label' => 'No storefront',
				'icon' => 'fa-ban',
				'target' => '_blank',
			);
		}
	}

	$clientErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
	if (is_file($clientErpFile)) {
		require_once $clientErpFile;
	}
	$erpShellFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
	if (is_file($erpShellFile)) {
		require_once $erpShellFile;
	}
	if (!$isSuper && !(function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only())
		&& function_exists('epc_erp_cp_shell_launcher_url') && strpos($here, '/shop/finance/erp') === false) {
		$erpUrl = epc_erp_cp_shell_launcher_url();
		if ($erpUrl !== '') {
			$actions[] = array(
				'url' => $erpUrl,
				'label' => 'Client ERP',
				'icon' => 'fa-university',
				'primary' => !$isPlatformOp,
			);
		}
	}

	return array(
		'eyebrow' => $eyebrow,
		'icon' => (string) ($industry['icon'] ?? 'fa-cog'),
		'role_label' => (string) ($shell['label'] ?? ''),
		'role_type' => (string) ($shell['type'] ?? ''),
		'actions' => $actions,
	);
}

/**
 * Render quick-link pills for the CP page header.
 */
function epc_cp_page_header_actions_html(array $actions): string
{
	if ($actions === array()) {
		return '';
	}
	$html = '<div class="epc-cp-page-header__actions">';
	foreach ($actions as $action) {
		if (empty($action['url']) || empty($action['label'])) {
			continue;
		}
		$class = 'epc-cp-page-header__pill';
		if (!empty($action['primary'])) {
			$class .= ' epc-cp-page-header__pill--primary';
		}
		$target = '';
		if (!empty($action['target'])) {
			$target = ' target="' . htmlspecialchars((string) $action['target'], ENT_QUOTES, 'UTF-8') . '" rel="noopener noreferrer"';
		}
		$icon = !empty($action['icon']) ? (string) $action['icon'] : 'fa-link';
		$html .= '<a class="' . $class . '" href="' . htmlspecialchars((string) $action['url'], ENT_QUOTES, 'UTF-8') . '"' . $target . '>';
		$html .= '<i class="fa ' . htmlspecialchars($icon, ENT_QUOTES, 'UTF-8') . '"></i>';
		$html .= '<span>' . htmlspecialchars((string) $action['label'], ENT_QUOTES, 'UTF-8') . '</span>';
		$html .= '</a>';
	}
	$html .= '</div>';
	return $html;
}

/**
 * Hide CP accordion submenus before JS (groups start collapsed).
 */
function epc_cp_menu_sections_early_style()
{
	return '<style id="epc-cp-menu-sections-early">'
		. '.epc-cp #side-menu>li:not(.active)>ul.nav-second-level,'
		. '.epc-cp-shell #side-menu>li:not(.active)>ul.nav-second-level,'
		. '.epc-cp-shell #side-menu>li:not(.active)>ul.epc-cp-nav-section__children{display:none!important;visibility:hidden!important;height:0!important;overflow:hidden!important}'
		. '.epc-cp #side-menu>li.active>ul.nav-second-level,'
		. '.epc-cp-shell #side-menu>li.active>ul.nav-second-level,'
		. '.epc-cp-shell #side-menu>li.active>ul.epc-cp-nav-section__children{display:block!important;visibility:visible!important;height:auto!important;overflow:visible!important}'
		. '</style>' . "\n";
}

/**
 * Sidebar width: expanded by default; icon strip only when user toggled or saved collapsed.
 */
function epc_cp_sidebar_early_init_script()
{
	return <<<'HTML'
<script>
(function(){
	var KEY='epc_cp_sidebar_collapsed';
	function isSuperCpPath(){
		var p=window.location.pathname||'';
		return p.indexOf('/tenant_hub/')!==-1||p.indexOf('/control/')!==-1||p.indexOf('/platform-erp/')!==-1;
	}
	function maybeRecover(){
		var pane=document.querySelector('#wrapper .content.epc-cp-main-pane')||document.querySelector('#wrapper .content');
		if(!pane){return;}
		var h=pane.offsetHeight||pane.scrollHeight||0;
		var inner=pane.innerText?pane.innerText.replace(/\s+/g,'').length:0;
		var hasHub=pane.querySelector('.epc-th-hero,.epc-th-kpi,.epc-erp-shell,.hpanel');
		if(h>=100&&inner>=20&&(!isSuperCpPath()||hasHub)){return;}
		try{localStorage.removeItem(KEY);localStorage.setItem(KEY,'0');}catch(e){}
		document.documentElement.classList.remove('epc-cp-sidebar-collapsed');
		if(document.body){document.body.classList.remove('epc-cp-sidebar-collapsed','hide-sidebar');}
		if(typeof window.epcCpSidebarApply==='function'){window.epcCpSidebarApply(false,true);}
	}
	function boot(){
		maybeRecover();
		window.setTimeout(maybeRecover,80);
		window.setTimeout(maybeRecover,350);
	}
	if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',boot);}else{boot();}
})();
</script>
HTML;
}

/**
 * ERP shell must not inherit CP icon-strip collapse (hides sub-tabs and blocks accordion clicks).
 */
function epc_erp_sidebar_early_init_script()
{
	return <<<'HTML'
<script>
(function(){
	function clearCpCollapse() {
		document.documentElement.classList.remove('epc-cp-sidebar-collapsed');
		if (document.body) {
			document.body.classList.remove('epc-cp-sidebar-collapsed', 'hide-sidebar');
		}
		try {
			localStorage.setItem('epc_cp_sidebar_collapsed', '0');
		} catch (e) {}
	}
	clearCpCollapse();
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', clearCpCollapse);
	}
})();
</script>
HTML;
}

/**
 * ERP Suite left sidebar accordion — footer only (outside eval'd main pane).
 */
function epc_erp_sidebar_accordion_script(): string
{
	return <<<'HTML'
<script id="epc-erp-sidebar-accordion">
(function(){
	var erpKey = 'epc_erp_menu_groups_open';

	function readSaved() {
		try {
			return JSON.parse(localStorage.getItem(erpKey) || 'null');
		} catch (e) {
			return null;
		}
	}

	function writeSaved() {
		var open = [];
		document.querySelectorAll('.epc-erp-sidebar-group.is-open').forEach(function(g) {
			var area = g.getAttribute('data-area');
			if (area) open.push(area);
		});
		try { localStorage.setItem(erpKey, JSON.stringify(open)); } catch (e) {}
	}

	function setGroupOpen(grp, open, closeSiblings) {
		if (!grp) return;
		var btn = grp.querySelector('.epc-erp-sidebar-group-hd');
		if (open && closeSiblings !== false) {
			document.querySelectorAll('.epc-erp-sidebar-group.is-open').forEach(function(other) {
				if (other !== grp) setGroupOpen(other, false, false);
			});
		}
		grp.classList.toggle('is-open', open);
		if (btn) btn.setAttribute('aria-expanded', open ? 'true' : 'false');
	}

	window.epcErpMenuSectionsSave = writeSaved;

	window.epcErpMenuSectionsInit = function() {
		var activeGroup = document.querySelector('.epc-erp-sidebar-group.is-active-area');
		document.querySelectorAll('.epc-erp-sidebar-group.is-open').forEach(function(g) {
			g.classList.remove('is-open');
			var h = g.querySelector('.epc-erp-sidebar-group-hd');
			if (h) h.setAttribute('aria-expanded', 'false');
		});
		var saved = readSaved();
		if (saved && saved.length) {
			saved.forEach(function(areaKey) {
				var g = document.querySelector('.epc-erp-sidebar-group[data-area="' + areaKey + '"]');
				if (g) setGroupOpen(g, true, false);
			});
			return;
		}
		if (activeGroup) setGroupOpen(activeGroup, true, false);
	};

	function ensureShellNavLinks() {
		document.querySelectorAll('.epc-erp-sidebar-item a[href], .epc-erp-breadcrumb a[href]').forEach(function(a) {
			var href = a.getAttribute('href') || '';
			if (!href || href.indexOf('javascript') === 0 || href.indexOf('#') === 0) return;
			if (href.indexOf('/shop/finance/erp') === -1) return;
			if (href.indexOf('epc_erp_shell=') !== -1) return;
			a.setAttribute('href', href + (href.indexOf('?') >= 0 ? '&' : '?') + 'epc_erp_shell=1');
		});
	}

	function bindAccordion() {
		var sidebar = document.getElementById('epc_erp_sidebar');
		if (!sidebar) return;
		sidebar.style.setProperty('pointer-events', 'auto', 'important');
		ensureShellNavLinks();
		document.querySelectorAll('.epc-erp-sidebar-group-hd').forEach(function(btn) {
			if (btn._epcErpAccordionBound) return;
			btn._epcErpAccordionBound = true;
			btn.addEventListener('click', function(e) {
				e.preventDefault();
				e.stopPropagation();
				var grp = btn.closest('.epc-erp-sidebar-group');
				if (!grp) return;
				var wasOpen = grp.classList.contains('is-open');
				setGroupOpen(grp, !wasOpen, true);
				writeSaved();
			});
		});
		window.epcErpMenuSectionsInit();
	}

	function bindMobileSidebar() {
		var sidebar = document.getElementById('epc_erp_sidebar');
		var backdrop = document.getElementById('epc_erp_sidebar_backdrop');
		var toggleBtn = document.getElementById('epc_erp_sidebar_toggle');
		var closeBtn = document.getElementById('epc_erp_sidebar_close');
		function setSidebarOpen(open) {
			if (!sidebar) return;
			document.body.classList.toggle('epc-erp-sidebar-open', open);
			if (toggleBtn) toggleBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
		}
		if (toggleBtn && !toggleBtn._epcErpToggleBound) {
			toggleBtn._epcErpToggleBound = true;
			toggleBtn.addEventListener('click', function() { setSidebarOpen(true); });
		}
		if (closeBtn && !closeBtn._epcErpCloseBound) {
			closeBtn._epcErpCloseBound = true;
			closeBtn.addEventListener('click', function() { setSidebarOpen(false); });
		}
		if (backdrop && !backdrop._epcErpBackdropBound) {
			backdrop._epcErpBackdropBound = true;
			backdrop.addEventListener('click', function() { setSidebarOpen(false); });
		}
	}

	function boot() {
		bindAccordion();
		bindMobileSidebar();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}
})();
</script>
HTML;
}

/**
 * Vanilla hide-menu toggle — works when jQuery/homer.js 404 on nginx.
 */
function epc_cp_hide_menu_vanilla_script(): string
{
	return <<<'HTML'
<script id="epc-cp-hide-menu-vanilla">
(function(){
	function bindHideMenu() {
		document.querySelectorAll('.hide-menu').forEach(function(el) {
			if (el._epcHideMenuBound) return;
			el._epcHideMenuBound = true;
			el.addEventListener('click', function(e) {
				e.preventDefault();
				if (window.innerWidth < 769) {
					document.body.classList.toggle('show-sidebar');
				} else if (typeof window.epcCpSidebarToggle === 'function') {
					window.epcCpSidebarToggle();
				} else {
					document.body.classList.toggle('hide-sidebar');
				}
			});
		});
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', bindHideMenu);
	} else {
		bindHideMenu();
	}
})();
</script>
HTML;
}

/**
 * CP #side-menu accordion — all section groups collapsed on first load; optional localStorage.
 */
function epc_cp_menu_sections_script()
{
	return <<<'HTML'
<script>
(function(){
	var KEY = 'epc_cp_menu_groups_open';

	function detachMetisMenu() {
		try {
			if (window.jQuery && jQuery.fn.metisMenu) {
				var $sm = jQuery('#side-menu');
				if ($sm.length && $sm.data('metisMenu')) {
					$sm.metisMenu('dispose');
				}
			}
		} catch (e) {}
	}

	function ensureMenuClickable() {
		var menu = document.getElementById('menu');
		var nav = document.getElementById('navigation');
		if (menu && !document.documentElement.classList.contains('epc-cp-sidebar-collapsed')) {
			menu.style.setProperty('pointer-events', 'auto', 'important');
		}
		if (nav) {
			nav.style.setProperty('pointer-events', 'auto', 'important');
		}
		if (typeof window.epcCpClearUiBlockers === 'function') {
			window.epcCpClearUiBlockers();
		}
	}
	window.epcCpEnsureMenuClickable = ensureMenuClickable;

	function groupId(li) {
		var label = li.querySelector('.nav-label');
		return label ? label.textContent.replace(/\s+/g, ' ').trim() : '';
	}

	function readSaved() {
		try {
			var raw = localStorage.getItem(KEY);
			return raw ? JSON.parse(raw) : null;
		} catch (e) {
			return null;
		}
	}

	function writeSaved(ids) {
		try { localStorage.setItem(KEY, JSON.stringify(ids || [])); } catch (e) {}
	}

	function collectOpen() {
		var ids = [];
		document.querySelectorAll('#side-menu > li.active').forEach(function(li) {
			if (sectionSubmenu(li)) {
				var id = groupId(li);
				if (id) ids.push(id);
			}
		});
		return ids;
	}

	function normalizePath(href) {
		if (!href || href.indexOf('javascript') === 0 || href === '#') return '';
		try {
			var u = document.createElement('a');
			u.href = href;
			var p = u.pathname.replace(/\/+$/, '') || '/';
			return p + (u.search || '');
		} catch (e) {
			return href;
		}
	}

	function sectionSubmenu(li) {
		if (!li) return null;
		for (var i = 0; i < li.children.length; i++) {
			var child = li.children[i];
			if (child && child.tagName === 'UL' &&
				(child.classList.contains('nav-second-level') || child.classList.contains('epc-cp-nav-section__children'))) {
				return child;
			}
		}
		return null;
	}

	function sectionToggleLink(li) {
		if (!li) return null;
		for (var i = 0; i < li.children.length; i++) {
			var child = li.children[i];
			if (child && child.tagName === 'A') return child;
		}
		return null;
	}

	function isSectionToggleLink(link) {
		if (!link) return false;
		var menu = document.getElementById('side-menu');
		var li = link.parentElement;
		if (!menu || !li || li.parentElement !== menu) return false;
		if (!sectionSubmenu(li)) return false;
		var href = (link.getAttribute('href') || '').trim();
		return href === '' || href === '#' || href.indexOf('javascript') === 0;
	}

	function markCurrentSubItem() {
		var here = normalizePath(window.location.pathname + window.location.search);
		if (!here) return;
		document.querySelectorAll('#side-menu ul.nav-second-level a[href], #side-menu ul.epc-cp-nav-section__children a[href]').forEach(function(a) {
			var target = normalizePath(a.getAttribute('href'));
			if (target && target === here) {
				var item = a.closest('li');
				if (item) item.classList.add('active');
			}
		});
	}

	function setGroupOpen(li, open, closeSiblings) {
		if (!li) return;
		var ul = sectionSubmenu(li);
		if (!ul) return;
		if (open && closeSiblings !== false) {
			document.querySelectorAll('#side-menu > li.active').forEach(function(other) {
				if (other !== li) setGroupOpen(other, false, false);
			});
		}
		if (open) {
			li.classList.add('active');
			ul.classList.add('collapse', 'in');
			ul.classList.remove('collapsing');
			ul.style.removeProperty('height');
			ul.style.removeProperty('max-height');
			ul.style.setProperty('display', 'block', 'important');
			ul.style.setProperty('visibility', 'visible', 'important');
			ul.style.setProperty('overflow', 'visible', 'important');
			ul.setAttribute('aria-expanded', 'true');
			var link = sectionToggleLink(li);
			if (link) link.setAttribute('aria-expanded', 'true');
		} else {
			li.classList.remove('active');
			ul.classList.add('collapse');
			ul.classList.remove('in', 'collapsing');
			ul.style.removeProperty('height');
			ul.style.removeProperty('max-height');
			ul.style.removeProperty('display');
			ul.style.removeProperty('visibility');
			ul.style.removeProperty('overflow');
			ul.setAttribute('aria-expanded', 'false');
			var linkClose = sectionToggleLink(li);
			if (linkClose) linkClose.setAttribute('aria-expanded', 'false');
		}
	}

	function toggleGroupFromLink(link) {
		if (!isSectionToggleLink(link)) return false;
		var li = link.parentElement;
		var wasCollapsed = typeof window.epcCpSidebarIsCollapsed === 'function' &&
			window.epcCpSidebarIsCollapsed() && window.innerWidth >= 769;
		if (wasCollapsed && typeof window.epcCpSidebarApply === 'function') {
			window.epcCpSidebarApply(false, true);
		}
		if (typeof window.epcCpClearUiBlockers === 'function') {
			window.epcCpClearUiBlockers();
		}
		var open = wasCollapsed ? true : !li.classList.contains('active');
		setGroupOpen(li, open, true);
		writeSaved(collectOpen());
		if (open) {
			window.setTimeout(function() {
				var ul = sectionSubmenu(li);
				if (!li.classList.contains('active') || !ul || ul.offsetHeight < 1) {
					setGroupOpen(li, true, true);
					writeSaved(collectOpen());
				}
				ensureMenuClickable();
			}, 160);
		}
		return true;
	}

	function syncOpenGroupsFromDom() {
		document.querySelectorAll('#side-menu > li').forEach(function(li) {
			if (!sectionSubmenu(li)) return;
			setGroupOpen(li, li.classList.contains('active'), false);
		});
	}

	function collapseAllGroups() {
		document.querySelectorAll('#side-menu > li').forEach(function(li) {
			if (!sectionSubmenu(li)) return;
			setGroupOpen(li, false, false);
		});
	}

	function openParentForActiveChild() {
		document.querySelectorAll('#side-menu > li').forEach(function(li) {
			if (li.querySelector('ul.nav-second-level li.active, ul.epc-cp-nav-section__children li.active')) {
				setGroupOpen(li, true, false);
			}
		});
	}

	function restoreSavedGroups() {
		var saved = readSaved();
		if (!saved || !saved.length) return;
		document.querySelectorAll('#side-menu > li').forEach(function(li) {
			if (!sectionSubmenu(li)) return;
			var id = groupId(li);
			if (saved.indexOf(id) >= 0) setGroupOpen(li, true, false);
		});
	}

	window.epcCpMenuSectionOpen = function(li, open) {
		setGroupOpen(li, open !== false, true);
		writeSaved(collectOpen());
	};

	window.epcCpMenuSectionsApplySaved = function() {
		markCurrentSubItem();
		openParentForActiveChild();
		restoreSavedGroups();
		syncOpenGroupsFromDom();
	};

	window.epcCpMenuSectionsPrepare = function() {
		var menu = document.getElementById('side-menu');
		if (!menu) return;
		detachMetisMenu();
		window.epcCpMenuSectionsApplySaved();
	};

	window.epcCpMenuSectionsBind = function() {
		var menu = document.getElementById('side-menu');
		var nav = document.getElementById('navigation');
		if (!menu || !nav) return;
		ensureMenuClickable();
		if (!nav._epcNavClickGuard) {
			nav._epcNavClickGuard = true;
			nav.addEventListener('pointerdown', ensureMenuClickable, true);
		}
		function bindSectionToggles() {
			for (var i = 0; i < menu.children.length; i++) {
				var li = menu.children[i];
				if (!li || li.tagName !== 'LI') continue;
				var link = sectionToggleLink(li);
				if (!link || !sectionSubmenu(li) || link._epcToggleBound) continue;
				link._epcToggleBound = true;
				(function(lnk) {
					lnk.addEventListener('click', function(e) {
						if (!toggleGroupFromLink(lnk)) return;
						e.preventDefault();
						e.stopPropagation();
					});
				})(link);
			}
		}
		bindSectionToggles();
		if (menu._epcSectionsBound) return;
		menu._epcSectionsBound = true;
		nav.addEventListener('click', function(e) {
			if (e.target.closest('ul.nav-second-level, ul.epc-cp-nav-section__children')) return;
			var node = e.target;
			var li = null;
			while (node && node !== nav) {
				if (node.parentElement === menu && node.tagName === 'LI') {
					li = node;
					break;
				}
				node = node.parentElement;
			}
			if (!li) return;
			var link = sectionToggleLink(li);
			if (!link || !toggleGroupFromLink(link)) return;
			e.preventDefault();
			e.stopPropagation();
		}, true);
	};

	window.epcCpRecoverMenuState = function() {
		try {
			localStorage.removeItem('epc_cp_menu_groups_open');
			localStorage.removeItem('epc_cp_sidebar_collapsed');
			localStorage.setItem('epc_cp_sidebar_collapsed', '0');
		} catch (err) {}
		document.documentElement.classList.remove('epc-cp-sidebar-collapsed');
		if (document.body) {
			document.body.classList.remove('epc-cp-sidebar-collapsed', 'hide-sidebar', 'modal-open');
		}
		if (typeof window.epcCpSidebarApply === 'function') {
			window.epcCpSidebarApply(false, false);
		}
		if (typeof window.epcCpClearUiBlockers === 'function') {
			window.epcCpClearUiBlockers();
		}
		if (typeof window.epcCpNuclearForceVisible === 'function') {
			window.epcCpNuclearForceVisible();
		}
	};

	window.epcCpMenuSectionsInit = function() {
		window.epcCpMenuSectionsPrepare();
		window.epcCpMenuSectionsBind();
	};

	function bootCpMenuSections() {
		if (!document.getElementById('side-menu')) return;
		window.epcCpMenuSectionsInit();
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', bootCpMenuSections);
	} else {
		bootCpMenuSections();
	}

	window.epcErpMenuSectionsInit = function() {
		var erpKey = 'epc_erp_menu_groups_open';
		var activeGroup = document.querySelector('.epc-erp-sidebar-group.is-active-area');
		document.querySelectorAll('.epc-erp-sidebar-group.is-open').forEach(function(g) {
			g.classList.remove('is-open');
			var h = g.querySelector('.epc-erp-sidebar-group-hd');
			if (h) h.setAttribute('aria-expanded', 'false');
		});
		var saved = null;
		try { saved = JSON.parse(localStorage.getItem(erpKey) || 'null'); } catch (e) {}
		if (saved && saved.length) {
			saved.forEach(function(areaKey) {
				var g = document.querySelector('.epc-erp-sidebar-group[data-area="' + areaKey + '"]');
				if (!g) return;
				g.classList.add('is-open');
				var h = g.querySelector('.epc-erp-sidebar-group-hd');
				if (h) h.setAttribute('aria-expanded', 'true');
			});
			return;
		}
		if (activeGroup) {
			activeGroup.classList.add('is-open');
			var activeHd = activeGroup.querySelector('.epc-erp-sidebar-group-hd');
			if (activeHd) activeHd.setAttribute('aria-expanded', 'true');
		}
	};

	window.epcErpMenuSectionsSave = function() {
		var erpKey = 'epc_erp_menu_groups_open';
		var open = [];
		document.querySelectorAll('.epc-erp-sidebar-group.is-open').forEach(function(g) {
			var area = g.getAttribute('data-area');
			if (area) open.push(area);
		});
		try { localStorage.setItem(erpKey, JSON.stringify(open)); } catch (e) {}
	};
})();
</script>
HTML;
}

/**
 * Shared sidebar collapse toggle (CP #menu + ERP inner sidebar).
 */
function epc_cp_sidebar_collapse_script()
{
	return <<<'HTML'
<script>
(function(){
	var KEY = 'epc_cp_sidebar_collapsed';
	var toggleLock = 0;
	function isCollapsed() {
		return document.documentElement.classList.contains('epc-cp-sidebar-collapsed');
	}
	function syncToggleBtn(btn, collapsed) {
		if (!btn) {
			return;
		}
		btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
		btn.setAttribute('aria-label', collapsed ? 'Show menu' : 'Hide menu');
		btn.setAttribute('title', 'Menu');
		var icon = btn.querySelector('.fa');
		if (icon) {
			icon.className = collapsed ? 'fa fa-chevron-right' : 'fa fa-chevron-left';
		}
	}
	function apply(collapsed, persist) {
		document.documentElement.classList.toggle('epc-cp-sidebar-collapsed', collapsed);
		if (document.body) {
			document.body.classList.toggle('epc-cp-sidebar-collapsed', collapsed);
			document.body.classList.toggle('hide-sidebar', collapsed);
		}
		syncToggleBtn(document.getElementById('epc-cp-sidebar-toggle'), collapsed);
		var erpBtn = document.getElementById('epc_erp_sidebar_collapse_toggle');
		if (erpBtn) {
			erpBtn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
			erpBtn.setAttribute('aria-label', collapsed ? 'Expand sidebar' : 'Collapse sidebar');
		}
		if (typeof window.epcFixCpSidebarScroll === 'function') {
			window.epcFixCpSidebarScroll();
		}
		if (persist) {
			try { localStorage.setItem(KEY, collapsed ? '1' : '0'); } catch (e) {}
		}
	}
	function toggle() {
		var now = Date.now();
		if (now - toggleLock < 350) {
			return;
		}
		toggleLock = now;
		if (typeof window.epcCpClearUiBlockers === 'function') {
			window.epcCpClearUiBlockers();
		}
		var wasCollapsed = isCollapsed();
		apply(!wasCollapsed, true);
		if (wasCollapsed && typeof window.epcCpMenuSectionsApplySaved === 'function') {
			window.setTimeout(window.epcCpMenuSectionsApplySaved, 50);
		}
		if (typeof window.epcCpNuclearForceVisible === 'function') {
			window.setTimeout(window.epcCpNuclearForceVisible, 60);
		}
	}
	window.epcCpSidebarToggle = toggle;
	window.epcCpSidebarApply = apply;
	window.epcCpSidebarIsCollapsed = isCollapsed;
	function bind() {
		var collapsed = isCollapsed();
		if (document.body) {
			document.body.classList.toggle('epc-cp-sidebar-collapsed', collapsed);
			document.body.classList.toggle('hide-sidebar', collapsed);
		}
		syncToggleBtn(document.getElementById('epc-cp-sidebar-toggle'), collapsed);
		var cpBtn = document.getElementById('epc-cp-sidebar-toggle');
		if (cpBtn && !cpBtn._epcBound) {
			cpBtn._epcBound = true;
			cpBtn.addEventListener('click', function(e) {
				e.preventDefault();
				e.stopPropagation();
				toggle();
			});
		}
		var erpBtn = document.getElementById('epc_erp_sidebar_collapse_toggle');
		if (erpBtn && !erpBtn._epcBound) {
			erpBtn._epcBound = true;
			erpBtn.addEventListener('click', function(e) { e.preventDefault(); toggle(); });
		}
		document.querySelectorAll('.epc-erp-sidebar-group-hd').forEach(function(btn) {
			if (btn._epcErpCollapseBound) {
				return;
			}
			btn._epcErpCollapseBound = true;
			btn.addEventListener('click', function(e) {
				if (!isCollapsed() || window.innerWidth < 769) {
					return;
				}
				e.preventDefault();
				var grp = btn.closest('.epc-erp-sidebar-group');
				apply(false, true);
				window.setTimeout(function() {
					if (grp) {
						grp.classList.add('is-open');
						btn.setAttribute('aria-expanded', 'true');
						if (typeof window.epcErpMenuSectionsSave === 'function') {
							window.epcErpMenuSectionsSave();
						}
					}
				}, 50);
			}, true);
		});
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', bind);
	} else {
		bind();
	}
})();
</script>
HTML;
}

/**
 * Lightweight stagger fade-in for dashboard cards (no jQuery; safe with nuclear reveal).
 */
function epc_cp_modern_reveal_script(): string
{
	return <<<'HTML'
<script id="epc-cp-modern-reveal">
(function(){
	if (!document.body || !document.body.classList.contains('epc-cp-modern')) {
		return;
	}
	var SEL = '.epc-scp-kpi__card,.epc-th-kpi__card,.epc-scp-quick-card,.epc-cp-card,.epc-cp-stat,.hpanel,.epc-scp-table-card,.epc-scp-form-card,.epc-scp-dashboard__hero';
	function reveal() {
		var root = document.querySelector('#wrapper .epc-cp-content-inner') || document.querySelector('#wrapper .content');
		if (!root) {
			return;
		}
		var items = root.querySelectorAll(SEL);
		for (var i = 0; i < items.length; i++) {
			var el = items[i];
			if (el.classList.contains('epc-cp-modern-in')) {
				continue;
			}
			el.classList.add('epc-cp-modern-in');
			el.style.setProperty('--epc-cp-reveal-i', String(i));
		}
	}
	function boot() {
		reveal();
		window.setTimeout(reveal, 120);
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}
})();
</script>
HTML;
}

/**
 * @return array{type:string,badge:string,heading:string,sub:string,tagline:string,body_class:string,features:array<int,string>}
 */
function epc_cp_login_context()
{
	$clientErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
	if (is_file($clientErpFile)) {
		require_once $clientErpFile;
	}
	$platformErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
	if (is_file($platformErpFile)) {
		require_once $platformErpFile;
	}

	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		return array(
			'type' => 'platform_erp',
			'badge' => 'Platform ERP',
			'heading' => 'ECOM AE Platform ERP',
			'sub' => 'ECOM AE Operations — finance & platform ledger',
			'tagline' => 'Operator ERP on ecomae registry · Super CP staff only',
			'body_class' => 'epc-cp-login--platform_erp',
			'features' => array(
				'fa-chart-line' => 'Platform finance, GL & VAT',
				'fa-building' => 'ECOM AE company operations',
				'fa-shield' => 'Isolated from client tenant databases',
			),
		);
	}

	if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()) {
		$row = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
		if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
			$trade = is_array($row) ? trim((string) ($row['trade_name'] ?? '')) : '';
			$key = function_exists('epc_portal_demo_cp_site_key') ? epc_portal_demo_cp_site_key() : '';
			if ($trade === '' && $key !== '') {
				$trade = strtoupper($key);
			}
			if ($trade === '') {
				$trade = 'Demo';
			}
			return array(
				'type' => 'demo_erp_only',
				'badge' => 'ERP-only demo',
				'heading' => $trade . ' — ERP Sandbox',
				'sub' => 'Finance, CRM & operations — no storefront',
				'tagline' => 'ECOM AE sandbox · expires automatically',
				'body_class' => 'epc-cp-login--demo_erp_only',
				'features' => array(
					'fa-chart-line' => 'Finance, GL & VAT reporting',
					'fa-users' => 'CRM & company operations',
					'fa-cubes' => 'Inventory & procurement',
				),
			);
		}
		if (function_exists('epc_portal_demo_is_autoparts_parity') && epc_portal_demo_is_autoparts_parity()) {
			$ctx = epc_brand_cp_context();
			$company = trim((string) ($ctx['company_name'] ?? ''));
			$product = trim((string) ($ctx['product_name'] ?? 'Control Panel'));
			$heading = $company !== '' ? $company : $product;
			return array(
				'type' => 'tenant',
				'badge' => function_exists('translate_str_by_id') ? translate_str_by_id(3992) : 'Control Panel',
				'heading' => $heading,
				'sub' => 'Commerce, orders & operations',
				'tagline' => trim((string) ($ctx['hub_tagline'] ?? 'Finance & operations')),
				'body_class' => 'epc-cp-login--tenant',
				'features' => array(
					'fa-shopping-cart' => 'Orders & fulfilment',
					'fa-chart-line' => 'ERP, finance & reports',
					'fa-credit-card' => 'Payment gateways & channels',
				),
			);
		}
		$trade = is_array($row) ? trim((string) ($row['trade_name'] ?? '')) : '';
		$key = function_exists('epc_portal_demo_cp_site_key') ? epc_portal_demo_cp_site_key() : '';
		if ($trade === '' && $key !== '') {
			$trade = strtoupper($key);
		}
		if ($trade === '') {
			$trade = 'Demo';
		}
		return array(
			'type' => 'demo_cp',
			'badge' => 'Demo CP',
			'heading' => $trade . ' — Sandbox',
			'sub' => 'Shop, catalog & prices — isolated demo tenant',
			'tagline' => 'ECOM AE sandbox · expires automatically',
			'body_class' => 'epc-cp-login--demo_cp',
			'features' => array(
				'fa-shopping-cart' => 'Orders & catalogue',
				'fa-tags' => 'Prices & promotions',
				'fa-cubes' => 'Inventory sample data',
			),
		);
	}

	if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
		$row = function_exists('epc_client_erp_tenant_row') ? epc_client_erp_tenant_row() : null;
		$trade = $row ? trim((string) ($row['trade_name'] ?? '')) : '';
		$key = function_exists('epc_client_erp_site_key') ? epc_client_erp_site_key() : '';
		if ($trade === '' && $key !== '') {
			$trade = strtoupper($key);
		}
		if ($trade === '') {
			$trade = 'Client';
		}
		return array(
			'type' => 'client_erp',
			'badge' => 'Client ERP',
			'heading' => $trade . ' ERP',
			'sub' => 'Company ERP — finance & operations',
			'tagline' => 'Secure tenant workspace · ECOM AE cloud',
			'body_class' => 'epc-cp-login--client_erp',
			'features' => array(
				'fa-chart-line' => 'Finance, GL & VAT reporting',
				'fa-cubes' => 'Inventory & procurement',
				'fa-users' => 'CRM & company operations',
			),
		);
	}

	$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
	if ($isSuper) {
		return array(
			'type' => 'super',
			'badge' => 'BOS · Control',
			'heading' => 'BOS — Business Operation System',
			'sub' => 'One login · operators control the whole fleet, tenants see only their own area',
			'tagline' => 'Unified control over every tenant, ERP-only client and demo',
			'body_class' => 'epc-cp-login--super',
			'features' => array(
				'fa-globe' => 'Fleet command — commerce, ERP-only & demo',
				'fa-rocket' => 'Tenant onboarding, templates & module packs',
				'fa-shield' => 'Platform health, governance & failover',
			),
		);
	}

	$ctx = epc_brand_cp_context();
	$company = trim((string) ($ctx['company_name'] ?? ''));
	$product = trim((string) ($ctx['product_name'] ?? 'Control Panel'));
	$heading = $company !== '' ? $company : $product;

	return array(
		'type' => 'tenant',
		'badge' => function_exists('translate_str_by_id') ? translate_str_by_id(3992) : 'Control Panel',
		'heading' => $heading,
		'sub' => 'Commerce, orders & operations',
		'tagline' => trim((string) ($ctx['hub_tagline'] ?? 'Finance & operations')),
		'body_class' => 'epc-cp-login--tenant',
		'features' => array(
			'fa-shopping-cart' => 'Orders & fulfilment',
			'fa-chart-line' => 'ERP, finance & reports',
			'fa-credit-card' => 'Payment gateways & channels',
		),
	);
}

/**
 * @return array{label:string,company:string,type:string}
 */
function epc_cp_shell_context()
{
	$login = epc_cp_login_context();
	if ($login['type'] === 'super') {
		return array(
			'type' => 'super',
			'label' => 'BOS · Operator',
			'company' => 'ECOM AE',
		);
	}
	if ($login['type'] === 'platform_erp') {
		return array(
			'type' => 'platform_erp',
			'label' => 'Platform ERP',
			'company' => 'ECOM AE Operations',
		);
	}
	if ($login['type'] === 'demo_erp_only') {
		$heading = preg_replace('/\s+—\s+ERP Sandbox$/', '', (string) $login['heading']);
		return array(
			'type' => 'demo_erp_only',
			'label' => 'ERP-only demo',
			'company' => $heading,
		);
	}
	if ($login['type'] === 'demo_cp') {
		$heading = preg_replace('/\s+—\s+Sandbox$/', '', (string) $login['heading']);
		return array(
			'type' => 'demo_cp',
			'label' => 'Demo CP',
			'company' => $heading,
		);
	}
	if ($login['type'] === 'client_erp') {
		$heading = preg_replace('/\s+ERP\s*$/', '', (string) $login['heading']);
		return array(
			'type' => 'client_erp',
			'label' => $login['heading'],
			'company' => $heading,
		);
	}
	$ctx = epc_brand_cp_context();
	return array(
		'type' => 'tenant',
		'label' => function_exists('translate_str_by_id') ? translate_str_by_id(3992) : 'Control Panel',
		'company' => trim((string) ($ctx['company_name'] ?? $ctx['product_name'] ?? '')),
	);
}

function epc_cp_shell_enqueue_assets($includeLoginHero = false)
{
	global $DP_Config;
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	$base = '/' . $backend . '/templates/bootstrap_admin/css/';
	echo '<link rel="stylesheet" href="' . htmlspecialchars(
		epc_cp_shell_asset_href($base . 'epc_cp_ui.css', '/content/general_pages/epc_cp_ui_css.php'),
		ENT_QUOTES,
		'UTF-8'
	) . '" />' . "\n";
	echo '<link rel="stylesheet" href="' . htmlspecialchars(
		epc_cp_shell_asset_href($base . 'epc_cp_professional.css', '/content/general_pages/epc_cp_professional_css.php'),
		ENT_QUOTES,
		'UTF-8'
	) . '" />' . "\n";
	echo '<link rel="stylesheet" href="' . htmlspecialchars(
		epc_cp_shell_asset_href($base . 'epc_cp_density.css', '/content/general_pages/epc_cp_density_css.php'),
		ENT_QUOTES,
		'UTF-8'
	) . '" />' . "\n";
	echo '<link rel="stylesheet" href="' . htmlspecialchars(
		epc_cp_shell_asset_href($base . 'epc_cp_tenant_polish.css', '/content/general_pages/epc_cp_tenant_polish_css.php'),
		ENT_QUOTES,
		'UTF-8'
	) . '" />' . "\n";
	epc_ecomae_hub_logo_enqueue();
	if ($includeLoginHero) {
		epc_cp_login_hero_enqueue();
		epc_cp_login_enqueue();
	}
}

/**
 * Animated hub for login left panel; static fallback when container is too narrow.
 */
function epc_cp_login_hero_markup()
{
	$useAnimated = true;
	if (!empty($_COOKIE['epc_cp_login_static']) && (string) $_COOKIE['epc_cp_login_static'] === '1') {
		$useAnimated = false;
	}
	if ($useAnimated) {
		return epc_ecomae_hub_logo('login-panel', array(
			'show_title' => true,
			'show_tagline' => false,
			'aria_label' => 'ECOM AE unified ERP and commerce cloud',
		));
	}
	return epc_ecomae_static_logo('login', array(
		'show_title' => true,
		'show_tagline' => true,
		'aria_label' => 'ECOM AE',
	));
}

/**
 * Emergency inline CP styles — only when enqueue cannot serve CSS.
 *
 * Historically this always inlined ~200KB (ui + professional + hub logo) even
 * after epc_cp_shell_enqueue_assets() already emitted cacheable <link> tags.
 * On epartscart that doubled login/desktop HTML to ~237KB and slowed every CP
 * first paint. Opt-in: ?epc_cp_inline_css=1
 */
function epc_cp_shell_inline_style_block()
{
	if (empty($_GET['epc_cp_inline_css'])) {
		return '';
	}
	global $DP_Config;
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	if ($root === '') {
		return '';
	}
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	$files = array(
		'/' . $backend . '/templates/bootstrap_admin/css/epc_cp_ui.css',
		'/' . $backend . '/templates/bootstrap_admin/css/epc_cp_professional.css',
		'/' . $backend . '/templates/bootstrap_admin/css/epc_cp_density.css',
		'/content/general_pages/epc_ecomae_hub_logo.css',
	);
	$css = '';
	foreach ($files as $rel) {
		$path = $root . $rel;
		if (!is_file($path)) {
			continue;
		}
		$chunk = file_get_contents($path);
		if ($chunk !== false && $chunk !== '') {
			$css .= $chunk . "\n";
		}
	}
	if ($css === '') {
		return '';
	}
	return '<style id="epc-cp-inline-css">' . $css . '</style>' . "\n";
}
