<?php
defined('_ASTEXE_') or die('No access');

// Enable gzip compression for large storefront pages (reduces 1.5MB → ~200KB transfer)
if (!ini_get('zlib.output_compression') && function_exists('ob_gzhandler')
    && strpos($_SERVER['HTTP_ACCEPT_ENCODING'] ?? '', 'gzip') !== false
    && !headers_sent()) {
    @ini_set('zlib.output_compression', 'On');
    @ini_set('zlib.output_compression_level', '6');
}

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();

//Переменные для подстановки в input модулей поисковых строк
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/search_strs_for_inputs.php");
//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");



// Получаем информацию об офисе
$customer_office_info = array();
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");
if($customer_offices[0] > 0){
	$customer_office_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
	$customer_office_query->execute(array($customer_offices[0]));
	$customer_office_info = $customer_office_query->fetch(PDO::FETCH_ASSOC);
}
$epc_contact_phone = !empty($DP_Config->epc_contact_phone) ? trim($DP_Config->epc_contact_phone) : trim((string) ($customer_office_info['phone'] ?? ''));
$epc_whatsapp_number = !empty($DP_Config->epc_whatsapp_number) ? trim($DP_Config->epc_whatsapp_number) : $epc_contact_phone;
$epc_contact_phone_href = preg_replace('/[^0-9+]/', '', $epc_contact_phone);
$epc_whatsapp_href_number = preg_replace('/[^0-9]/', '', $epc_whatsapp_number);
// Header WhatsApp is a public contact channel (after Request a call back).
// Product-row WhatsApp ordering stays gated separately for guests.
if ($epc_whatsapp_href_number === '' && $epc_contact_phone_href !== '') {
	$epc_whatsapp_href_number = preg_replace('/[^0-9]/', '', $epc_contact_phone_href);
}
$epc_head_office_title = !empty($DP_Config->epc_head_office_title) ? trim($DP_Config->epc_head_office_title) : 'Head Office';
$epc_head_office_address = !empty($DP_Config->epc_head_office_address) ? trim(str_replace('\\n', "\n", $DP_Config->epc_head_office_address)) : '';
$epc_head_office_email = !empty($DP_Config->epc_head_office_email) ? trim($DP_Config->epc_head_office_email) : trim($customer_office_info['email']);
$epc_head_office_map_url = !empty($DP_Config->epc_head_office_map_url) ? trim($DP_Config->epc_head_office_map_url) : '';
$epc_global_locations_summary = !empty($DP_Config->epc_global_locations_summary) ? trim($DP_Config->epc_global_locations_summary) : '15 countries, multiple locations';
$epc_global_locations_countries = !empty($DP_Config->epc_global_locations_countries) ? trim(str_replace('\\n', "\n", $DP_Config->epc_global_locations_countries)) : '';
$epc_global_locations_map_url = !empty($DP_Config->epc_global_locations_map_url) ? trim($DP_Config->epc_global_locations_map_url) : $epc_head_office_map_url;
$epc_global_location_blocks = array();
if($epc_global_locations_countries != '')
{
	$epc_global_location_blocks = preg_split("/\n\s*\n/", $epc_global_locations_countries);
	$epc_global_location_blocks = array_filter(array_map('trim', $epc_global_location_blocks));
}
$epc_global_location_options = array();
foreach($epc_global_location_blocks as $epc_global_location_block)
{
	$epc_location_lines = preg_split("/\n+/", $epc_global_location_block);
	$epc_location_title = trim($epc_location_lines[0]);
	if($epc_location_title == '')
	{
		$epc_location_title = 'Location';
	}
	$epc_global_location_options[] = $epc_location_title;
}



require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_currency.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_customer_trade.php");
$epc_currency_records = epc_currency_records($db_link, $DP_Config);
$epc_selected_currency_iso = epc_currency_selected_iso($epc_currency_records, $DP_Config, $db_link);
$epc_header_user_id = class_exists('DP_User') ? (int)DP_User::getUserId() : 0;
$epc_currency_locked_for_user = ($epc_header_user_id > 0 && epc_trade_currency_locked($db_link, $epc_header_user_id));
$currency_record = $epc_currency_records[$epc_selected_currency_iso];
$currency_sign = $currency_record["sign"];
//Строка для обозначения валюты
if($DP_Config->currency_show_mode == "no")
{
	$currency_indicator = "";
}
else if($DP_Config->currency_show_mode == "sign_before" || $DP_Config->currency_show_mode == "sign_after")
{
	$currency_indicator = $currency_sign;
}
else
{
	$currency_indicator = $currency_record["caption_short"];
}
$epc_currency_js_config = epc_currency_js_config($epc_currency_records, $epc_selected_currency_iso, $DP_Config->shop_currency, $DP_Config->currency_show_mode);
require_once $_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal.php";
$epc_portal_site = epc_portal_site_profile();
$epc_portal_industry_code = isset($epc_portal_site['industry']) ? $epc_portal_site['industry'] : 'auto_parts';
$epc_portal_industry_meta = epc_portal_industry($epc_portal_industry_code);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_theme_templates.php';
$epc_portal_style_template = epc_portal_normalize_theme_template(
	$epc_portal_industry_code,
	(string) (epc_portal_load_site_settings()['theme_template'] ?? 'classic')
);
$epc_platform_marketing = (in_array(epc_portal_host(), array('www.ecomae.com', 'ecomae.com'), true)
	|| !empty($GLOBALS['epc_industry_subdomain_active']))
	&& empty($GLOBALS['epc_demo_storefront_context']);
$epc_commerce_storefront = function_exists('epc_portal_commerce_storefront_enabled') && epc_portal_commerce_storefront_enabled();
if ($epc_platform_marketing) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_data.php';
	$epc_platform_nav = epc_ecomae_platform_nav();
}
$epc_er_retail = function_exists('epc_portal_electronics_retail_enabled') && epc_portal_electronics_retail_enabled();
$epc_cpi_consulting = function_exists('epc_portal_consulting_primeinvest_enabled') && epc_portal_consulting_primeinvest_enabled();
$epc_frn_retail = function_exists('epc_portal_fashion_retail_namshi_enabled') && epc_portal_fashion_retail_namshi_enabled();
$epc_jrk_retail = function_exists('epc_portal_jewellery_retail_kiyasha_enabled') && epc_portal_jewellery_retail_kiyasha_enabled();
$epc_asp_pro = function_exists('epc_portal_automotive_spareparts_pro_enabled') && epc_portal_automotive_spareparts_pro_enabled();
$epc_custom_storefront = $epc_er_retail || $epc_cpi_consulting || $epc_frn_retail || $epc_jrk_retail;
$epc_storefront_package = function_exists('epc_portal_active_storefront_package')
	? (string) epc_portal_active_storefront_package()
	: '';
