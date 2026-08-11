(function () {
	"use strict";

	// Sticky manual choice (localStorage + epc_lang cookie). Auto IP language only when unset.
	var epcTranslateManualKey = "epcTranslateManualLanguage";
	var epcTranslateAutoKey = "epcTranslateAutoLanguage";
	var epcTranslateAutoAppliedKey = "epcTranslateAutoAppliedLanguage";

	function epcRoot() {
		return document.getElementById("epc_google_translate_root") || document.querySelector(".epc-google-translate-top");
	}

	function epcCmsActiveLangs() {
		var root = epcRoot();
		var raw = root ? (root.getAttribute("data-cms-langs") || "en") : "en";
		if (window.epcCmsActiveLangs && window.epcCmsActiveLangs.length) {
			return window.epcCmsActiveLangs.map(function (s) { return String(s).toLowerCase(); });
		}
		return raw.split(",").map(function (s) { return s.trim().toLowerCase(); }).filter(Boolean);
	}

	function epcCmsCurrentLang() {
		var root = epcRoot();
		if (root && root.getAttribute("data-cms-lang")) {
			return String(root.getAttribute("data-cms-lang")).toLowerCase();
		}
		if (typeof window.epcCmsCurrentLang === "string" && window.epcCmsCurrentLang) {
			return String(window.epcCmsCurrentLang).toLowerCase();
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

	function epcCookieDomains() {
		var hostParts = window.location.hostname.split(".");
		var domains = ["", window.location.hostname];
		if (hostParts.length > 2) {
			domains.push("." + hostParts.slice(-2).join("."));
		}
		return domains;
	}

	function epcClearTranslateCookie() {
		var domains = epcCookieDomains();
		for (var i = 0; i < domains.length; i++) {
			var d = domains[i] ? "; domain=" + domains[i] : "";
			document.cookie = "googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/" + d;
		}
	}

	function epcClearManualLanguageCookie() {
		var domains = epcCookieDomains();
		for (var i = 0; i < domains.length; i++) {
			var d = domains[i] ? "; domain=" + domains[i] : "";
			document.cookie = "epc_lang=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/" + d;
		}
	}

	/** Google Translate cookie only — never marks a language as user-chosen. */
	function epcSetTranslateCookie(lang) {
		lang = String(lang || "en").toLowerCase();
		var domains = epcCookieDomains();
		for (var i = 0; i < domains.length; i++) {
			var d = domains[i] ? "; domain=" + domains[i] : "";
			if (lang === "en") {
				document.cookie = "googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/" + d;
			} else {
				document.cookie = "googtrans=/en/" + lang + "; path=/; max-age=31536000; SameSite=Lax" + d;
			}
		}
	}

	function epcTranslateCookieLanguage() {
		var match = document.cookie.match(/(?:^|;\s*)googtrans=([^;]+)/);
		if (match) {
			var parts = decodeURIComponent(match[1]).split("/");
			if (parts.length >= 3 && parts[2]) {
				return parts[2];
			}
		}
		return "";
	}

	function epcReadManualLanguage() {
		try {
			var stored = localStorage.getItem(epcTranslateManualKey) || "";
			if (stored) {
				return String(stored).toLowerCase();
			}
		} catch (e) {}
		// epc_lang is written only by manual picks (survives when localStorage is blocked).
		var epcMatch = document.cookie.match(/(?:^|;\s*)epc_lang=([^;]+)/);
		if (epcMatch) {
			return decodeURIComponent(epcMatch[1] || "").toLowerCase();
		}
		return "";
	}

	function epcWriteManualLanguage(lang) {
		lang = String(lang || "en").toLowerCase();
		try {
			localStorage.setItem(epcTranslateManualKey, lang);
			sessionStorage.removeItem(epcTranslateAutoAppliedKey);
		} catch (e) {}
		var domains = epcCookieDomains();
		for (var i = 0; i < domains.length; i++) {
			var d = domains[i] ? "; domain=" + domains[i] : "";
			document.cookie = "epc_lang=" + encodeURIComponent(lang) + "; path=/; max-age=31536000; SameSite=Lax" + d;
		}
		epcSetTranslateCookie(lang);
	}

	/** ASP.NET app paths must not be rewritten to /en/storefront/... */
	function epcIsAspNetAppPath(path) {
		path = String(path || "/");
		return /^\/(storefront|cp|erp|bos|marketing|platform-assets|_framework|auth)\b/i.test(path);
	}

	function epcCmsLangNavigate(lang) {
		lang = String(lang || "").toLowerCase();
		var active = epcCmsActiveLangs();
		if (!lang || active.indexOf(lang) === -1) {
			return false;
		}
		var path = window.location.pathname || "/";
		if (epcIsAspNetAppPath(path)) {
			return false;
		}
		if (typeof window.lang_selected === "function") {
			window.lang_selected(lang);
			return true;
		}
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "lang=" + lang + "; path=/; expires=" + date.toUTCString();
		var search = window.location.search || "";
		var hash = window.location.hash || "";
		var parts = path.split("/");
		if (parts.length > 1 && /^[a-z]{2}(?:-[a-zA-Z]+)?$/i.test(parts[1] || "")) {
			parts[1] = lang;
			window.location.assign(parts.join("/") + search + hash);
			return true;
		}
		// Only prepend /{lang} for classic PHP multilang roots — never for /storefront/*.
		if (path === "/" || path === "") {
			window.location.assign("/" + lang + "/" + search + hash);
			return true;
		}
		return false;
	}

	function epcApplyNativeTranslate(lang) {
		lang = String(lang || "en").toLowerCase();
		epcWriteManualLanguage(lang);
		epcTranslateStatus("Language set manually: " + lang);

		if (lang === "en") {
			epcClearTranslateCookie();
			epcSetTranslateCookie("en");
			// Stay on the same ASP.NET URL (e.g. /storefront/login).
			if (!epcCmsLangNavigate("en")) {
				window.location.reload();
			}
			return;
		}

		if (epcCmsLangNavigate(lang)) {
			return;
		}
		var combo = document.querySelector("#google_translate_element select.goog-te-combo");
		if (combo) {
			combo.value = lang;
			combo.dispatchEvent(new Event("change"));
		}
		window.setTimeout(function () {
			window.location.reload();
		}, 400);
	}

	function epcApplyAutoTranslate(lang) {
		if (!lang || lang === "en") {
			return;
		}
		// Never auto-override a sticky manual choice (including manual English).
		if (epcReadManualLanguage()) {
			return;
		}
		try {
			if (sessionStorage.getItem(epcTranslateAutoAppliedKey) === lang) {
				return;
			}
			sessionStorage.setItem(epcTranslateAutoAppliedKey, lang);
		} catch (e) {}
		epcSetTranslateCookie(lang);
		var select = document.getElementById("epc_native_translate_select");
		if (select) {
			select.value = lang;
		}
		if (epcCmsLangNavigate(lang)) {
			return;
		}
		var attempts = 0;
		(function applyWhenReady() {
			var combo = document.querySelector("#google_translate_element select.goog-te-combo");
			if (combo) {
				combo.value = lang;
				combo.dispatchEvent(new Event("change"));
				window.setTimeout(function () {
					window.location.reload();
				}, 500);
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
			epcWriteManualLanguage(this.value || "en");
		});
	}

	function epcSupportedTranslateLanguage(lang) {
		var select = document.getElementById("epc_native_translate_select");
		if (!select || !lang) {
			return "";
		}
		var normalized = String(lang).trim().replace("_", "-");
		if (!normalized) {
			return "";
		}
		var base = normalized.split("-")[0].toLowerCase();
		var aliases = { he: "iw", jv: "jw", zh: "zh-CN" };
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
			// Explicit English markets — do NOT fall through to browser (fixes US→Afrikaans).
			US: "en", GB: "en", AU: "en", NZ: "en", IE: "en", CA: "en",
			// Arabic / GCC
			AE: "ar", SA: "ar", QA: "ar", KW: "ar", BH: "ar", OM: "ar", JO: "ar", LB: "ar",
			EG: "ar", IQ: "ar", MA: "ar", DZ: "ar", TN: "ar", LY: "ar", YE: "ar", SY: "ar",
			// South Africa → Afrikaans only when IP is ZA (not US browser quirk)
			ZA: "af",
			FR: "fr", BE: "fr", CH: "fr", LU: "fr", MC: "fr",
			DE: "de", AT: "de", LI: "de",
			ES: "es", MX: "es", AR: "es", CL: "es", CO: "es", PE: "es", VE: "es", UY: "es", PY: "es", BO: "es", EC: "es",
			IT: "it", SM: "it", VA: "it",
			PT: "pt", BR: "pt",
			RU: "ru", BY: "ru", KZ: "ru", KG: "ru", TJ: "ru",
			TR: "tr", CY: "tr",
			IN: "hi",
			PK: "ur",
			CN: "zh-CN", HK: "zh-CN", MO: "zh-CN", SG: "zh-CN", TW: "zh-CN",
			NL: "nl", DK: "da", SE: "sv", NO: "no", FI: "fi",
			PL: "pl", CZ: "cs", SK: "sk", HU: "hu", RO: "ro", BG: "bg",
			GR: "el", RS: "sr", HR: "hr", SI: "sl", UA: "uk",
			TH: "th", VN: "vi", ID: "id", MY: "ms", KR: "ko", JP: "ja",
			IR: "fa", IL: "iw", BD: "bn", LK: "si", NP: "ne"
		};
		if (Object.prototype.hasOwnProperty.call(map, country)) {
			return map[country];
		}
		return epcLanguageFromIpApiLanguages(languages) || "";
	}

	function epcBrowserLanguage() {
		var list = navigator.languages && navigator.languages.length
			? navigator.languages
			: [navigator.language || navigator.userLanguage || ""];
		for (var i = 0; i < list.length; i++) {
			var lang = epcSupportedTranslateLanguage(list[i]);
			if (lang) {
				return lang;
			}
		}
		return "";
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
		var manualLanguage = epcReadManualLanguage();
		if (manualLanguage) {
			var select = document.getElementById("epc_native_translate_select");
			if (select && select.querySelector('option[value="' + manualLanguage + '"]')) {
				select.value = manualLanguage;
			}
			epcTranslateStatus("Language set manually: " + manualLanguage);
			// Re-apply non-English manual GT if cookie was cleared.
			if (manualLanguage !== "en" && epcTranslateCookieLanguage() !== manualLanguage) {
				epcApplyNativeTranslate(manualLanguage);
			}
			return;
		}

		var currentLanguage = epcTranslateCookieLanguage();
		// Keep a prior auto language only until geo can confirm; EN markets clear stale af/etc.
		var stickyAutoCookie = currentLanguage && currentLanguage !== "en";

		epcDetectVisitorCountryWithRetry(0)
			.then(function (data) {
				// Bail if user picked a language while the geo lookup was in flight.
				if (epcReadManualLanguage()) {
					epcTranslateStatus("Language set manually: " + epcReadManualLanguage());
					return;
				}
				var lang = "";
				var country = "";
				var countryMapped = false;
				if (data && data.country) {
					country = String(data.country).toUpperCase();
					lang = epcLanguageForCountry(country, data.languages);
					countryMapped = true;
				}
				// Only use browser language when country is unknown — never override EN markets.
				if (!countryMapped) {
					lang = stickyAutoCookie ? currentLanguage : (epcBrowserLanguage() || "en");
				} else if (!lang) {
					lang = "en";
				}
				// Country says English → drop leftover googtrans (common US→Afrikaans leftover).
				if (lang === "en" && stickyAutoCookie) {
					epcClearTranslateCookie();
					epcSetTranslateCookie("en");
					var selectEn = document.getElementById("epc_native_translate_select");
					if (selectEn) {
						selectEn.value = "en";
					}
					epcTranslateStatus("Auto language: English" + (country ? " (" + country + ")" : ""));
					window.location.reload();
					return;
				}
				epcSaveAutoLanguage(country, lang);
				if (lang && lang !== "en") {
					var select = document.getElementById("epc_native_translate_select");
					if (select) {
						select.value = lang;
					}
					epcTranslateStatus("Auto language: " + (country ? country + " → " : "") + lang);
					if (!stickyAutoCookie || currentLanguage !== lang) {
						epcApplyAutoTranslate(lang);
					}
				} else {
					epcTranslateStatus("Auto language: English" + (country ? " (" + country + ")" : ""));
				}
			})
			.catch(function () {
				if (epcReadManualLanguage()) {
					return;
				}
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

		var manual = epcReadManualLanguage();
		var preferred = manual || epcTranslateCookieLanguage() || epcCmsCurrentLang() || "en";
		if (select.querySelector('option[value="' + preferred + '"]')) {
			select.value = preferred;
		} else {
			select.value = "en";
		}

		select.addEventListener("change", function () {
			epcApplyNativeTranslate(this.value || "en");
		});

		if (manual) {
			epcTranslateStatus("Language set manually: " + manual);
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
