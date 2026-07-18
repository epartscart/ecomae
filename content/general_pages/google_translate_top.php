<?php
defined('_ASTEXE_') or die('No access');
?>
<style>
	.epc-google-translate-top {
		position: relative;
		z-index: 10000;
		background: #ffffff;
		border-bottom: 1px solid #e5e5e5;
		padding: 6px 12px;
		text-align: right;
		min-height: 42px;
	}
	.epc-google-translate-top__inner {
		display: inline-flex;
		align-items: center;
		gap: 8px;
		max-width: 100%;
	}
	.epc-google-translate-top__label {
		color: #444;
		font-size: 13px;
		line-height: 1;
		white-space: nowrap;
	}
	.epc-google-translate-top__status {
		color: #667085;
		font-size: 12px;
		line-height: 1.2;
		max-width: 320px;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.epc-native-translate-select {
		appearance: none;
		-webkit-appearance: none;
		background: #fff url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='11' height='7' viewBox='0 0 11 7'%3E%3Cpath fill='%23555' d='M5.5 7 0 0h11z'/%3E%3C/svg%3E") no-repeat right 10px center;
		border: 1px solid #d7d7d7;
		border-radius: 4px;
		color: #333;
		cursor: pointer;
		font-size: 13px;
		height: 32px;
		line-height: 32px;
		max-width: 240px;
		min-width: 185px;
		padding: 0 32px 0 10px;
		display: inline-block;
	}
	.epc-native-translate-select:focus {
		border-color: #5b9dd9;
		box-shadow: 0 0 0 2px rgba(91, 157, 217, 0.18);
		outline: none;
	}
	#google_translate_element {
		height: 0;
		left: -9999px;
		overflow: hidden;
		position: absolute;
		top: -9999px;
		width: 0;
	}
	#google_translate_element .goog-te-gadget {
		color: transparent;
		font-size: 0;
		line-height: 1;
	}
	#google_translate_element .goog-te-gadget span {
		display: none;
	}
	#google_translate_element .goog-te-combo {
		background: #fff;
		border: 1px solid #d7d7d7;
		border-radius: 4px;
		color: #333;
		cursor: pointer;
		font-size: 13px;
		height: 32px;
		margin: 0;
		max-width: 260px;
		min-width: 185px;
		padding: 0 8px;
	}
	@media (max-width: 767px) {
		.epc-google-translate-top {
			text-align: center;
			padding-left: 6px;
			padding-right: 6px;
		}
		.epc-google-translate-top__inner {
			justify-content: center;
		}
		.epc-google-translate-top__label {
			display: none;
		}
		.epc-google-translate-top__status {
			display: none;
		}
	}