?>
<!DOCTYPE html>
<html lang="<?php echo $multilang_params['lang']; ?>" data-theme="default" data-epc-industry="<?php echo htmlspecialchars($epc_portal_industry_code, ENT_QUOTES, 'UTF-8'); ?>" data-epc-style="<?php echo htmlspecialchars($epc_portal_style_template, ENT_QUOTES, 'UTF-8'); ?>" data-epc-commerce="<?php echo $epc_commerce_storefront ? 'on' : 'off'; ?>" data-epc-storefront="<?php echo htmlspecialchars($epc_storefront_package !== '' ? $epc_storefront_package : 'default', ENT_QUOTES, 'UTF-8'); ?>">
<head>
	<!-- Google tag (gtag.js) - per-tenant measurement ID, deferred until after first paint -->
	<?php
	$epc_ga_id = function_exists('epc_portal_ga_measurement_id') ? epc_portal_ga_measurement_id() : 'G-J19D1KHXCG';
	$epc_ga_id = preg_replace('/[^A-Z0-9\-]/', '', strtoupper((string) $epc_ga_id));
	if ($epc_ga_id === '') {
		$epc_ga_id = 'G-J19D1KHXCG';
	}
	?>
	<script>
	  window.dataLayer = window.dataLayer || [];
	  function gtag(){dataLayer.push(arguments);}
	  window.addEventListener('load', function () {
	    var s = document.createElement('script');
	    s.async = true;
	    s.src = 'https://www.googletagmanager.com/gtag/js?id=<?php echo htmlspecialchars($epc_ga_id, ENT_QUOTES, 'UTF-8'); ?>';
	    s.onload = function () {
	      gtag('js', new Date());
	      gtag('config', '<?php echo htmlspecialchars($epc_ga_id, ENT_QUOTES, 'UTF-8'); ?>');
	    };
	    document.head.appendChild(s);
	  }, { once: true });
	</script>
	<?php
	// Microsoft Clarity — per-host project (epartscart: xoflbamawu)
	if (function_exists('epc_portal_clarity_script_html')) {
		echo epc_portal_clarity_script_html();
	}
	?>
	<base href="/templates/nero/"/>


	<meta charset="UTF-8"/>
	<meta name="viewport" content="width=device-width, initial-scale=1"/>
	<meta name="viewport" content="width=device-width, initial-scale=1.0, minimum-scale=1.0, maximum-scale=1.0, user-scalable=no"/>

	<?php
	if (!empty($DP_Content->service_data['epc_canonical_url'])) {
		$epc_canonical_url = $DP_Content->service_data['epc_canonical_url'];
	} else {
		$epc_canonical_path = isset($_SERVER['REQUEST_URI']) ? strtok($_SERVER['REQUEST_URI'], '?') : '/';
		$epc_canonical_url = rtrim($DP_Config->domain_path, '/') . $epc_canonical_path;
	}
	?>
	<link rel="canonical" href="<?php echo htmlspecialchars($epc_canonical_url, ENT_QUOTES, 'UTF-8'); ?>"/>
	<?php if (!$epc_custom_storefront) { ?>
	<link rel="dns-prefetch" href="//image.umapi.ru"/>
	<link rel="dns-prefetch" href="//api.umapi.ru"/>
	<link rel="preconnect" href="https://image.umapi.ru" crossorigin/>
	<?php } else { ?>
	<link rel="preconnect" href="https://fonts.googleapis.com"/>
	<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
	<?php } ?>
	<link rel="dns-prefetch" href="//flagcdn.com"/>
	<link rel="preconnect" href="https://www.googletagmanager.com" crossorigin/>

	<?php
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_worldclass.php';
	if ($DP_Content->main_flag) {
		echo epc_storefront_json_ld_organization();
		echo epc_storefront_json_ld_website();
	}
	?>

	<style><?php echo epc_portal_theme_css(false); ?></style>
	<?php if ($epc_custom_storefront) { ?>
	<style>
	@media(max-width:600px){
		.epc-wc-trust .container>div{gap:16px!important;justify-content:flex-start!important;}
		.epc-wc-newsletter__form{flex-direction:column!important;}
		.epc-wc-newsletter__form button{width:100%;}
		#epc_wc_cookie .container{flex-direction:column!important;}
	}
	</style>
	<?php } ?>
	<?php
	if ($epc_er_retail) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronics_retail_helpers.php';
		if (is_object($DP_Content)) {
			epc_electronics_retail_apply_seo($DP_Content);
		}
		echo '<link rel="stylesheet" href="/content/general_pages/epc_electronics_retail.css?v=20260621" />';
		echo '<link rel="stylesheet" href="/content/general_pages/epc_electronics_retail_virgin_hero.css?v=20260621" />';
		echo '<link rel="stylesheet" href="/content/general_pages/epc_electronicae_storefront.css?v=20260621" />';
	}
	if ($epc_cpi_consulting) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_consulting_primeinvest_helpers.php';
		if (is_object($DP_Content) && function_exists('epc_cpi_apply_seo')) {
			epc_cpi_apply_seo($DP_Content);
		}
		echo '<link rel="stylesheet" href="/content/general_pages/epc_consulting_primeinvest.css?v=20260621" />';
		echo '<link rel="stylesheet" href="/content/general_pages/epc_consulting_primeinvest_hero.css?v=20260621" />';
	}
	if ($epc_frn_retail) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_fashion_retail_namshi_helpers.php';
		if (is_object($DP_Content) && function_exists('epc_fashion_retail_namshi_apply_seo')) {
			epc_fashion_retail_namshi_apply_seo($DP_Content);
		}
		echo '<link rel="stylesheet" href="/content/general_pages/epc_fashion_retail_namshi.css?v=20260621" />';
		echo '<link rel="stylesheet" href="/content/general_pages/epc_fashion_retail_namshi_hero.css?v=20260621" />';
	}
	if ($epc_jrk_retail) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_jewellery_retail_kiyasha_helpers.php';
		if (is_object($DP_Content) && function_exists('epc_jewellery_retail_kiyasha_apply_seo')) {
			epc_jewellery_retail_kiyasha_apply_seo($DP_Content);
		}
		echo '<link rel="stylesheet" href="/content/general_pages/epc_jewellery_retail_kiyasha.css?v=20260621" />';
		echo '<link rel="stylesheet" href="/content/general_pages/epc_jewellery_retail_kiyasha_hero.css?v=20260621" />';
	}
	if ($epc_asp_pro) {
		echo '<link rel="stylesheet" href="/content/general_pages/epc_automotive_spareparts.css?v=20260621" />';
	}
	if ($epc_custom_storefront) {
		echo '<link rel="stylesheet" href="/content/general_pages/epc_storefront_animations.css?v=20260621" />';
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_storefront_logo.php';
	epc_portal_storefront_hub_logo_enqueue();
	?>

	<?php
	$_epcFaviconFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_favicon.php';
	if (is_file($_epcFaviconFile)) {
		require_once $_epcFaviconFile;
		echo epc_portal_favicon_link_tags($epc_portal_industry_code);
	} else {
	?>
    <link rel="icon" type="image/svg+xml" href="/favicon.svg?v=20260517"/>
    <link rel="alternate icon" href="/favicon.ico?v=20260517"/>
    <link rel="shortcut icon" href="/favicon.ico?v=20260517"/>
	<?php } ?>

	<?php
	$epc_pwa_enabled = function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname();
	if ($epc_pwa_enabled) {
	?>
	<link rel="manifest" href="/manifest.webmanifest"/>
	<meta name="theme-color" content="#dc2626"/>
	<meta name="mobile-web-app-capable" content="yes"/>
	<meta name="apple-mobile-web-app-capable" content="yes"/>
	<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent"/>
	<meta name="apple-mobile-web-app-title" content="eParts Cart"/>
	<link rel="apple-touch-icon" href="/icons/pwa-icon-192.svg"/>
	<script>
	(function () {
	  if ('serviceWorker' in navigator) {
	    window.addEventListener('load', function () {
	      navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(function () {});
	    }, { once: true });
	  }
	  if (!window.Capacitor) {
	    return;
	  }
	  document.addEventListener('click', function (ev) {
	    var el = ev.target;
	    while (el && el.tagName !== 'A') {
	      el = el.parentElement;
	    }
	    if (!el || !el.href) {
	      return;
	    }
	    try {
	      var u = new URL(el.href, location.href);
	      var host = (u.hostname || '').replace(/^www\./, '');
	      if (host === 'epartscart.com' || host.slice(-14) === '.epartscart.com') {
	        return;
	      }
	      if (u.protocol !== 'http:' && u.protocol !== 'https:') {
	        return;
	      }
	      ev.preventDefault();
	      var browser = window.Capacitor.Plugins && window.Capacitor.Plugins.Browser;
	      if (browser && typeof browser.open === 'function') {
	        browser.open({ url: u.href });
	      } else {
	        window.open(u.href, '_blank', 'noopener');
	      }
	    } catch (e) {}
	  }, true);
	})();
	</script>
	<?php } ?>

	
    <!-- CSS -->
	<link href="assets/css/style_all.css?v=<?=(int)$DP_Template->data_value->version;?>" rel="stylesheet" type="text/css" title="default"/>
	<style id="epc-fast-paint">
	/* Override style_all preloader lock — never trap scroll behind a stuck white mask. */
	html, body { overflow-x: hidden !important; overflow-y: auto !important; }
	#preloader { display: none !important; visibility: hidden !important; pointer-events: none !important; }
	</style>
	
	<link href="css/catalogue/catalogue.css" rel="stylesheet" type="text/css"/>
	<link href="/modules/slider/css/style.css" rel="stylesheet" type="text/css"/>
	
	<link href="css/astself.css" rel="stylesheet" type="text/css"/>

	<?php
	// Guests always get a session cookie from auth plugin — do NOT treat that as "logged in".
	// Full vendors.js only when a real user id cookie is present, or on non-home pages that need UI widgets.
	$epc_has_logged_user = !empty($_COOKIE['u_id']) && (int) $_COOKIE['u_id'] > 0;
	$epc_use_full_vendors = (!$DP_Content->main_flag) || $epc_has_logged_user;
	if ($epc_use_full_vendors) {
	?>
	<!-- JS -->
	<script src="assets/js/vendors.js"></script>
	<?php
	} else {
	?>
	<script src="assets/js/vendors_main.js"></script>
	<?php
	}
	?>

	<docpart type="head" name="head" />
	<?php
	if ($DP_Content->main_flag && isset($epc_portal_site['trade_name']) && $epc_portal_site['trade_name'] !== '') {
		$_epcPageTitle = htmlspecialchars($epc_portal_site['trade_name'], ENT_QUOTES, 'UTF-8');
		$_epcTagline = isset($epc_portal_site['tagline']) ? htmlspecialchars($epc_portal_site['tagline'], ENT_QUOTES, 'UTF-8') : '';
		if ($_epcTagline !== '') {
			$_epcPageTitle .= ' — ' . $_epcTagline;
		}
		echo '<title>' . $_epcPageTitle . '</title>' . "\n";
	}
	?>
	<?php
	if( ! $DP_Content->main_flag ){
	?>
	<link rel="stylesheet" href="css/docpart/style.css" type="text/css" />
	<script src="/lib/jQuery_ui/jquery-ui.js" defer></script>
	<link href="/lib/jQuery_ui/jquery-ui.css" rel="stylesheet">
	<?php
	}
	?>
	<style>
	.epc-currency-switcher{margin-top:7px;}
	.epc-currency-switcher select{background:#fff;border:1px solid #dbe4ee;border-radius:999px;color:#0f172a;font-size:12px;font-weight:900;height:32px;padding:0 10px;}
	.epc-currency-note{color:#64748b;font-size:10px;font-weight:700;margin-top:2px;}
	.epc-currency-base-note{color:#64748b;display:block;font-size:10px;font-weight:700;line-height:1.2;margin-top:2px;white-space:nowrap;}
	.epc-mobile-currency-select{background:#fff;border:0;border-radius:999px;color:#0f172a;display:inline-block;font-size:12px;font-weight:900;height:30px;margin:8px 6px 0 0;padding:0 8px;vertical-align:middle;}
	</style>
	<script>
	window.epcCurrencyConfig = <?php echo json_encode($epc_currency_js_config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	if (window.epcCurrencyConfig && window.epcCurrencyConfig.base === '643' && window.epcCurrencyConfig.countryMap && window.epcCurrencyConfig.countryMap.AE) {
		window.epcCurrencyConfig.base = window.epcCurrencyConfig.countryMap.AE;
		window.epcCurrencyConfig.selected = window.epcCurrencyConfig.base;
	}
	function epcSetCookie(name, value, days) {
		var maxAge = days ? '; max-age=' + (days * 86400) : '';
		document.cookie = name + '=' + encodeURIComponent(value) + maxAge + '; path=/; SameSite=Lax';
	}
	function epcGetCurrencyRecord(iso) {
		var cfg = window.epcCurrencyConfig || {};
		return cfg.currencies && cfg.currencies[iso] ? cfg.currencies[iso] : (cfg.currencies ? cfg.currencies[cfg.base] : null);
	}
	function epcSetDisplayCurrency(iso, manual) {
		var cfg = window.epcCurrencyConfig || {};
		if(!cfg.currencies || !cfg.currencies[iso]) { return; }
		try { localStorage.setItem('epc_currency', iso); if(manual){ localStorage.setItem('epc_currency_manual', '1'); } } catch(e) {}
		epcSetCookie('epc_currency', iso, 365);
		cfg.selected = iso;
		window.epcCurrencyConfig = cfg;
	}
	function epcCurrencyIndicator(rec) {
		var cfg = window.epcCurrencyConfig || {};
		if(!rec) { return ''; }
		if(cfg.mode === 'no') { return ''; }
		return (cfg.mode === 'short_name_after') ? rec.caption_short : rec.sign;
	}
	function epcFormatMoney(amount) {
		var cfg = window.epcCurrencyConfig || {};
		var rec = epcGetCurrencyRecord(cfg.selected);
		if(!rec) { return Number(amount || 0).toFixed(2); }
		var rate = Number(rec.rate || 1);
		if(rate <= 0) { rate = 1; }
		var value = Number(amount || 0) / rate;
		var number = value.toLocaleString(undefined, {minimumFractionDigits: 2, maximumFractionDigits: 2});
		var indicator = epcCurrencyIndicator(rec);
		if(cfg.mode === 'no' || !indicator) { return number; }
		if(cfg.mode === 'sign_after' || cfg.mode === 'short_name_after') { return number + ' ' + indicator; }
		return indicator + ' ' + number;
	}
	/**
	 * Re-format visible money only. Do NOT reload — a reload re-runs part search and
	 * can change which rows are painted (filters/race), while prices must stay the same set.
	 */
	function epcRefreshStorefrontMoneyUI() {
		var cfg = window.epcCurrencyConfig || {};
		var rec = epcGetCurrencyRecord(cfg.selected);
		if(!rec) { return; }
		var indicator = epcCurrencyIndicator(rec);
		if(typeof currency_indicator !== 'undefined') { currency_indicator = indicator; }
		if(typeof currency_sign !== 'undefined') { currency_sign = rec.sign; }
		var select = document.getElementById('epc_currency_select');
		if(select && select.value !== cfg.selected) { select.value = cfg.selected; }
		var mobileSelect = document.getElementById('epc_currency_select_mobile');
		if(mobileSelect && mobileSelect.value !== cfg.selected) { mobileSelect.value = cfg.selected; }
		var inds = document.querySelectorAll('.balance_indicator');
		for(var i = 0; i < inds.length; i++) { inds[i].textContent = indicator; }
		var balanceNodes = document.querySelectorAll('[data-epc-base-balance]');
		for(var b = 0; b < balanceNodes.length; b++) {
			var bal = parseFloat(balanceNodes[b].getAttribute('data-epc-base-balance'));
			if(isNaN(bal)) { continue; }
			var rate = Number(rec.rate || 1);
			if(rate <= 0) { rate = 1; }
			balanceNodes[b].textContent = (bal / rate).toLocaleString(undefined, {minimumFractionDigits: 2, maximumFractionDigits: 2});
		}
		if(typeof headlines !== 'undefined' && headlines && headlines.price && headlines.price.caption) {
			var cap = String(headlines.price.caption);
			var sortHtml = '';
			var imgMatch = cap.match(/(\s*<img[\s\S]*)/i);
			if(imgMatch) { sortHtml = imgMatch[1]; }
			var mainPart = cap.replace(/\s*<img[\s\S]*/i, '').replace(/,\s*[^,<]+$/i, '');
			if(!mainPart) { mainPart = 'Price'; }
			headlines.price.caption = mainPart + ', ' + indicator + sortHtml;
		}
		var priceNodes = document.querySelectorAll('[data-epc-base-price]');
		if(priceNodes.length) {
			for(var p = 0; p < priceNodes.length; p++) {
				var base = parseFloat(priceNodes[p].getAttribute('data-epc-base-price'));
				if(isNaN(base)) { continue; }
				priceNodes[p].textContent = epcFormatMoney(base);
			}
		} else {
			// Fallback only when price nodes are not annotated — never preferred (avoids re-filter races).
			try {
				if(typeof manufacturersReview === 'function' && typeof epc_brand_picker_mode !== 'undefined' && epc_brand_picker_mode) {
					manufacturersReview();
				} else if(typeof resultReview === 'function' && typeof Products !== 'undefined' && Products && Products.All && Products.All.length) {
					resultReview();
				}
			} catch (reviewErr) {}
		}
		// Keep price column header label in sync when table already rendered.
		var thPrices = document.querySelectorAll('#all_table_products th.th_price');
		for(var th = 0; th < thPrices.length; th++) {
			var thNode = thPrices[th];
			var thImg = thNode.querySelector('img');
			var thLabel = (thNode.textContent || '').replace(/\s+/g, ' ').trim().replace(/,\s*[^,]+$/i, '');
			if(!thLabel) { thLabel = 'Price'; }
			thNode.textContent = '';
			thNode.appendChild(document.createTextNode(thLabel + ', ' + indicator + ' '));
			if(thImg) { thNode.appendChild(thImg); }
		}
	}
	function epcApplyDisplayCurrency(iso, manual) {
		epcSetDisplayCurrency(iso, !!manual);
		epcRefreshStorefrontMoneyUI();
	}
	function epcDetectCurrencyByCountry() {
		var cfg = window.epcCurrencyConfig || {};
		try { if(localStorage.getItem('epc_currency_manual') === '1') { return; } } catch(e) {}
		if(!cfg.countryMap || !window.fetch) { return; }
		// Skip if country already known — avoids ipapi.co on every navigation.
		try {
			var known = (document.cookie.match(/(?:^|; )epc_country=([^;]*)/) || [])[1] || '';
			if (known) { return; }
		} catch (e2) {}
		fetch('https://ipapi.co/json/').then(function(r){ return r.json(); }).then(function(data) {
			var country = data && data.country_code ? String(data.country_code).toUpperCase() : '';
			var iso = country && cfg.countryMap[country] ? cfg.countryMap[country] : '';
			if(country) { epcSetCookie('epc_country', country, 30); }
			if(iso && cfg.currencies && cfg.currencies[iso] && iso !== cfg.selected) {
				epcApplyDisplayCurrency(iso, false);
			}
		}).catch(function(){});
	}
	document.addEventListener('DOMContentLoaded', function(){
		var select = document.getElementById('epc_currency_select');
		if(select) {
			select.value = (window.epcCurrencyConfig || {}).selected || select.value;
			select.onchange = function(){ epcApplyDisplayCurrency(this.value, true); };
		}
		var mobileSelect = document.getElementById('epc_currency_select_mobile');
		if(mobileSelect) {
			mobileSelect.value = (window.epcCurrencyConfig || {}).selected || mobileSelect.value;
			mobileSelect.onchange = function(){ epcApplyDisplayCurrency(this.value, true); };
		}
		// Defer geo currency probe until after first paint / idle.
		var runGeo = function(){ try { epcDetectCurrencyByCountry(); } catch (e) {} };
		if (window.requestIdleCallback) { requestIdleCallback(runGeo, { timeout: 4000 }); }
		else { window.addEventListener('load', function(){ setTimeout(runGeo, 1500); }, { once: true }); }
	});
	</script>
	
</head>
<body>
<?php
$_epc_ff_banner = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_failover_banner.php';
if (is_readable($_epc_ff_banner)) {
	require_once $_epc_ff_banner;
	if (function_exists('epc_failover_emit_page_chrome')) {
		epc_failover_emit_page_chrome();
	}
}
?>

<?php require($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/google_translate_top.php"); ?>
<?php require($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/site_professional_shell.php"); ?>


<?php
// Full-screen preloader + body{overflow:hidden} blocks the whole site if jQuery/app.js is slow.
// Auth plugin always sets a guest session cookie — never gate UX on that alone.
// Skip the preloader entirely for a snappy first paint; keep a no-op unlock for legacy CSS.
?>
<script>
(function () {
	function epcUnlockStorefrontScroll() {
		try {
			document.documentElement.style.overflow = '';
			document.body.style.overflowX = 'hidden';
			document.body.style.overflowY = 'auto';
		} catch (e) {}
		var pre = document.getElementById('preloader');
		if (pre) {
			pre.style.display = 'none';
			pre.style.visibility = 'hidden';
			pre.style.opacity = '0';
			pre.style.pointerEvents = 'none';
		}
	}
	epcUnlockStorefrontScroll();
	document.addEventListener('DOMContentLoaded', epcUnlockStorefrontScroll);
	window.addEventListener('load', epcUnlockStorefrontScroll);
	setTimeout(epcUnlockStorefrontScroll, 0);
	setTimeout(epcUnlockStorefrontScroll, 400);
})();
</script>


<div class="container">
<?php
require_once($_SERVER["DOCUMENT_ROOT"]."/content/general/actions_alert.php");//Вывод сообщений о результатах выполнения действий
?>
</div>



<?php
if(!empty($DP_Template->data_value->message_header)){
?>
<div class="alert alert-info" style="background-color: #f5f5f5; border: solid 1px #ddd; margin: 0; border-left: 0; border-right: 0; border-top: 0;">
	<div class="container">
		<h4><strong><i class="fa fa-bullhorn"></i> <?php echo translate_str_by_id(4811); ?></strong></h4>
		<div><?=$DP_Template->data_value->message_header;?></div>
	</div>
</div>
<?php
}
?>



<?php if ($epc_er_retail) {
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_electronics_retail_header.php';
} elseif ($epc_cpi_consulting) {
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_consulting_primeinvest_header.php';
} elseif ($epc_frn_retail) {
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_fashion_retail_namshi_header.php';
} elseif ($epc_jrk_retail) {
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_jewellery_retail_kiyasha_header.php';
} else { ?>
<header class="hidden-xs">
<?php if ($epc_platform_marketing) { ?>
	<div class="top-menu-line" style="background:#082f49;">
		<div class="container">
			<nav class="navbar navbar-default navbar-header-full navbar-static-top" role="navigation" style="margin-bottom:0;background:transparent;border:0;">
				<div class="navbar-header">
					<a class="navbar-brand epm-brand epm-brand--nero" href="/" style="color:#fff;font-weight:700;display:inline-flex;align-items:center;gap:10px;">
						<?php
						require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
						epc_ecomae_hub_logo_enqueue();
						echo epc_ecomae_hub_logo('header', array('show_title' => false, 'show_tagline' => false, 'aria_label' => 'ECOM AE platform'));
						?>
						<span>ECOM <span style="color:#38bdf8;">AE</span></span>
					</a>
				</div>
				<ul class="nav navbar-nav navbar-right">
					<?php if (!empty($epc_platform_nav)) {
						foreach ($epc_platform_nav as $navItem) { ?>
					<li><a href="<?php echo htmlspecialchars($navItem['href'], ENT_QUOTES, 'UTF-8'); ?>" style="color:#e2e8f0;"><?php echo htmlspecialchars($navItem['label'], ENT_QUOTES, 'UTF-8'); ?></a></li>
					<?php }
					} else { ?>
					<li><a href="/" style="color:#e2e8f0;">Platform</a></li>
					<li><a href="https://www.ecomae.com/cp/" style="color:#e2e8f0;">Operator login</a></li>
					<li><a href="/platform/contact" style="color:#38bdf8;">Contact</a></li>
					<?php } ?>
					<li><a href="https://www.ecomae.com/cp/" style="color:#38bdf8;font-weight:700;"><i class="fa fa-th-large"></i> Super CP</a></li>
				</ul>
			</nav>
		</div>
	</div>
<?php } else { ?>
    <div class="top-menu-line" style="background: <?=$DP_Template->data_value->top_menu_color;?>;">
		<style>
		/* Hide clutter from top menu (Cart / Orders / Balance / Information / Contacts) */
		.top-menu-line .top-menu-ul > li:has(> a[href*="/shop/cart"]),
		.top-menu-line .top-menu-ul > li:has(> a[href*="/shop/orders"]),
		.top-menu-line .top-menu-ul > li:has(> a[href*="/shop/balans"]),
		.top-menu-line .top-menu-ul > li:has(> a[href*="/kontakty"]),
		.top-menu-line .top-menu-ul > li:has(> a[href*="/contacts"]),
		.top-menu-line .top-menu-ul > li.have_child:has(a[href*="/payment"]),
		.top-menu-line .top-menu-ul > li.have_child:has(a[href*="/delivery"]){display:none!important}
		</style>
		<div class="container">
			<table>
				<tr>
					<td>
						<nav class="navbar navbar-default navbar-header-full yamm navbar-static-top" role="navigation">
							<docpart type="module" name="top_menu" />
						</nav>
					</td>
					<td>
						<?php
						//Баланс покупателя
						if(DP_User::getUserId() > 0)
						{
						?>
							<!-- Модуль индикации непросмотренных сообщений -->
							<div id="not_viewed_msg" class="new-header-user-box" style="display:none;">
								<a href="/shop/orders?read=0">
									<i class="fa fa-envelope"></i>
									<small><span id="not_viewed_msg_count"></span></small>
								</a>
								<script>
									//Функция обновления информации о количестве непрочитанных сообщений
									function update_cnt_not_viewed_msg()
									{
										jQuery.ajax({
											type: "POST",
											async: true,
											url: "/content/shop/order_process/ajax_get_cnt_not_viewed_msg.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
											dataType: "json",//Тип возвращаемого значения
											success: function(answer)
											{
												if(answer.status == 1)
												{
													if(answer.count > 0){
														document.getElementById("not_viewed_msg_count").innerHTML = answer.count;
														document.getElementById("not_viewed_msg").style.display = 'inline-block';
													}else{
														document.getElementById("not_viewed_msg").style.display = 'none';
													}
												}else{
													document.getElementById("not_viewed_msg").style.display = 'none';
												}
											}
										});
									}
									update_cnt_not_viewed_msg();//Запрос при загрузке страницы
									//Запускаем запросы непросмотренных сообщений
									var timerId_cnt_not_viewed_msg = setInterval(function() {
										update_cnt_not_viewed_msg();
									}, 400000);
								</script>
							</div>
							
							<!-- Модуль индикации непросмотренных сообщений по VIN запросам -->
							<div id="not_viewed_msg_vin" class="new-header-user-box" style="display:none;">
								<a href="/requests">
									<i class="fa fa-rocket"></i>
									<small><span id="header_vin_count"></span></small>
								</a>
							</div>
							<script>
								//Функция обновления информации по корзине
								function update_cnt_not_viewed_msg_vin()
								{
									jQuery.ajax({
										type: "POST",
										async: true,
										url: "/content/requests/ajax_get_vin_info.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
										dataType: "json",
										success: function(answer){
											if(answer.count > 0){
												document.getElementById("header_vin_count").innerHTML = answer.count;
												document.getElementById("not_viewed_msg_vin").style.display = 'inline-block';
											}else{
												document.getElementById("not_viewed_msg_vin").style.display = 'none';
											}
										}
									});
								}

								update_cnt_not_viewed_msg_vin();//Запрос при загрузке страницы
								//Запускаем запросы непросмотренных сообщений
								var timerId_cnt_not_viewed_msg_vin = setInterval(function() {
									update_cnt_not_viewed_msg_vin();
								}, 400000);
							</script>
							
							<!-- Модуль индикации непросмотренных сообщений по возвратам -->
							<div id="not_viewed_msg_returns" class="new-header-user-box" style="display:none;">
								<a href="<?php echo $multilang_params['lang_href']; ?>/shop/returns/returns_list?read=0">
									<i class="fa fa-reply"></i>
									<small><span id="not_viewed_msg_count_returns"></span></small>
								</a>
								<script>
									//Функция обновления информации о количестве непрочитанных сообщений
									function update_cnt_not_viewed_msg_returns()
									{
										jQuery.ajax({
											type: "POST",
											async: true,
											url: "/content/shop/order_process/ajax_get_cnt_not_viewed_msg.php?returns=1&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
											dataType: "json",//Тип возвращаемого значения
											success: function(answer)
											{
												if(answer.status == 1)
												{
													if(answer.count > 0){
														document.getElementById("not_viewed_msg_count_returns").innerHTML = answer.count;
														document.getElementById("not_viewed_msg_returns").style.display = 'inline-block';
													}else{
														document.getElementById("not_viewed_msg_returns").style.display = 'none';
													}
												}else{
													document.getElementById("not_viewed_msg_returns").style.display = 'none';
												}
											}
										});
									}
									update_cnt_not_viewed_msg_returns();//Запрос при загрузке страницы
									//Запускаем запросы непросмотренных сообщений
									var timerId_cnt_not_viewed_msg_returns = setInterval(function() {
										update_cnt_not_viewed_msg_returns();
									}, 400000);
								</script>
							</div>
							
							<div class="new-header-user-box">
								<a href="<?php echo $multilang_params['lang_href']; ?>/shop/balans" class="user_balance">
									<i><span class="balance_indicator"><?=$currency_indicator;?></span></i>
									<?php
									$stmt = $db_link->prepare('SELECT *,( IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = :user_id AND `income`=1 AND `active` = 1), 0) - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = :user_id AND `income`=0 AND `active` = 1),0) ) AS `balance` FROM `shop_users_accounting` WHERE `user_id` = :user_id AND `active` = 1;');
									$stmt->bindValue(':user_id', DP_User::getUserId());
									$stmt->execute();
									$balance_record = $stmt->fetch(PDO::FETCH_ASSOC);
									$balance = ($balance_record !== false) ? (float) $balance_record['balance'] : 0.0;
									$balance_display = epc_currency_format_amount($balance, $epc_currency_records, $epc_selected_currency_iso, 'no');
									?>
									<span class="balance_text" data-epc-base-balance="<?php echo htmlspecialchars(number_format($balance, 2, '.', ''), ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($balance_display, ENT_QUOTES, 'UTF-8'); ?></span>
								</a>
							</div>
							
						<?php
						}
						?>
						
						<?php require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/workshop/epc_garage_header_link.php'; ?>
						<?php require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_header_link.php'; ?>
						
						<?php
						if ((int) DP_User::getUserId() > 0) {
							$userProfile = DP_User::getUserProfile();
							$user_tab_caption = trim($userProfile['name'] . ' ' . $userProfile['surname']);
							if ($user_tab_caption === '') {
								$user_tab_caption = translate_str_by_id(3452);
							}
						?>
						<div class="new-header-user-box dropdown" id="loginDropdown">
							<span class="dropdown-toggle" data-toggle="dropdown">
								<a><i class="fa fa-user" aria-hidden="true"></i> <?php echo htmlspecialchars($user_tab_caption, ENT_QUOTES, 'UTF-8'); ?></a>
							</span>
							<div class="dropdown-menu dropdown-menu-right dropdown-login-box animated flipCenter" id="onDropDownProc">
								<?php
								$login_form_postfix = 'header_top_tab';
								require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
								?>
							</div>
						</div>
						<script>
							$('#loginDropdown').on('hide.bs.dropdown', function (event) {
								if (document.querySelector('#onDropDownProc').contains(window.event.target)) {
									event.stopImmediatePropagation();
									return false;
								}
							});
						</script>
						<?php
						} else {
							require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_auth_links.php';
						?>
						<div class="new-header-user-box">
							<?php epc_storefront_auth_links_render($multilang_params); ?>
							<?php echo epc_storefront_auth_links_styles(); ?>
						</div>
						<?php
						}
						?>
						
						<div class="new-header-user-box">
							<?php
							//Модуль выбора языка
							require( $_SERVER['DOCUMENT_ROOT'].'/modules/lang/module.php' );
							?>
						</div>
						
					</td>
				</tr>
			</table>
		</div>
	</div>
	

	
	<div class="logo-line" style="background: <?=$DP_Template->data_value->header_color;?>;">
		<div class="container">
			<div class="table-group">
				
				<div class="table-control">
					<a class="header-logo" href="<?php echo $DP_Config->domain_path; ?>">
						<?php require($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/animated_epartscart_logo.php"); ?>
					</a>
				</div>
				
				<div class="table-control epc-header-contact-col">
					<div class="epc-header-geo-phone">
						<div class="geo-point-box text-left">
							<?php
							$geo_point_class = 'hidden';
							$query_geo = $db_link->prepare("SELECT `activated` FROM `modules` WHERE `id` = ?;");
							$query_geo->execute( array(38) );
							$module_geo = $query_geo->fetch();
							if($module_geo['activated'] == 1)
							{
								$geo_point_class = '';
							}
							?>
							<div class="<?=$geo_point_class;?>">
								<table>
									<tr>
										<td><i class="fa fa-location-arrow" aria-hidden="true"></i></td>
										<td><span><docpart type="module" name="geo_point" /></span></td>
									</tr>
								</table>
							</div>
							<?php
							if($module_geo['activated'] == 0)
							{
							?>
								<table>
									<tr>
										<td><i class="fa fa-location-arrow" aria-hidden="true"></i></td>
										<td><span><?=trim(translate_str_by_id($customer_office_info['city'])).'<br/>'.trim(translate_str_by_id($customer_office_info['address']));?></span></td>
									</tr>
								</table>
							<?php
							}
							?>
						</div>
						<div class="header-phone-box text-left">
							<a href="tel:<?=htmlspecialchars($epc_contact_phone_href, ENT_QUOTES, 'UTF-8');?>" class="phone call-me"><?=htmlspecialchars($epc_contact_phone, ENT_QUOTES, 'UTF-8');?></a>
						</div>
					</div>
					<div class="timetable-box text-left">
					<table>
						<tr>
							<td><i class="fa fa-clock-o" aria-hidden="true"></i></td>
							<td><span><?=trim(html_entity_decode(translate_str_by_id($customer_office_info['timetable'])));?></span></td>
						</tr>
					</table>
					</div>
				</div>
				
				<div class="table-control epc-header-right-col">
					<div class="epc-header-actions-row">
						<div class="header-call-box"><a href="<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu"><?php echo translate_str_by_id(4817); ?></a></div>
						<?php if ($epc_whatsapp_href_number !== '') { ?>
						<div class="header-whatsapp-box">
							<a href="https://wa.me/<?= htmlspecialchars($epc_whatsapp_href_number, ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener noreferrer" title="Chat on WhatsApp">
								<i class="fa fa-whatsapp" aria-hidden="true"></i> WhatsApp contact
							</a>
						</div>
						<?php } ?>
						<?php if ($epc_commerce_storefront) { ?>
						<div class="header-bulk-upload-box"><a href="<?php echo $multilang_params['lang_href']; ?>/shop/bulk-upload"><i class="fa fa-file-excel-o"></i> Excel bulk upload</a></div>
						<?php } ?>
						<div class="epc-currency-switcher">
							<?php if ($epc_currency_locked_for_user) { ?>
							<div class="epc-currency-locked" title="Dealing currency assigned by manager">
								<strong><?php echo htmlspecialchars($currency_record['caption_short'], ENT_QUOTES, 'UTF-8'); ?></strong>
							</div>
							<div class="epc-currency-note">Your dealing currency</div>
							<?php } else { ?>
							<select id="epc_currency_select" aria-label="Display currency">
								<?php foreach($epc_currency_records as $epc_currency_iso => $epc_currency_row) { ?>
								<option value="<?php echo htmlspecialchars($epc_currency_iso, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($epc_currency_row['caption_short'], ENT_QUOTES, 'UTF-8'); ?></option>
								<?php } ?>
							</select>
							<div class="epc-currency-note">Display currency</div>
							<?php } ?>
						</div>
					</div>
				</div>
				
			</div>
		</div>
	</div>
	


	<?php
	$epc_header_vehicle_catalog_url = rtrim($multilang_params['lang_href'], '/') . '/vehicle-catalog';
	// Top VIN search must use Laximo OEM catalog (same as homepage VIN tab).
	$epc_header_laximo_url = rtrim($multilang_params['lang_href'], '/') . '/katalog-laximo';
	$header_search_form_1_hidden = '';
	$header_search_form_2_hidden = 'hidden';
	$header_search_form_3_hidden = 'hidden';
	$header_search_form_engine_hidden = 'hidden';
	$header_search_form_car_hidden = 'hidden';
	$header_search_form_attr_hidden = 'hidden';
	$epc_header_search_active = '1';
	$epc_header_warehouse_search_url = rtrim($multilang_params['lang_href'], '/') . '/shop/warehouse-search';
	$epc_header_attr_field = isset($_GET['field']) ? trim((string) $_GET['field']) : 'all';
	$epc_header_attr_q = isset($_GET['q']) ? trim((string) $_GET['q']) : '';
	if (!is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_price_extra_fields.php')) {
		$epc_header_attr_options = array(array('key' => 'all', 'label' => 'All fields'));
	} else {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_price_extra_fields.php';
		$epc_header_attr_options = function_exists('epc_price_extra_search_options')
			? epc_price_extra_search_options()
			: array(array('key' => 'all', 'label' => 'All fields'));
	}
	if( $DP_Content->content_type == "category" || $DP_Content->url =="shop/search" )
	{
		$header_search_form_1_hidden = 'hidden';
		$header_search_form_2_hidden = '';
		$epc_header_search_active = '2';
	}
	if (isset($DP_Content->url) && (string) $DP_Content->url === 'shop/warehouse-search') {
		$header_search_form_1_hidden = 'hidden';
		$header_search_form_2_hidden = 'hidden';
		$header_search_form_attr_hidden = '';
		$epc_header_search_active = 'attr';
	}
	?>
	<?php if ($epc_commerce_storefront) { ?>
	<div class="schearch-line">
		<div class="container">
			<div class="row">
				<div class="col-sm-7 col-md-8">
					<table>
						<tr>
							<td>
								<a class="header-home-btn" href="<?php echo htmlspecialchars(rtrim($multilang_params['lang_href'], '/') . '/', ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-home" aria-hidden="true"></i></a>
								
								<?php
								$stmt = $db_link->prepare('SELECT COUNT(`id`) AS `count_id` FROM `shop_catalogue_categories` WHERE `published_flag` = ? AND `parent` = ?;');
								$stmt->execute(array(1,0));
								$check_categories_exist_record = $stmt->fetch(PDO::FETCH_ASSOC);
								if( $check_categories_exist_record["count_id"] > 0 )
								{
								?>
								<a class="header-cat-btn" onClick="showCatalogMenu();"><i class="fa fa-bars" aria-hidden="true"></i> <?php echo translate_str_by_id(4201); ?> <span class="hidden-sm"><?php echo translate_str_by_id(4769); ?></span></a>
								<?php
								}
								?>
							</td>
							<td class="search-table-td">
								<div class="header-search-box epc-header-search" data-active-mode="<?php echo htmlspecialchars($epc_header_search_active, ENT_QUOTES, 'UTF-8'); ?>">
									<div class="epc-header-search__tabs" role="tablist" aria-label="Search type">
										<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === '1') ? ' active' : ''; ?>" data-search-mode="1" role="tab" aria-selected="<?php echo ($epc_header_search_active === '1') ? 'true' : 'false'; ?>"><i class="fa fa-barcode" aria-hidden="true"></i><span>Part number</span></button>
										<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === '3') ? ' active' : ''; ?>" data-search-mode="3" role="tab" aria-selected="<?php echo ($epc_header_search_active === '3') ? 'true' : 'false'; ?>"><i class="fa fa-id-card" aria-hidden="true"></i><span>VIN</span></button>
										<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === 'engine') ? ' active' : ''; ?>" data-search-mode="engine" role="tab" aria-selected="<?php echo ($epc_header_search_active === 'engine') ? 'true' : 'false'; ?>"><i class="fa fa-cogs" aria-hidden="true"></i><span>Engine</span></button>
										<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === 'car') ? ' active' : ''; ?>" data-search-mode="car" role="tab" aria-selected="<?php echo ($epc_header_search_active === 'car') ? 'true' : 'false'; ?>"><i class="fa fa-car" aria-hidden="true"></i><span>By car</span></button>
										<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === 'attr') ? ' active' : ''; ?>" data-search-mode="attr" role="tab" aria-selected="<?php echo ($epc_header_search_active === 'attr') ? 'true' : 'false'; ?>"><i class="fa fa-sliders" aria-hidden="true"></i><span>More info</span></button>
										<?php if( $epc_header_search_active === '2' ) { ?>
										<button type="button" class="epc-header-search__tab active" data-search-mode="2" role="tab" aria-selected="true"><i class="fa fa-tag" aria-hidden="true"></i><span>By name</span></button>
										<?php } ?>
									</div>
									<div class="epc-header-search__body">
									<form action="<?php echo $multilang_params['lang_href']; ?>/shop/part_search" method="GET" class="header_search_form_1 <?=$header_search_form_1_hidden;?>">
										<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
										<div class="input-group">
											<input value="<?=$value_for_input_search;?>" type="text" class="form-control" placeholder="Part number or OE code" name="article" autocomplete="off" />
											<span class="input-group-btn">
												<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
											</span>
										</div>
									</form>
									<form action="<?php echo $multilang_params['lang_href']; ?>/shop/search" method="GET" class="header_search_form_2 <?=$header_search_form_2_hidden;?>">
										<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
										<div class="input-group">
											<input value="<?=$value_for_input_search_string;?>" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4796); ?>" name="search_string" autocomplete="off" />
											<span class="input-group-btn">
												<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
											</span>
										</div>
									</form>
									<form action="<?php echo htmlspecialchars($epc_header_laximo_url, ENT_QUOTES, 'UTF-8'); ?>" method="GET" class="header_search_form_3 <?=$header_search_form_3_hidden;?>" onsubmit="return epcHeaderVinSubmit(this);">
										<input type="hidden" name="task" value="vehicles" />
										<input type="hidden" name="ft" value="FindVehicle" />
										<input type="hidden" name="c" value="" />
										<input type="hidden" name="ssd" value="" />
										<div class="input-group">
											<input value="" type="text" class="form-control epc-header-search__vin-input" placeholder="Enter VIN / Frame" name="identString" maxlength="32" autocomplete="off" autocapitalize="characters" spellcheck="false" />
											<span class="input-group-btn">
												<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
											</span>
										</div>
									</form>
									<form action="<?php echo htmlspecialchars($epc_header_vehicle_catalog_url, ENT_QUOTES, 'UTF-8'); ?>" method="GET" class="header_search_form_engine <?=$header_search_form_engine_hidden;?>" onsubmit="return epcHeaderEngineSubmit(this);">
										<div class="input-group">
											<input value="" type="text" class="form-control epc-header-search__engine-input" placeholder="Engine code (e.g. 3L, 12R, 5L)" name="engine" maxlength="12" autocomplete="off" autocapitalize="characters" spellcheck="false" />
											<span class="input-group-btn">
												<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
											</span>
										</div>
									</form>
									<div class="header_search_form_car <?=$header_search_form_car_hidden;?>">
										<a class="epc-header-search__car-link" href="<?php echo htmlspecialchars($epc_header_vehicle_catalog_url, ENT_QUOTES, 'UTF-8'); ?>">
											<span class="epc-header-search__car-copy"><i class="fa fa-list-ol" aria-hidden="true"></i> Choose year, make, model and engine</span>
											<strong>Open vehicle catalog</strong>
										</a>
									</div>
									<form action="<?php echo htmlspecialchars($epc_header_warehouse_search_url, ENT_QUOTES, 'UTF-8'); ?>" method="GET" class="header_search_form_attr <?=$header_search_form_attr_hidden;?>" onsubmit="return epcHeaderAttrSubmit(this);">
										<div class="epc-header-search__attr-row">
											<select class="form-control epc-header-search__attr-field" name="field" aria-label="Search field">
												<?php foreach ($epc_header_attr_options as $epcAttrOpt) { ?>
												<option value="<?php echo htmlspecialchars($epcAttrOpt['key'], ENT_QUOTES, 'UTF-8'); ?>"<?php echo ($epc_header_attr_field === $epcAttrOpt['key']) ? ' selected' : ''; ?>><?php echo htmlspecialchars($epcAttrOpt['label'], ENT_QUOTES, 'UTF-8'); ?></option>
												<?php } ?>
											</select>
											<div class="input-group">
												<input value="<?php echo htmlspecialchars($epc_header_attr_q, ENT_QUOTES, 'UTF-8'); ?>" type="text" class="form-control epc-header-search__attr-input" placeholder="Engine, size, country, cross ref…" name="q" maxlength="120" autocomplete="off" />
												<span class="input-group-btn">
													<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
												</span>
											</div>
										</div>
									</form>
									</div>
								</div>
							</td>
						</tr>
					</table>
				</div>
				
				<div class="col-sm-5 col-md-4">
					<div class="menu-box">
						
						<div class="menu-box-item">
							<a title="<?php echo translate_str_by_id(3583); ?>" href="<?php echo $multilang_params['lang_href']; ?>/shop/orders" class="orders-i">
								<svg class="menu-box-icon svg-icon"><use xlink:href="/templates/nero/img/menu-i.svg#orders-i"></use></svg>
							</a>
						</div>
						
						<div class="menu-box-item">
							<a title="<?php echo translate_str_by_id(4818); ?>" href="<?php echo $multilang_params['lang_href']; ?>/shop/sravneniya">
								<svg class="menu-box-icon svg-icon"><use xlink:href="/templates/nero/img/menu-i.svg#compare-i"></use></svg>
							</a>
						</div>
						
						<div class="menu-box-item">
							<a title="<?php echo translate_str_by_id(4767); ?>" href="<?php echo $multilang_params['lang_href']; ?>/shop/zakladki">
								<svg class="menu-box-icon svg-icon"><use xlink:href="/templates/nero/img/menu-i.svg#bookmarks-i"></use></svg>
							</a>
						</div>
						
						<div class="menu-box-item">
							<a title="<?php echo translate_str_by_id(4669); ?>" href="<?php echo $multilang_params['lang_href']; ?>/garazh">
								<svg class="menu-box-icon svg-icon"><use xlink:href="/templates/nero/img/menu-i.svg#garage-i"></use></svg>
							</a>
						</div>
						
						<div class="menu-box-item">
							<a title="<?php echo translate_str_by_id(4655); ?>" href="<?php echo $multilang_params['lang_href']; ?>/shop/balans">
								<svg class="menu-box-icon svg-icon"><use xlink:href="/templates/nero/img/menu-i.svg#balance-i"></use></svg>
							</a>
						</div>
						
						<div class="menu-box-item">
							<a title="<?php echo translate_str_by_id(4410); ?>" href="<?php echo $multilang_params['lang_href']; ?>/shop/cart">
								<svg class="menu-box-icon svg-icon"><use xlink:href="/templates/nero/img/menu-i.svg#cart-i"></use></svg>
								<span class="" id="header_cart_items_count"></span>
							</a>
						</div>
						
						<div class="menu-box-item">
							<a title="Quotes" href="<?php echo $multilang_params['lang_href']; ?>/shop/quotes">
								<i class="fa fa-file-text-o" style="font-size: 22px; line-height: 40px; color: inherit;"></i>
							</a>
						</div>
						
					</div>
				</div>
			</div>
		</div>
		<div id="dp_menu">
			<div class="container">
				<div class="vertical-tabs-right">
					<?php
					include($_SERVER["DOCUMENT_ROOT"]."/modules/shop/catalogue/dp_menu.php");
					?>
				</div>
			</div>
		</div>
	</div>
	<?php } ?>
<?php } ?>
</header>
<?php } ?>



<!-- header box for mobile -->
<?php if (!$epc_custom_storefront && $epc_platform_marketing) { ?>
<div class="header-box-mobile hidden-sm hidden-md hidden-lg" style="background:#082f49;padding:10px 0;">
	<div class="container">
		<a href="/" style="color:#fff;font-weight:700;font-size:18px;display:inline-flex;align-items:center;gap:8px;">
			<?php
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
			echo epc_ecomae_hub_logo('micro', array('show_title' => false, 'show_tagline' => false, 'aria_label' => 'ECOM AE'));
			?>
			<span>ECOM AE</span>
		</a>
		<div class="pull-right">
			<a href="/platform/demo" style="color:#e2e8f0;margin-left:10px;">Demo</a>
			<a href="https://www.ecomae.com/cp/" style="color:#38bdf8;margin-left:10px;">Super CP</a>
		</div>
	</div>
</div>
<?php } elseif (!$epc_custom_storefront) { ?>
<div class="header-box-mobile hidden-sm hidden-md hidden-lg">
	<nav class="navbar navbar-default navbar-header-full yamm navbar-static-top" role="navigation">
		<div class="container">
			<div class="navbar-header">
				<a id="ar-brand" class="logo_min" href="<?php echo $DP_Config->domain_path; ?>">
					<?php require($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/animated_epartscart_logo.php"); ?>
				</a>
				
				<a class="mobile-box-phone" href="tel:<?=htmlspecialchars($epc_contact_phone_href, ENT_QUOTES, 'UTF-8');?>"><?=htmlspecialchars($epc_contact_phone, ENT_QUOTES, 'UTF-8');?></a>
				<?php if ($epc_whatsapp_href_number !== '') { ?>
				<a class="mobile-box-whatsapp" href="https://wa.me/<?= htmlspecialchars($epc_whatsapp_href_number, ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener noreferrer" title="Chat on WhatsApp"><i class="fa fa-whatsapp" aria-hidden="true"></i> WhatsApp contact</a>
				<?php } ?>
				<a class="mobile-box-bulk-upload" href="<?php echo $multilang_params['lang_href']; ?>/shop/bulk-upload"><i class="fa fa-file-excel-o" aria-hidden="true"></i> Excel upload</a>
				<?php if (!function_exists('epc_portal_storefront_enabled') || epc_portal_storefront_enabled()): ?>
				<a class="mobile-box-erp-login" href="<?php
					$epc_gl = isset($multilang_params['lang_href']) ? rtrim((string)$multilang_params['lang_href'], '/') : '/en';
					echo htmlspecialchars($epc_gl . '/garage/login', ENT_QUOTES, 'UTF-8');
				?>"><i class="fa fa-wrench" aria-hidden="true"></i> Garage Manager</a>
				<a class="mobile-box-erp-login" href="<?php echo function_exists('epc_portal_erp_url') ? epc_portal_erp_url((string) $multilang_params['lang_href']) : ($multilang_params['lang_href'] . '/erp'); ?>"><i class="fa fa-line-chart" aria-hidden="true"></i> <?php echo htmlspecialchars(translate_str_by_key('epc_menu_erp_login') ?: 'ERP Login', ENT_QUOTES, 'UTF-8'); ?></a>
				<?php endif; ?>
				<select id="epc_currency_select_mobile" class="epc-mobile-currency-select" aria-label="Display currency" <?php if ($epc_currency_locked_for_user) { ?>disabled="disabled"<?php } ?>>
					<?php foreach($epc_currency_records as $epc_currency_iso => $epc_currency_row) { ?>
					<option value="<?php echo htmlspecialchars($epc_currency_iso, ENT_QUOTES, 'UTF-8'); ?>" <?php echo ($epc_currency_iso === $epc_selected_currency_iso) ? 'selected="selected"' : ''; ?>><?php echo htmlspecialchars($epc_currency_row['caption_short'], ENT_QUOTES, 'UTF-8'); ?></option>
					<?php } ?>
				</select>
				<?php if ($epc_currency_locked_for_user) { ?><span class="epc-mobile-currency-locked-note">Fixed currency</span><?php } ?>
				
				<?php
				if ((int) DP_User::getUserId() === 0) {
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_auth_links.php';
					echo '<span class="epc-mobile-auth-links">' . epc_storefront_auth_links_html($multilang_params, 'epc-auth-header-links epc-auth-header-links--mobile') . '</span>';
					echo epc_storefront_auth_links_styles();
					echo '<style>.epc-mobile-auth-links{display:inline-block;vertical-align:middle;margin-right:6px;font-size:12px}.epc-auth-header-links--mobile .epc-auth-header-links__sep{margin:0 2px}</style>';
				}
				?>
				<button type="button" class="navbar-toggle header_fa_user_btn" data-toggle="collapse" data-target="#bs-example-navbar-collapse-2"><i class="fa fa-user"></i></button>
				
				<button type="button" class="navbar-toggle" data-toggle="collapse" data-target="#bs-example-navbar-collapse-1"><i class="fa fa-bars"></i></button>
				
				<div class="lang_module">
				<?php
				//Модуль выбора языка
				require( $_SERVER['DOCUMENT_ROOT'].'/modules/lang/module.php' );
				?>
				</div>
				
			</div>
			
			<div class="collapse navbar-collapse" id="bs-example-navbar-collapse-1">
				<?php if ($epc_commerce_storefront) { ?><docpart type="module" name="top_menu_catalog" /><?php } ?>
				<docpart type="module" name="top_menu" />
			</div>
			
			<div class="row">
				<div class="collapse" id="bs-example-navbar-collapse-2">
				<div class="header-user-box">
					<div class="new-header-user-box">
						<?php
						if($module_geo['activated'] == 1)
						{
						?>
						<div class="geo-point-user-box" onclick="openPopupWindow_CityList();">
							<table>
								<tr>
									<td class="geo-td-icon"><i class="fa fa-location-arrow" aria-hidden="true"></i></td>
									<td class="geo-td-text"><span><?=trim($customer_office_info['city']).'<br/>'.trim($customer_office_info['address']);?></span></td>
								</tr>
							</table>
						</div>
						<?php
						}
						//Единый механизм формы авторизации
						$login_form_postfix = "header_top_tab_mob";
						require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
						?>
					</div>
				</div>
				</div>
			</div>
		</div>
	</nav>
	
	<div class="col-xs-12 mobile-search-div">
		<table>
			<tr>
				<td>
					<div class="header-search-box epc-header-search" data-active-mode="<?php echo htmlspecialchars($epc_header_search_active, ENT_QUOTES, 'UTF-8'); ?>">
						<div class="epc-header-search__tabs" role="tablist" aria-label="Search type">
							<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === '1') ? ' active' : ''; ?>" data-search-mode="1" role="tab"><i class="fa fa-barcode" aria-hidden="true"></i><span>Part</span></button>
							<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === '3') ? ' active' : ''; ?>" data-search-mode="3" role="tab"><i class="fa fa-id-card" aria-hidden="true"></i><span>VIN</span></button>
							<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === 'engine') ? ' active' : ''; ?>" data-search-mode="engine" role="tab"><i class="fa fa-cogs" aria-hidden="true"></i><span>Eng</span></button>
							<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === 'car') ? ' active' : ''; ?>" data-search-mode="car" role="tab"><i class="fa fa-car" aria-hidden="true"></i><span>Car</span></button>
							<button type="button" class="epc-header-search__tab<?php echo ($epc_header_search_active === 'attr') ? ' active' : ''; ?>" data-search-mode="attr" role="tab"><i class="fa fa-sliders" aria-hidden="true"></i><span>Info</span></button>
							<?php if( $epc_header_search_active === '2' ) { ?>
							<button type="button" class="epc-header-search__tab active" data-search-mode="2" role="tab"><i class="fa fa-tag" aria-hidden="true"></i><span>Name</span></button>
							<?php } ?>
						</div>
						<div class="epc-header-search__body">
						<form action="<?php echo $multilang_params['lang_href']; ?>/shop/part_search" method="GET" class="header_search_form_1 <?=$header_search_form_1_hidden;?>">
							<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
							<div class="input-group">
								<input value="<?=$value_for_input_search;?>" type="text" class="form-control" placeholder="Part number or OE code" name="article" autocomplete="off" />
								<span class="input-group-btn">
									<button class="btn btn-ar" type="submit"><i class="fa fa-search" aria-hidden="true"></i></button>
								</span>
							</div>
						</form>
						<form action="<?php echo $multilang_params['lang_href']; ?>/shop/search" method="GET" class="header_search_form_2 <?=$header_search_form_2_hidden;?>">
							<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
							<div class="input-group">
								<input value="<?=$value_for_input_search_string;?>" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4796); ?>" name="search_string" autocomplete="off" />
								<span class="input-group-btn">
									<button class="btn btn-ar" type="submit"><i class="fa fa-search" aria-hidden="true"></i></button>
								</span>
							</div>
						</form>
						<form action="<?php echo htmlspecialchars($epc_header_laximo_url, ENT_QUOTES, 'UTF-8'); ?>" method="GET" class="header_search_form_3 <?=$header_search_form_3_hidden;?>" onsubmit="return epcHeaderVinSubmit(this);">
							<input type="hidden" name="task" value="vehicles" />
							<input type="hidden" name="ft" value="FindVehicle" />
							<input type="hidden" name="c" value="" />
							<input type="hidden" name="ssd" value="" />
							<div class="input-group">
								<input value="" type="text" class="form-control epc-header-search__vin-input" placeholder="VIN / Frame" name="identString" maxlength="32" autocomplete="off" autocapitalize="characters" spellcheck="false" />
								<span class="input-group-btn">
									<button class="btn btn-ar" type="submit"><i class="fa fa-search" aria-hidden="true"></i></button>
								</span>
							</div>
						</form>
						<form action="<?php echo htmlspecialchars($epc_header_vehicle_catalog_url, ENT_QUOTES, 'UTF-8'); ?>" method="GET" class="header_search_form_engine <?=$header_search_form_engine_hidden;?>" onsubmit="return epcHeaderEngineSubmit(this);">
							<div class="input-group">
								<input value="" type="text" class="form-control epc-header-search__engine-input" placeholder="Engine code (3L, 12R…)" name="engine" maxlength="12" autocomplete="off" autocapitalize="characters" spellcheck="false" />
								<span class="input-group-btn">
									<button class="btn btn-ar" type="submit"><i class="fa fa-search" aria-hidden="true"></i></button>
								</span>
							</div>
						</form>
						<div class="header_search_form_car <?=$header_search_form_car_hidden;?>">
							<a class="epc-header-search__car-link" href="<?php echo htmlspecialchars($epc_header_vehicle_catalog_url, ENT_QUOTES, 'UTF-8'); ?>">
								<span class="epc-header-search__car-copy"><i class="fa fa-list-ol" aria-hidden="true"></i> Year, make, model</span>
								<strong>Open catalog</strong>
							</a>
						</div>
						<form action="<?php echo htmlspecialchars($epc_header_warehouse_search_url, ENT_QUOTES, 'UTF-8'); ?>" method="GET" class="header_search_form_attr <?=$header_search_form_attr_hidden;?>" onsubmit="return epcHeaderAttrSubmit(this);">
							<div class="epc-header-search__attr-row">
								<select class="form-control epc-header-search__attr-field" name="field" aria-label="Search field">
									<?php foreach ($epc_header_attr_options as $epcAttrOpt) { ?>
									<option value="<?php echo htmlspecialchars($epcAttrOpt['key'], ENT_QUOTES, 'UTF-8'); ?>"<?php echo ($epc_header_attr_field === $epcAttrOpt['key']) ? ' selected' : ''; ?>><?php echo htmlspecialchars($epcAttrOpt['label'], ENT_QUOTES, 'UTF-8'); ?></option>
									<?php } ?>
								</select>
								<div class="input-group">
									<input value="<?php echo htmlspecialchars($epc_header_attr_q, ENT_QUOTES, 'UTF-8'); ?>" type="text" class="form-control epc-header-search__attr-input" placeholder="Engine, size, country…" name="q" maxlength="120" autocomplete="off" />
									<span class="input-group-btn">
										<button class="btn btn-ar" type="submit"><i class="fa fa-search" aria-hidden="true"></i></button>
									</span>
								</div>
							</div>
						</form>
						</div>
					</div>
				</td>
				<td>
					<a href="<?php echo $multilang_params['lang_href']; ?>/shop/cart" class="header-cart-box">
						<i class="fa fa-shopping-cart" aria-hidden="true"></i>
						<span class="" id="header_cart_items_count_mobile"></span>
					</a>
				</td>
			</tr>
		</table>
	</div>
