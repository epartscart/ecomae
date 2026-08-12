/**
 * PHP part_search warehouse parity helpers for ASP.NET StorefrontSearchApp.
 * Visual classes match professional shell; this file only wires behavior.
 */
(function () {
	"use strict";

	function esc(s) {
		return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
			return ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c];
		});
	}

	function table() { return document.getElementById("all_table_products"); }

	function offerRows() {
		var t = table();
		if (!t) return [];
		return Array.prototype.slice.call(t.querySelectorAll("tbody tr[data-offer-key]"));
	}

	function pricesVisible() {
		var t = table();
		return !t || t.getAttribute("data-prices-visible") === "1";
	}

	function uniqueSorted(values) {
		var map = Object.create(null);
		values.forEach(function (v) {
			var s = String(v || "").trim();
			if (s) map[s] = 1;
		});
		return Object.keys(map).sort(function (a, b) { return a.localeCompare(b); });
	}

	function rebuildFilterOptions() {
		var mfrBox = document.getElementById("epc_filter_manufacturer_options");
		var storBox = document.getElementById("epc_filter_storage_options");
		if (!mfrBox || !storBox) return;
		var rows = offerRows();
		var mfrs = uniqueSorted(rows.map(function (tr) { return tr.getAttribute("data-manufacturer") || ""; }));
		var storages = uniqueSorted(rows.map(function (tr) { return tr.getAttribute("data-storage") || ""; }));
		function fill(box, values, prefix) {
			var checked = Object.create(null);
			box.querySelectorAll('input[type="checkbox"]:checked').forEach(function (cb) {
				checked[cb.value] = 1;
			});
			if (!values.length) {
				box.innerHTML = '<span class="epc-fitment-message">Waiting for offers…</span>';
				return;
			}
			box.innerHTML = values.map(function (v, i) {
				var id = prefix + "_" + i;
				var isChecked = Object.keys(checked).length === 0 || checked[v];
				return '<input class="css-checkbox" type="checkbox" id="' + id + '" value="' + esc(v) + '"' +
					(isChecked ? " checked" : "") + '>' +
					'<label class="css-label" for="' + id + '">' + esc(v) + '</label>';
			}).join("");
			box.querySelectorAll('input[type="checkbox"]').forEach(function (cb) {
				cb.addEventListener("change", applyFilters);
			});
		}
		fill(mfrBox, mfrs, "epc_mfr");
		fill(storBox, storages.length ? storages : ["—"], "epc_stor");
	}

	function selectedValues(boxId) {
		var box = document.getElementById(boxId);
		if (!box) return [];
		return Array.prototype.slice.call(box.querySelectorAll('input[type="checkbox"]:checked')).map(function (cb) {
			return cb.value;
		});
	}

	function applyFilters() {
		var mfrs = selectedValues("epc_filter_manufacturer_options");
		var storages = selectedValues("epc_filter_storage_options");
		var minPrice = parseFloat((document.getElementById("epc_filter_price_min") || {}).value);
		var maxPrice = parseFloat((document.getElementById("epc_filter_price_max") || {}).value);
		var minTerm = parseFloat((document.getElementById("epc_filter_term_min") || {}).value);
		var maxTerm = parseFloat((document.getElementById("epc_filter_term_max") || {}).value);
		var inStockOnly = !!(document.getElementById("epc_filter_instock") || {}).checked;
		var visible = 0;
		offerRows().forEach(function (tr) {
			var mfr = tr.getAttribute("data-manufacturer") || "";
			var stor = tr.getAttribute("data-storage") || "—";
			var price = parseFloat(tr.getAttribute("data-price") || "0");
			var term = parseFloat(tr.getAttribute("data-term") || "0");
			var exist = parseInt(tr.getAttribute("data-exist") || "0", 10);
			var ok = true;
			if (mfrs.length && mfrs.indexOf(mfr) === -1) ok = false;
			if (storages.length && storages.indexOf(stor) === -1 && storages.indexOf("—") === -1) ok = false;
			if (!isNaN(minPrice) && price < minPrice) ok = false;
			if (!isNaN(maxPrice) && price > maxPrice) ok = false;
			if (!isNaN(minTerm) && term < minTerm) ok = false;
			if (!isNaN(maxTerm) && term > maxTerm) ok = false;
			if (inStockOnly && !(exist > 0)) ok = false;
			tr.setAttribute("data-filtered-out", ok ? "0" : "1");
			if (ok) visible += 1;
		});
		var status = document.getElementById("epc_filter_status");
		if (status) status.textContent = visible + " offer(s) visible";
		["genuine", "aftermarket"].forEach(function (kind) {
			var cell = document.querySelector('td.epc-part-type-split[data-section="' + kind + '"]');
			if (!cell) return;
			var n = document.querySelectorAll('tr.epc-part-type-row--' + kind + ':not([data-filtered-out="1"])').length;
			var count = cell.querySelector(".epc-part-type-count");
			if (count) count.textContent = "(" + n + ")";
			cell.closest("tr").style.display = n === 0 ? "none" : "";
		});
	}

	function resetFilters() {
		["epc_filter_price_min", "epc_filter_price_max", "epc_filter_term_min", "epc_filter_term_max"].forEach(function (id) {
			var el = document.getElementById(id);
			if (el) el.value = "";
		});
		var instock = document.getElementById("epc_filter_instock");
		if (instock) instock.checked = false;
		rebuildFilterOptions();
		applyFilters();
	}

	function toggleFilter() {
		var body = document.getElementById("filter_div_style_body");
		var pos = document.getElementById("filter_position");
		var link = document.getElementById("filter_div_a_text");
		if (!body) return;
		var open = body.getAttribute("data-open") !== "0";
		body.style.display = open ? "none" : "block";
		if (pos) pos.style.display = open ? "none" : "block";
		body.setAttribute("data-open", open ? "0" : "1");
		if (link) {
			link.innerHTML = open
				? '<i class="fa fa-arrow-circle-down" aria-hidden="true"></i> Show filter'
				: '<i class="fa fa-arrow-circle-up" aria-hidden="true"></i> Hide filter';
		}
	}

	function compactFitment(value) {
		return String(value || "").replace(/[^A-Za-z0-9]/g, "").toUpperCase();
	}

	function brandsEquivalent(left, right) {
		return compactFitment(left) !== "" && compactFitment(left) === compactFitment(right);
	}

	function umapi(action, params) {
		// ASP.NET-only fitment/catalog — no product .php proxies (PHP deletion-ready).
		var art = (params && (params.article || params.Article)) || "";
		var brand = (params && (params.brand || params.Brand || params.manufacturer)) || "";
		if (String(action || "") === "brands") {
			return fetch("/storefront/search-brands?article=" + encodeURIComponent(art) + "&limit=100", {
				cache: "no-store",
				credentials: "same-origin"
			}).then(function (r) {
				return r.json().catch(function () { return {}; }).then(function (data) {
					if (!r.ok) {
						return Promise.reject(new Error((data && data.message) || ("HTTP " + r.status)));
					}
					var brands = (data && data.brands) || [];
					var rows = brands.map(function (b) {
						return {
							brand: b.brand || b.Brand || "",
							BRAND: b.brand || b.Brand || "",
							manufacturer: b.brand || b.Brand || "",
							article: art,
							DISPLAY_NR: art,
							SEARCH_NUMBER: art,
							ARTICLE: art,
							name: b.name || b.Name || "",
							TITLE: b.name || b.Name || ""
						};
					});
					return { data: rows };
				});
			});
		}
		if (String(action || "") === "analogs" || String(action || "") === "fitment") {
			return fetch(
				"/storefront/fitment?article=" + encodeURIComponent(art)
					+ "&brand=" + encodeURIComponent(brand)
					+ "&language=en",
				{ cache: "no-store", credentials: "same-origin" }
			).then(function (r) {
				return r.json().catch(function () { return {}; }).then(function (data) {
					if (!r.ok && !(data && (data.PC || data.CV || data.Motorcycle))) {
						return Promise.reject(new Error((data && data.message) || ("HTTP " + r.status)));
					}
					return data || {};
				});
			});
		}
		return Promise.reject(new Error("Unknown fitment catalog action."));
	}

	function loadEpartscrossFitmentFallback(article, widget) {
		if (!widget || !article) return;
		widget.className = "epc-fitment-message";
		widget.innerHTML = '<div class="epc-fitment-message">Loading vehicle applicability from cross-reference catalog…</div>';
		var oldScript = document.getElementById("epc-fitment-epartscross-script");
		if (oldScript && oldScript.parentNode) oldScript.parentNode.removeChild(oldScript);
		var script = document.createElement("script");
		script.id = "epc-fitment-epartscross-script";
		script.type = "text/javascript";
		script.async = true;
		script.onerror = function () {
			widget.innerHTML = '<div class="epc-fitment-message">Vehicle fitment is temporarily unavailable. Try again later.</div>';
		};
		var lang = (document.documentElement.getAttribute("lang") || "en").toLowerCase();
		if (lang !== "ru") lang = "en";
		// PHP epartscross fitment widget twin (ASP.NET /storefront proxy, no product PHP).
		script.src = "/storefront/fitment-widget.js?n=" + encodeURIComponent(article)
			+ "&lang=" + encodeURIComponent(lang) + "&_=" + Date.now();
		document.body.appendChild(script);
	}

	function openFitment(article, brand) {
		var panel = document.getElementById("epc-fitment-panel");
		var brandsBox = document.getElementById("epc-fitment-brands");
		var typesBox = document.getElementById("epc-fitment-types");
		var shell = document.getElementById("epc-fitment-widget-shell");
		var widget = document.getElementById("applicability_widget");
		if (!panel || !brandsBox) return;
		if (panel.parentNode !== document.body) {
			document.body.appendChild(panel);
		}
		panel.classList.add("is-open");
		brandsBox.className = "epc-fitment-message";
		brandsBox.textContent = "Loading matching brands from eparts catalog…";
		if (typesBox) typesBox.style.display = "none";
		if (shell) shell.style.display = "none";
		if (widget) {
			widget.className = "epc-fitment-message";
			widget.textContent = "Select a brand/part box to load fitment.";
		}
		umapi("brands", { article: article || "", source: "fitment" })
			.then(function (data) {
				var rows = Array.isArray(data) ? data : (data && data.data) || [];
				if (!rows.length) {
					brandsBox.textContent = "No catalog brands found for this article.";
					// Still try crossbase applicability for the typed article (PHP fallback path).
					if (shell) shell.style.display = "block";
					loadEpartscrossFitmentFallback(article || "", widget);
					return;
				}
				brandsBox.className = "epc-fitment-brand-grid";
				brandsBox.innerHTML = rows.slice(0, 40).map(function (row) {
					var b = row.BRAND || row.brand || row.MANUFACTURER || "";
					var a = row.DISPLAY_NR || row.SEARCH_NUMBER || row.ARTICLE || article || "";
					var title = row.TITLE || row.name || row.DES || "";
					return '<button type="button" class="epc-fitment-brand-card" data-brand="' + esc(b)
						+ '" data-article="' + esc(a) + '">'
						+ "<strong>" + esc(b) + "</strong><br><span style=\"font-weight:500;color:#64748b\">"
						+ esc(a) + "</span>"
						+ (title ? "<br><small>" + esc(title) + "</small>" : "")
						+ "</button>";
				}).join("");
				brandsBox.querySelectorAll("button").forEach(function (btn) {
					btn.addEventListener("click", function () {
						brandsBox.querySelectorAll("button").forEach(function (b) { b.classList.remove("active"); });
						btn.classList.add("active");
						loadFitmentFor(btn.getAttribute("data-article"), btn.getAttribute("data-brand"));
					});
				});
				var preferred = brand || "";
				var match = Array.prototype.slice.call(brandsBox.querySelectorAll("button")).find(function (btn) {
					return brandsEquivalent(btn.getAttribute("data-brand") || "", preferred);
				}) || brandsBox.querySelector("button");
				if (match) match.click();
			})
			.catch(function (err) {
				brandsBox.textContent = (err && err.message) || "Fitment lookup is temporarily unavailable.";
				if (shell) shell.style.display = "block";
				loadEpartscrossFitmentFallback(article || "", widget);
			});
	}

	function loadFitmentFor(article, brand) {
		var typesBox = document.getElementById("epc-fitment-types");
		var shell = document.getElementById("epc-fitment-widget-shell");
		var widget = document.getElementById("applicability_widget");
		if (typesBox) typesBox.style.display = "flex";
		if (shell) shell.style.display = "block";
		if (!widget) return;
		widget.className = "epc-fitment-message";
		widget.textContent = "Loading vehicle applicability…";
		// PHP resolveAndLoadFitment: analogs→article_links, else epartscross widget fallback.
		umapi("analogs", { article: article || "", brand: brand || "", limit: 30, offset: 0, source: "fitment" })
			.then(function (data) {
				var pc = (data && data.PC) || [];
				var cv = (data && data.CV) || [];
				var moto = (data && data.Motorcycle) || [];
				var total = (Array.isArray(pc) ? pc.length : 0)
					+ (Array.isArray(cv) ? cv.length : 0)
					+ (Array.isArray(moto) ? moto.length : 0);
				if (total > 0) {
					window.__epcFitmentPayload = { PC: pc, CV: cv, Motorcycle: moto };
					renderFitmentSection("PC");
					return;
				}
				loadEpartscrossFitmentFallback(article || "", widget);
			})
			.catch(function () {
				loadEpartscrossFitmentFallback(article || "", widget);
			});
	}

	function renderFitmentSection(section) {
		var widget = document.getElementById("applicability_widget");
		var typesBox = document.getElementById("epc-fitment-types");
		var fitment = window.__epcFitmentPayload || {};
		if (typesBox) {
			typesBox.querySelectorAll("button").forEach(function (btn) {
				btn.classList.toggle("active", btn.getAttribute("data-section") === section);
			});
		}
		if (!widget) return;
		var rows = section === "ALL"
			? [].concat(fitment.PC || [], fitment.CV || [], fitment.Motorcycle || [])
			: (fitment[section] || []);
		var total = (fitment.PC || []).length + (fitment.CV || []).length + (fitment.Motorcycle || []).length;
		if (!total) {
			widget.className = "epc-fitment-message";
			widget.textContent = "No vehicle fitment was found in Epart catalog for this part.";
			return;
		}
		if (!rows.length) {
			widget.className = "epc-fitment-message";
			widget.textContent = "No rows in this vehicle type. Choose another tab or All vehicles.";
			return;
		}
		widget.className = "";
		widget.innerHTML = '<div style="overflow:auto"><table class="table table-condensed table-striped" style="font-size:12px"><thead><tr>' +
			"<th>Make</th><th>Model</th><th>Modification</th><th>Years</th><th>Engine / body</th></tr></thead><tbody>" +
			rows.slice(0, 200).map(function (row) {
				var make = row.MAKE || row.MANUFACTURER || "";
				var model = row.MODEL_SERIES || row.MODEL || "";
				var mod = row.PASSENGER_CAR || row.COMMERCIAL_VEHICLE || row.MOTORBIKE || "";
				var years = [row.CI_FROM || "", row.CI_TO || ""].filter(Boolean).join(" - ") || "—";
				var engine = [row.CAPACITY_TECH || row.CAPACITY_LT || "", row.FUEL_TYPE || "", row.BODY_TYPE || ""].filter(Boolean).join(" / ") || "—";
				return "<tr><td>" + esc(make) + "</td><td>" + esc(model) + "</td><td>" + esc(mod)
					+ "</td><td>" + esc(years) + "</td><td>" + esc(engine) + "</td></tr>";
			}).join("") + "</tbody></table></div>";
	}

	function focusCross() {
		var nav = document.querySelector(".epc-seo-cross-refs");
		if (!nav) return;
		nav.classList.add("is-highlight");
		nav.scrollIntoView({ behavior: "smooth", block: "start" });
		window.setTimeout(function () { nav.classList.remove("is-highlight"); }, 2400);
	}

	function qtyValue(aid) {
		var input = document.getElementById("count_need_" + aid);
		var n = input ? parseInt(input.value, 10) : 1;
		return isNaN(n) || n < 1 ? 1 : n;
	}

	function bumpQty(aid, delta, max, min) {
		var input = document.getElementById("count_need_" + aid);
		if (!input) return;
		var n = qtyValue(aid) + delta;
		if (n < (min || 1)) n = min || 1;
		if (max > 0 && n > max) n = max;
		input.value = String(n);
	}

	function productFromRow(tr) {
		return {
			manufacturer: tr.getAttribute("data-manufacturer") || "",
			article: tr.getAttribute("data-article") || "",
			article_show: tr.getAttribute("data-article-show") || "",
			name: tr.getAttribute("data-name") || "",
			exist: parseInt(tr.getAttribute("data-exist") || "0", 10),
			price: parseFloat(tr.getAttribute("data-price") || "0"),
			time_to_exe: tr.getAttribute("data-term") || "0",
			time_to_exe_guaranteed: tr.getAttribute("data-term-g") || tr.getAttribute("data-term") || "0",
			storage: tr.getAttribute("data-storage") || "",
			min_order: parseInt(tr.getAttribute("data-min-order") || "1", 10),
			probability: parseInt(tr.getAttribute("data-probability") || "100", 10),
			office_id: parseInt(tr.getAttribute("data-office-id") || "0", 10),
			storage_id: parseInt(tr.getAttribute("data-storage-id") || "0", 10),
			price_purchase: parseFloat(tr.getAttribute("data-price-purchase") || "0"),
			markup: parseInt(tr.getAttribute("data-markup") || "0", 10),
			json_params: tr.getAttribute("data-json-params") || "",
			product_type: parseInt(tr.getAttribute("data-product-type") || "2", 10),
			check_hash: tr.getAttribute("data-check-hash") || ""
		};
	}

	function postJson(url, body) {
		return fetch(url, {
			method: "POST",
			credentials: "same-origin",
			headers: { "Content-Type": "application/json", "Accept": "application/json" },
			body: JSON.stringify(body || {})
		}).then(function (r) {
			return r.json().catch(function () { return { ok: false, status: false, message: "Bad response" }; });
		});
	}

	function addToCartFromRow(tr) {
		if (!pricesVisible()) {
			window.location.href = "/storefront/login?returnUrl=" + encodeURIComponent(window.location.pathname + window.location.search);
			return;
		}
		var p = productFromRow(tr);
		var aid = tr.getAttribute("data-aid") || "0";
		postJson("/storefront/cart/add", {
			productType: Number(p.product_type || 2),
			manufacturer: p.manufacturer || "",
			article: p.article_show || p.article || "",
			countNeed: qtyValue(aid),
			price: Number(p.price || 0),
			minOrder: Number(p.min_order || 1),
			exist: Number(tr.getAttribute("data-exist") || "0"),
			confirmWrites: true
		}).then(function (data) {
			if (data && data.ok === true && (data.writesBlocked === false || data.status === "written" || data.would_write === true)) {
				if (data.writesBlocked) {
					alert((data && (data.detail || data.message)) || "Cart write is ASP.NET dry-run only for this session.");
					return;
				}
				window.location.href = "/storefront/cart-app";
				return;
			}
			alert((data && (data.detail || data.message || data.error)) || "Could not add to cart. Please log in and try again.");
		}).catch(function () { alert("Could not add to cart."); });
	}

	function addToQuoteFromRow(tr) {
		if (!pricesVisible()) {
			window.location.href = "/storefront/login?returnUrl=" + encodeURIComponent(window.location.pathname + window.location.search);
			return;
		}
		var p = productFromRow(tr);
		var body = {
			productType: Number(p.product_type || 2),
			manufacturer: p.manufacturer || "",
			article: p.article_show || p.article || "",
			countNeed: 1,
			confirmWrites: true
		};
		var url = p.check_hash ? "/storefront/quotes/add-item" : "/storefront/quotes/add-manual";
		postJson(url, body).then(function (data) {
			if (data && data.ok === true && !data.writesBlocked) {
				window.location.href = "/storefront/quotes-app";
				return;
			}
			alert((data && (data.detail || data.message || data.error)) || "Could not add to quote. Please log in.");
		}).catch(function () { alert("Could not add to quote."); });
	}

	function wireRowActions(root) {
		(root || document).querySelectorAll("tr[data-offer-key]").forEach(function (tr) {
			if (tr.getAttribute("data-actions-wired") === "1") return;
			tr.setAttribute("data-actions-wired", "1");
			var aid = tr.getAttribute("data-aid") || "0";
			var fit = tr.querySelector(".epc-btn-fitment");
			if (fit) {
				fit.addEventListener("click", function () {
					openFitment(tr.getAttribute("data-article-show") || tr.getAttribute("data-article"), tr.getAttribute("data-manufacturer"));
				});
			}
			var minus = tr.querySelector(".count_need_minus");
			var plus = tr.querySelector(".count_need_plus");
			var max = parseInt(tr.getAttribute("data-exist") || "0", 10);
			var min = parseInt(tr.getAttribute("data-min-order") || "1", 10);
			if (minus) minus.addEventListener("click", function (e) { e.preventDefault(); bumpQty(aid, -1, max, min); });
			if (plus) plus.addEventListener("click", function (e) { e.preventDefault(); bumpQty(aid, 1, max, min); });
			var cart = tr.querySelector(".epc-btn-cart");
			if (cart) cart.addEventListener("click", function () { addToCartFromRow(tr); });
			var quote = tr.querySelector(".epc-btn-quote");
			if (quote) quote.addEventListener("click", function () { addToQuoteFromRow(tr); });
		});
	}

	function photoKey(brand, article) {
		return String(brand || "").replace(/[^A-Za-z0-9]/g, "").toUpperCase() + "|" +
			String(article || "").replace(/[^A-Za-z0-9]/g, "").toUpperCase();
	}

	function applySearchRowPhoto(brand, article, url) {
		if (!url) return;
		var key = photoKey(brand, article);
		document.querySelectorAll('[data-epc-row-photo="' + key + '"]').forEach(function (cell) {
			if (cell.getAttribute("data-epc-row-photo-loaded") === "1") return;
			cell.setAttribute("data-epc-row-photo-loaded", "1");
			cell.innerHTML = '<button type="button" class="epc-search-row-photo__btn" aria-label="View product photo">' +
				'<img src="' + esc(url) + '" alt="" loading="lazy"></button>';
			var btn = cell.querySelector("button");
			if (btn) {
				btn.style.cursor = "zoom-in";
				btn.onclick = function (ev) {
					ev.preventDefault();
					ev.stopPropagation();
					window.open(url, "_blank", "noopener");
				};
			}
		});
	}

	function loadSearchRowPhotoOnClick(brand, article, triggerEl) {
		if (!brand || !article) return;
		if (triggerEl) {
			if (triggerEl.getAttribute("data-epc-photo-loading") === "1") return;
			triggerEl.setAttribute("data-epc-photo-loading", "1");
			triggerEl.innerHTML = '<i class="fa fa-spinner fa-spin"></i>';
		}
		function finishEmpty() {
			if (!triggerEl) return;
			triggerEl.removeAttribute("data-epc-photo-loading");
			triggerEl.innerHTML = '<i class="fa fa-ban"></i>';
			triggerEl.title = "No photo available";
		}
		var endpoint = "/storefront/product-image?brand=" + encodeURIComponent(brand) +
			"&article=" + encodeURIComponent(article);
		fetch(endpoint, { credentials: "same-origin" })
			.then(function (r) { return r.ok ? r.json() : null; })
			.then(function (data) {
				var url = data && (data.url || data.imageUrl || (data.images && data.images[0] && (data.images[0].url || data.images[0])));
				if (triggerEl) triggerEl.removeAttribute("data-epc-photo-loading");
				if (!url) {
					finishEmpty();
					return;
				}
				applySearchRowPhoto(brand, article, url);
			})
			.catch(function () { finishEmpty(); });
	}

	function bindSearchRowPhotoLoaders(root) {
		var scope = root || document;
		scope.querySelectorAll(".epc-search-row-photo__btn--load").forEach(function (btn) {
			if (btn.getAttribute("data-epc-photo-bound") === "1") return;
			btn.setAttribute("data-epc-photo-bound", "1");
			btn.addEventListener("click", function (ev) {
				ev.preventDefault();
				ev.stopPropagation();
				loadSearchRowPhotoOnClick(
					btn.getAttribute("data-epc-photo-brand") || "",
					btn.getAttribute("data-epc-photo-article") || "",
					btn
				);
			});
		});
	}

	window.epcBindSearchRowPhotoLoaders = bindSearchRowPhotoLoaders;
	window.epcLoadSearchRowPhotoOnClick = loadSearchRowPhotoOnClick;

	function compactToken(s) {
		return String(s || "").replace(/[^A-Za-z0-9]/g, "").toUpperCase();
	}

	function partsHref(brand, article) {
		return "/en/parts/" + encodeURIComponent(String(brand || "").toUpperCase()) + "/" +
			encodeURIComponent(String(article || ""));
	}

	/**
	 * ASP.NET /storefront/cross-search — fill SEO cross nav + count (local CP analogs).
	 * No product .php URLs (PHP remains reference-only under /php-reference/*).
	 */
	function loadAspNetCrossSearch(article, brand) {
		var art = String(article || "").trim();
		var br = String(brand || "").trim();
		if (!art) return Promise.resolve();
		var nav = document.getElementById("epc-cross-base");
		if (nav) {
			art = nav.getAttribute("data-article") || art;
			br = nav.getAttribute("data-brand") || br;
		}
		var url = "/storefront/cross-search?article=" + encodeURIComponent(art) + "&limit=600";
		if (br) url += "&brand=" + encodeURIComponent(br);
		return fetch(url, { credentials: "same-origin" })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data || data.status === false) return data;
				var refs = data.references || [];
				var stock = data.stock || [];
				var total = Number(data.unique_reference_count || data.reference_count || refs.length) || refs.length;
				var countEl = document.getElementById("epc-cross-search-count");
				if (countEl) countEl.textContent = total + " references";
				var btn = document.getElementById("epc-cross-search-btn");
				if (btn) btn.setAttribute("title", "Open " + total + " cross references");
				var list = document.getElementById("epc-cross-ref-list");
				var loading = document.getElementById("epc-cross-loading");
				var more = document.getElementById("epc-cross-more");
				var stockBox = document.getElementById("epc-sf-cross-stock");
				var selfA = compactToken(art);
				var selfB = compactToken(br);
				var stockKeys = Object.create(null);
				stock.forEach(function (s) {
					stockKeys[compactToken(s.brand) + "|" + compactToken(s.article_norm || s.article)] = 1;
				});
				if (stockBox && stock.length) {
					stockBox.hidden = false;
					stockBox.innerHTML = "<strong>Cross references in stock (" + stock.length + ")</strong><ul>" +
						stock.slice(0, 40).map(function (s) {
							var b = s.brand || "";
							var a = s.article || s.article_norm || "";
							return "<li><a href=\"" + partsHref(b, a) + "\">" + esc(b) + " " + esc(a) + "</a>" +
								(s.name ? " <span style=\"color:#64748b\">" + esc(s.name) + "</span>" : "") + "</li>";
						}).join("") + "</ul>";
				}
				if (list) {
					var shown = 0;
					var html = [];
					for (var i = 0; i < refs.length && shown < 80; i++) {
						var ref = refs[i];
						var rb = ref.brand || "";
						var ra = ref.article || ref.article_norm || "";
						var rn = compactToken(ref.article_norm || ra);
						if (!rb || !rn) continue;
						if (rn === selfA && compactToken(rb) === selfB) continue;
						var sameArticleAlias = rn === selfA;
						var key = compactToken(rb) + "|" + rn;
						var inStock = !!stockKeys[key];
						html.push("<li>" +
							"<a href=\"" + partsHref(rb, ra) + "\">" + esc(rb) + " " + esc(ra) + "</a> " +
							"<span style=\"color:#64748b\">(" +
							(sameArticleAlias ? "related manufacturer" : ("part number " + esc(ra))) +
							")</span> " +
							(inStock
								? "<span class=\"epc-avail-yes\">In stock</span>"
								: "<span class=\"epc-avail-no\">Not in stock</span>") +
							"</li>");
						shown++;
					}
					list.innerHTML = html.length
						? html.join("")
						: "<li style=\"list-style:none;color:#64748b;\">No cross references found for this article.</li>";
					if (more) {
						if (total > shown) {
							more.style.display = "block";
							more.textContent = "Showing " + shown + " of " + total.toLocaleString() +
								" unique crosses (full network via cross search).";
						} else {
							more.style.display = "none";
						}
					}
				}
				if (loading) loading.style.display = "none";
				return data;
			})
			.catch(function () {
				var loading = document.getElementById("epc-cross-loading");
				if (loading) {
					loading.textContent = "Cross search unavailable — showing local manufacturer aliases only.";
				}
			});
	}

	window.epcLoadAspNetCrossSearch = loadAspNetCrossSearch;

	window.epcWarehouseParity = {
		rebuildFilterOptions: rebuildFilterOptions,
		applyFilters: applyFilters,
		wireRowActions: wireRowActions,
		openFitment: openFitment,
		bindSearchRowPhotoLoaders: bindSearchRowPhotoLoaders,
		loadAspNetCrossSearch: loadAspNetCrossSearch
	};

	function boot() {
		var toggle = document.getElementById("filter_div_a_text");
		if (toggle) toggle.addEventListener("click", function (e) { e.preventDefault(); toggleFilter(); });
		var reset = document.getElementById("epc_filter_reset");
		if (reset) reset.addEventListener("click", function (e) { e.preventDefault(); resetFilters(); });
		["epc_filter_price_min", "epc_filter_price_max", "epc_filter_term_min", "epc_filter_term_max", "epc_filter_instock"].forEach(function (id) {
			var el = document.getElementById(id);
			if (el) el.addEventListener("change", applyFilters);
			if (el && el.tagName === "INPUT" && el.type !== "checkbox") el.addEventListener("input", applyFilters);
		});
		var fitBtn = document.getElementById("epc-fitment-check-btn");
		if (fitBtn) {
			fitBtn.addEventListener("click", function () {
				openFitment(fitBtn.getAttribute("data-article") || "", fitBtn.getAttribute("data-brand") || "");
			});
		}
		var fitClose = document.getElementById("epc-fitment-close");
		if (fitClose) {
			fitClose.addEventListener("click", function () {
				var panel = document.getElementById("epc-fitment-panel");
				if (panel) panel.classList.remove("is-open");
			});
		}
		var typesBox = document.getElementById("epc-fitment-types");
		if (typesBox) {
			typesBox.querySelectorAll("button").forEach(function (btn) {
				btn.addEventListener("click", function () {
					renderFitmentSection(btn.getAttribute("data-section") || "PC");
				});
			});
		}
		var crossBtn = document.getElementById("epc-cross-search-btn");
		if (crossBtn) {
			crossBtn.addEventListener("click", function () {
				focusCross();
				var nav = document.getElementById("epc-cross-base");
				loadAspNetCrossSearch(
					(nav && nav.getAttribute("data-article")) || "",
					(nav && nav.getAttribute("data-brand")) || ""
				);
			});
		}
		rebuildFilterOptions();
		applyFilters();
		wireRowActions(document);
		bindSearchRowPhotoLoaders(document);
		var crossNav = document.getElementById("epc-cross-base");
		// Inline CHPU bootstrap already fetches /storefront/cross-search — do not double-hit DB.
		if (crossNav && !window.__epcChpuCrossBootstrapped) {
			loadAspNetCrossSearch(
				crossNav.getAttribute("data-article") || "",
				crossNav.getAttribute("data-brand") || ""
			);
		}
		var body = document.getElementById("epcSfOfferBody");
		if (body && window.MutationObserver) {
			var moTimer = 0;
			var mo = new MutationObserver(function () {
				// Debounce — bulk appendProduct / translate mutations were freezing the tab.
				if (moTimer) window.clearTimeout(moTimer);
				moTimer = window.setTimeout(function () {
					moTimer = 0;
					rebuildFilterOptions();
					applyFilters();
					wireRowActions(body);
					bindSearchRowPhotoLoaders(body);
				}, 120);
			});
			mo.observe(body, { childList: true, subtree: false });
		}
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", boot);
	} else {
		boot();
	}
})();
