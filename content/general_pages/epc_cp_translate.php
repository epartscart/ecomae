<?php
/**
 * Google Translate widget for Super CP, tenant CP, and ERP shell.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_portal_db.php';

function epc_cp_translate_language_options(): array
{
	return array(
		'en' => 'English',
		'ar' => 'العربية',
		'ru' => 'Русский',
		'fr' => 'Français',
		'de' => 'Deutsch',
		'hi' => 'हिन्दी',
		'ur' => 'اردو',
		'es' => 'Español',
		'pt' => 'Português',
		'it' => 'Italiano',
		'tr' => 'Türkçe',
		'zh-CN' => '中文（简体）',
		'ja' => '日本語',
		'ko' => '한국어',
		'nl' => 'Nederlands',
		'pl' => 'Polski',
		'fa' => 'فارسی',
		'bn' => 'বাংলা',
		'ta' => 'தமிழ்',
		'vi' => 'Tiếng Việt',
		'id' => 'Indonesia',
		'th' => 'ไทย',
		'uk' => 'Українська',
		'he' => 'עברית',
		'sv' => 'Svenska',
		'cs' => 'Čeština',
		'ro' => 'Română',
	);
}

function epc_cp_translate_normalize_lang(?string $lang): string
{
	$lang = strtolower(trim((string) $lang));
	$lang = str_replace('_', '-', $lang);
	$aliases = array('iw' => 'he', 'jw' => 'jv', 'zh' => 'zh-CN');
	if (isset($aliases[$lang])) {
		$lang = $aliases[$lang];
	}
	$options = epc_cp_translate_language_options();
	if (isset($options[$lang])) {
		return $lang;
	}
	$base = explode('-', $lang)[0];
	if (isset($options[$base])) {
		return $base;
	}
	return 'en';
}

function epc_cp_translate_default_lang(): string
{
	$settings = function_exists('epc_portal_load_site_settings') ? epc_portal_load_site_settings() : array();
	$lang = isset($settings['cp_default_lang']) ? (string) $settings['cp_default_lang'] : 'en';
	return epc_cp_translate_normalize_lang($lang);
}

function epc_cp_translate_accept_language_hint(): string
{
	$raw = isset($_SERVER['HTTP_ACCEPT_LANGUAGE']) ? (string) $_SERVER['HTTP_ACCEPT_LANGUAGE'] : '';
	return substr(preg_replace('/[^\w,\-;=.\*]/', '', $raw), 0, 120);
}

function epc_cp_translate_render(string $context = 'cp'): string
{
	static $rendered = false;
	if ($rendered) {
		return '';
	}
	$rendered = true;

	$context = ($context === 'erp') ? 'erp' : 'cp';
	$tenantDefault = epc_cp_translate_default_lang();
	$acceptLang = epc_cp_translate_accept_language_hint();
	$options = epc_cp_translate_language_options();
	$rootClass = 'epc-cp-translate epc-cp-translate--' . $context;

	ob_start();
	?>
<style>
	.epc-cp-translate { position: relative; z-index: 10001; }
	.epc-cp-translate--cp { display: inline-flex; align-items: center; margin: 0 6px 0 0; }
	.epc-cp-translate--cp .epc-cp-translate__select {
		appearance: none;
		-webkit-appearance: none;
		background: rgba(255,255,255,0.95) url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='11' height='7' viewBox='0 0 11 7'%3E%3Cpath fill='%23555' d='M5.5 7 0 0h11z'/%3E%3C/svg%3E") no-repeat right 8px center;
		border: 1px solid #d7d7d7;
		border-radius: 4px;
		color: #333;
		cursor: pointer;
		font-size: 12px;
		height: 30px;
		line-height: 28px;
		max-width: 150px;
		min-width: 110px;
		padding: 0 26px 0 8px;
	}
	.epc-cp-translate--erp { display: inline-flex; align-items: center; margin-right: 10px; }
	.epc-cp-translate--erp .epc-cp-translate__select {
		appearance: none;
		-webkit-appearance: none;
		background: #fff url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='11' height='7' viewBox='0 0 11 7'%3E%3Cpath fill='%23555' d='M5.5 7 0 0h11z'/%3E%3C/svg%3E") no-repeat right 8px center;
		border: 1px solid #cbd5e1;
		border-radius: 4px;
		color: #334155;
		cursor: pointer;
		font-size: 12px;
		height: 28px;
		line-height: 26px;
		max-width: 160px;
		min-width: 120px;
		padding: 0 26px 0 8px;
	}
	.epc-cp-translate__select:focus { border-color: #5b9dd9; outline: none; }
	.epc-cp-translate #google_translate_element_cp {
		height: 0; left: -9999px; overflow: hidden; position: absolute; top: -9999px; width: 0;
	}
	body.epc-cp-shell, body.epc-erp-cp-shell { top: 0 !important; position: relative !important; }
	.goog-te-banner-frame, .skiptranslate.goog-te-banner-frame { display: none !important; height: 0 !important; }
</style>
<?php if ($context === 'cp') { ?>
<li class="dropdown epc-cp-translate-nav notranslate" translate="no">
	<label class="sr-only" for="epc_cp_native_translate_select">Language</label>
	<select id="epc_cp_native_translate_select" class="epc-cp-translate__select notranslate" aria-label="Select language" translate="no">
		<?php foreach ($options as $code => $label) { ?>
		<option value="<?php echo htmlspecialchars($code, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></option>
		<?php } ?>
	</select>
	<div id="google_translate_element_cp"></div>
</li>
<?php } else { ?>
<div class="<?php echo htmlspecialchars($rootClass, ENT_QUOTES, 'UTF-8'); ?> notranslate" translate="no">
	<label class="sr-only" for="epc_cp_native_translate_select">Language</label>
	<select id="epc_cp_native_translate_select" class="epc-cp-translate__select notranslate" aria-label="Select language" translate="no">
		<?php foreach ($options as $code => $label) { ?>
		<option value="<?php echo htmlspecialchars($code, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></option>
		<?php } ?>
	</select>
	<div id="google_translate_element_cp"></div>
</div>
<?php } ?>
<script>
(function(){
	var epcCpTenantDefaultLang = <?php echo json_encode($tenantDefault, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var epcCpAcceptLanguageHint = <?php echo json_encode($acceptLang, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var epcTranslateManualKey = 'epcTranslateManualLanguage';
	var epcTranslateAutoAppliedKey = 'epcTranslateAutoAppliedLanguage';

	function epcTranslateCookieLanguage() {
		var match = document.cookie.match(/(?:^|;\s*)googtrans=([^;]+)/);
		if (!match) {
			var epcMatch = document.cookie.match(/(?:^|;\s*)epc_lang=([^;]+)/);
			if (epcMatch) return decodeURIComponent(epcMatch[1]) || 'en';
			return 'en';
		}
		var parts = decodeURIComponent(match[1]).split('/');
		return parts.length >= 3 && parts[2] ? parts[2] : 'en';
	}

	function epcClearTranslateCookie() {
		var hostParts = window.location.hostname.split('.');
		var domains = ['', window.location.hostname];
		if (hostParts.length > 2) domains.push('.' + hostParts.slice(-2).join('.'));
		for (var i = 0; i < domains.length; i++) {
			document.cookie = 'googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/' + (domains[i] ? '; domain=' + domains[i] : '');
			document.cookie = 'epc_lang=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/' + (domains[i] ? '; domain=' + domains[i] : '');
		}
	}

	function epcSetTranslateCookie(lang) {
		var hostParts = window.location.hostname.split('.');
		var domains = ['', window.location.hostname];
		if (hostParts.length > 2) domains.push('.' + hostParts.slice(-2).join('.'));
		for (var i = 0; i < domains.length; i++) {
			document.cookie = 'googtrans=/en/' + lang + '; path=/; max-age=31536000; SameSite=Lax' + (domains[i] ? '; domain=' + domains[i] : '');
			document.cookie = 'epc_lang=' + encodeURIComponent(lang) + '; path=/; max-age=31536000; SameSite=Lax' + (domains[i] ? '; domain=' + domains[i] : '');
		}
	}

	function epcSupportedTranslateLanguage(lang) {
		var select = document.getElementById('epc_cp_native_translate_select');
		if (!select || !lang) return '';
		var normalized = String(lang).trim().replace('_', '-');
		var base = normalized.split('-')[0].toLowerCase();
		var aliases = { he: 'he', iw: 'he', jv: 'jv', jw: 'jv', zh: 'zh-CN' };
		var candidates = [normalized, base, aliases[base]];
		for (var i = 0; i < candidates.length; i++) {
			if (candidates[i] && select.querySelector('option[value="' + candidates[i] + '"]')) return candidates[i];
		}
		return '';
	}

	function epcLanguageFromHint(languages) {
		var parts = String(languages || '').split(',');
		for (var i = 0; i < parts.length; i++) {
			var lang = epcSupportedTranslateLanguage(parts[i]);
			if (lang) return lang;
		}
		return '';
	}

	function epcLanguageForCountry(countryCode, languages) {
		var country = String(countryCode || '').toUpperCase();
		var map = {
			AE:'ar',SA:'ar',QA:'ar',KW:'ar',BH:'ar',OM:'ar',JO:'ar',LB:'ar',EG:'ar',IQ:'ar',MA:'ar',DZ:'ar',TN:'ar',
			FR:'fr',BE:'fr',CH:'fr',DE:'de',AT:'de',ES:'es',MX:'es',IT:'it',PT:'pt',BR:'pt',RU:'ru',TR:'tr',
			IN:'hi',PK:'ur',CN:'zh-CN',HK:'zh-CN',TW:'zh-CN',NL:'nl',PL:'pl',UA:'uk',IR:'fa',IL:'he',BD:'bn'
		};
		return map[country] || epcLanguageFromHint(languages) || '';
	}

	function epcBrowserLanguage() {
		var list = navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language || ''];
		for (var i = 0; i < list.length; i++) {
			var lang = epcSupportedTranslateLanguage(list[i]);
			if (lang) return lang;
		}
		return epcLanguageFromHint(epcCpAcceptLanguageHint);
	}

	function epcReadManualLanguage() {
		try { return localStorage.getItem(epcTranslateManualKey) || ''; } catch (e) { return ''; }
	}

	function epcApplyNativeTranslate(lang) {
		if (lang === 'en') { epcClearTranslateCookie(); window.location.reload(); return; }
		epcSetTranslateCookie(lang);
		var combo = document.querySelector('#google_translate_element_cp select.goog-te-combo');
		if (combo) { combo.value = lang; combo.dispatchEvent(new Event('change')); }
		window.setTimeout(function(){ window.location.reload(); }, 500);
	}

	function epcApplyAutoTranslate(lang) {
		if (!lang || lang === 'en') return;
		try {
			if (sessionStorage.getItem(epcTranslateAutoAppliedKey) === lang) return;
			sessionStorage.setItem(epcTranslateAutoAppliedKey, lang);
		} catch (e) {}
		epcSetTranslateCookie(lang);
		var select = document.getElementById('epc_cp_native_translate_select');
		if (select) select.value = lang;
		var attempts = 0;
		(function applyWhenReady() {
			var combo = document.querySelector('#google_translate_element_cp select.goog-te-combo');
			if (combo) {
				combo.value = lang;
				combo.dispatchEvent(new Event('change'));
				window.setTimeout(function(){ window.location.reload(); }, 600);
				return;
			}
			attempts++;
			if (attempts < 24) { window.setTimeout(applyWhenReady, 250); return; }
			window.location.reload();
		})();
	}

	function epcFetchJson(url) {
		return fetch(url, {cache:'no-store'}).then(function(r){ return r.ok ? r.json() : null; });
	}

	function epcDetectVisitorCountry() {
		return epcFetchJson('https://ipapi.co/json/').then(function(data) {
			if (data && data.country_code) return { country: data.country_code, languages: data.languages || '' };
			return epcFetchJson('https://ipwho.is/').then(function(fb) {
				return fb && fb.country_code ? { country: fb.country_code, languages: '' } : null;
			});
		});
	}

	function epcAutoTranslateInit() {
		var currentLanguage = epcTranslateCookieLanguage();
		var manualLanguage = epcReadManualLanguage();
		var select = document.getElementById('epc_cp_native_translate_select');
		if (select) select.value = currentLanguage || 'en';
		if (manualLanguage) return;
		if (currentLanguage !== 'en') return;

		var tenantDefault = epcSupportedTranslateLanguage(epcCpTenantDefaultLang);
		if (tenantDefault && tenantDefault !== 'en') {
			epcApplyAutoTranslate(tenantDefault);
			return;
		}

		epcDetectVisitorCountry().then(function(data) {
			var lang = '';
			if (data && data.country) lang = epcLanguageForCountry(data.country, data.languages);
			if (!lang || lang === 'en') lang = epcBrowserLanguage() || 'en';
			if (lang && lang !== 'en') epcApplyAutoTranslate(lang);
		}).catch(function() {
			var lang = epcBrowserLanguage() || 'en';
			if (lang !== 'en') epcApplyAutoTranslate(lang);
		});
	}

	function epcInitNativeTranslateSelect() {
		var select = document.getElementById('epc_cp_native_translate_select');
		if (!select) return;
		select.addEventListener('change', function() {
			try { localStorage.setItem(epcTranslateManualKey, this.value); } catch (e) {}
			epcApplyNativeTranslate(this.value);
		});
		epcAutoTranslateInit();
	}

	window.epcCpGoogleTranslateElementInit = function() {
		new google.translate.TranslateElement({
			pageLanguage: 'en',
			layout: google.translate.TranslateElement.InlineLayout.HORIZONTAL,
			autoDisplay: false
		}, 'google_translate_element_cp');
		epcInitNativeTranslateSelect();
	};
})();
</script>
<script src="//translate.google.com/translate_a/element.js?cb=epcCpGoogleTranslateElementInit"></script>
	<?php
	return (string) ob_get_clean();
}