</div>
<?php } ?>
<div class="row"></div>
<!-- end header box for mobile -->



<?php
if ($DP_Content->main_flag && ! $epc_custom_storefront)
{
	if (epc_portal_home_mode() === 'automotive_spareparts_pro') {
		$GLOBALS['epc_home_perf_fast'] = true;
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal_automotive_spareparts_home.php");
	} elseif (epc_portal_home_mode() === 'auto_parts') {
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/home_professional_showcase.php");
	} elseif (epc_portal_home_mode() === 'platform') {
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_ecomae_platform_home.php");
	} else {
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal_industry_home.php");
	}
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_page_builder_render.php");
}
?>

<?php
if ($DP_Content->main_flag && in_array(epc_portal_home_mode(), array('auto_parts', 'automotive_spareparts_pro'), true))
{
	$GLOBALS['epc_home_perf_fast'] = true;
	//Секция "Отправить запрос продавцу"
	if(isset($DP_Config->section_send_request) && $DP_Config->section_send_request)
	{
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/vin_zapros/section_vin_request.php");
	}

	// Front page catalog shortcuts shown before the existing search tabs/content.
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epart_catalog_front_links.php");
}
?>



<div id="sb-site">
<div class="boxed">

<?php
if ($DP_Content->main_flag && $epc_custom_storefront)
{
	if (epc_portal_home_mode() === 'electronics_retail') {
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal_electronics_retail_home.php");
	} elseif (epc_portal_home_mode() === 'consulting_primeinvest') {
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal_consulting_primeinvest_home.php");
	} elseif (epc_portal_home_mode() === 'fashion_retail') {
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal_fashion_retail_namshi_home.php");
	} elseif (epc_portal_home_mode() === 'jewellery_retail') {
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_portal_jewellery_retail_kiyasha_home.php");
	}
}
?>