</style>
<div class="epc-google-translate-top notranslate" translate="no">
	<div class="epc-google-translate-top__inner">
		<span class="epc-google-translate-top__label">Language</span>
		<span id="epc_translate_auto_status" class="epc-google-translate-top__status">Auto language: checking location...</span>
		<select id="epc_native_translate_select" class="epc-native-translate-select notranslate" aria-label="Select language" translate="no">
			<option value="af">Afrikaans</option>
			<option value="sq">Shqip</option>
			<option value="am">አማርኛ</option>
			<option value="ar">العربية</option>
			<option value="hy">Հայերեն</option>
			<option value="az">Azərbaycanca</option>
			<option value="eu">Euskara</option>
			<option value="be">Беларуская</option>
			<option value="bn">বাংলা</option>
			<option value="bs">Bosanski</option>
			<option value="bg">Български</option>
			<option value="ca">Català</option>
			<option value="ceb">Cebuano</option>
			<option value="ny">Chichewa</option>
			<option value="zh-CN">中文（简体）</option>
			<option value="zh-TW">中文（繁體）</option>
			<option value="co">Corsu</option>
			<option value="hr">Hrvatski</option>
			<option value="cs">Čeština</option>
			<option value="da">Dansk</option>
			<option value="nl">Nederlands</option>
			<option value="en">English</option>
			<option value="eo">Esperanto</option>
			<option value="et">Eesti</option>
			<option value="tl">Filipino</option>
			<option value="fi">Suomi</option>
			<option value="fr">Français</option>
			<option value="fy">Frysk</option>
			<option value="gl">Galego</option>
			<option value="ka">ქართული</option>
			<option value="de">Deutsch</option>
			<option value="el">Ελληνικά</option>
			<option value="gu">ગુજરાતી</option>
			<option value="ht">Kreyòl Ayisyen</option>
			<option value="ha">Hausa</option>
			<option value="haw">ʻŌlelo Hawaiʻi</option>
			<option value="iw">עברית</option>
			<option value="hi">हिन्दी</option>
			<option value="hmn">Hmoob</option>
			<option value="hu">Magyar</option>
			<option value="is">Íslenska</option>
			<option value="ig">Igbo</option>
			<option value="id">Indonesia</option>
			<option value="ga">Gaeilge</option>
			<option value="it">Italiano</option>
			<option value="ja">日本語</option>
			<option value="jw">Basa Jawa</option>
			<option value="kn">ಕನ್ನಡ</option>
			<option value="kk">Қазақша</option>
			<option value="km">ខ្មែរ</option>
			<option value="ko">한국어</option>
			<option value="ku">Kurdî</option>
			<option value="ky">Кыргызча</option>
			<option value="lo">ລາວ</option>
			<option value="la">Latina</option>
			<option value="lv">Latviešu</option>
			<option value="lt">Lietuvių</option>
			<option value="lb">Lëtzebuergesch</option>
			<option value="mk">Македонски</option>
			<option value="mg">Malagasy</option>
			<option value="ms">Melayu</option>
			<option value="ml">മലയാളം</option>
			<option value="mt">Malti</option>
			<option value="mi">Māori</option>
			<option value="mr">मराठी</option>
			<option value="mn">Монгол</option>
			<option value="my">မြန်မာ</option>
			<option value="ne">नेपाली</option>
			<option value="no">Norsk</option>
			<option value="ps">پښتو</option>
			<option value="fa">فارسی</option>
			<option value="pl">Polski</option>
			<option value="pt">Português</option>
			<option value="pa">ਪੰਜਾਬੀ</option>
			<option value="ro">Română</option>
			<option value="ru">Русский</option>
			<option value="sm">Gagana Samoa</option>
			<option value="gd">Gàidhlig</option>
			<option value="sr">Српски</option>
			<option value="st">Sesotho</option>
			<option value="sn">Shona</option>
			<option value="sd">سنڌي</option>
			<option value="si">සිංහල</option>
			<option value="sk">Slovenčina</option>
			<option value="sl">Slovenščina</option>
			<option value="so">Soomaali</option>
			<option value="es">Español</option>
			<option value="su">Basa Sunda</option>
			<option value="sw">Kiswahili</option>
			<option value="sv">Svenska</option>
			<option value="tg">Тоҷикӣ</option>
			<option value="ta">தமிழ்</option>
			<option value="te">తెలుగు</option>
			<option value="th">ไทย</option>
			<option value="tr">Türkçe</option>
			<option value="uk">Українська</option>
			<option value="ur">اردو</option>
			<option value="uz">Oʻzbekcha</option>
			<option value="vi">Tiếng Việt</option>
			<option value="cy">Cymraeg</option>
			<option value="xh">IsiXhosa</option>
			<option value="yi">ייִדיש</option>
			<option value="yo">Yorùbá</option>
			<option value="zu">IsiZulu</option>
		</select>
		<div id="google_translate_element"></div>
	</div>
