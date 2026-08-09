(function () {
	"use strict";

	var epcTranslateManualKey = "epcTranslateManualLanguage";
	var epcTranslateAutoKey = "epcTranslateAutoLanguage";
	var epcTranslateAutoAppliedKey = "epcTranslateAutoAppliedLanguage";

	function epcRoot() {
		return document.getElementById("epc_google_translate_root") || document.querySelector(".epc-google-translate-top");
	}

	function epcCmsActiveLangs() {
		var root = epcRoot();
		var raw = root ? (root.getAttribute("data-cms-langs") || "en") : "en";
		return raw.split(",").map(function (s) { return s.trim().toLowerCase(); }).filter(Boolean);
	}

	function epcCmsCurrentLang() {
		var root = epcRoot();
		if (root && root.getAttribute("data-cms-lang")) {
			return String(root.getAttribute("data-cms-lang")).toLowerCase();
		}
		var m = (window.location.pathname || "").match(/^\/([a-z]{2})(?:\/|$)/i);
		return m ? m[1].toLowerCase() : "en";
	}

	function epcCfCountryHint() {
		var root = epcRoot();
		var c = root ? String(root.getAttribute("data-cf-country") || "").toUpperCase() : "";
		if (!c || c === "XX" || c === "T1") return "";
		return c;
	}

	function epcTranslateStatus(message) {
		var status = document.getElementById("epc_translate_auto_status");
		if (status) {
			status.textContent = message || "";
			status.title = message || "";
		}
	}

	function epcCmsLangNavigate(lang) {
		lang = String(lang || "").toLowerCase();
		var active = epcCmsActiveLangs();
		if (!lang || active.indexOf(lang) === -1) {
			return false;
		}
		if (typeof window.lang_selected === "function") {
			window.lang_selected(lang);
			return true;
		}
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "lang=" + lang + "; path=/; expires=" + date.toUTCString();
		var path = window.location.pathname || "/";
		var search = window.location.search || "";
		var hash = window.location.hash || "";
		var parts = path.split("/");
		if (parts.length > 1 && /^[a-z]{2}(?:-[a-zA-Z]+)?$/i.test(parts[1] || "")) {
			parts[1] = lang;
			window.location.assign(parts.join("/") + search + hash);
			return true;
		}
		window.location.assign("/" + lang + (path === "/" ? "/" : path) + search + hash);
		return true;
	}

	function epcTranslateCookieLanguage() {
		var match = document.cookie.match(/(?:^|;\s*)googtrans=([^;]+)/);
		if (!match) {
			return "en";
		}
		var parts = decodeURIComponent(match[1]).split("/");
		return parts.length >= 3 && parts[2] ? parts[2] : "en";
	}

	function epcClearTranslateCookie() {
		var hostParts = window.location.hostname.split(".");
		var domains = ["", window.location.hostname];
		if (hostParts.length > 2) {
			domains.push("." + hostParts.slice(-2).join("."));
		}
		for (var i = 0; i < domains.length; i++) {
			document.cookie = "googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/" + (domains[i] ? "; domain=" + domains[i] : "");
		}
	}

	function epcSetTranslateCookie(lang) {
		var hostParts = window.location.hostname.split(".");
		var domains = ["", window.location.hostname];
		if (hostParts.length > 2) {
			domains.push("." + hostParts.slice(-2).join("."));
		}
		for (var i = 0; i < domains.length; i++) {
			document.cookie = "googtrans=/en/" + lang + "; path=/; max-age=31536000; SameSite=Lax" + (domains[i] ? "; domain=" + domains[i] : "");
		}
	}

	function epcApplyNativeTranslate(lang) {
		lang = String(lang || "en").toLowerCase();
		try {
			localStorage.setItem(epcTranslateManualKey, lang);
			// Allow a later pick (or auto) after the user cancelled / chose English.
			sessionStorage.removeItem(epcTranslateAutoAppliedKey);
		} catch (e) {}

		// English must always clear googtrans first. CMS navigate used to return early
		// and leave /en/<other> cookies stuck — then further picks looked "dead".
		if (lang === "en") {
			epcClearTranslateCookie();
			epcCmsLangNavigate("en");
			window.location.reload();
			return;
		}

		if (epcCmsLangNavigate(lang)) {
			return;
		}
		epcSetTranslateCookie(lang);
		var combo = document.querySelector("#google_translate_element select.goog-te-combo");
		if (combo) {
			combo.value = lang;
			combo.dispatchEvent(new Event("change"));
		}
		window.setTimeout(function () {
			window.location.reload();
		}, 500);
	}

	function epcApplyAutoTranslate(lang) {
		if (!lang || lang === "en") {
			return;
		}
		try {
			if (sessionStorage.getItem(epcTranslateAutoAppliedKey) === lang) {
				return;
			}
			sessionStorage.setItem(epcTranslateAutoAppliedKey, lang);
		} catch (e) {}
		if (epcCmsLangNavigate(lang)) {
			return;
		}
		epcSetTranslateCookie(lang);
		var select = document.getElementById("epc_native_translate_select");
		if (select) {
			select.value = lang;
		}
		var attempts = 0;
		(function applyWhenReady() {
			var combo = document.querySelector("#google_translate_element select.goog-te-combo");
			if (combo) {
				combo.value = lang;
				combo.dispatchEvent(new Event("change"));
				window.setTimeout(function () {
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
		var combo = document.querySelector("#google_translate_element select.goog-te-combo");
		if (!combo) {
			if ((attempts || 0) < 30) {
				setTimeout(function () {
					epcAttachGoogleTranslateChange((attempts || 0) + 1);
				}, 250);
			}
			return;
		}
		combo.addEventListener("change", function () {
			try {
				localStorage.setItem(epcTranslateManualKey, this.value || "en");
			} catch (e) {}
		});
	}

	function epcSupportedTranslateLanguage(lang) {
		var select = document.getElementById("epc_native_translate_select");
		if (!select || !lang) {
			return "";
		}
		var normalized = String(lang).trim();
		if (!normalized) {
			return "";
		}
		normalized = normalized.replace("_", "-");
		var base = normalized.split("-")[0].toLowerCase();
		var aliases = {
			he: "iw",
			jv: "jw",
			zh: "zh-CN"
		};
		var candidates = [normalized, base, aliases[base]];
		for (var i = 0; i < candidates.length; i++) {
			if (candidates[i] && select.querySelector('option[value="' + candidates[i] + '"]')) {
				return candidates[i];
			}
		}
		return "";
	}

	function epcLanguageFromIpApiLanguages(languages) {
		var parts = String(languages || "").split(",");
		for (var i = 0; i < parts.length; i++) {
			var lang = epcSupportedTranslateLanguage(parts[i]);
			if (lang) {
				return lang;
			}
		}
		return "";
	}

	function epcLanguageForCountry(countryCode, languages) {
		var country = String(countryCode || "").toUpperCase();
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
		return map[country] || epcLanguageFromIpApiLanguages(languages) || "en";
	}

	function epcBrowserLanguage() {
		var list = navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language || navigator.userLanguage || ""];
		for (var i = 0; i < list.length; i++) {
			var lang = epcSupportedTranslateLanguage(list[i]);
			if (lang) {
				return lang;
			}
		}
		return "";
	}

	function epcReadManualLanguage() {
		try {
			return localStorage.getItem(epcTranslateManualKey) || "";
		} catch (e) {
			return "";
		}
	}

	function epcSaveAutoLanguage(country, lang) {
		try {
			localStorage.setItem(epcTranslateAutoKey, JSON.stringify({
				country: country || "",
				lang: lang || "",
				time: Date.now()
			}));
		} catch (e) {}
	}

	function epcFetchJson(url) {
		return fetch(url, { cache: "no-store" }).then(function (response) {
			return response.ok ? response.json() : null;
		});
	}

	function epcDetectVisitorCountry() {
		var hint = epcCfCountryHint();
		if (hint) {
			return Promise.resolve({ country: hint, languages: "", source: "cf-ipcountry" });
		}
		return epcFetchJson("https://ipapi.co/json/")
			.then(function (data) {
				if (data && data.country_code) {
					return {
						country: data.country_code,
						languages: data.languages || "",
						source: "ipapi"
					};
				}
				return epcFetchJson("https://ipwho.is/")
					.then(function (fallback) {
						return fallback && fallback.country_code ? {
							country: fallback.country_code,
							languages: "",
							source: "ipwhois"
						} : null;
					});
			});
	}

	function epcDetectVisitorCountryWithRetry(attempts) {
		return epcDetectVisitorCountry().then(function (data) {
			if (data && data.country) {
				return data;
			}
			if ((attempts || 0) < 2) {
				return new Promise(function (resolve) {
					window.setTimeout(resolve, 700);
				}).then(function () {
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
			epcTranslateStatus("Language set manually: " + manualLanguage);
			return;
		}
		if (currentLanguage !== "en") {
			epcTranslateStatus("Auto language active: " + currentLanguage);
			return;
		}
		epcDetectVisitorCountryWithRetry(0)
			.then(function (data) {
				var lang = "";
				var country = "";
				if (data && data.country) {
					country = String(data.country).toUpperCase();
					lang = epcLanguageForCountry(country, data.languages);
				}
				if (!lang || lang === "en") {
					lang = epcBrowserLanguage() || "en";
				}
				epcSaveAutoLanguage(country, lang);
				if (lang && lang !== "en") {
					var select = document.getElementById("epc_native_translate_select");
					if (select) {
						select.value = lang;
					}
					epcTranslateStatus("Auto language: " + (country ? country + " -> " : "") + lang);
					epcApplyAutoTranslate(lang);
				} else {
					epcTranslateStatus("Auto language: English");
				}
			})
			.catch(function () {
				var lang = epcBrowserLanguage() || "en";
				if (lang !== "en") {
					epcTranslateStatus("Auto language from browser: " + lang);
					epcApplyAutoTranslate(lang);
				} else {
					epcTranslateStatus("Auto language: English");
				}
			});
	}

	function epcInitNativeTranslateSelect() {
		var select = document.getElementById("epc_native_translate_select");
		if (!select) {
			return;
		}
		var preferred = epcCmsCurrentLang() || epcTranslateCookieLanguage() || "en";
		if (select.querySelector('option[value="' + preferred + '"]')) {
			select.value = preferred;
		} else {
			select.value = epcTranslateCookieLanguage() || "en";
		}
		if (select.value && select.value !== "en") {
			epcTranslateStatus("Language: " + select.value);
		}
		select.addEventListener("change", function () {
			epcApplyNativeTranslate(this.value);
		});
		// Empty "auto" sentinel: if manual was English-only cancel, still allow GT cookie restore.
		var cookieLang = epcTranslateCookieLanguage();
		if (cookieLang && cookieLang !== "en" && select.querySelector('option[value="' + cookieLang + '"]')) {
			select.value = cookieLang;
			epcTranslateStatus("Language: " + cookieLang);
		}
		var cmsLang = epcCmsCurrentLang();
		var active = epcCmsActiveLangs();
		if (cmsLang && cmsLang !== "en" && active.indexOf(cmsLang) !== -1) {
			epcTranslateStatus("Language: " + cmsLang);
			return;
		}
		epcAutoTranslateByCountry();
	}

	function googleTranslateElementInit() {
		if (!window.google || !window.google.translate) {
			return;
		}
		new google.translate.TranslateElement({
			pageLanguage: "en",
			layout: google.translate.TranslateElement.InlineLayout.HORIZONTAL,
			autoDisplay: false
		}, "google_translate_element");
		epcInitNativeTranslateSelect();
		epcAttachGoogleTranslateChange(0);
	}
	window.googleTranslateElementInit = googleTranslateElementInit;

	function epcLoadGoogleTranslate() {
		if (window.__epcGoogleTranslateLoading) { return; }
		window.__epcGoogleTranslateLoading = true;
		var s = document.createElement("script");
		s.async = true;
		s.src = "//translate.google.com/translate_a/element.js?cb=googleTranslateElementInit";
		document.head.appendChild(s);
	}

	function epcBoot() {
		if (window.requestIdleCallback) {
			requestIdleCallback(epcLoadGoogleTranslate, { timeout: 3500 });
		} else if (document.readyState === "complete") {
			setTimeout(epcLoadGoogleTranslate, 800);
		} else {
			window.addEventListener("load", function () { setTimeout(epcLoadGoogleTranslate, 800); }, { once: true });
		}
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", epcBoot);
	} else {
		epcBoot();
	}
})();