<?php
$epc_skip_storefront_main_container = $DP_Content->main_flag && $epc_custom_storefront;
if (! $epc_skip_storefront_main_container) {
?>

<?php
if( ! $DP_Content->main_flag)
{
	$epc_platform_subpage = $epc_platform_marketing && !empty($DP_Content->service_data['epc_platform_marketing']);
	if (!$epc_platform_subpage) {
?>
<div class="main-header">
	<div class="container">
		<div class="row">
			<div class="col-sm-12">
				<h1 class="page-title"><?php echo $DP_Content->value; ?></h1>
			</div>
			<div class="col-sm-12">
				<docpart type="module" name="bread_crumbs" />
			</div>
		</div>
	</div>
</div>
<script>
(function(){var bc=document.querySelector('.breadcrumb');if(!bc)return;var links=bc.querySelectorAll('a');for(var i=0;i<links.length;i++){var t=links[i].textContent||'';if(/^[0-9]+_[0-9]+_[0-9a-f]{20,}$/.test(t.trim())){links[i].textContent='Home';}}})();
</script>
<?php
	}
}
?>



<?php
if( ! isset($product_id) )
{
	$product_id = null;
}
// Left catalog column only on shop/catalog contexts (same as default Docpart / demo stores — not on every CMS page).
if( $DP_Content->content_type == "category" || $DP_Content->url =="shop/search" || $DP_Content->id == 324 || $DP_Content->id == 326 || $DP_Content->id == 328 || $DP_Content->id == 330 || $DP_Content->id == 332 || $DP_Content->id == 334 || isset( $DP_Content->service_data["sp"] ))
{
	$left_col_class = " class=\"hidden-xs hidden-sm col-md-3\"";
	$right_col_class = " class=\"col-md-9\"";
	$btn_show_hide_left_coll = " class=\"hidden-md hidden-lg\"";
	if (!empty($epc_er_retail) || !empty($epc_frn_retail) || !empty($epc_cpi_consulting) || !empty($epc_jrk_retail)) {
		$left_col_class = " class=\"hidden-xs hidden-sm hidden-md hidden-lg\"";
		$right_col_class = " class=\"col-md-12\"";
		$btn_show_hide_left_coll = " class=\"hidden-xs hidden-sm hidden-md hidden-lg\"";
	}
}
else
{
	$left_col_class = " class=\"hidden-xs hidden-sm hidden-md hidden-lg\"";
	$right_col_class = " class=\"col-md-12\"";
	$btn_show_hide_left_coll = " class=\"hidden-xs hidden-sm hidden-md hidden-lg\"";
}

// Для некоторых страниц нужно дополнительно добавить обертку
$add_row_div = 'style="margin:0;"';
if( $product_id > 0 || $DP_Content->content_type == "category" || $DP_Content->id == 298 || $DP_Content->id == 302 || $DP_Content->id == 376 || $DP_Content->id == 385 || $DP_Content->id == 385 || isset($DP_Content->service_data["article_search_chpu"]))
{
	$add_row_div = '';
}
?>

<div class="container">
    <div class="row">
		
		<div <?=$btn_show_hide_left_coll;?>>
			<div class="row" style="margin: 0px 0px 15px 0px;">
			<div class="col-xs-12">
				<a onClick="show_hide_left_coll();" style="text-decoration: none; background-color: #f9f9f9; border: 1px solid #ddd; color: #222; position: relative; padding: 5px 10px;"><i class="fa fa-filter" aria-hidden="true"></i> <span><?php echo translate_str_by_id(4812); ?></span></a>
			</div>
			</div>
			<script>
			function show_hide_left_coll(){
				if ( $('#left_col').hasClass('hidden-xs')) {
					$('#left_col').removeClass('hidden-xs');
					$('#left_col').removeClass('hidden-sm');
				}else{
					$('#left_col').addClass('hidden-sm');
					$('#left_col').addClass('hidden-xs');
				}
			}
			</script>
		</div>
		
		<div <?php echo $left_col_class;?> id="left_col"<?php if ($epc_custom_storefront) { ?> style="display:none !important;"<?php } ?>>
			<?php if (!$epc_custom_storefront) { ?>
			<docpart type="module" name="left_menu" />
			<?php } ?>
		</div>
		
		<div <?php echo $right_col_class;?> id="right_col">
            <div class="row mainContainer" id="Container">
				<?php
				//Получаем дополнительный текст для URL
				$text_before_main = "";//Если текст нужен до основного содержимого
				$text_after_main = "";//Если текст нужен после основного содержимого
				$url = getPageUrl();
				
				$stmt = $db_link->prepare('SELECT * FROM `text_for_url` WHERE `url` = :url;');
				$stmt->bindValue(':url', $url);
				$stmt->execute();
				$url_text_record = $stmt->fetch(PDO::FETCH_ASSOC);
				
				if( $url_text_record != false )
				{
					if($url_text_record["before_main"] == 1)
					{
						$text_before_main = $url_text_record["content"];
					}
					else
					{
						$text_after_main = $url_text_record["content"];
					}
				}
				
				// Дополнительный текст страницы
				if($text_before_main != ''){
					echo "<div class=\"col-lg-12\"><br/>".$text_before_main."</div>";
				}
				?>
				
				<?php
				if( ! $DP_Content->main_flag)
				{
				?>
				<div class="row" <?=$add_row_div;?>>
				<div class="col-lg-12">	
				<docpart type="main" name="main" />
				</div>
				</div>
				<?php
				}
				?>
				
				<?php
				if($DP_Content->main_flag && epc_portal_home_mode() !== 'platform' && ! $epc_custom_storefront)
				{
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_industry_catalog.php';
					$epc_home_catalog_profile = epc_portal_industry_catalog_profile();
					if ($epc_home_catalog_profile !== null) {
						require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_industry_catalog_print.php';
					} else {
						$category_block_type = 1;
						$category_id = 0;
						$stmt = $db_link->prepare('SELECT COUNT(`id`) AS `count_id` FROM `shop_catalogue_categories` WHERE `published_flag` = :published_flag AND `parent` = :parent;');
						$stmt->bindValue(':published_flag', 1);
						$stmt->bindValue(':parent', 0);
						$stmt->execute();
						$check_categories_exist_record = $stmt->fetch(PDO::FETCH_ASSOC);
						if($check_categories_exist_record["count_id"] > 0)
						{
							?>
							<div class="col-lg-12 epc-goods-catalog">
								<h2 class="section-title"><?php echo translate_str_by_id(3994); ?></h2>
								<?php require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printCategories.php"); ?>
							</div>
							<?php
						}
					}
				}
				?>
				
				<?php
				//Отображаем блок новостей на главной
				if($DP_Content->main_flag && epc_portal_home_mode() !== 'platform' && ! $epc_custom_storefront){
					
					$news_access = 1;
					$root_content = 311;// id корневого материала раздела Новости
					$news_count = 4;// Количество новостей для отображения
					
					// Проверим включен ли модуль новостей
					$query_news_module = $db_link->prepare("SELECT `activated` FROM `modules` WHERE `id` = ?;");
					$query_news_module->execute( array(49) );
					$news_module_row = $query_news_module->fetch();
					if($news_module_row['activated'] == 0)
					{
						$news_access = 0;
					}
					
					// Проверим что корневой материал новостей опубликован
					$stmt = $db_link->prepare('SELECT `published_flag` FROM `content` WHERE `id` = ?;');
					$stmt->execute(array($root_content));
					$news = $stmt->fetch(PDO::FETCH_ASSOC);
					if($news['published_flag'] == 0){
						$news_access = 0;
					}
					
					if($news_access === 1){
						
						$news_arr = array();
						
						//Получаем новости из БД
						$stmt = $db_link->prepare('SELECT `id`, `value`, `time_created`, `description_tag`, `url` FROM `content` WHERE `parent` = :parent AND `published_flag` = 1 ORDER BY `id` DESC LIMIT :limit;');
						$stmt->bindValue(':parent', (int)$root_content);
						$stmt->bindValue(':limit', (int)$news_count, PDO::PARAM_INT);
						$stmt->execute();
						while($news = $stmt->fetch(PDO::FETCH_ASSOC))
						{
							$news_arr[] = $news;
						}
						
						if(!empty($news_arr)){
							?>
							<div class="col-lg-12">
								<h2 class="section-title" onClick="location='<?php echo $multilang_params['lang_href']; ?>/novosti';"><?php echo translate_str_by_id(4809); ?></h2>
							</div>
							
							<div class="news_box col-lg-12">
							<div class="row">
							<?php
							foreach($news_arr as $news){
								$news["img"] = '';
								if(file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/news/".$news["id"].".jpg")){
									$news["img"] = "/content/files/news/".$news["id"].".jpg";
								}else if(file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/news/".$news["id"].".png")){
									$news["img"] = "/content/files/news/".$news["id"].".png";
								}
							?>
								<div class="col-sm-6 col-md-3">
									<div class="news_item_box">
										<a href="<?php echo $multilang_params['lang_href']; ?><?php echo "/".$news["url"]; ?>">
											<div>
												<?php
												if($news["img"] == ''){
												?>
												<div class="news_item_img"><i style="color: <?=$DP_Template->data_value->news_color;?>; font-size: 85px; padding-top: 33px;" class="fa fa-picture-o" aria-hidden="true"></i></div>
												<?php
												}else{
												?>
												<div class="news_item_img" style="background:url('<?=$news["img"];?>') no-repeat; background-position: center;"></div>
												<?php
												}
												?>
												<div class="news_item_name"><?php echo translate_str_by_id($news["value"]); ?></div>
												<div class="news_item_text"><?php echo translate_str_by_id($news["description_tag"]); ?></div>
												<small class="news_item_clock"><i class="fa fa-clock-o" aria-hidden="true"></i> <?php echo date("d.m.Y", $news["time_created"]); ?></small>
											</div>
										</a>
									</div>
								</div>
							<?php
							}
							?>
							</div>
							</div>
							<?php
						}
					}
				}
				?>
				
				<?php
				// Дополнительный текст страницы
				if($text_after_main != ''){
					echo "<div class=\"col-lg-12\"><br/>".$text_after_main."</div>";
				}
				?>
				
			</div>
		</div>
	</div>
</div>

<?php } /* ! $epc_skip_storefront_main_container */ ?>