</div>
<script>
	var epcTranslateManualKey = 'epcTranslateManualLanguage';
	var epcTranslateAutoKey = 'epcTranslateAutoLanguage';
	var epcTranslateAutoAppliedKey = 'epcTranslateAutoAppliedLanguage';

	function epcTranslateStatus(message) {
		var status = document.getElementById('epc_translate_auto_status');
		if (status) {
			status.textContent = message || '';
			status.title = message || '';
		}
	}

	function epcTranslateCookieLanguage() {
		var match = document.cookie.match(/(?:^|;\s*)googtrans=([^;]+)/);
		if (!match) {
			return 'en';
		}
		var parts = decodeURIComponent(match[1]).split('/');
		return parts.length >= 3 && parts[2] ? parts[2] : 'en';
	}

	function epcClearTranslateCookie() {
		var hostParts = window.location.hostname.split('.');
		var domains = ['', window.location.hostname];
		if (hostParts.length > 2) {
			domains.push('.' + hostParts.slice(-2).join('.'));
		}
		for (var i = 0; i < domains.length; i++) {
			document.cookie = 'googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/' + (domains[i] ? '; domain=' + domains[i] : '');
		}
	}

	function epcSetTranslateCookie(lang) {
		var hostParts = window.location.hostname.split('.');
		var domains = ['', window.location.hostname];
		if (hostParts.length > 2) {
			domains.push('.' + hostParts.slice(-2).join('.'));
		}
		for (var i = 0; i < domains.length; i++) {
			document.cookie = 'googtrans=/en/' + lang + '; path=/; max-age=31536000; SameSite=Lax' + (domains[i] ? '; domain=' + domains[i] : '');
		}
	}

	function epcApplyNativeTranslate(lang) {
		if (lang === 'en') {
			epcClearTranslateCookie();
			window.location.reload();
			return;
		}
		epcSetTranslateCookie(lang);
		var combo = document.querySelector('#google_translate_element select.goog-te-combo');
		if (combo) {
			combo.value = lang;
			combo.dispatchEvent(new Event('change'));
		}
		window.setTimeout(function() {
			window.location.reload();
		}, 500);
	}

	function epcApplyAutoTranslate(lang) {
		if (!lang || lang === 'en') {
			return;
		}
		try {
			if (sessionStorage.getItem(epcTranslateAutoAppliedKey) === lang) {
				return;
			}
			sessionStorage.setItem(epcTranslateAutoAppliedKey, lang);
		} catch (e) {}
		epcSetTranslateCookie(lang);
		var select = document.getElementById('epc_native_translate_select');
		if (select) {
			select.value = lang;
		}
		var attempts = 0;
		(function applyWhenReady() {
			var combo = document.querySelector('#google_translate_element select.goog-te-combo');
			if (combo) {
				combo.value = lang;
				combo.dispatchEvent(new Event('change'));
				window.setTimeout(function() {
					window.location.reload();
				}, 600);
				return;
			}
			attempts++;
			if (attempts < 24) {
				window.setTimeout(applyWhenReady, 250);
				return;
			}
			window.location.reload();
		})();
	}

	function epcAttachGoogleTranslateChange(attempts) {
		var combo = document.querySelector('#google_translate_element select.goog-te-combo');
		if (!combo) {
			if ((attempts || 0) < 30) {
				setTimeout(function() {
					epcAttachGoogleTranslateChange((attempts || 0) + 1);
				}, 250);
			}
			return;
		}
		combo.addEventListener('change', function() {
			try {
				localStorage.setItem(epcTranslateManualKey, this.value || 'en');
			} catch (e) {}
		});
	}

	function epcSupportedTranslateLanguage(lang) {
		var select = document.getElementById('epc_native_translate_select');
		if (!select || !lang) {
			return '';
		}
		var normalized = String(lang).trim();
		if (!normalized) {
			return '';
		}
		normalized = normalized.replace('_', '-');
		var base = normalized.split('-')[0].toLowerCase();
		var aliases = {
			he: 'iw',
			jv: 'jw',
			zh: 'zh-CN'
		};
		var candidates = [normalized, base, aliases[base]];
		for (var i = 0; i < candidates.length; i++) {
			if (candidates[i] && select.querySelector('option[value="' + candidates[i] + '"]')) {
				return candidates[i];
			}
		}
		return '';
	}

	function epcLanguageFromIpApiLanguages(languages) {
		var parts = String(languages || '').split(',');
		for (var i = 0; i < parts.length; i++) {
			var lang = epcSupportedTranslateLanguage(parts[i]);
			if (lang) {
				return lang;
			}
		}
		return '';
	}

	function epcLanguageForCountry(countryCode, languages) {
		var country = String(countryCode || '').toUpperCase();
		var map = {
			AE: 'ar', SA: 'ar', QA: 'ar', KW: 'ar', BH: 'ar', OM: 'ar', JO: 'ar', LB: 'ar', EG: 'ar', IQ: 'ar', MA: 'ar', DZ: 'ar', TN: 'ar',
			FR: 'fr', BE: 'fr', CH: 'fr', LU: 'fr', MC: 'fr',
			DE: 'de', AT: 'de', LI: 'de',
			ES: 'es', MX: 'es', AR: 'es', CL: 'es', CO: 'es', PE: 'es', VE: 'es', UY: 'es', PY: 'es', BO: 'es', EC: 'es',
			IT: 'it', SM: 'it', VA: 'it',
			PT: 'pt', BR: 'pt',
			RU: 'ru', BY: 'ru', KZ: 'ru', KG: 'ru', TJ: 'ru',
			TR: 'tr', CY: 'tr',
			IN: 'hi',
			PK: 'ur',
			CN: 'zh-CN', HK: 'zh-CN', MO: 'zh-CN', SG: 'zh-CN', TW: 'zh-CN',
			NL: 'nl', DK: 'da', SE: 'sv', NO: 'no', FI: 'fi',
			PL: 'pl', CZ: 'cs', SK: 'sk', HU: 'hu', RO: 'ro', BG: 'bg',
			GR: 'el', RS: 'sr', HR: 'hr', SI: 'sl', UA: 'uk',
			TH: 'th', VN: 'vi', ID: 'id', MY: 'ms', KR: 'ko', JP: 'ja',
			IR: 'fa', IL: 'iw', BD: 'bn', LK: 'si', NP: 'ne'
		};
		return map[country] || epcLanguageFromIpApiLanguages(languages) || 'en';
	}

	function epcBrowserLanguage() {
		var list = navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language || navigator.userLanguage || ''];
		for (var i = 0; i < list.length; i++) {
			var lang = epcSupportedTranslateLanguage(list[i]);
			if (lang) {
				return lang;
			}
		}
		return '';
	}

	function epcReadManualLanguage() {
		try {
			return localStorage.getItem(epcTranslateManualKey) || '';
		} catch (e) {
			return '';
		}
	}

	function epcSaveAutoLanguage(country, lang) {
		try {
			localStorage.setItem(epcTranslateAutoKey, JSON.stringify({
				country: country || '',
				lang: lang || '',
				time: Date.now()
			}));
		} catch (e) {}
	}

	function epcFetchJson(url) {
		return fetch(url, {cache: 'no-store'}).then(function(response) {
			return response.ok ? response.json() : null;
		});
	}

	function epcDetectVisitorCountry() {
		return epcFetchJson('https://ipapi.co/json/')
			.then(function(data) {
				if (data && data.country_code) {
					return {
						country: data.country_code,
						languages: data.languages || '',
						source: 'ipapi'
					};
				}
				return epcFetchJson('https://ipwho.is/')
					.then(function(fallback) {
						return fallback && fallback.country_code ? {
							country: fallback.country_code,
							languages: '',
							source: 'ipwhois'
						} : null;
					});
			});
	}

	function epcDetectVisitorCountryWithRetry(attempts) {
		return epcDetectVisitorCountry().then(function(data) {
			if (data && data.country) {
				return data;
			}
			if ((attempts || 0) < 2) {
				return new Promise(function(resolve) {
					window.setTimeout(resolve, 700);
				}).then(function() {
					return epcDetectVisitorCountryWithRetry((attempts || 0) + 1);
				});
			}
			return null;
		});
	}

	function epcAutoTranslateByCountry() {
		var currentLanguage = epcTranslateCookieLanguage();
		var manualLanguage = epcReadManualLanguage();
		if (manualLanguage) {
			epcTranslateStatus('Language set manually: ' + manualLanguage);
			return;
		}
		if (currentLanguage !== 'en') {
			epcTranslateStatus('Auto language active: ' + currentLanguage);
			return;
		}
		epcDetectVisitorCountryWithRetry(0)
			.then(function(data) {
				var lang = '';
				var country = '';
				if (data && data.country) {
					country = String(data.country).toUpperCase();
					lang = epcLanguageForCountry(country, data.languages);
				}
				if (!lang || lang === 'en') {
					lang = epcBrowserLanguage() || 'en';
				}
				epcSaveAutoLanguage(country, lang);
				if (lang && lang !== 'en') {
					var select = document.getElementById('epc_native_translate_select');
					if (select) {
						select.value = lang;
					}
					epcTranslateStatus('Auto language: ' + (country ? country + ' -> ' : '') + lang);
					epcApplyAutoTranslate(lang);
				} else {
					epcTranslateStatus('Auto language: English');
				}
			})
			.catch(function() {
				var lang = epcBrowserLanguage() || 'en';
				if (lang !== 'en') {
					epcTranslateStatus('Auto language from browser: ' + lang);
					epcApplyAutoTranslate(lang);
				} else {
					epcTranslateStatus('Auto language: English');
				}
			});
	}

	function epcInitNativeTranslateSelect() {
		var select = document.getElementById('epc_native_translate_select');
		if (!select) {
			return;
		}
		select.value = epcTranslateCookieLanguage();
		if (select.value && select.value !== 'en') {
			epcTranslateStatus('Auto language active: ' + select.value);
		}
		select.addEventListener('change', function() {
			try {
				localStorage.setItem(epcTranslateManualKey, this.value);
			} catch (e) {}
			epcApplyNativeTranslate(this.value);
		});
		epcAutoTranslateByCountry();
	}

	function googleTranslateElementInit() {
		new google.translate.TranslateElement({
			pageLanguage: 'en',
			layout: google.translate.TranslateElement.inlineLayout.HORIZONTAL,
			autoDisplay: false
		}, 'google_translate_element');
		epcInitNativeTranslateSelect();
		epcAttachGoogleTranslateChange(0);
	}
	// Load Google Translate after first paint — sync head script blocked every navigation.
	function epcLoadGoogleTranslate() {
		if (window.__epcGoogleTranslateLoading) { return; }
		window.__epcGoogleTranslateLoading = true;
		var s = document.createElement('script');
		s.async = true;
		s.src = '//translate.google.com/translate_a/element.js?cb=googleTranslateElementInit';
		document.head.appendChild(s);
	}
	if (window.requestIdleCallback) {
		requestIdleCallback(epcLoadGoogleTranslate, { timeout: 3500 });
	} else {
		window.addEventListener('load', function () { setTimeout(epcLoadGoogleTranslate, 800); }, { once: true });
	}
</script>
