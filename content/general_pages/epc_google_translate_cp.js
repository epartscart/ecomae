(function () {
	"use strict";

	function epcCpConfig(name, fallback) {
		var root = document.getElementById("epc_cp_translate_root") || document.querySelector(".epc-cp-translate, .epc-cp-translate-nav");
		if (!root) return fallback || "";
		return root.getAttribute(name) || fallback || "";
	}

	var epcCpTenantDefaultLang = "";
	var epcCpAcceptLanguageHint = "";
	var epcTranslateManualKey = "epcTranslateManualLanguage";
	var epcTranslateAutoAppliedKey = "epcTranslateAutoAppliedLanguage";

	function epcTranslateCookieLanguage() {
		var match = document.cookie.match(/(?:^|;\s*)googtrans=([^;]+)/);
		if (!match) {
			var epcMatch = document.cookie.match(/(?:^|;\s*)epc_lang=([^;]+)/);
			if (epcMatch) return decodeURIComponent(epcMatch[1]) || "en";
			return "en";
		}
		var parts = decodeURIComponent(match[1]).split("/");
		return parts.length >= 3 && parts[2] ? parts[2] : "en";
	}

	function epcClearTranslateCookie() {
		var hostParts = window.location.hostname.split(".");
		var domains = ["", window.location.hostname];
		if (hostParts.length > 2) domains.push("." + hostParts.slice(-2).join("."));
		for (var i = 0; i < domains.length; i++) {
			document.cookie = "googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/" + (domains[i] ? "; domain=" + domains[i] : "");
			document.cookie = "epc_lang=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/" + (domains[i] ? "; domain=" + domains[i] : "");
		}
	}

	function epcSetTranslateCookie(lang) {
		var hostParts = window.location.hostname.split(".");
		var domains = ["", window.location.hostname];
		if (hostParts.length > 2) domains.push("." + hostParts.slice(-2).join("."));
		for (var i = 0; i < domains.length; i++) {
			document.cookie = "googtrans=/en/" + lang + "; path=/; max-age=31536000; SameSite=Lax" + (domains[i] ? "; domain=" + domains[i] : "");
			document.cookie = "epc_lang=" + encodeURIComponent(lang) + "; path=/; max-age=31536000; SameSite=Lax" + (domains[i] ? "; domain=" + domains[i] : "");
		}
	}

	function epcSupportedTranslateLanguage(lang) {
		var select = document.getElementById("epc_cp_native_translate_select");
		if (!select || !lang) return "";
		var normalized = String(lang).trim().replace("_", "-");
		var base = normalized.split("-")[0].toLowerCase();
		var aliases = { he: "he", iw: "he", jv: "jv", jw: "jv", zh: "zh-CN" };
		var candidates = [normalized, base, aliases[base]];
		for (var i = 0; i < candidates.length; i++) {
			if (candidates[i] && select.querySelector('option[value="' + candidates[i] + '"]')) return candidates[i];
		}
		return "";
	}

	function epcLanguageFromHint(languages) {
		var parts = String(languages || "").split(",");
		for (var i = 0; i < parts.length; i++) {
			var lang = epcSupportedTranslateLanguage(parts[i]);
			if (lang) return lang;
		}
		return "";
	}

	function epcLanguageForCountry(countryCode, languages) {
		var country = String(countryCode || "").toUpperCase();
		var map = {
AE:'ar',SA:'ar',QA:'ar',KW:'ar',BH:'ar',OM:'ar',JO:'ar',LB:'ar',EG:'ar',IQ:'ar',MA:'ar',DZ:'ar',TN:'ar',
			FR:'fr',BE:'fr',CH:'fr',DE:'de',AT:'de',ES:'es',MX:'es',IT:'it',PT:'pt',BR:'pt',RU:'ru',TR:'tr',
			IN:'hi',PK:'ur',CN:'zh-CN',HK:'zh-CN',TW:'zh-CN',NL:'nl',PL:'pl',UA:'uk',IR:'fa',IL:'he',BD:'bn'
		};
		return map[country] || epcLanguageFromHint(languages) || "";
	}

	function epcBrowserLanguage() {
		var list = navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language || ""];
		for (var i = 0; i < list.length; i++) {
			var lang = epcSupportedTranslateLanguage(list[i]);
			if (lang) return lang;
		}
		return epcLanguageFromHint(epcCpAcceptLanguageHint);
	}

	function epcReadManualLanguage() {
		try { return localStorage.getItem(epcTranslateManualKey) || ""; } catch (e) { return ""; }
	}

	function epcApplyNativeTranslate(lang) {
		lang = String(lang || "en").toLowerCase();
		try {
			localStorage.setItem(epcTranslateManualKey, lang);
			sessionStorage.removeItem(epcTranslateAutoAppliedKey);
		} catch (e) {}
		if (lang === "en") {
			epcClearTranslateCookie();
			window.location.reload();
			return;
		}
		epcSetTranslateCookie(lang);
		var combo = document.querySelector("#google_translate_element_cp select.goog-te-combo");
		if (combo) { combo.value = lang; combo.dispatchEvent(new Event("change")); }
		window.setTimeout(function () { window.location.reload(); }, 500);
	}

	function epcApplyAutoTranslate(lang) {
		if (!lang || lang === "en") return;
		try {
			if (sessionStorage.getItem(epcTranslateAutoAppliedKey) === lang) return;
			sessionStorage.setItem(epcTranslateAutoAppliedKey, lang);
		} catch (e) {}
		epcSetTranslateCookie(lang);
		var select = document.getElementById("epc_cp_native_translate_select");
		if (select) select.value = lang;
		var attempts = 0;
		(function applyWhenReady() {
			var combo = document.querySelector("#google_translate_element_cp select.goog-te-combo");
			if (combo) {
				combo.value = lang;
				combo.dispatchEvent(new Event("change"));
				window.setTimeout(function () { window.location.reload(); }, 600);
				return;
			}
			attempts++;
			if (attempts < 24) { window.setTimeout(applyWhenReady, 250); return; }
			window.location.reload();
		})();
	}

	function epcFetchJson(url) {
		return fetch(url, { cache: "no-store" }).then(function (r) { return r.ok ? r.json() : null; });
	}

	function epcCfCountryHint() {
		var c = String(epcCpConfig("data-cf-country", "")).toUpperCase();
		if (!c || c === "XX" || c === "T1") return "";
		return c;
	}

	function epcDetectVisitorCountry() {
		var hint = epcCfCountryHint();
		if (hint) return Promise.resolve({ country: hint, languages: "" });
		return epcFetchJson("https://ipapi.co/json/").then(function (data) {
			if (data && data.country_code) return { country: data.country_code, languages: data.languages || "" };
			return epcFetchJson("https://ipwho.is/").then(function (fb) {
				return fb && fb.country_code ? { country: fb.country_code, languages: "" } : null;
			});
		});
	}

	function epcAutoTranslateInit() {
		epcCpTenantDefaultLang = epcCpConfig("data-tenant-default-lang", "en");
		epcCpAcceptLanguageHint = epcCpConfig("data-accept-language", "");
		var currentLanguage = epcTranslateCookieLanguage();
		var manualLanguage = epcReadManualLanguage();
		var select = document.getElementById("epc_cp_native_translate_select");
		if (select) select.value = currentLanguage || "en";
		if (manualLanguage) return;
		if (currentLanguage !== "en") return;

		var tenantDefault = epcSupportedTranslateLanguage(epcCpTenantDefaultLang);
		if (tenantDefault && tenantDefault !== "en") {
			epcApplyAutoTranslate(tenantDefault);
			return;
		}

		epcDetectVisitorCountry().then(function (data) {
			var lang = "";
			if (data && data.country) lang = epcLanguageForCountry(data.country, data.languages);
			if (!lang || lang === "en") lang = epcBrowserLanguage() || "en";
			if (lang && lang !== "en") epcApplyAutoTranslate(lang);
		}).catch(function () {
			var lang = epcBrowserLanguage() || "en";
			if (lang !== "en") epcApplyAutoTranslate(lang);
		});
	}

	function epcInitNativeTranslateSelect() {
		var select = document.getElementById("epc_cp_native_translate_select");
		if (!select) return;
		select.addEventListener("change", function () {
			try { localStorage.setItem(epcTranslateManualKey, this.value); } catch (e) {}
			epcApplyNativeTranslate(this.value);
		});
		epcAutoTranslateInit();
	}

	window.epcCpGoogleTranslateElementInit = function () {
		if (!window.google || !window.google.translate) return;
		new google.translate.TranslateElement({
			pageLanguage: "en",
			layout: google.translate.TranslateElement.InlineLayout.HORIZONTAL,
			autoDisplay: false
		}, "google_translate_element_cp");
		epcInitNativeTranslateSelect();
	};

	function epcLoad() {
		if (window.__epcCpGoogleTranslateLoading) return;
		window.__epcCpGoogleTranslateLoading = true;
		var s = document.createElement("script");
		s.async = true;
		s.src = "//translate.google.com/translate_a/element.js?cb=epcCpGoogleTranslateElementInit";
		document.head.appendChild(s);
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", epcLoad);
	} else {
		epcLoad();
	}
})();