<?php
if ($epc_custom_storefront && $DP_Content->main_flag) {
	/* trust badges already rendered inside each home template – skip duplicate */
	$epc_wc_accent = '#0ea5e9';
	$epc_wc_bg = '#f8fafc';
	if ($epc_portal_industry_code === 'electronics') { $epc_wc_accent = '#e10a0a'; $epc_wc_bg = '#fafafa'; }
	elseif ($epc_portal_industry_code === 'fashion') { $epc_wc_accent = '#c026d3'; $epc_wc_bg = '#fdf4ff'; }
	elseif ($epc_portal_industry_code === 'jewellery') { $epc_wc_accent = '#b8860b'; $epc_wc_bg = '#fffbeb'; }
	elseif (in_array($epc_portal_industry_code, array('tax_advisory', 'consultancy'), true)) { $epc_wc_accent = '#0f766e'; $epc_wc_bg = '#f0fdfa'; }
	echo epc_storefront_newsletter_section($epc_wc_accent, $epc_wc_bg, $epc_portal_industry_code);
}
?>
<?php if ($epc_er_retail) {
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_electronics_retail_footer.php';
} elseif ($epc_cpi_consulting) {
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_consulting_primeinvest_footer.php';
} elseif ($epc_frn_retail) {
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_fashion_retail_namshi_footer.php';
} elseif ($epc_jrk_retail) {
	require $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_jewellery_retail_kiyasha_footer.php';
} else { ?>
<aside id="footer-widgets" style="background: <?=$DP_Template->data_value->footer_bg;?>;">
    <div class="container">
        <div class="row">
			<div class="col-md-8">
               
				<div class="row">
					<div class="col-sm-4">
						<docpart type="module" name="footer-menu-1" />
					</div>
					<div class="col-sm-4">
						<docpart type="module" name="footer-menu-2" />
					</div>
					<div class="col-sm-4">
						<docpart type="module" name="footer-menu-3" />
					</div>
				</div>
				
				<h3 class="footer-widget-title"><?php echo translate_str_by_id(4798); ?></h3>
                <p><?php echo translate_str_by_id(4813); ?></p>
				<div class="input-group">
					<a class="btn btn-block btn-ar btn-primary" href="<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu"><?php echo translate_str_by_id(4800); ?></a>
                </div>
				
            </div>
            
			<div class="col-md-4">
				<div class="row">
					
					<div class="col-sm-6 col-md-12">
						<h3 class="footer-widget-title"><?php echo translate_str_by_id(4810); ?></h3>
						<div class="epc-footer-locations">
							<div class="epc-footer-office">
								<strong><i class="fa fa-map-marker" aria-hidden="true"></i> <?php echo htmlspecialchars($epc_head_office_title, ENT_QUOTES, 'UTF-8'); ?></strong>
								<?php if($epc_head_office_address != '') { ?>
								<span><?=nl2br(htmlspecialchars($epc_head_office_address, ENT_QUOTES, 'UTF-8'));?></span>
								<?php } else { ?>
								<span><?=translate_str_by_id(4815).' '.trim(translate_str_by_id($customer_office_info['city'])).' '.trim(translate_str_by_id($customer_office_info['address']));?></span>
								<?php } ?>
								<?php if($epc_contact_phone != '') { ?>
								<span><?=htmlspecialchars($epc_contact_phone, ENT_QUOTES, 'UTF-8');?></span>
								<?php } ?>
								<?php if($epc_head_office_email != '') { ?>
								<span><?=htmlspecialchars($epc_head_office_email, ENT_QUOTES, 'UTF-8');?></span>
								<?php } ?>
								<?php if($epc_head_office_map_url != '') { ?>
								<a class="epc-footer-map-link" href="<?=htmlspecialchars($epc_head_office_map_url, ENT_QUOTES, 'UTF-8');?>" target="_blank" rel="noopener"><i class="fa fa-map" aria-hidden="true"></i> View head office map</a>
								<?php } ?>
							</div>
							<div class="epc-footer-global">
								<strong><i class="fa fa-globe" aria-hidden="true"></i> Global locations</strong>
								<span><?=htmlspecialchars($epc_global_locations_summary, ENT_QUOTES, 'UTF-8');?></span>
								<?php if(count($epc_global_location_blocks) > 0) { ?>
								<label class="epc-footer-location-label" for="epc-footer-location-select">Choose country / location</label>
								<select class="epc-footer-location-select" id="epc-footer-location-select" onchange="epcFooterShowLocation(this.value);">
									<?php foreach($epc_global_location_options as $epc_location_index => $epc_location_title) { ?>
									<option value="<?=$epc_location_index;?>"><?=htmlspecialchars($epc_location_title, ENT_QUOTES, 'UTF-8');?></option>
									<?php } ?>
								</select>
								<div class="epc-footer-location-list" id="epc-footer-location-list">
									<?php foreach($epc_global_location_blocks as $epc_location_index => $epc_global_location_block) { ?>
									<div class="epc-footer-location-item" data-epc-footer-location="<?=$epc_location_index;?>"<?php if($epc_location_index > 0) { ?> style="display:none;"<?php } ?>>
										<?=nl2br(htmlspecialchars($epc_global_location_block, ENT_QUOTES, 'UTF-8'));?>
									</div>
									<?php } ?>
								</div>
								<script>
								function epcFooterShowLocation(index) {
									var list = document.getElementById('epc-footer-location-list');
									if (!list) {
										return;
									}
									var items = list.querySelectorAll('[data-epc-footer-location]');
									for (var i = 0; i < items.length; i++) {
										items[i].style.display = items[i].getAttribute('data-epc-footer-location') === String(index) ? 'block' : 'none';
									}
								}
								</script>
								<?php } ?>
								<?php if($epc_global_locations_map_url != '') { ?>
								<a class="epc-footer-map-link" href="<?=htmlspecialchars($epc_global_locations_map_url, ENT_QUOTES, 'UTF-8');?>" target="_blank" rel="noopener"><i class="fa fa-location-arrow" aria-hidden="true"></i> View global map</a>
								<?php } ?>
							</div>
						</div>
					</div>
					<div class="col-sm-6 col-md-12">
						<h3 class="footer-widget-title"><?php echo translate_str_by_id(3378); ?></h3>
						<div><?=trim(html_entity_decode(translate_str_by_id($customer_office_info['timetable'])));?></div>
					</div>
					
					<?php
					$data_value = (array) $DP_Template->data_value;
					if(!empty($data_value)){
						$pay_arr = array();
						foreach($data_value as $item_data_value_key => $item_data_value){
							if(strpos($item_data_value_key, 'pay_') === 0){
								if($item_data_value == 1){
									$pay_arr[] = str_replace('pay_','',$item_data_value_key);
								}
							}
						}
						if(!empty($pay_arr)){
							?>
							<div class="col-xs-12">
								<h3 class="footer-widget-title"><?php echo translate_str_by_id(4814); ?></h3>
								<div style="line-height:1em;">
									<?php
									foreach($pay_arr as $item_pay_name){
									?>
									<div class="footer_pay_box">
										<div class="footer_pay_logo" style="background:url('/content/files/images/icons/pay/<?=$item_pay_name;?>.jpg') no-repeat; background-position:center;"></div>
									</div>
									<?php
									}
									?>
								</div>
							</div>
							<?php
						}
					}
					?>
					
				</div>
            </div>
			
        </div> <!-- row -->
    </div> <!-- container -->
</aside> <!-- footer-widgets -->



<footer id="footer" style="position: relative; background: <?=$DP_Template->data_value->footer_bg;?>;">
	
	<p>&copy; <?php echo date('Y', time()); ?> <?php require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php'; echo htmlspecialchars(epc_brand_trade_name(), ENT_QUOTES, 'UTF-8'); ?> <?php echo epc_brand_hosted_by_html(); ?></p>

	<div class="icons-holder hidden">

	</div>

	
</footer>
<?php } /* !$epc_custom_storefront footer */ ?>

</div> <!-- boxed -->
</div> <!-- sb-site -->


<?php if($DP_Config->show_cookie) : ?>
<div id="cookie-message">
	<div class="cookie-inner">
		<p><?php echo translate_str_by_id(5656); ?></p>
		<button type="button" class="btn btn-sm cookie-btn cookie-btn--accept"><?php echo translate_str_by_id(5657); ?></button>
		<button type="button" class="btn btn-sm btn-default cookie-btn--decline" style="margin-left:8px">Decline</button>
	</div>
</div>
<script>
(function(){
	var note=document.getElementById('cookie-message');
	if(!note)return;
	var accept=note.querySelector('.cookie-btn--accept');
	var decline=note.querySelector('.cookie-btn--decline');
	function hide(val){document.cookie='cookies_policy='+encodeURIComponent(val)+'; max-age='+(365*86400)+'; path=/; SameSite=Lax';note.classList.remove('show');}
	if(!document.cookie.match(/(?:^|; )cookies_policy=/))note.classList.add('show');
	if(accept)accept.addEventListener('click',function(){hide('accepted');});
	if(decline)decline.addEventListener('click',function(){hide('declined');});
})();
</script>
<?php endif; ?>



<?php
if(!$epc_custom_storefront && $DP_Content->id == 349)
{
?>
<!-- Модальное Окно для страницы vin-запроса -->
<link href="/content/general_pages/vin_zapros/hystmodal.min.css" rel="stylesheet" type="text/css"/>
<script src="/content/general_pages/vin_zapros/hystmodal.min.js" ></script>
<div class="hystmodal" id="vinModal" aria-hidden="true">
	<div class="hystmodal__wrap">
		<div class="hystmodal__window" role="dialog" aria-modal="true">
			<button data-hystclose class="hystmodal__close">Закрыть</button>
			<!-- Ваш HTML код модального окна -->
			<div class="body_modal">
				<div class="text">
					<h4><?php echo translate_str_by_id(5653); ?></h4>
					<p><?php echo translate_str_by_id(5654); ?></p>
					<p><?php echo translate_str_by_id(5655); ?></p>
				</div>
				<div class="img"><img src="/content/general_pages/vin_zapros/vin.png" alt=""></div>
			</div>
		</div>
	</div>
</div>
<?php
}
?>




<?php
// AI Parts Expert — all tenant storefronts + platform marketing
if (
	!empty($DP_Config)
	&& (!isset($DP_Config->epc_parts_agent_enabled) || (string) $DP_Config->epc_parts_agent_enabled !== '0')
) {
	if (
		!$epc_custom_storefront
		&& !(function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname())
	) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ai_parts_expert_widget.php';
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_parts_agent_widget.php';
}
//Подключение скрипта нижней панели
require_once($_SERVER["DOCUMENT_ROOT"]."/modules/shop/bottom_panel/bottom_panel.php");
?>

<div id="back-top">
    <a href="<?php echo $epc_er_retail ? '#epc_er_header' : ($epc_jrk_retail ? '#epc_jrk_header' : ($epc_frn_retail ? '#epc_frn_header' : ($epc_cpi_consulting ? '#epc_cpi_header' : '#header'))); ?>"><i class="fa fa-chevron-up"></i></a>
</div>

<script src="assets/js/styleswitcher.js" defer></script>
<script>SyntaxHighlighter = {all: function(){return;}};</script>

<script src="assets/js/app.js?v=93" defer></script>

<?php if ($epc_er_retail) { ?>
<script>
(function () {
	window.epcHeaderVinSubmit = function () { return true; };
	var tiles = document.querySelectorAll('.product_div_tile');
	for (var i = 0; i < tiles.length; i++) {
		if (tiles[i].querySelector('.product_div_price_crossed_out')) {
			tiles[i].classList.add('epc-er-has-sale');
		}
	}
	var origShowCatalog = window.showCatalogMenu;
	window.showCatalogMenu = function () {
		var panel = document.getElementById('dp_menu');
		if (panel) {
			panel.parentElement.classList.toggle('is-open');
		}
		if (typeof origShowCatalog === 'function') {
			return origShowCatalog.apply(this, arguments);
		}
		if (panel) {
			panel.style.display = panel.style.display === 'block' ? 'none' : 'block';
		}
	};
})();
</script>
<?php } ?>

<?php
if( ! $DP_Content->main_flag ){
?>

<script src="assets/js/DropdownHover.js" defer></script>
<script src="assets/js/holder.js" defer></script>
<script src="assets/js/commerce.js" defer></script>
<script src="assets/js/e-commerce_product.js" defer></script>

<?php
}
?>

<?php
if ($epc_custom_storefront) {
	echo epc_storefront_cookie_consent();
	echo epc_storefront_newsletter_js();
	echo '<script src="/content/general_pages/epc_storefront_animations.js?v=20260621" defer></script>';
}
?>

<?php
// First-party website traffic tracker (CP → Website tracker / Website traffic).
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_web_tracker.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_web_tracker.php';
	if (function_exists('epc_web_tracker_beacon_html')) {
		echo epc_web_tracker_beacon_html();
	}
}
?>

</body>
</html>