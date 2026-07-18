(function (global) {
	'use strict';

	var CATEGORY_ICON_BASE = '/content/files/epc-cata/category-icons/';

	function setCategoryIconBase(base) {
		if (base) { CATEGORY_ICON_BASE = String(base).replace(/\/?$/, '/'); }
	}

	function resolveIconBase(options) {
		return (options && options.iconBase) ? String(options.iconBase).replace(/\/?$/, '/') : CATEGORY_ICON_BASE;
	}

	function sortCategoriesByOrder(categories) {
		return (categories || []).slice().sort(function (a, b) {
			var ao = parseInt(a.ORDER || a.order || 0, 10) || 0;
			var bo = parseInt(b.ORDER || b.order || 0, 10) || 0;
			if (ao !== bo) { return ao - bo; }
			return (parseInt(a.STR_ID || a.CATEGORY_ID || 0, 10) || 0) - (parseInt(b.STR_ID || b.CATEGORY_ID || 0, 10) || 0);
		});
	}

	var CAR_MOD_CATEGORIES = [
		{ id: 2, name: 'Filters', fa: 'fa-filter' },
		{ id: 1, name: 'Service parts', fa: 'fa-wrench' },
		{ id: 4, name: 'Suspension', fa: 'fa-compress' },
		{ id: 5, name: 'Brake System', fa: 'fa-stop-circle' },
		{ id: 7, name: 'Damping', fa: 'fa-arrows-v' },
		{ id: 8, name: 'Belt Drive', fa: 'fa-circle-o' },
		{ id: 15, name: 'Windscreen Cleaning', fa: 'fa-eye' },
		{ id: 9, name: 'Clutch', fa: 'fa-cog' },
		{ id: 10, name: 'Ignition', fa: 'fa-bolt' },
		{ id: 3, name: 'Engine', fa: 'fa-cogs' },
		{ id: 16, name: 'Wheel Drive', fa: 'fa-life-ring' },
		{ id: 11, name: 'Bodywork', fa: 'fa-car' },
		{ id: 12, name: 'Electrics', fa: 'fa-lightbulb-o' },
		{ id: 6, name: 'Wheels', fa: 'fa-circle-thin' },
		{ id: 18, name: 'Steering', fa: 'fa-random' },
		{ id: 17, name: 'Fuel Supply', fa: 'fa-tint' },
		{ id: 26, name: 'Fuel Mixture Formation', fa: 'fa-flask' },
		{ id: 19, name: 'Cooling', fa: 'fa-thermometer-half' },
		{ id: 20, name: 'Exhaust', fa: 'fa-cloud' },
		{ id: 24, name: 'Axle Drive', fa: 'fa-cogs' },
		{ id: 23, name: 'Heating, Ventilation', fa: 'fa-fire' },
		{ id: 22, name: 'Air Conditioning', fa: 'fa-snowflake-o' },
		{ id: 13, name: 'Manual Transmission', fa: 'fa-gears' },
		{ id: 14, name: 'Automatic Transmission', fa: 'fa-refresh' },
		{ id: 872, name: 'Auto Chemicals', fa: 'fa-flask' },
		{ id: 34, name: 'Electric Drive', fa: 'fa-bolt' }
	];

	var DEFAULT_VEHICLE_SECTIONS = [
		{ key: 'passenger', label: 'Passengers' },
		{ key: 'commercial', label: 'Commercial' },
		{ key: 'motorbike', label: 'Motorcycles' }
	];

	function vehicleSectionLabel(section) {
		var key = text(section || 'passenger');
		for (var i = 0; i < DEFAULT_VEHICLE_SECTIONS.length; i++) {
			if (DEFAULT_VEHICLE_SECTIONS[i].key === key) {
				return DEFAULT_VEHICLE_SECTIONS[i].label;
			}
		}
		return 'Passengers';
	}

	var DEFAULT_CATEGORY_ICONS = [
		{ re: /filter/i, icon: 'fa-filter' },
		{ re: /service/i, icon: 'fa-wrench' },
		{ re: /suspension/i, icon: 'fa-compress' },
		{ re: /brake/i, icon: 'fa-stop-circle' },
		{ re: /damp|shock/i, icon: 'fa-arrows-v' },
		{ re: /belt|timing/i, icon: 'fa-circle-o' },
		{ re: /wind|wiper|glass/i, icon: 'fa-eye' },
		{ re: /clutch/i, icon: 'fa-cog' },
		{ re: /ignition|spark|glow/i, icon: 'fa-bolt' },
		{ re: /engine/i, icon: 'fa-cogs' },
		{ re: /wheel|drive|tyre|tire/i, icon: 'fa-life-ring' },
		{ re: /body|bumper|wing/i, icon: 'fa-car' },
		{ re: /cool|radiat|heat/i, icon: 'fa-thermometer-half' },
		{ re: /exhaust/i, icon: 'fa-cloud' },
		{ re: /fuel/i, icon: 'fa-tint' },
		{ re: /electric|light|lamp/i, icon: 'fa-lightbulb-o' }
	];

	function text(v) { return String(v == null ? '' : v); }

	var MODEL_LETTER_COLS = 6;
	var MODEL_LETTER_MAX_ROWS = 6;

	function normalizeCatalogYear(value) {
		var raw = text(value).trim();
		if (!raw) { return ''; }
		var ym = raw.match(/^(\d{4})-/);
		if (ym) { return ym[1]; }
		raw = raw.replace(/-01$/, '').replace(/-00$/, '');
		var digits = raw.replace(/[^0-9]/g, '');
		if (!digits) { return ''; }
		if (digits.length >= 4) { return digits.slice(0, 4); }
		if (digits.length === 2) {
			var n = parseInt(digits, 10);
			if (isNaN(n)) { return ''; }
			return (n >= 70 ? '19' : '20') + digits;
		}
		return digits;
	}

	function cleanDate(value) {
		return normalizeCatalogYear(value);
	}

	function esc(v) {
		return text(v).replace(/[&<>"']/g, function (c) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[c];
		});
	}

	function manufacturerId(item) {
		return parseInt(item.MFA_ID || item.mfa_id || item.ext_id || item.makeId || 0, 10);
	}

	function modelId(item) {
		var id = parseInt(item.MS_ID || item.ms_id || item.modelId || 0, 10);
		if (id <= 0) {
			id = parseInt(item.ext_id || 0, 10);
		}
		return id;
	}

	function categoryId(item) {
		return parseInt(item.STR_ID || item.CATEGORY_ID || item.strId || item.ext_id || 0, 10);
	}

	function categoryName(item) {
		return text(item.CATEGORY_NAME || item.name || '');
	}

	function normalizeCategoryItem(row, subcategoriesMap) {
		if (!row || typeof row !== 'object') { return row; }
		var sid = parseInt(row.STR_ID || row.CATEGORY_ID || row.ext_id || row.str_id || row.id || 0, 10);
		var name = text(row.CATEGORY_NAME || row.name || row.title || '');
		var iconId = parseInt(row.ICON_ID || row.icon_id || 0, 10);
		var entry = {
			STR_ID: sid,
			CATEGORY_ID: sid,
			CATEGORY_NAME: name,
			name: name,
			ORDER: parseInt(row.ORDER || row.order || 0, 10) || 0,
			source: text(row.source || '')
		};
		if (iconId > 0) {
			entry.ICON_ID = iconId;
		} else if (sid > 0) {
			entry.ICON_ID = sid;
		}
		var children = row.children;
		if ((!children || !children.length) && subcategoriesMap) {
			var key = String(sid);
			if (subcategoriesMap[key]) { children = subcategoriesMap[key]; }
		}
		if (children && children.length) { entry.children = children; }
		return entry;
	}

	function normalizeCategories(rows, subcategoriesMap) {
		return (rows || []).map(function (row) {
			return normalizeCategoryItem(row, subcategoriesMap);
		});
	}

	function normalizeCategoryNameKey(name) {
		return text(name).toLowerCase().replace(/[^a-z0-9]+/g, ' ').replace(/\s+/g, ' ').trim();
	}

	var CATEGORY_NAME_ALIASES = {
		'body': 11, 'bodywork': 11, 'body work': 11,
		'filter': 2, 'filters': 2,
		'service': 1, 'service parts': 1, 'service part': 1,
		'suspension': 4,
		'brake': 5, 'brake system': 5, 'brakes': 5,
		'damping': 7, 'shock absorber': 7, 'shocks': 7,
		'belt drive': 8, 'belt': 8, 'timing belt': 8,
		'windscreen cleaning': 15, 'wiper': 15, 'wipers': 15,
		'clutch': 9,
		'ignition': 10,
		'engine': 3,
		'wheel drive': 16, 'drive shaft': 16,
		'electrics': 12, 'electric': 12, 'electrical': 12,
		'wheels': 6, 'wheel': 6,
		'steering': 18,
		'fuel supply': 17, 'fuel': 17,
		'fuel mixture formation': 26, 'fuel mixture': 26, 'injection': 26,
		'cooling': 19, 'cooling system': 19,
		'exhaust': 20, 'exhaust system': 20,
		'axle drive': 24, 'axle': 24,
		'heating ventilation': 23, 'heating': 23, 'ventilation': 23,
		'air conditioning': 22, 'ac': 22,
		'manual transmission': 13, 'manual gearbox': 13,
		'automatic transmission': 14, 'automatic gearbox': 14,
		'auto chemicals': 872,
		'electric drive': 34, 'hybrid': 34
	};

	function presentationCategoryIndex(presRows) {
		var byStrId = {};
		var byUmapiId = {};
		var byName = {};
		(presRows || []).forEach(function (c) {
			var sid = String(c.STR_ID || c.CATEGORY_ID || '');
			if (sid) { byStrId[sid] = c; }
			var umapiId = String(c.umapi_category_id || '');
			if (umapiId) { byUmapiId[umapiId] = c; }
			var key = normalizeCategoryNameKey(c.CATEGORY_NAME || c.name || '');
			if (key && !byName[key]) { byName[key] = c; }
		});
		return { byStrId: byStrId, byUmapiId: byUmapiId, byName: byName };
	}

	function resolveCategoryAliasByName(name) {
		var key = normalizeCategoryNameKey(name);
		if (!key) { return null; }
		if (CATEGORY_NAME_ALIASES[key]) {
			var strId = CATEGORY_NAME_ALIASES[key];
			return { strId: strId, iconId: strId };
		}
		var i;
		for (i = 0; i < CAR_MOD_CATEGORIES.length; i++) {
			var cname = CAR_MOD_CATEGORIES[i].name.toLowerCase();
			if (key === cname || key.indexOf(cname) !== -1 || cname.indexOf(key) !== -1) {
				return { strId: CAR_MOD_CATEGORIES[i].id, iconId: CAR_MOD_CATEGORIES[i].id };
			}
		}
		for (i = 0; i < DEFAULT_CATEGORY_ICONS.length; i++) {
			if (DEFAULT_CATEGORY_ICONS[i].re.test(name)) {
				var j;
				for (j = 0; j < CAR_MOD_CATEGORIES.length; j++) {
					if (DEFAULT_CATEGORY_ICONS[j].re.test(CAR_MOD_CATEGORIES[j].name)) {
						return { strId: CAR_MOD_CATEGORIES[j].id, iconId: CAR_MOD_CATEGORIES[j].id };
					}
				}
			}
		}
		return null;
	}

	function resolvePresentationCategory(presRows, apiId, strId, apiName) {
		var idx = presentationCategoryIndex(presRows);
		var pres = idx.byStrId[strId] || idx.byStrId[apiId] || idx.byUmapiId[apiId] || idx.byUmapiId[strId];
		if (pres) { return pres; }
		var nameKey = normalizeCategoryNameKey(apiName);
		if (nameKey && idx.byName[nameKey]) { return idx.byName[nameKey]; }
		if (nameKey && CATEGORY_NAME_ALIASES[nameKey] && idx.byStrId[String(CATEGORY_NAME_ALIASES[nameKey])]) {
			return idx.byStrId[String(CATEGORY_NAME_ALIASES[nameKey])];
		}
		if (nameKey) {
			var i;
			for (i = 0; i < (presRows || []).length; i++) {
				var prow = presRows[i];
				var pkey = normalizeCategoryNameKey(prow.CATEGORY_NAME || prow.name || '');
				if (!pkey) { continue; }
				if (pkey === nameKey || pkey.indexOf(nameKey) !== -1 || nameKey.indexOf(pkey) !== -1) {
					return prow;
				}
			}
		}
		return null;
	}

	function extractUmapiCategoryEntries(apiPayload) {
		var body = apiPayload;
		if (body && body.data && typeof body.data === 'object' && !Array.isArray(body.data)) {
			body = body.data;
		}
		if (Array.isArray(apiPayload)) {
			return apiPayload.slice();
		}
		if (!body || typeof body !== 'object') {
			return [];
		}
		if (Array.isArray(body)) {
			return body.slice();
		}
		if (!body.root && !body.quic) {
			return [];
		}
		var out = [];
		var seen = {};
		function pushEntry(item) {
			if (!item || typeof item !== 'object') { return; }
			var id = item.STR_ID || item.CATEGORY_ID || (item.CATEGORY_IDS && item.CATEGORY_IDS.length ? item.CATEGORY_IDS[0] : 0);
			id = parseInt(id, 10) || 0;
			if (id <= 0 || seen[String(id)]) { return; }
			seen[String(id)] = true;
			out.push({
				STR_ID: id,
				CATEGORY_ID: id,
				CATEGORY_NAME: text(item.DES || item.CATEGORY_NAME || item.name || item.title || ''),
				name: text(item.DES || item.CATEGORY_NAME || item.name || item.title || ''),
				umapi_category_id: String(item.CATEGORY_ID || id),
				source: text(item.source || 'umapi')
			});
		}
		(body.quic || []).forEach(pushEntry);
		if (!out.length) {
			(body.root || []).forEach(pushEntry);
		}
		return out;
	}

	function vehicleCategoriesFromUmapi(apiPayload, presentationCategories, subcategoriesMap, options) {
		options = options || {};
		var presRows = presentationCategories || [];
		var subMap = subcategoriesMap || {};
		var apiEntries = extractUmapiCategoryEntries(apiPayload);
		if (!apiEntries.length) {
			return options.fallbackAll ? normalizeCategories(presRows, subMap) : [];
		}
		var out = [];
		var usedStrIds = {};
		apiEntries.forEach(function (row) {
			var apiId = String(row.CATEGORY_ID || row.STR_ID || row.ext_id || row.id || '');
			var strId = String(row.STR_ID || row.CATEGORY_ID || row.ext_id || row.id || '');
			var apiName = text(row.CATEGORY_NAME || row.name || '');
			var presRow = resolvePresentationCategory(presRows, apiId, strId, apiName);
			var entry;
			if (presRow) {
				strId = String(presRow.STR_ID || presRow.CATEGORY_ID || strId);
				if (usedStrIds[strId]) { return; }
				usedStrIds[strId] = true;
				var iconId = parseInt(presRow.ICON_ID || presRow.icon_id || 0, 10);
				if (iconId <= 0) {
					var presAlias = resolveCategoryAliasByName(apiName);
					iconId = presAlias ? presAlias.iconId : (parseInt(strId, 10) || 0);
				}
				entry = Object.assign({}, presRow, {
					STR_ID: parseInt(strId, 10) || strId,
					CATEGORY_ID: parseInt(strId, 10) || strId,
					CATEGORY_NAME: text(presRow.CATEGORY_NAME || presRow.name || apiName),
					name: text(presRow.CATEGORY_NAME || presRow.name || apiName),
					ICON_ID: iconId,
					umapi_category_id: apiId,
					source: text(row.source || presRow.source || 'umapi')
				});
				if (subMap[strId]) {
					entry.children = subMap[strId];
				}
			} else {
				var alias = resolveCategoryAliasByName(apiName);
				if (alias) {
					strId = String(alias.strId);
					if (usedStrIds[strId]) { return; }
					usedStrIds[strId] = true;
					entry = normalizeCategoryItem({
						STR_ID: alias.strId,
						CATEGORY_ID: alias.strId,
						ICON_ID: alias.iconId,
						CATEGORY_NAME: apiName,
						name: apiName,
						umapi_category_id: apiId,
						source: 'umapi'
					}, subMap);
				} else {
					if (usedStrIds[strId]) { return; }
					usedStrIds[strId] = true;
					entry = normalizeCategoryItem(Object.assign({}, row, { umapi_category_id: apiId }), subMap);
				}
			}
			if (entry && (entry.CATEGORY_NAME || entry.name)) {
				out.push(entry);
			}
		});
		var filteredSubMap = {};
		out.forEach(function (c) {
			var sid = String(c.STR_ID || c.CATEGORY_ID || '');
			if (subMap[sid]) {
				filteredSubMap[sid] = subMap[sid];
			} else if (c.children) {
				filteredSubMap[sid] = c.children;
			}
		});
		return normalizeCategories(out, filteredSubMap).map(function (c) {
			var sid = String(c.STR_ID || c.CATEGORY_ID || '');
			if (!c.children && filteredSubMap[sid]) {
				return Object.assign({}, c, { children: filteredSubMap[sid] });
			}
			return c;
		});
	}

	function supplierId(item) {
		return parseInt(item.SUP_ID || item.sup_id || 0, 10);
	}

	function articleBrand(item) {
		return text(item.ART_SUP_BRAND || item.SUP_BRAND || item.brand || item.cross_brand || item.crossBrand || '');
	}

	function articleNumber(item) {
		return text(item.ART_ARTICLE_NR || item.article || item.cross_article || item.crossNumber || item.sku || item.partNumber || '');
	}

	function articleGroup(item) {
		return text(item.PRODUCT_GROUP || item.name || item.NAME_PARTS || item.ART_PRODUCT_NAME || item.partname || '');
	}

	function manufacturerLogoUrl(item) {
		if (item.logo_url) { return text(item.logo_url); }
		var id = manufacturerId(item);
		return id > 0 ? '/api/umapi_image.php?kind=manufacturer&id=' + encodeURIComponent(id) + '&v=12' : '';
	}

	function supplierLogoUrl(item) {
		if (item.supplier_logo_url || item.logo_url) {
			return text(item.supplier_logo_url || item.logo_url);
		}
		var id = supplierId(item);
		return id > 0 ? '/api/umapi_image.php?kind=supplier&id=' + encodeURIComponent(id) : '';
	}

	function umapiModelCdnUrls(id) {
		id = parseInt(id || 0, 10);
		if (id <= 0) { return []; }
		var base = 'https://image.umapi.ru/MODEL_SERIES/' + encodeURIComponent(id);
		return [base + '.jpg', base + '.png'];
	}

	function modelImageUrl(item) {
		var img = text(item.image_url || item.img || '');
		if (text(item.source || '') === 'carcat') {
			return img;
		}
		var id = modelId(item);
		if (img && id <= 0) {
			return img;
		}
		if (id > 0) {
			if (img) { return img; }
			return '/api/umapi_image.php?kind=model&id=' + encodeURIComponent(id);
		}
		return img;
	}

	function modelImageFallback(img) {
		if (!img || !img.parentNode) { return; }
		img.parentNode.innerHTML = '<i class="fa fa-car epc-vc-model-silhouette"></i>';
	}

	function manufacturerLogoHtml(item) {
		var url = manufacturerLogoUrl(item);
		if (url) {
			return '<img src="' + esc(url) + '" alt="" loading="lazy" decoding="async" onerror="this.replaceWith(this.parentNode.querySelector(\'.epc-vc-make-placeholder\') || (function(){var s=document.createElement(\'span\');s.className=\'epc-vc-make-placeholder\';s.textContent=\'' + esc((text(item.MANUFACTURER || item.name || '?')).charAt(0) || '?') + '\';return s;})())">';
		}
		var initial = esc((text(item.MANUFACTURER || item.name || '?')).charAt(0) || '?');
		return '<span class="epc-vc-make-placeholder">' + initial + '</span>';
	}

	function supplierLogoHtml(item) {
		var brand = articleBrand(item);
		var url = supplierLogoUrl(item);
		if (url) {
			return '<img src="' + esc(url) + '" alt="" loading="lazy" decoding="async" onerror="this.parentNode.innerHTML=\'<span class=\\\'epc-vc-brand-initial\\\'>' + esc((brand.charAt(0) || '?')) + '</span>\'">';
		}
		return '<span class="epc-vc-brand-initial">' + esc((brand.charAt(0) || '?')) + '</span>';
	}

	function modelImageHtml(item) {
		var url = modelImageUrl(item);
		var id = modelId(item);
		if (url) {
			return '<img src="' + esc(url) + '" alt="" loading="lazy" decoding="async" referrerpolicy="no-referrer"' +
				(id > 0 ? ' data-ms-id="' + esc(String(id)) + '"' : '') +
				' onerror="window.epcVcCatalogUi&&window.epcVcCatalogUi.modelImageFallback(this)">';
		}
		return '<i class="fa fa-car epc-vc-model-silhouette"></i>';
	}

	function renderModelBrandLogoHtml(makeItem) {
		if (!makeItem) { return ''; }
		var mfaId = manufacturerId(makeItem);
		if (mfaId <= 0) { return ''; }
		var url = manufacturerLogoUrl(makeItem);
		if (!url) {
			url = '/api/umapi_image.php?kind=manufacturer&id=' + encodeURIComponent(mfaId);
		}
		var name = text(makeItem.MANUFACTURER || makeItem.name || '');
		return '<img class="epc-cm-model-brand-logo-img" src="' + esc(url) + '" alt="' + esc(name) + '" loading="lazy" decoding="async" onerror="this.parentNode.style.display=\'none\'">';
	}

	function modelLetterFromLabel(name) {
		name = text(name).trim();
		if (!name) { return '#'; }
		var ch = name.charAt(0);
		if (/[0-9]/.test(ch)) { return ch; }
		if (/[A-Za-z]/.test(ch)) { return ch.toUpperCase(); }
		return '0-9';
	}

	function itemMakeName(item) {
		return text(item && (item.MANUFACTURER || item.make_name || item._makeName || ''));
	}

	function stripMakePrefixFromModelName(name, makeName) {
		name = text(name).trim();
		makeName = text(makeName).trim();
		if (!name || !makeName) { return name; }
		var lowerName = name.toLowerCase();
		var lowerMake = makeName.toLowerCase();
		if (lowerName.indexOf(lowerMake) === 0) {
			var rest = name.slice(makeName.length).replace(/^[\s\-–—/]+/, '').trim();
			if (rest) { return rest; }
		}
		return name;
	}

	function modelNameForLetter(item, makeName) {
		if (!item) { return ''; }
		makeName = makeName || itemMakeName(item);
		return stripMakePrefixFromModelName(modelDisplayName(item), makeName);
	}

	function modelLetter(item, makeName) {
		return modelLetterFromLabel(modelNameForLetter(item, makeName));
	}

	function renderModelLetterIndex(groups, options) {
		options = options || {};
		var html = '<div class="epc-cm-model-letter-index" aria-label="Models by letter">';
		(groups || []).forEach(function (group) {
			var rep = group.representative;
			if (!rep) { return; }
			var letter = group.letter || modelLetter(rep.item);
			var name = group.displayName || modelDisplayName(rep.item);
			var years = modelYearDisplayForGroup(group);
			var variantIndices = group.variants.map(function (v) { return v.index; }).join(',');
			html += '<button type="button" class="epc-cm-model-letter-name" data-index="' + rep.index + '" data-model-index="' + rep.index + '" data-model-variant-indices="' + esc(variantIndices) + '" data-model-letter="' + esc(letter) + '" data-year-from="' + esc(group.bounds.from) + '" data-year-to="' + esc(group.bounds.to) + '" data-year-label="' + esc(years) + '">' + esc(name) + '</button>';
		});
		html += '</div>';
		if (options.showAllLink !== false && groups.length > MODEL_LETTER_COLS * MODEL_LETTER_MAX_ROWS) {
			html += '<div class="epc-cm-model-show-all-wrap"><button type="button" class="epc-cm-model-show-all">Show all <i class="fa fa-chevron-down" aria-hidden="true"></i></button></div>';
		}
		return html;
	}

	function categoryIconId(item) {
		var iconId = parseInt(item.ICON_ID || item.icon_id || 0, 10);
		if (iconId > 0) { return iconId; }
		var alias = resolveCategoryAliasByName(categoryName(item));
		if (alias && alias.iconId > 0) { return alias.iconId; }
		var label = categoryName(item).toLowerCase();
		for (var i = 0; i < CAR_MOD_CATEGORIES.length; i++) {
			if (CAR_MOD_CATEGORIES[i].name.toLowerCase() === label) {
				return CAR_MOD_CATEGORIES[i].id;
			}
		}
		return 0;
	}

	function categoryIcon(name, icons) {
		var label = text(name);
		for (var j = 0; j < CAR_MOD_CATEGORIES.length; j++) {
			if (CAR_MOD_CATEGORIES[j].name.toLowerCase() === label.toLowerCase()) {
				return CAR_MOD_CATEGORIES[j].fa;
			}
		}
		var list = icons || DEFAULT_CATEGORY_ICONS;
		for (var i = 0; i < list.length; i++) {
			if (list[i].re.test(label)) { return list[i].icon; }
		}
		return 'fa-wrench';
	}

	function categoryIconHtml(item, icons, iconBase) {
		var id = categoryIconId(item);
		var name = categoryName(item);
		var fa = categoryIcon(name, icons);
		var base = iconBase || CATEGORY_ICON_BASE;
		var remoteImg = text(item.img || item.image_url || item.imageUrl || '');
		if (remoteImg) {
			return '<img src="' + esc(remoteImg) + '" alt="" loading="lazy" decoding="async" onerror="this.parentNode.innerHTML=\'<i class=\\\'fa ' + fa + '\\\'></i>\'">';
		}
		if (id > 0) {
			return '<img src="' + esc(base + id + '.png') + '" alt="" loading="lazy" decoding="async" onerror="this.parentNode.innerHTML=\'<i class=\\\'fa ' + fa + '\\\'></i>\'">';
		}
		return '<i class="fa ' + fa + '"></i>';
	}

	function renderCatalogHeading(options) {
		options = options || {};
		var title = options.title || 'Catalog';
		var subtitle = options.subtitle || 'find by category or assembly';
		return '<div class="epc-cm-catalog-head"><h1><em>' + esc(title) + '</em> — ' + esc(subtitle) + '</h1></div>';
	}

	function renderStepPicker(steps, activeIndex, pickerOpts) {
		pickerOpts = pickerOpts || {};
		var html = '<div class="epc-cm-steps epc-cm-steps--row">' + steps.map(function (step, i) {
			var cls = 'epc-cm-step';
			if (i === activeIndex) { cls += ' is-active'; }
			else if (step.value) { cls += ' is-done'; }
			else if (i > activeIndex) { cls += ' is-muted'; }
			return '<div class="' + cls + '" data-step="' + i + '" data-step-key="' + esc(step.key || '') + '">' +
				'<span class="epc-cm-step-num">' + (i + 1) + '</span>' +
				'<div><span class="epc-cm-step-label">' + esc(step.label) + '</span>' +
				(step.value ? '<span class="epc-cm-step-value">' + esc(step.value) + '</span>' : '') +
				'</div><span class="epc-cm-step-chevron"><i class="fa fa-chevron-right"></i></span></div>';
		}).join('') + '</div>';
		if (pickerOpts.brandPanel) {
			html += '<div class="epc-cm-step-dropdown">' + pickerOpts.brandPanel + '</div>';
		}
		if (pickerOpts.enginePanel) {
			html += '<div class="epc-cm-step-dropdown">' + pickerOpts.enginePanel + '</div>';
		}
		return html;
	}

	function bindStepPicker(container, onStep) {
		if (!container) { return; }
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-step'), function (el) {
			el.onclick = function () {
				if (el.classList.contains('is-muted')) { return; }
				var idx = parseInt(el.getAttribute('data-step'), 10);
				if (!isNaN(idx) && typeof onStep === 'function') { onStep(idx, el); }
			};
		});
	}

	function renderMakeGrid(items, options) {
		options = options || {};
		var popular = items.filter(function (item) {
			return String(item.POPULAR_PC || item.POPULAR_CV || item.POPULAR_MTB || item.popular || '') === '1';
		});
		var html = '<p class="epc-vc-section-title">Brand — select make</p>';
		if (options.search !== false) {
			html += '<input type="search" class="form-control epc-vc-make-search" placeholder="Find brand" aria-label="Find brand">';
		}
		html += '<p class="epc-vc-list-count">' + items.length + ' makes</p>';
		if (popular.length) {
			html += '<div class="epc-vc-section-bar">Popular</div><div class="epc-vc-make-grid epc-vc-make-grid-popular">' + popular.map(function (item, index) {
				return makeCardHtml(item, items.indexOf(item), options);
			}).join('') + '</div>';
		}
		html += '<div class="epc-vc-section-bar">All brands</div><div class="epc-vc-make-grid">' + items.map(function (item, index) {
			return makeCardHtml(item, index, options);
		}).join('') + '</div>';
		return html;
	}

	function makeCardHtml(item, index, options) {
		var name = text(item.MANUFACTURER || item.name || 'Make');
		var selected = options.selectedId && manufacturerId(item) === parseInt(options.selectedId, 10);
		return '<div class="epc-vc-make-card' + (selected ? ' is-selected' : '') + '" data-index="' + index + '" data-search="' + esc(name) + '" data-mfa-id="' + esc(manufacturerId(item)) + '">' +
			'<div class="epc-vc-make-logo">' + manufacturerLogoHtml(item) + '</div>' +
			'<strong>' + esc(name) + '</strong></div>';
	}

	function currentCatalogYear() {
		return new Date().getFullYear();
	}

	function modelYearEndYear(item) {
		var to = normalizeCatalogYear(item.CI_TO || item.year_to || '');
		if (!to || to === '9999') { return currentCatalogYear(); }
		return parseInt(to, 10) || currentCatalogYear();
	}

	function modelYearBounds(item) {
		var fromRaw = normalizeCatalogYear(item.CI_FROM || item.year_from || '');
		var from = parseInt(fromRaw, 10) || 0;
		var to = modelYearEndYear(item);
		if (!from && to) { from = to; }
		return { from: from, to: to };
	}

	function modelMatchesYear(item, year) {
		if (!year) { return true; }
		var y = parseInt(year, 10);
		if (isNaN(y)) { return true; }
		var bounds = modelYearBounds(item);
		if (!bounds.from) { return true; }
		return bounds.from <= y && y <= bounds.to;
	}

	function modelYearRange(item) {
		var from = normalizeCatalogYear(item.CI_FROM || item.year_from || '');
		var to = normalizeCatalogYear(item.CI_TO || item.year_to || '');
		if (!from && !to) { return ''; }
		if (from && to && to !== from && to !== '9999') { return from + '–' + to; }
		return from || to;
	}

	function modelYearDisplayLabel(fromRaw, toRaw, openEnded) {
		var from = normalizeCatalogYear(fromRaw || '');
		var to = normalizeCatalogYear(toRaw || '');
		if (!from) { return to && to !== '9999' ? to : ''; }
		if (openEnded === undefined) {
			openEnded = !to || to === '9999';
		}
		if (openEnded) { return from + '-p.t.'; }
		if (from === to) { return from; }
		return from;
	}

	function modelYearDisplayForCard(item) {
		return modelYearDisplayLabel(item.CI_FROM || item.year_from || '', item.CI_TO || item.year_to || '');
	}

	function normalizeModelNameKey(name) {
		return text(name).trim().toLowerCase().replace(/\s+/g, ' ');
	}

	function modelSeriesCodeToken(item) {
		var code = modelSeriesCode(item);
		if (!code) { return ''; }
		return code.replace(/_/g, '').replace(/^\(|\)$/g, '').trim().toLowerCase();
	}

	function modelIndexBaseLabel(item) {
		return normalizeModelNameKey(modelDisplayName(item));
	}

	function modelIndexYearSuffix(item) {
		return modelYearDisplayLabel(item.CI_FROM || item.year_from || '', item.CI_TO || item.year_to || '');
	}

	function modelIndexDisambiguator(item) {
		var code = modelSeriesCodeToken(item);
		if (code) { return code; }
		return modelIndexYearSuffix(item) || (modelId(item) > 0 ? String(modelId(item)) : '');
	}

	function assignModelGroupDisplayNames(groups, options) {
		options = options || {};
		var defaultMake = text(options.makeName || '');
		var buckets = {};
		(groups || []).forEach(function (group) {
			var item = group.variants[0].item;
			var base = modelIndexBaseLabel(item);
			group._baseLabel = base;
			if (!buckets[base]) { buckets[base] = []; }
			buckets[base].push(group);
		});
		Object.keys(buckets).forEach(function (base) {
			var bucket = buckets[base];
			if (bucket.length === 1) {
				bucket[0].displayName = base;
				return;
			}
			var used = {};
			bucket.forEach(function (group) {
				var item = group.variants[0].item;
				var code = modelSeriesCodeToken(item);
				var yearTag = modelIndexYearSuffix(item);
				var id = modelId(item);
				var suffix = code || yearTag || (id > 0 ? String(id) : '');
				var label = suffix ? (base + ' (' + suffix + ')') : base;
				if (used[label]) {
					suffix = code || yearTag || '';
					if (suffix && id > 0) {
						suffix = suffix + ' #' + id;
					} else if (id > 0) {
						suffix = '#' + id;
					}
					label = suffix ? (base + ' (' + suffix + ')') : base;
				}
				used[label] = true;
				group.displayName = label;
			});
		});
		(groups || []).forEach(function (group) {
			var item = group.variants[0].item;
			group.letter = modelLetter(item, itemMakeName(item) || defaultMake);
			delete group._baseLabel;
		});
	}

	function mergeVariantYearBounds(items) {
		var min = 9999;
		var max = 0;
		(items || []).forEach(function (item) {
			var bounds = modelYearBounds(item);
			if (bounds.from && bounds.from < min) { min = bounds.from; }
			if (bounds.to && bounds.to > max) { max = bounds.to; }
		});
		return {
			from: min === 9999 ? 0 : min,
			to: max || currentCatalogYear()
		};
	}

	function modelYearDisplayForGroup(group) {
		var bounds = group.bounds || mergeVariantYearBounds((group.variants || []).map(function (v) { return v.item; }));
		if (!bounds.from) { return ''; }
		var anyOpen = (group.variants || []).some(function (v) {
			var to = normalizeCatalogYear(v.item.CI_TO || v.item.year_to || '');
			return !to || to === '9999';
		});
		return modelYearDisplayLabel(String(bounds.from), anyOpen ? '' : String(bounds.to), anyOpen);
	}

	function pickModelRepresentative(variants, selectedYear) {
		var list = (variants || []).slice();
		if (!list.length) { return null; }
		if (selectedYear) {
			var matched = list.filter(function (v) { return modelMatchesYear(v.item, selectedYear); });
			if (matched.length) { list = matched; }
		}
		list.sort(function (a, b) {
			var ba = modelYearBounds(a.item);
			var bb = modelYearBounds(b.item);
			var aOpen = !normalizeCatalogYear(a.item.CI_TO || a.item.year_to || '') || normalizeCatalogYear(a.item.CI_TO || a.item.year_to || '') === '9999';
			var bOpen = !normalizeCatalogYear(b.item.CI_TO || b.item.year_to || '') || normalizeCatalogYear(b.item.CI_TO || b.item.year_to || '') === '9999';
			if (aOpen !== bOpen) { return aOpen ? -1 : 1; }
			return bb.from - ba.from;
		});
		return list[0];
	}

	function buildModelDisplayGroups(items, options) {
		options = options || {};
		var groupMap = {};
		var order = [];
		(items || []).forEach(function (item, index) {
			var id = modelId(item);
			var key = id > 0 ? ('ms:' + id) : ('idx:' + index);
			if (!groupMap[key]) {
				groupMap[key] = {
					key: key,
					MS_ID: id > 0 ? id : 0,
					variants: []
				};
				order.push(key);
			}
			groupMap[key].variants.push({ item: item, index: index });
		});
		var groups = order.map(function (key) { return groupMap[key]; });
		assignModelGroupDisplayNames(groups, options);
		return groups.map(function (group) {
			group.representative = pickModelRepresentative(group.variants);
			if (group.representative && group.representative.item) {
				var repItem = group.representative.item;
				var repId = modelId(repItem);
				if (repId > 0) {
					group.MS_ID = repId;
					if (text(repItem.source || '') !== 'carcat' && !repItem.image_url && !repItem.img && repId > 0) {
						repItem.image_url = '/api/umapi_image.php?kind=model&id=' + encodeURIComponent(repId);
					}
				}
			}
			group.bounds = mergeVariantYearBounds(group.variants.map(function (v) { return v.item; }));
			return group;
		});
	}

	function collectModelLetterTabsFromGroups(groups) {
		var tabs = ['All'];
		var seen = {};
		(groups || []).forEach(function (group) {
			var letter = group.letter || modelLetter(group.representative ? group.representative.item : {});
			if (!seen[letter]) {
				seen[letter] = true;
				tabs.push(letter);
			}
		});
		return tabs;
	}

	function collectModelYearOptions(items) {
		var min = 9999;
		var max = 0;
		(items || []).forEach(function (item) {
			var bounds = modelYearBounds(item);
			if (bounds.from && bounds.from < min) { min = bounds.from; }
			if (bounds.to && bounds.to > max) { max = bounds.to; }
		});
		if (min > max || min === 9999) { return []; }
		min = Math.max(1960, min);
		max = Math.max(min, Math.min(2027, Math.max(currentCatalogYear(), max)));
		var years = [];
		for (var y = min; y <= max; y++) {
			years.push(String(y));
		}
		return years;
	}

	function resolveModelSelectionIndex(items, variantIndices, selectedYear) {
		var indices = (variantIndices || []).filter(function (idx) {
			return !isNaN(idx) && items[idx];
		});
		if (!indices.length) { return -1; }
		if (indices.length === 1) { return indices[0]; }
		var variants = indices.map(function (idx) { return { item: items[idx], index: idx }; });
		var picked = pickModelRepresentative(variants, selectedYear);
		return picked ? picked.index : indices[0];
	}

	function rowMatchesSelectedYear(fromAttr, toAttr, selectedYear) {
		if (!selectedYear) { return true; }
		var y = parseInt(selectedYear, 10);
		if (isNaN(y)) { return true; }
		var from = parseInt(fromAttr, 10) || 0;
		var to = parseInt(toAttr, 10) || currentCatalogYear();
		if (!from) { return true; }
		return from <= y && y <= to;
	}

	function modelDisplayName(item) {
		var series = text(item.MODEL_SERIES || item.name || '');
		if (!series) { return 'Model'; }
		var m = series.match(/^([^(]+)/);
		return (m ? m[1] : series).trim() || series;
	}

	function modelSeriesCode(item) {
		var code = text(item.MODEL_CODE || item.model_code || '');
		if (code) {
			code = code.replace(/^\(|\)$/g, '').trim();
			return code ? '(' + code + ')' : '';
		}
		var series = text(item.MODEL_SERIES || item.name || '');
		var m = series.match(/\(([^)]+)\)/);
		return m ? '(' + m[1].trim() + ')' : '';
	}

	function renderModelGrid(items, options) {
		options = options || {};
		var carMod = options.carModLayout !== false;
		var makeName = text(options.makeName || '');
		var title = options.title;
		if (title === undefined || title === null) {
			title = makeName ? (makeName + ' — models') : 'Select model';
		}
		var displayGroups = buildModelDisplayGroups(items, { makeName: makeName });
		var yearKeys = collectModelYearOptions(items);
		var letterTabs = collectModelLetterTabsFromGroups(displayGroups);
		var html = '';
		if (makeName && options.brandHead !== false && carMod) {
			var homeHref = text(options.homeHref || '#');
			html += '<div class="epc-cm-model-chrome">';
			html += '<div class="epc-cm-model-brand-row">';
			html += '<div class="epc-cm-model-brand-logo">' + renderModelBrandLogoHtml(options.makeItem || { MANUFACTURER: makeName, MFA_ID: options.makeId || 0 }) + '</div>';
			html += '<div class="epc-cm-model-brand-main">';
			var letterFilter = options.letterFilter !== false && displayGroups.length > 1;
			var yearFilter = options.yearFilter !== false && yearKeys.length > 1;
			if (letterFilter || yearFilter) {
				html += '<div class="epc-cm-model-toolbar">';
				if (letterFilter) {
					html += '<div class="epc-cm-model-letter-tabs" role="tablist" aria-label="Filter models by letter">';
					letterTabs.forEach(function (letter) {
						html += '<button type="button" class="epc-cm-model-letter-tab' + (letter === 'All' ? ' active' : '') + '" data-model-letter="' + esc(letter) + '">' + esc(letter) + '</button>';
					});
					html += '</div>';
				}
				if (yearFilter) {
					html += '<label class="epc-cm-mod-filter epc-cm-model-year-filter-wrap"><span class="epc-cm-mod-filter-label">Year</span>' +
						'<select class="form-control input-sm epc-cm-model-year-filter"><option value="">All years</option>' +
						yearKeys.map(function (y) { return '<option value="' + esc(y) + '">' + esc(y) + '</option>'; }).join('') +
						'</select></label>';
				}
				html += '</div>';
			}
			if (options.letterIndex !== false && displayGroups.length > 1) {
				html += renderModelLetterIndex(displayGroups, { showAllLink: options.showAllLink, carModLayout: carMod });
			}
			html += '</div></div>';
			html += '<div class="epc-cm-mod-window-head epc-cm-model-window-head"><h1><em>' + esc(makeName) + '</em> :: Model selection</h1></div>';
			html += '<nav class="epc-cm-model-bc" aria-label="Breadcrumb">';
			html += '<a href="' + esc(homeHref) + '">Home</a><span class="epc-cm-model-bc-sep">&rsaquo;</span>';
			html += '<span class="epc-cm-model-bc-current">' + esc(makeName) + '</span></nav>';
			html += '</div>';
		} else if (makeName && options.brandHead !== false) {
			html += '<div class="epc-cm-brand-head"><h2><em>' + esc(makeName) + '</em> — Models</h2></div>';
		} else if (title) {
			html += '<p class="epc-vc-section-title">' + esc(title) + '</p>';
		}
		if (!(makeName && options.brandHead !== false && carMod)) {
			var letterFilterLegacy = options.letterFilter !== false && carMod && displayGroups.length > 1;
			var yearFilterLegacy = options.yearFilter !== false && yearKeys.length > 1;
			if (letterFilterLegacy || yearFilterLegacy) {
				html += '<div class="epc-cm-model-toolbar">';
				if (letterFilterLegacy) {
					html += '<div class="epc-cm-model-letter-tabs" role="tablist" aria-label="Filter models by letter">';
					letterTabs.forEach(function (letter) {
						html += '<button type="button" class="epc-cm-model-letter-tab' + (letter === 'All' ? ' active' : '') + '" data-model-letter="' + esc(letter) + '">' + esc(letter) + '</button>';
					});
					html += '</div>';
				}
				if (yearFilterLegacy) {
					html += '<label class="epc-cm-mod-filter epc-cm-model-year-filter-wrap"><span class="epc-cm-mod-filter-label">Year</span>' +
						'<select class="form-control input-sm epc-cm-model-year-filter"><option value="">All years</option>' +
						yearKeys.map(function (y) { return '<option value="' + esc(y) + '">' + esc(y) + '</option>'; }).join('') +
						'</select></label>';
				}
				html += '</div>';
			}
		}
		html += '<p class="epc-vc-list-count epc-cm-model-count">' + displayGroups.length + ' models</p>';
		var gridClass = 'epc-vc-model-grid';
		if (carMod && (options.columns >= 8 || options.columns === '8')) {
			gridClass += ' is-8-col';
		}
		html += '<div class="' + gridClass + '" id="epc-cm-model-grid">' + displayGroups.map(function (group) {
			return renderModelCardHtml(group, options);
		}).join('') + '</div>';
		return html;
	}

	function renderModelCardHtml(group, options) {
		options = options || {};
		var carMod = options.carModLayout !== false;
		var rep = group.representative;
		var item = rep.item;
		var index = rep.index;
		var displayName = group.displayName || modelDisplayName(item);
		var seriesCode = modelSeriesCode(item);
		var years = group.yearLabel || modelYearDisplayForGroup(group);
		var letter = group.letter || modelLetter(item);
		var search = displayName + ' ' + years;
		var selected = options.selectedId && modelId(item) === parseInt(options.selectedId, 10);
		var variantIndices = group.variants.map(function (v) { return v.index; }).join(',');
		var nameHtml = '<span class="epc-vc-model-name">' + esc(displayName) + '</span>';
		if (seriesCode) {
			nameHtml += ' <span class="epc-vc-model-code">' + esc(seriesCode) + '</span>';
		}
		return '<div class="epc-vc-model-card' + (selected ? ' is-selected' : '') + (carMod ? ' is-car-mod' : '') + '" role="button" tabindex="0" data-index="' + index + '" data-model-index="' + index + '" data-model-variant-indices="' + esc(variantIndices) + '" data-letter="' + esc(letter) + '" data-year-from="' + esc(group.bounds.from) + '" data-year-to="' + esc(group.bounds.to) + '" data-year-label="' + esc(years) + '" data-search="' + esc(search) + '">' +
			'<div class="epc-vc-model-head"><strong>' + nameHtml + '</strong></div>' +
			'<div class="epc-vc-model-photo">' + modelImageHtml(item) + '</div>' +
			(years ? '<small class="epc-vc-model-year">' + esc(years) + '</small>' : '') +
			'</div>';
	}

	function filterModelGridRows(container, searchTerm) {
		if (!container) { return 0; }
		var host = container.querySelector('.epc-vc-model-grid') ? container : (container.closest ? container.closest('.epc-vc') : null) || container;
		var letterBtn = host.querySelector('.epc-cm-model-letter-tab.active');
		var letter = letterBtn ? (letterBtn.getAttribute('data-model-letter') || 'All') : 'All';
		var year = (host.querySelector('.epc-cm-model-year-filter') || {}).value || '';
		var term = text(searchTerm || '').toLowerCase();
		var visible = 0;
		Array.prototype.forEach.call(host.querySelectorAll('.epc-vc-model-card'), function (card) {
			var show = true;
			if (letter && letter !== 'All' && card.getAttribute('data-letter') !== letter) { show = false; }
			if (show && year && !rowMatchesSelectedYear(card.getAttribute('data-year-from'), card.getAttribute('data-year-to'), year)) { show = false; }
			if (show && term && (card.getAttribute('data-search') || '').toLowerCase().indexOf(term) === -1) { show = false; }
			card.style.display = show ? '' : 'none';
			if (show) { visible++; }
		});
		var letterIndexVisible = 0;
		Array.prototype.forEach.call(host.querySelectorAll('.epc-cm-model-letter-name'), function (btn) {
			var showBtn = true;
			if (letter && letter !== 'All' && btn.getAttribute('data-model-letter') !== letter) { showBtn = false; }
			if (showBtn && year && !rowMatchesSelectedYear(btn.getAttribute('data-year-from'), btn.getAttribute('data-year-to'), year)) { showBtn = false; }
			if (showBtn && term && (btn.textContent || '').toLowerCase().indexOf(term) === -1) { showBtn = false; }
			btn.style.display = showBtn ? '' : 'none';
			if (showBtn) { letterIndexVisible++; }
		});
		updateModelLetterShowAll(host, letterIndexVisible);
		var count = host.querySelector('.epc-cm-model-count');
		if (count) { count.textContent = visible + ' models'; }
		return visible;
	}

	function resetModelLetterIndexCollapse(container) {
		if (!container) { return; }
		var letterIndex = container.querySelector('.epc-cm-model-letter-index');
		if (letterIndex) { letterIndex.classList.remove('is-expanded'); }
	}

	function updateModelLetterShowAll(container, visibleCount) {
		if (!container) { return; }
		var wrap = container.querySelector('.epc-cm-model-show-all-wrap');
		var letterIndex = container.querySelector('.epc-cm-model-letter-index');
		if (!wrap || !letterIndex) { return; }
		var expanded = letterIndex.classList.contains('is-expanded');
		var needsCollapse = visibleCount > MODEL_LETTER_COLS * MODEL_LETTER_MAX_ROWS;
		wrap.style.display = needsCollapse && !expanded ? '' : 'none';
	}

	function categoryChildren(item, subMap) {
		if (!item) { return []; }
		if (Array.isArray(item.children) && item.children.length) { return item.children; }
		if (Array.isArray(item.CHILD) && item.CHILD.length) { return item.CHILD; }
		var sid = categoryId(item);
		subMap = subMap || {};
		return subMap[sid] || subMap[String(sid)] || [];
	}

	function subcategoryName(item) {
		return text(item.CATEGORY_NAME || item.name || item.label || '');
	}

	function subcategoryId(item) {
		return text(item.STR_ID || item.CATEGORY_ID || item.id || item.sub_id || '');
	}

	function isServicePartsCategory(name) {
		return /service\s*parts?|oil\s*filter|fuel\s*filter|air\s*filter|cabin\s*filter|pollen\s*filter|wiper|brake\s*(pad|disc|shoe|drum)|spark\s*plug|glow\s*plug|belt|timing|coolant|thermostat|water\s*pump|clutch\s*disc|clutch\s*kit|suspension\s*arm|ball\s*joint|tie\s*rod|shock\s*absorber|strut|mounting|bearing\s*kit|service\s*kit|maintenance/i.test(text(name));
	}

	function renderCategoryFlyout(parentId, parentName, children) {
		if (!children || !children.length) { return ''; }
		var html = '<div class="epc-cm-cat-flyout" hidden aria-hidden="true">';
		html += '<div class="epc-cm-cat-flyout-head">' + esc(parentName) + '</div>';
		html += '<ul class="epc-cm-cat-flyout-list">';
		children.forEach(function (sub) {
			var subName = subcategoryName(sub);
			var subId = subcategoryId(sub);
			html += '<li><button type="button" class="epc-cm-cat-flyout-item" data-parent-str="' + esc(parentId) + '" data-sub-str="' + esc(subId) + '" data-sub-name="' + esc(subName) + '">' + esc(subName) + '</button></li>';
		});
		html += '</ul></div>';
		return html;
	}

	function buildVehicleContext(make, model, car, options) {
		options = options || {};
		if (!make && !model && !car) { return null; }
		return {
			make: make || null,
			model: model || null,
			car: car || null,
			langHref: options.langHref || '',
			homeHref: options.homeHref || options.langHref || '',
			callbacks: options.callbacks || {}
		};
	}

	function modificationYearLongDisplay(item) {
		if (!item) { return ''; }
		var from = normalizeCatalogYear(item.CI_FROM || item.year_from || item.YEAR_FROM || '');
		if (!from) { return ''; }
		var to = normalizeCatalogYear(item.CI_TO || item.year_to || item.YEAR_TO || '');
		var openEnded = !to || to === '9999';
		return modelYearDisplayLabel(from, openEnded ? '' : to, openEnded);
	}

	function modificationYearBannerDisplay(item) {
		return modificationYearLongDisplay(item);
	}

	function modificationBreadcrumbLabel(item) {
		if (!item) { return ''; }
		var liter = modificationLiter(item);
		var trim = modificationTrimName(item);
		if (liter && liter !== '--') { return liter + ' ' + trim; }
		return trim;
	}

	function modificationDriveLabel(item) {
		var kind = modificationDriveKind(item);
		if (kind === 'awd') { return 'All WD'; }
		if (kind === 'rwd') { return 'Rear WD'; }
		return 'Front WD';
	}

	function modificationBodyLabel(item) {
		if (!item) { return 'Sedan'; }
		var body = text(item.BODY_TYPE || item.body_type || item.PLATFORM_TYPE || '');
		if (body) { return body.replace(/\s*\/\s*/g, ' / ').trim(); }
		var name = text(item.MODIFICATION || item.PASSENGER_CAR || item.COMMERCIAL_VEHICLE || item.carName || '');
		var known = ['Sedan', 'Hatchback', 'SUV', 'Coupe', 'Wagon', 'Van', 'Pickup', 'Convertible', 'MPV', 'Crossover'];
		var i;
		for (i = 0; i < known.length; i++) {
			if (new RegExp('\\b' + known[i] + '\\b', 'i').test(name)) { return known[i]; }
		}
		return 'Sedan';
	}

	function modificationEngineCodesDisplay(item) {
		if (!item) { return ''; }
		var codes = [];
		var skip = /^(plug-in hybrid|hybrid|electric motor|electric|phev|hev)$/i;
		function add(val) {
			val = text(val || '').trim();
			if (!val || skip.test(val)) { return; }
			val.split(/[,;|/]+/).forEach(function (part) {
				part = part.trim();
				if (part && !skip.test(part) && codes.indexOf(part) === -1) { codes.push(part); }
			});
		}
		['MOTOR_CODE', 'E_MOTOR_CODE', 'ENGINE_CODE', 'ENG_CODE', 'ENGINE', 'engine_code'].forEach(function (key) {
			add(item[key]);
		});
		if (codes.length) { return codes.join(', '); }
		var title = text(item.MODIFICATION || item.PASSENGER_CAR || item.COMMERCIAL_VEHICLE || item.carName || '');
		var match = title.match(/\(([^)]+)\)/);
		if (match) { return match[1].trim(); }
		return modificationEngineCode(item);
	}

	function engineListPowerKw(engine) {
		return parseFloat(String(engine && (engine.POWER_KW_START || engine.POWER_KW || '')).replace(',', '.')) || 0;
	}

	function engineListCapacityCc(engine) {
		var cc = parseFloat(String(engine && (engine.CAPACITY_CCM_START || engine.CAPACITY_CCM || '')).replace(',', '.')) || 0;
		if (cc > 0) { return cc; }
		var lt = parseFloat(String(engine && (engine.CAPACITY_LT || '')).replace(',', '.')) || 0;
		return lt > 0 && lt < 20 ? lt * 1000 : 0;
	}

	function engineListIsElectric(engine) {
		var fuel = text(engine && engine.FUEL_TYPE || '').toLowerCase();
		var construction = text(engine && engine.ENGINE_CONSTRUCTION || '').toLowerCase();
		return /electric/.test(fuel) || /alternator/.test(construction);
	}

	function engineListIsPetrol(engine) {
		var fuel = text(engine && engine.FUEL_TYPE || '').toLowerCase();
		return (/petrol|gasoline|benzin|essence/.test(fuel) || /petrol\/gas/.test(fuel)) && !engineListIsElectric(engine);
	}

	function engineListClosestByPower(engines, targetKw) {
		if (!engines || !engines.length || !targetKw) { return null; }
		var best = null;
		var bestDiff = Infinity;
		engines.forEach(function (engine) {
			var kw = engineListPowerKw(engine);
			if (!kw) { return; }
			var diff = Math.abs(kw - targetKw);
			if (diff < bestDiff) {
				bestDiff = diff;
				best = engine;
			}
		});
		return best;
	}

	function engineListClosestByCapacity(engines, targetCc) {
		if (!engines || !engines.length || !targetCc) { return null; }
		var best = null;
		var bestDiff = Infinity;
		engines.forEach(function (engine) {
			var cc = engineListCapacityCc(engine);
			if (!cc) { return; }
			var diff = Math.abs(cc - targetCc);
			if (diff < bestDiff) {
				bestDiff = diff;
				best = engine;
			}
		});
		return best;
	}

	function modificationCapacityCc(item) {
		var cc = parseFloat(String(item.CAPACITY_TECH || '').replace(',', '.')) || 0;
		if (cc >= 100) { return cc; }
		var lt = parseFloat(String(item.CAPACITY_LT || item.CAPACITY || item.displacement || '').replace(',', '.')) || 0;
		return lt > 0 && lt < 20 ? lt * 1000 : cc;
	}

	function enrichModificationsWithEngines(items, engines) {
		engines = engines || [];
		if (!engines.length) { return items || []; }
		var electrics = engines.filter(engineListIsElectric);
		var petrols = engines.filter(engineListIsPetrol);
		return (items || []).map(function (item) {
			if (!item || typeof item !== 'object') { return item; }
			if (text(item.ENGINE_CODE) || text(item.MOTOR_CODE)) { return item; }
			var out = Object.assign({}, item);
			var modKw = parseFloat(String(out.POWER_KW || out.POWER_KW_START || '').replace(',', '.')) || 0;
			var modCc = modificationCapacityCc(out);
			var fuel = text(out.FUEL_TYPE || out.fuel || '').toLowerCase();
			var engineType = text(out.ENGINE_TYPE || out.engine_type || '').toLowerCase();
			var isHybrid = /hybrid|petrol\/electric|phev|plug/.test(fuel + ' ' + engineType);
			var isEv = (/electric|\bev\b/.test(fuel) || /electric motor/.test(engineType)) && !isHybrid;
			if (isEv) {
				var ev = engineListClosestByPower(electrics, modKw);
				if (ev) {
					out.MOTOR_CODE = text(ev.ENGINE_CODE || ev.ENG_CODE || '');
					out.ENGINE_CODE = out.MOTOR_CODE;
				}
			} else if (isHybrid) {
				var petrol = engineListClosestByCapacity(petrols, modCc);
				var petrolKw = petrol ? engineListPowerKw(petrol) : 0;
				var motor = engineListClosestByPower(electrics, Math.max(0, modKw - petrolKw));
				if (!motor && modKw) { motor = engineListClosestByPower(electrics, modKw * 0.55); }
				if (petrol) { out.ENGINE_CODE = text(petrol.ENGINE_CODE || petrol.ENG_CODE || ''); }
				if (motor) { out.MOTOR_CODE = text(motor.ENGINE_CODE || motor.ENG_CODE || ''); }
			} else {
				var ice = engineListClosestByCapacity(petrols.length ? petrols : engines.filter(function (e) { return !engineListIsElectric(e); }), modCc);
				if (ice) { out.ENGINE_CODE = text(ice.ENGINE_CODE || ice.ENG_CODE || ''); }
			}
			return out;
		});
	}

	function modificationFuelPowerLong(item) {
		if (!item) { return ''; }
		var kw = cleanPowerNum(item.POWER_KW || item.POWER_KW_START || '');
		var hp = cleanPowerNum(item.POWER_PS || item.POWER_HP || item.POWER_PS_START || '');
		var power = '';
		if (kw && hp) { power = kw + ' kW / ' + hp + ' Hp'; }
		else if (kw) { power = kw + ' kW'; }
		else if (hp) { power = hp + ' Hp'; }
		var fuel = text(item.FUEL_TYPE || item.fuel || '');
		if (/hybrid|phev|plug/i.test(fuel)) { fuel = 'Petrol/Electro'; }
		else if (/electric|\bev\b/i.test(fuel)) { fuel = 'Electric'; }
		return [power, fuel].filter(Boolean).join(' ');
	}

	function vehicleSpecLine(ctx) {
		if (!ctx || !ctx.car) { return ''; }
		var car = ctx.car;
		var model = ctx.model;
		var parts = [];
		var body = modificationBodyLabel(car);
		var trim = modificationTrimName(car);
		parts.push(trim ? (body + ' ' + trim).trim() : body);
		parts.push(modificationYearLongDisplay(car) || modelYearRange(model));
		var fuel = text(car.FUEL_TYPE || car.fuel || '');
		if (fuel) { parts.push(fuel); }
		var liter = modificationLiter(car);
		if (liter && liter !== '--') { parts.push(liter + 'L'); }
		var codes = modificationEngineCodesDisplay(car);
		if (codes) { parts.push('(' + codes + ')'); }
		var power = modificationPowerDisplay(car);
		if (power) { parts.push(power); }
		parts.push(modificationDriveLabel(car));
		return parts.filter(Boolean).join(', ');
	}

	function vehicleBannerText(ctx) {
		if (!ctx) { return ''; }
		var makeName = text((ctx.make && (ctx.make.MANUFACTURER || ctx.make.name)) || '');
		var modelName = modelDisplayName(ctx.model || {}) || text((ctx.model && (ctx.model.MODEL_SERIES || ctx.model.name)) || '');
		var trim = modificationTrimName(ctx.car || {});
		var body = modificationBodyLabel(ctx.car || {});
		var year = modificationYearBannerDisplay(ctx.car || {});
		var codes = modificationEngineCodesDisplay(ctx.car || {});
		var line = [makeName, modelName].filter(Boolean).join(' ');
		var sub = [trim, body].filter(Boolean).join(' ');
		if (sub) { line += ', ' + sub; }
		if (year) { line += ', ' + year; }
		if (codes) { line += ' (' + codes + ')'; }
		return line;
	}

	function renderVehicleContextBar(ctx, options) {
		options = options || {};
		if (!ctx) { return ''; }
		var make = ctx.make || {};
		var model = ctx.model || {};
		var car = ctx.car || {};
		var makeName = text(make.MANUFACTURER || make.name || '');
		var modelName = modelDisplayName(model) || text(model.MODEL_SERIES || model.name || '');
		var trimName = modificationTrimName(car);
		var bcLabel = modificationBreadcrumbLabel(car) || trimName || text(car.MODIFICATION || car.carName || car.title || '');
		var homeHref = options.homeHref || ctx.homeHref || '#';
		var garageHref = options.garageHref || (ctx.langHref || options.langHref || '') + '/garazh/bloknot?garage=0';
		var showGarage = options.showGarage !== false;
		var html = '<div class="epc-cm-veh-chrome">';

		html += '<div class="epc-cm-veh-topbar">';
		html += '<div class="epc-cm-veh-topbar-logo">' + manufacturerLogoHtml(make) + '</div>';
		html += '<div class="epc-cm-veh-topbar-selects">';
		if (makeName) {
			html += '<button type="button" class="epc-cm-veh-topbar-select" data-bc="make">' + esc(makeName) + ' <i class="fa fa-caret-down"></i></button>';
		}
		if (modelName) {
			html += '<button type="button" class="epc-cm-veh-topbar-select" data-bc="model">' + esc(modelName) + ' <i class="fa fa-caret-down"></i></button>';
		}
		if (trimName) {
			html += '<button type="button" class="epc-cm-veh-topbar-select" data-bc="engine">' + esc(trimName) + ' <i class="fa fa-caret-down"></i></button>';
		}
		html += '</div>';
		html += '<div class="epc-cm-veh-topbar-thumb">' + modelImageHtml(model) + '</div>';
		html += '<div class="epc-cm-veh-topbar-spec">' + esc(vehicleSpecLine(ctx)) + '</div>';
		if (showGarage) {
			html += '<a class="epc-cm-veh-garage-btn" href="' + esc(garageHref) + '" title="Add to garage"><i class="fa fa-car"></i> Add to garage</a>';
		}
		html += '</div>';

		var banner = vehicleBannerText(ctx);
		if (banner) {
			html += '<div class="epc-cm-veh-banner">' + esc(banner) + '</div>';
		}

		html += '<nav class="epc-cm-veh-bc" aria-label="Vehicle">';
		html += '<a href="' + esc(homeHref) + '">Home</a><span class="epc-cm-veh-bc-sep">›</span>';
		if (makeName) {
			html += '<button type="button" class="epc-cm-veh-bc-btn" data-bc="make">' + esc(makeName) + '</button><span class="epc-cm-veh-bc-sep">›</span>';
		}
		if (modelName) {
			html += '<button type="button" class="epc-cm-veh-bc-btn" data-bc="model">' + esc(modelName) + '</button><span class="epc-cm-veh-bc-sep">›</span>';
		}
		if (bcLabel) {
			html += '<span class="epc-cm-veh-bc-current">' + esc(bcLabel) + '</span>';
		}
		html += '</nav>';

		var bodyLabel = modificationBodyLabel(car);
		var driveLabel = modificationDriveLabel(car);
		var yearLong = modificationYearLongDisplay(car) || modelYearRange(model);
		var liter = modificationLiter(car);
		var engineCodes = modificationEngineCodesDisplay(car);
		var fuelPower = modificationFuelPowerLong(car);
		html += '<div class="epc-cm-veh-hero-grid">';
		html += '<div class="epc-cm-veh-card">';
		html += '<div class="epc-cm-veh-card-icon"><i class="fa fa-car"></i></div>';
		html += '<div class="epc-cm-veh-card-meta"><span>' + esc(bodyLabel) + '</span><span>' + esc(driveLabel) + '</span></div>';
		html += '<div class="epc-cm-veh-card-titles">';
		html += '<strong class="epc-cm-veh-card-model">' + esc(modelName) + '</strong>';
		html += '<strong class="epc-cm-veh-card-trim">' + esc(trimName) + '</strong>';
		html += '<strong class="epc-cm-veh-card-year">' + esc(yearLong) + '</strong>';
		html += '</div>';
		html += '<div class="epc-cm-veh-card-tools"><i class="fa fa-filter" title="Filters"></i><i class="fa fa-refresh" title="Service parts"></i></div>';
		html += '</div>';
		html += '<div class="epc-cm-veh-engine-card">';
		html += '<div class="epc-cm-veh-engine-liter">' + esc(liter !== '--' ? liter : '') + '</div>';
		html += '<div class="epc-cm-veh-engine-codes">' + esc(engineCodes) + '</div>';
		html += '<div class="epc-cm-veh-engine-power">' + esc(fuelPower) + '</div>';
		html += '</div>';
		html += '</div>';

		html += '</div>';
		return html;
	}

	function bindVehicleContextBar(container, ctx, options) {
		if (!container || !ctx) { return; }
		options = options || {};
		var cb = ctx.callbacks || options.callbacks || {};
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-veh-bc-btn, .epc-cm-veh-topbar-select'), function (btn) {
			btn.onclick = function () {
				var key = btn.getAttribute('data-bc');
				if (key === 'make' && typeof cb.onMake === 'function') { cb.onMake(); return; }
				if (key === 'model' && typeof cb.onModel === 'function') { cb.onModel(); return; }
				if (key === 'engine' && typeof cb.onEngine === 'function') { cb.onEngine(); return; }
			};
		});
	}

	function renderCategoryTreeNodes(categories, options, level) {
		if (!categories || !categories.length) { return ''; }
		var subMap = options.subcategoriesMap || {};
		var activeId = options.activeStrId || '';
		var activeSub = options.activeSubName || '';
		var html = '<ul class="epc-cm-cat-tree-level" data-level="' + level + '">';
		categories.forEach(function (c) {
			var sid = categoryId(c);
			var name = categoryName(c);
			var children = categoryChildren(c, subMap);
			var hasChild = children.length > 0;
			var isActive = String(sid) === String(activeId);
			var isOpen = hasChild && (options.expandAll || isActive || (options.expandedIds && options.expandedIds[String(sid)]));
			html += '<li class="epc-cm-cat-tree-li' + (hasChild ? ' has-children' : '') + (isOpen ? ' is-open' : '') + '" data-search="' + esc(name) + '">';
			html += '<div class="epc-cm-cat-tree-row">';
			if (hasChild) {
				html += '<button type="button" class="epc-cm-cat-tree-toggle" aria-expanded="' + (isOpen ? 'true' : 'false') + '" aria-label="Expand ' + esc(name) + '"><i class="fa ' + (isOpen ? 'fa-chevron-down' : 'fa-chevron-right') + ' epc-cm-cat-tree-icon"></i></button>';
			} else {
				html += '<span class="epc-cm-cat-tree-toggle is-spacer"></span>';
			}
			html += '<button type="button" class="epc-cm-cat-tree-item' + (isActive && !activeSub ? ' is-active' : '') + '" data-str="' + esc(sid) + '" data-name="' + esc(name) + '">' + esc(name) + '</button>';
			html += '</div>';
			if (hasChild) {
				html += '<div class="epc-cm-cat-tree-children"' + (isOpen ? '' : ' hidden') + '><ul>';
				children.forEach(function (sub) {
					var subName = subcategoryName(sub);
					var subId = subcategoryId(sub);
					var subActive = isActive && (activeSub === subName || String(subId) === String(options.activeSubId || ''));
					html += '<li data-search="' + esc(subName) + '"><button type="button" class="epc-cm-cat-tree-subitem' + (subActive ? ' is-active' : '') + '" data-parent-str="' + esc(sid) + '" data-sub-str="' + esc(subId) + '" data-sub-name="' + esc(subName) + '">' + esc(subName) + '</button></li>';
				});
				html += '</ul></div>';
			}
			html += '</li>';
		});
		html += '</ul>';
		return html;
	}

	function renderCategoryTreeSidebar(categories, options) {
		options = options || {};
		var html = '<aside class="epc-cm-cat-sidebar"><div class="epc-cm-cat-sidebar-head">Sections</div><div class="epc-cm-cat-sidebar-search"><input type="search" class="form-control input-sm epc-cm-section-q" placeholder="FIND SECTION.." aria-label="Find section"></div>';
		html += '<div class="epc-cm-cat-tree">' + renderCategoryTreeNodes(categories, options, 0) + '</div></aside>';
		return html;
	}

	function bindCategoryTreeSidebar(container, onSelect, options) {
		if (!container) { return; }
		var search = container.querySelector('.epc-cm-section-q');
		if (search) {
			search.oninput = function () {
				var term = (search.value || '').toLowerCase();
				Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-cat-tree-li'), function (li) {
					var parentMatch = (li.getAttribute('data-search') || '').toLowerCase().indexOf(term) !== -1;
					var childMatch = false;
					Array.prototype.forEach.call(li.querySelectorAll('.epc-cm-cat-tree-subitem'), function (sub) {
						var sm = (sub.getAttribute('data-sub-name') || '').toLowerCase().indexOf(term) !== -1;
						sub.parentNode.style.display = term && !sm && !parentMatch ? 'none' : '';
						if (sm) { childMatch = true; }
					});
					li.style.display = !term || parentMatch || childMatch ? '' : 'none';
					if (term && (parentMatch || childMatch) && li.classList.contains('has-children')) {
						li.classList.add('is-open');
						var ch = li.querySelector('.epc-cm-cat-tree-children');
						if (ch) { ch.hidden = false; }
						var tg = li.querySelector('.epc-cm-cat-tree-toggle i');
						if (tg) { tg.className = 'fa fa-chevron-down epc-cm-cat-tree-icon'; }
					}
				});
			};
		}
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-cat-tree-toggle'), function (btn) {
			btn.onclick = function (e) {
				e.stopPropagation();
				var li = btn.closest('.epc-cm-cat-tree-li');
				if (!li) { return; }
				var open = li.classList.toggle('is-open');
				var ch = li.querySelector('.epc-cm-cat-tree-children');
				if (ch) { ch.hidden = !open; }
				btn.setAttribute('aria-expanded', open ? 'true' : 'false');
				var icon = btn.querySelector('i');
				if (icon) { icon.className = 'fa ' + (open ? 'fa-chevron-down' : 'fa-chevron-right') + ' epc-cm-cat-tree-icon'; }
			};
		});
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-cat-tree-item'), function (btn) {
			btn.onclick = function () {
				Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-cat-tree-item, .epc-cm-cat-tree-subitem'), function (b) { b.classList.remove('is-active'); });
				btn.classList.add('is-active');
				if (typeof onSelect === 'function') {
					onSelect(btn.getAttribute('data-str'), btn.getAttribute('data-name'), btn, null);
				}
			};
		});
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-cat-tree-subitem'), function (btn) {
			btn.onclick = function () {
				Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-cat-tree-item, .epc-cm-cat-tree-subitem'), function (b) { b.classList.remove('is-active'); });
				btn.classList.add('is-active');
				if (typeof onSelect === 'function') {
					onSelect(btn.getAttribute('data-parent-str'), btn.getAttribute('data-sub-name'), btn, btn.getAttribute('data-sub-str'));
				}
			};
		});
	}

	function renderCategoryWorkspace(categories, options) {
		options = options || {};
		if (options.sortByOrder !== false) {
			categories = sortCategoriesByOrder(categories);
		}
		var html = '';
		if (options.vehicleCtx) {
			html += renderVehicleContextBar(options.vehicleCtx, options);
		}
		var treeOpts = Object.assign({}, options, {
			subcategoriesMap: options.subcategoriesMap || {}
		});
		html += '<div class="epc-cm-catalog-layout">';
		html += renderCategoryTreeSidebar(categories, treeOpts);
		html += '<div class="epc-cm-catalog-main">' + renderCategoryGrid(categories, treeOpts) + '</div></div>';
		return html;
	}

	function bindCategoryWorkspace(container, categories, options, onSelect) {
		if (!container) { return; }
		options = options || {};
		if (options.vehicleCtx) {
			bindVehicleContextBar(container, options.vehicleCtx, options);
		}
		bindCategoryTreeSidebar(container, onSelect, options);
		bindCategoryGrid(container.querySelector('.epc-cm-catalog-main') || container, onSelect, options);
	}

	function renderPartsListCategoryHead(catName, count) {
		var icon = isServicePartsCategory(catName) ? 'fa-wrench' : categoryIcon(catName);
		return '<div class="epc-cm-parts-cat-head"><div class="epc-cm-parts-cat-icon"><i class="fa ' + icon + '"></i></div>' +
			'<div><strong>' + esc(catName || 'Category') + '</strong><span>' + (count || 0) + ' parts</span></div></div>';
	}

	function articleImageUrl(item) {
		if (item.article_image_url || item.ARTICLE_IMAGE || item.IMG_SRC) {
			return text(item.article_image_url || item.ARTICLE_IMAGE || item.IMG_SRC);
		}
		var art = articleNumber(item);
		var brand = articleBrand(item);
		if (art && brand) {
			return '/api/umapi_image.php?kind=article&brand=' + encodeURIComponent(brand) + '&article=' + encodeURIComponent(art);
		}
		return '';
	}

	function articleCriteriaText(item) {
		if (item.CRITERIA) { return text(item.CRITERIA); }
		if (item.criteria) { return text(item.criteria); }
		var crits = item.CRITERIAS || item.criterias;
		if (crits && typeof crits === 'object') {
			var parts = [];
			Object.keys(crits).forEach(function (key) {
				var val = text(crits[key]);
				if (!val) { return; }
				parts.push(key ? key + ' - ' + val : val);
			});
			return parts.join('; ');
		}
		return '';
	}

	function articlePropsList(item) {
		var props = item.PROPS || item.props || item.properties;
		if (!props || typeof props !== 'object') { return []; }
		return Object.keys(props).map(function (key) {
			return { name: key, value: text(props[key]) };
		}).filter(function (p) { return p.name; });
	}

	function articleThumbHtml(item) {
		var url = articleImageUrl(item);
		if (url) {
			return '<div class="epc-cm-tdlist-photo"><img src="' + esc(url) + '" alt="" loading="lazy" decoding="async"></div>';
		}
		return '<div class="epc-cm-tdlist-photo is-logo">' + supplierLogoHtml(item) + '</div>';
	}

	function renderPartsListHead() {
		return '<div class="epc-cm-tdlist-head">' +
			'<div class="epc-cm-tdlist-head-name">Brand — Number, Name</div>' +
			'<div class="epc-cm-tdlist-head-meta">' +
			'<span class="epc-cm-tdlist-head-avail">Avail.</span>' +
			'<span class="epc-cm-tdlist-head-shpt">shpt</span>' +
			'<span class="epc-cm-tdlist-head-action">Action</span>' +
			'</div></div>';
	}

	function renderPartsListRow(item, options) {
		options = options || {};
		var art = articleNumber(item);
		var brand = articleBrand(item);
		var group = articleGroup(item);
		var shopUrl = typeof options.partSearchUrl === 'function' ? options.partSearchUrl(brand, art) : '#';
		var productUrl = typeof options.productDetailUrl === 'function' ? options.productDetailUrl(brand, art) : shopUrl;
		var checkLabel = options.checkPriceLabel || 'Check price';
		var criteria = articleCriteriaText(item);
		var props = articlePropsList(item);
		var propsHtml = props.length ? '<ul class="epc-cm-tdlist-props">' + props.slice(0, 5).map(function (p) {
			return '<li><span class="epc-cm-tdlist-prop-name">' + esc(p.name) + ':</span> <span class="epc-cm-tdlist-prop-val">' + esc(p.value || '—') + '</span></li>';
		}).join('') + '</ul>' : '';
		return '<div class="epc-cm-tdlist-item is-clickable" data-search="' + esc(art + ' ' + brand + ' ' + group) + '" data-brand="' + esc(brand) + '" data-product-url="' + esc(productUrl) + '">' +
			'<div class="epc-cm-tdlist-parthead">' +
			'<div class="epc-cm-tdlist-parthead-left">' +
			'<span class="epc-cm-tdlist-camera"><i class="fa fa-camera"></i></span>' +
			'<span class="epc-cm-tdbrand">' + esc(brand) + '</span>' +
			'<span class="epc-cm-tdarticle">' + esc(art) + '</span>' +
			'</div>' +
			'<div class="epc-cm-tdlist-parthead-name"><a class="epc-cm-tdname" href="' + esc(productUrl) + '"><b>' + esc(group) + '</b></a></div>' +
			'</div>' +
			'<div class="epc-cm-tdlist-cols">' +
			'<div class="epc-cm-tdlist-cols-left">' + articleThumbHtml(item) +
			(criteria ? '<div class="epc-cm-tdlist-criteria">' + esc(criteria) + '</div>' : '') +
			propsHtml +
			'</div>' +
			'<div class="epc-cm-tdlist-cols-right">' +
			'<div class="epc-cm-tdlist-price-row">' +
			'<span class="epc-cm-tdlist-avail">—</span>' +
			'<span class="epc-cm-tdlist-shpt"><i class="fa fa-check-square-o"></i> Stock today</span>' +
			'<span class="epc-cm-tdlist-price-action">' +
			'<a class="epc-cm-list-ask btn btn-xs btn-check-price" href="' + esc(shopUrl) + '" onclick="event.stopPropagation();"><i class="fa fa-search"></i> ' + esc(checkLabel) + '</a>' +
			(options.showCart !== false ? '<a class="btn btn-xs btn-cart" href="' + esc(shopUrl) + '" onclick="event.stopPropagation();"><i class="fa fa-shopping-cart"></i></a>' : '') +
			'</span></div></div></div></div>';
	}

	function renderArticlesWorkspace(categories, rows, options) {
		options = options || {};
		var panelOptions = options.panelOptions || {};
		panelOptions.categoryName = panelOptions.categoryName || options.categoryName || '';
		var built = renderArticlesPanel(rows, panelOptions);
		var html = '';
		if (options.vehicleCtx) {
			html += renderVehicleContextBar(options.vehicleCtx, options);
		}
		html += '<div class="epc-cm-catalog-layout is-articles">';
		html += renderCategoryTreeSidebar(categories, {
			activeStrId: options.activeStrId,
			activeSubName: options.activeSubName,
			subcategoriesMap: options.subcategoriesMap,
			expandedIds: options.expandedIds || {}
		});
		html += '<div class="epc-cm-catalog-main">';
		if (panelOptions.partsListLayout && panelOptions.categoryName) {
			html += renderPartsListCategoryHead(panelOptions.categoryName, built.visible.length);
		}
		html += built.html;
		html += '</div></div>';
		return { html: html, visible: built.visible, filters: built.filters };
	}

	function bindArticlesWorkspace(container, categories, rows, options, onCategorySelect) {
		if (!container) { return; }
		options = options || {};
		var panelOptions = options.panelOptions || {};
		if (options.vehicleCtx) {
			bindVehicleContextBar(container, options.vehicleCtx, options);
		}
		bindCategoryTreeSidebar(container, onCategorySelect, options);
		var main = container.querySelector('.epc-cm-catalog-main');
		if (main) {
			bindArticlesPanel(main, rows, panelOptions, options.onPanelChange);
		}
	}

	function renderCategoryGrid(categories, options) {
		options = options || {};
		var barLabel = options.barLabel || 'Catalog';
		var icons = options.icons || DEFAULT_CATEGORY_ICONS;
		var showIcons = options.showIcons !== false;
		var initialLimit = parseInt(options.initialLimit, 10) || 0;
		var linkBase = typeof options.linkBase === 'function' ? options.linkBase : null;
		var iconBase = resolveIconBase(options);
		if (options.sortByOrder !== false) {
			categories = sortCategoriesByOrder(categories);
		}
		var html = '';
		if (options.carModHeading !== false) {
			html += renderCatalogHeading({ title: options.headingTitle || 'Catalog', subtitle: options.headingSubtitle || 'find by category or assembly' });
		}
		if (options.search !== false) {
			html += '<div class="epc-cm-cat-filter"><i class="fa fa-search"></i><input type="search" class="form-control epc-vc-cat-search" placeholder="Find category.." aria-label="Find category"></div>';
		}
		if (barLabel && options.showBar !== false) {
			html += '<div class="epc-vc-section-bar">' + esc(barLabel) + ' · Categories</div>';
		}
		if (options.showCount !== false) {
			html += '<p class="epc-vc-list-count">' + categories.length + ' categories</p>';
		}
		html += '<div class="epc-vc-cat-grid' + (showIcons ? '' : ' is-no-icons') + (initialLimit > 0 ? ' has-collapsed' : '') + '">' +
			categories.map(function (c, idx) {
				var name = categoryName(c);
				var sid = categoryId(c);
				var children = categoryChildren(c, options.subcategoriesMap || {});
				var iconHtml = showIcons
					? '<div class="epc-vc-cat-icon">' + categoryIconHtml(c, icons, iconBase) + '</div>'
					: '';
				var collapsed = initialLimit > 0 && idx >= initialLimit ? ' is-collapsed' : '';
				var cardInner = iconHtml + '<strong>' + esc(name) + '</strong>' + renderCategoryFlyout(sid, name, children);
				var href = linkBase ? linkBase(sid, name, c) : '';
				var wrapCls = 'epc-vc-cat-card-wrap' + (children.length ? ' has-flyout' : '');
				if (href) {
					return '<div class="' + wrapCls + '"><a class="epc-vc-cat-card' + collapsed + '" href="' + esc(href) + '" data-search="' + esc(name) + '" data-str="' + esc(sid) + '">' + cardInner + '</a></div>';
				}
				return '<div class="' + wrapCls + '"><div class="epc-vc-cat-card' + collapsed + '" data-search="' + esc(name) + '" data-str="' + esc(sid) + '">' + cardInner + '</div></div>';
			}).join('') + '</div>';
		if (initialLimit > 0 && categories.length > initialLimit) {
			html += '<div class="epc-cm-show-all-wrap"><button type="button" class="epc-cm-show-all-sections">' +
				esc(options.showAllLabel || 'Show all sections') + ' <i class="fa fa-chevron-down"></i></button></div>';
		}
		return html;
	}

	function cleanPowerNum(v) {
		var n = parseFloat(String(v == null ? '' : v).replace(',', '.'));
		if (isNaN(n)) {
			return text(v).replace(/\.0+$/, '').replace(/(\.\d*?)0+$/, '$1');
		}
		return String(Math.round(n) === n ? Math.round(n) : n);
	}

	function modificationYearDisplay(item, options) {
		options = options || {};
		return modificationYearLongDisplay(item);
	}

	function modificationEngineCode(item) {
		if (!item) { return ''; }
		var code = text(item.ENGINE_CODE || item.ENG_CODE || item.ENGINE || item.engine_code || '');
		if (code) { return code; }
		var name = text(item.MODIFICATION || item.PASSENGER_CAR || item.carName || item.title || item.name || '');
		var m = name.match(/\(([A-Z0-9][A-Z0-9\-]+)\)/);
		return m ? m[1] : '';
	}

	function modificationId(item) {
		if (!item || typeof item !== 'object') { return 0; }
		var raw = item.ID !== undefined && item.ID !== null && item.ID !== '' ? item.ID
			: (item.carId !== undefined && item.carId !== null && item.carId !== '' ? item.carId
			: (item.PC_ID || item.TYP_ID || item.ext_id || item.modification_id || ''));
		raw = text(raw).trim();
		if (!raw) { return 0; }
		var num = parseInt(raw, 10);
		if (!isNaN(num) && String(num) === raw) { return num; }
		return raw;
	}

	function parseCarcatYear(value) {
		return normalizeCatalogYear(value);
	}

	function carcatValueYears(value) {
		if (!value || typeof value !== 'object') { return { from: '', to: '' }; }
		var desc = value.description;
		if (!desc || typeof desc !== 'object') { desc = {}; }
		return {
			from: parseCarcatYear(desc.year_from || desc.yearFrom || value.year_from || ''),
			to: parseCarcatYear(desc.year_to || desc.yearTo || value.year_to || '')
		};
	}

	function carcatParamValue(params, keys) {
		if (!params || !params.length) { return ''; }
		var want = (keys || []).map(function (k) { return String(k).toLowerCase(); });
		var match = params.find(function (p) {
			var key = text(p.key).toLowerCase();
			var name = text(p.name).toLowerCase();
			return want.indexOf(key) !== -1 || want.some(function (needle) { return name.indexOf(needle) !== -1; });
		});
		return match ? text(match.value).trim() : '';
	}

	function parseCarcatPowerString(value) {
		var out = { kw: '', ps: '' };
		value = text(value).trim();
		if (!value) { return out; }
		var kwMatch = value.match(/(\d+(?:[.,]\d+)?)\s*k?w\b/i);
		var psMatch = value.match(/(\d+(?:[.,]\d+)?)\s*(?:hp|ps|bhp|л\.?\s?с\.?)\b/i);
		if (kwMatch) { out.kw = cleanPowerNum(kwMatch[1]); }
		if (psMatch) { out.ps = cleanPowerNum(psMatch[1]); }
		if (!out.kw && !out.ps && /^\d+(?:[.,]\d+)?$/.test(value)) {
			out.kw = cleanPowerNum(value);
		}
		return out;
	}

	function parseCarcatCapacityString(value) {
		value = text(value).trim();
		if (!value) { return ''; }
		var m = value.match(/(\d+(?:[.,]\d+)?)\s*(?:l|liter|litre|л)\b/i) || value.match(/^(\d+(?:[.,]\d+)?)/);
		return m ? cleanPowerNum(m[1]) : value;
	}

	function parseCarcatFuelLabel(value) {
		value = text(value).trim();
		if (!value) { return ''; }
		var lower = value.toLowerCase();
		if (/diesel/.test(lower)) { return 'Diesel'; }
		if (/petrol|gasoline|\bgas\b|benzin|essence/.test(lower)) { return 'Petrol'; }
		if (/electric|\bev\b/.test(lower)) { return 'Electric'; }
		if (/hybrid|phev|hev|plug/.test(lower)) { return value; }
		return value;
	}

	function carcatCatalogImageSlug(catalogId) {
		catalogId = text(catalogId).trim();
		if (catalogId.indexOf('pl_') === 0) {
			return catalogId.slice(3);
		}
		return catalogId;
	}

	function carcatModelImageFromId(catalogId, imgId) {
		imgId = text(imgId).trim().replace(/\\\//g, '/');
		catalogId = text(catalogId).trim();
		if (!imgId || !catalogId) { return ''; }
		var slug = carcatCatalogImageSlug(catalogId);
		if (!slug) { return ''; }
		return '/api/carcat_image.php?path=' + encodeURIComponent('static/images/' + slug + '/' + imgId + '.png');
	}

	function carcatModelImage(row, catalogId) {
		var img = text(row.img || row.modelImg || row.image || row.image_url || '');
		if (!img && row.description && typeof row.description === 'object') {
			img = text(row.description.img || row.description.image || '');
		}
		if (!img) {
			var imgId = text(row.img_id || '');
			if (imgId) {
				img = carcatModelImageFromId(catalogId || row.catalogId || row.catalog_id || '', imgId);
			}
		}
		return img;
	}

	function carcatBrandImageFromCatalogId(catalogId) {
		catalogId = text(catalogId);
		if (!catalogId || catalogId.indexOf('pl_') !== 0) {
			return '';
		}
		return '/api/carcat_image.php?path=' + encodeURIComponent('static/brands/' + catalogId + '.png');
	}

	function normalizeCarcatCatalog(row) {
		if (!row || typeof row !== 'object') { return row; }
		var id = text(row.id || row.catalogId || '');
		var img = text(row.img || row.brandLogo || '');
		if (!img) {
			img = carcatBrandImageFromCatalogId(id);
		}
		return {
			MFA_ID: id,
			id: id,
			MANUFACTURER: text(row.name || row.brand || ''),
			name: text(row.name || row.brand || ''),
			img: img,
			source: 'carcat'
		};
	}

	function normalizeCarcatModel(row, makeName, catalogId) {
		if (!row || typeof row !== 'object') { return row; }
		var years = carcatValueYears(row);
		if (!years.from && row.description && typeof row.description === 'object') {
			years = carcatValueYears({ description: row.description });
		}
		var id = text(row.id || row.modelId || '');
		var name = text(row.name || row.modelName || '');
		var img = carcatModelImage(row, catalogId);
		var toYear = years.to;
		if (toYear && toYear !== '9999' && parseInt(toYear, 10) >= currentCatalogYear()) {
			toYear = '9999';
		}
		return {
			MS_ID: id,
			id: id,
			MODEL_SERIES: name,
			name: name,
			MANUFACTURER: text(makeName || row.brand || row.manufacturer || ''),
			img: img,
			image_url: img,
			scope: text(row.scope || ''),
			CI_FROM: years.from,
			CI_TO: toYear || years.to,
			source: 'carcat',
			_carcat: row
		};
	}

	function normalizeCarcatModels(items, makeName, catalogId) {
		return (items || []).map(function (row) {
			return normalizeCarcatModel(row, makeName, catalogId);
		});
	}

	function normalizeCarcatParameterOption(value, param) {
		if (!value || typeof value !== 'object') { return value; }
		var years = carcatValueYears(value);
		var label = text(value.value).trim();
		var code = (value.description && value.description.code) ? text(value.description.code).replace(/\\#/g, '#') : '';
		return normalizeModificationItem({
			ID: text(value.idx),
			carId: text(value.idx),
			MODIFICATION: label,
			carName: label,
			PASSENGER_CAR: label,
			ENGINE_CODE: code,
			CI_FROM: years.from,
			CI_TO: years.to,
			FUEL_TYPE: '',
			BODY_TYPE: '',
			_carcatParam: {
				idx: text(value.idx),
				paramKey: param && param.key ? param.key : '',
				paramName: param && param.name ? param.name : ''
			}
		});
	}

	function normalizeCarcatCar(car) {
		if (!car || typeof car !== 'object') { return car; }
		var params = Array.isArray(car.parameters) ? car.parameters : [];
		var specs = car.specs && typeof car.specs === 'object' ? car.specs : {};
		var modelCode = carcatParamValue(params, ['model_code']);
		var yearFrom = '';
		var yearMatch = modelCode.match(/(?:from|since)\s*(\d{4})/i) || modelCode.match(/(\d{4})[-/]/);
		if (yearMatch) { yearFrom = parseCarcatYear(yearMatch[1]); }
		if (!yearFrom) { yearFrom = parseCarcatYear(carcatParamValue(params, ['year', 'model_year'])); }
		if (!yearFrom && specs.year) { yearFrom = parseCarcatYear(specs.year); }
		var trim = text(car.title || car.name || carcatParamValue(params, ['model', 'model_code']) || car.modelName);
		var engine = carcatParamValue(params, ['engine', 'engine_code', 'motor']);
		if (!engine && specs.engineCode) { engine = text(specs.engineCode); }
		var body = carcatParamValue(params, ['body', 'body_type']);
		if (!body && specs.body) { body = text(specs.body); }
		var drive = carcatParamValue(params, ['drive', 'drive_type']);
		if (!drive && specs.drive) { drive = text(specs.drive); }
		var fuelRaw = carcatParamValue(params, ['fuel', 'fuel_type', 'engine_type']);
		if (!fuelRaw && specs.engine) { fuelRaw = text(specs.engine); }
		if (!fuelRaw && specs.fuel) { fuelRaw = text(specs.fuel); }
		var fuel = parseCarcatFuelLabel(fuelRaw);
		var powerRaw = carcatParamValue(params, ['power', 'power_kw', 'engine_power', 'max_power']);
		var powerParsed = parseCarcatPowerString(powerRaw);
		var kw = cleanPowerNum(specs.power_kw || specs.powerKw || powerParsed.kw || carcatParamValue(params, ['power_kw', 'max_power_kw']));
		var ps = cleanPowerNum(specs.power_ps || specs.powerPs || specs.horsepower || powerParsed.ps || carcatParamValue(params, ['power_ps', 'horsepower', 'hp', 'max_power_ps']));
		if (!ps && kw) {
			var hpNum = Math.round(parseFloat(String(kw).replace(',', '.')) * 1.341);
			if (!isNaN(hpNum) && hpNum > 0) { ps = String(hpNum); }
		}
		var capacityRaw = carcatParamValue(params, ['displacement', 'engine_capacity', 'capacity', 'volume', 'litre', 'liter', 'engine_size']);
		if (!capacityRaw && specs.capacity_lt) { capacityRaw = text(specs.capacity_lt); }
		if (!capacityRaw && specs.displacement) { capacityRaw = text(specs.displacement); }
		var capacity = parseCarcatCapacityString(capacityRaw);
		var codeMatch = modelCode.match(/\(([A-Z0-9][A-Z0-9\-]+)\)/i);
		return normalizeModificationItem({
			ID: car.id || car.carId,
			carId: car.id || car.carId,
			MODIFICATION: trim,
			carName: trim,
			PASSENGER_CAR: trim,
			ENGINE_CODE: engine || (codeMatch ? codeMatch[1] : ''),
			BODY_TYPE: body,
			DRIVE_TYPE: drive,
			FUEL_TYPE: fuel,
			POWER_KW: kw,
			POWER_PS: ps,
			CAPACITY_LT: capacity,
			CI_FROM: yearFrom,
			_carcatCar: car
		});
	}

	function normalizeCarcatCars(items) {
		return (items || []).map(normalizeCarcatCar);
	}

	function normalizeCarcatGroup(group) {
		if (!group || typeof group !== 'object') { return group; }
		var order = parseInt(group.description, 10);
		if (isNaN(order)) { order = 0; }
		return normalizeCategoryItem({
			STR_ID: group.id,
			CATEGORY_ID: group.id,
			CATEGORY_NAME: text(group.name),
			name: text(group.name),
			img: text(group.img || ''),
			ORDER: order,
			hasSubgroups: !!group.hasSubgroups,
			hasParts: !!group.hasParts,
			source: 'carcat'
		});
	}

	function normalizeCarcatGroups(items) {
		return (items || []).map(normalizeCarcatGroup);
	}

	function normalizeModificationItem(row) {
		if (!row || typeof row !== 'object') { return row; }
		var raw = row.raw_json;
		if (typeof raw === 'string' && raw) {
			try { raw = JSON.parse(raw); } catch (e) { raw = null; }
		}
		var base = raw && typeof raw === 'object' ? raw : row;
		var rawId = text(row.ID || row.carId || base.ID || base.carId || '').trim();
		var id = modificationId(base) || modificationId(row);
		if (!id && rawId) { id = rawId; }
		var title = text(base.MODIFICATION || base.PASSENGER_CAR || base.COMMERCIAL_VEHICLE || base.MOTORBIKE || row.title || base.title || base.name || row.name || '');
		var out = {
			ID: id,
			carId: id,
			ext_id: id || parseInt(row.ext_id || 0, 10),
			PC_ID: parseInt(base.PC_ID || row.PC_ID || id || 0, 10) || id,
			CV_ID: parseInt(base.CV_ID || row.CV_ID || 0, 10) || 0,
			MTB_ID: parseInt(base.MTB_ID || row.MTB_ID || 0, 10) || 0,
			MODIFICATION: title,
			carName: title,
			PASSENGER_CAR: text(base.PASSENGER_CAR || row.PASSENGER_CAR || title),
			COMMERCIAL_VEHICLE: text(base.COMMERCIAL_VEHICLE || row.COMMERCIAL_VEHICLE || ''),
			MOTORBIKE: text(base.MOTORBIKE || row.MOTORBIKE || ''),
			POWER_KW: cleanPowerNum(base.POWER_KW || row.power_kw || row.POWER_KW || ''),
			POWER_PS: cleanPowerNum(base.POWER_PS || row.power_ps || row.POWER_PS || ''),
			FUEL_TYPE: text(base.FUEL_TYPE || row.fuel_type || row.FUEL_TYPE || base.FUEL || row.FUEL || ''),
			ENGINE_TYPE: text(base.ENGINE_TYPE || row.engine_type || row.ENGINE_TYPE || ''),
			ENGINE_CODE: text(base.ENGINE_CODE || base.ENG_CODE || row.engine_code || row.ENGINE_CODE || row.ENG_CODE || ''),
			DRIVE_TYPE: text(base.DRIVE_TYPE || row.drive_type || row.DRIVE_TYPE || base.AXLE_CONFIGURATION || row.AXLE_CONFIGURATION || ''),
			BODY_TYPE: text(base.BODY_TYPE || row.body_type || row.BODY_TYPE || base.PLATFORM_TYPE || row.PLATFORM_TYPE || ''),
			MOTOR_CODE: text(base.MOTOR_CODE || row.motor_code || row.MOTOR_CODE || base.E_MOTOR_CODE || row.E_MOTOR_CODE || ''),
			CAPACITY_LT: text(base.CAPACITY_LT || row.capacity_lt || row.CAPACITY_LT || ''),
			CAPACITY: text(base.CAPACITY || base.CAPACITY_TECH || row.CAPACITY || ''),
			CI_FROM: normalizeCatalogYear(base.CI_FROM || row.year_from || row.CI_FROM || ''),
			CI_TO: normalizeCatalogYear(base.CI_TO || row.year_to || row.CI_TO || ''),
			source: text(row.source || base.source || '')
		};
		if (row._carcatParam || base._carcatParam) { out._carcatParam = row._carcatParam || base._carcatParam; }
		if (row._carcatCar || base._carcatCar) { out._carcatCar = row._carcatCar || base._carcatCar; }
		if (row._carcat || base._carcat) { out._carcat = row._carcat || base._carcat; }
		return out;
	}

	function normalizeModifications(items) {
		return (items || []).map(normalizeModificationItem);
	}

	function dedupeModifications(items) {
		var seen = {};
		var out = [];
		normalizeModifications(items).forEach(function (item) {
			var id = modificationId(item);
			var key = (id !== 0 && id !== '' && id !== null && id !== undefined) ? String(id) : [
				modificationTrimName(item),
				modificationYearDisplay(item),
				modificationPowerDisplay(item),
				modificationEngineCode(item),
				text(item.ENGINE_TYPE || '')
			].join('|');
			if (seen[key]) { return; }
			seen[key] = true;
			out.push(item);
		});
		return out;
	}

	function modificationLiter(item) {
		var fuel = text(item.FUEL_TYPE || item.fuel || '').toLowerCase();
		if (/electric|\bev\b/.test(fuel)) { return '--'; }
		var cap = text(item.CAPACITY_LT || item.CAPACITY || item.displacement || '');
		var m = cap.match(/^(\d+(?:\.\d+)?)/);
		if (m) { return m[1]; }
		var name = text(item.MODIFICATION || item.PASSENGER_CAR || item.carName || item.title || '');
		m = name.match(/^(\d+(?:\.\d+)?)/);
		return m ? m[1] : '--';
	}

	function modificationTrimName(item) {
		var name = text(item.MODIFICATION || item.PASSENGER_CAR || item.carName || item.title || item.name || '');
		var known = ['DM-p Hybrid', 'DM-i Hybrid', 'DM Hybrid', 'Plug-In Hybrid', 'PHEV', 'HEV', 'EV', 'Hybrid', 'Electric'];
		var i;
		for (i = 0; i < known.length; i++) {
			if (new RegExp('\\b' + known[i].replace(/[-/\\^$*+?.()|[\]{}]/g, '\\$&') + '\\b', 'i').test(name)) {
				if (known[i].toLowerCase() === 'electric') { return 'EV'; }
				return known[i];
			}
		}
		var first = name.split(',')[0].trim().replace(/^\d+(?:\.\d+)?\s*/, '').trim();
		first = first.replace(/\b(All-wheel Drive|Front-Wheel Drive|Plug-In Hybrid|Hybrid|Electric|PHEV|HEV)\b.*$/i, '').trim();
		return first || name.split(',')[0].trim() || name;
	}

	function modificationEngineTypeLabel(item) {
		var engineType = text(item.ENGINE_TYPE || item.engine_type || '');
		var fuel = text(item.FUEL_TYPE || item.fuel || '');
		if (/plug-in hybrid|phev/i.test(engineType)) { return engineType; }
		if (/electric motor/i.test(engineType)) { return 'Electric'; }
		if (/electric|\bev\b/i.test(fuel) && !/petrol|diesel|hybrid|\/electric/.test(fuel)) { return 'Electric'; }
		if (/hybrid|plug|petrol\/electric/i.test(fuel + ' ' + engineType)) {
			return engineType || 'Plug-In Hybrid';
		}
		return engineType || fuel;
	}

	function modificationEngineTypeParts(item) {
		var label = modificationEngineTypeLabel(item);
		var codes = modificationEngineCodesDisplay(item);
		return { label: label, codes: codes, search: [label, codes].filter(Boolean).join(' ') };
	}

	function modificationFuelCategory(item) {
		var fuel = text(item.FUEL_TYPE || item.fuel || '').toLowerCase();
		if (/diesel/.test(fuel)) { return 'diesel'; }
		if (/petrol|gasoline|\bgas\b|benzin|essence/.test(fuel)) { return 'petrol'; }
		if (/hybrid|phev|hev|plug/.test(fuel)) { return 'hybrid'; }
		if (/electric|\bev\b/.test(fuel)) { return 'electric'; }
		if (/lpg|cng|ethanol|flex/.test(fuel)) { return 'gas'; }
		return 'other';
	}

	function modificationFuelLabel(key) {
		var labels = {
			petrol: 'Petrol',
			diesel: 'Diesel',
			hybrid: 'Hybrid',
			electric: 'Electric',
			gas: 'Gas / LPG',
			other: 'Other'
		};
		return labels[key] || 'Other';
	}

	function modificationFuelOrder() {
		return ['petrol', 'diesel', 'hybrid', 'electric', 'gas', 'other'];
	}

	function groupModificationsByFuel(items) {
		var groups = {};
		(items || []).forEach(function (item, index) {
			var key = modificationFuelCategory(item);
			if (!groups[key]) { groups[key] = []; }
			groups[key].push({ item: item, index: index });
		});
		return groups;
	}

	function modificationEngineTypeHtml(item) {
		var parts = modificationEngineTypeParts(item);
		var label = parts.label;
		var html = '';
		if (label) {
			var cls = /^(electric)$/i.test(label) ? 'epc-cm-mod-fuel-link' : 'epc-cm-mod-engine-type-label';
			html += '<span class="' + cls + '">' + esc(label) + '</span>';
		}
		if (parts.codes) {
			parts.codes.split(/,\s*/).forEach(function (code) {
				code = code.trim();
				if (!code || code.toLowerCase() === label.toLowerCase()) { return; }
				html += (html ? ',&nbsp;' : '') + '<span class="epc-cm-mod-engine-code">' + esc(code) + '</span>';
			});
		}
		return html || esc(text(item.MODIFICATION || item.PASSENGER_CAR || item.carName || ''));
	}

	function modificationPowerDisplay(item) {
		var kw = cleanPowerNum(item.POWER_KW || item.POWER_KW_START || '');
		var hp = cleanPowerNum(item.POWER_PS || item.POWER_HP || item.POWER_PS_START || '');
		if (kw && hp) { return kw + 'Kw/' + hp + 'Hp'; }
		if (kw) { return kw + 'Kw'; }
		if (hp) { return hp + 'Hp'; }
		return '';
	}

	function modificationDriveKind(item) {
		var drive = text(item.DRIVE_TYPE || item.drive || '');
		var name = text(item.MODIFICATION || item.PASSENGER_CAR || item.carName || item.title || '');
		if (!drive && /\b4WD\b/i.test(name)) { drive = '4WD'; }
		if (!drive && /\bAWD\b/i.test(name)) { drive = 'AWD'; }
		if (/all\s*wd|awd|4wd|4x4/i.test(drive + ' ' + name)) { return 'awd'; }
		if (/rear\s*wd|rwd/i.test(drive + ' ' + name)) { return 'rwd'; }
		return 'fwd';
	}

	function renderDriveIconHtml(item, options) {
		options = options || {};
		var kind = modificationDriveKind(item);
		var title = kind === 'awd' ? 'All WD' : (kind === 'rwd' ? 'Rear WD' : 'Front WD');
		var iconMap = {
			awd: '/content/files/epc-cata/drive-icons/allwd.png',
			fwd: '/content/files/epc-cata/drive-icons/frontwd.png',
			rwd: '/content/files/epc-cata/drive-icons/rearwd.png'
		};
		return '<img class="epc-cm-mod-drive-icon' + (options.compact ? ' is-compact' : '') + '" src="' + esc(iconMap[kind] || iconMap.fwd) + '" alt="' + esc(title) + '" title="' + esc(title) + '" loading="lazy" width="38" height="22">';
	}

	function modificationTableHeading(makeName, modelItem) {
		var modelSeries = text(modelItem && (modelItem.MODEL_SERIES || modelItem.name || modelItem.title || ''));
		var from = normalizeCatalogYear(modelItem && (modelItem.CI_FROM || modelItem.year_from || ''));
		return [text(makeName), modelSeries, from ? from + '-xx' : ''].filter(Boolean).join(' ');
	}

	function collectModificationFilters(items) {
		var liters = {};
		var trims = {};
		var years = {};
		(items || []).forEach(function (item) {
			var liter = modificationLiter(item);
			if (liter) { liters[liter] = true; }
			var trim = modificationTrimName(item);
			if (trim) { trims[trim] = true; }
			var year = modificationYearDisplay(item, { fullYear: true });
			if (year) { years[year] = true; }
		});
		return {
			liters: Object.keys(liters).sort(function (a, b) {
				if (a === '--') { return 1; }
				if (b === '--') { return -1; }
				return parseFloat(a) - parseFloat(b);
			}),
			trims: Object.keys(trims).sort(function (a, b) { return a.localeCompare(b); }),
			years: Object.keys(years).sort(function (a, b) {
				return (parseInt(String(b).slice(0, 4), 10) || 0) - (parseInt(String(a).slice(0, 4), 10) || 0);
			})
		};
	}

	function renderModificationFilterSelect(label, filterKey, values, placeholder) {
		var html = '<label class="epc-cm-mod-filter"><span class="epc-cm-mod-filter-label">' + esc(label) + '</span><select class="form-control input-sm epc-cm-mod-filter-select" data-mod-filter="' + esc(filterKey) + '">';
		html += '<option value="">' + esc(placeholder || label) + '</option>';
		(values || []).forEach(function (value) {
			html += '<option value="' + esc(value) + '">' + esc(value) + '</option>';
		});
		html += '</select></label>';
		return html;
	}

	function renderModificationTableRow(item, index, options) {
		options = options || {};
		var liter = modificationLiter(item);
		var trim = modificationTrimName(item);
		var year = modificationYearDisplay(item, { fullYear: options.fullYear !== false });
		var power = modificationPowerDisplay(item);
		var selected = options.selectedId && String(modificationId(item)) === String(options.selectedId);
		var paramIdx = item._carcatParam && item._carcatParam.idx ? text(item._carcatParam.idx) : '';
		var engineText = modificationEngineTypeParts(item);
		var search = [liter, trim, year, engineText.search, power, modificationDriveKind(item)].join(' ');
		return '<button type="button" class="epc-cm-mod-row' + (selected ? ' is-selected' : '') + '" data-index="' + index + '"' + (paramIdx ? ' data-parameter-idx="' + esc(paramIdx) + '"' : '') + ' data-liter="' + esc(liter) + '" data-trim="' + esc(trim) + '" data-year="' + esc(year) + '" data-engine="' + esc(engineText.search) + '" data-search="' + esc(search) + '">' +
			'<span class="epc-cm-mod-col is-liter">' + esc(liter) + '</span>' +
			'<span class="epc-cm-mod-col is-model"><span class="epc-cm-mod-trim-link">' + esc(trim) + '</span></span>' +
			'<span class="epc-cm-mod-col is-year">' + esc(year) + '</span>' +
			'<span class="epc-cm-mod-col is-engine">' + modificationEngineTypeHtml(item) + '</span>' +
			'<span class="epc-cm-mod-col is-power">' + esc(power) + '</span>' +
			'<span class="epc-cm-mod-col is-drive">' + renderDriveIconHtml(item) + '</span>' +
			'</button>';
	}

	function renderModificationTable(items, options) {
		options = options || {};
		items = dedupeModifications(items);
		var filters = collectModificationFilters(items);
		var heading = options.heading || modificationTableHeading(options.makeName, options.modelItem || { MODEL_SERIES: options.modelName });
		var html = '<div class="epc-cm-mod-window">';
		html += '<div class="epc-cm-mod-window-head"><h1>' + esc(heading) + '</h1></div>';
		if (options.makeName) {
			html += '<div class="epc-cm-mod-breadcrumb"><button type="button" class="epc-cm-mod-back" data-mod-back="1" aria-label="Back to models"><i class="fa fa-home"></i></button>' +
				'<span class="epc-cm-mod-crumb-sep">&rsaquo;</span><span>' + esc(options.makeName) + '</span>' +
				(options.modelName ? '<span class="epc-cm-mod-crumb-sep">&rsaquo;</span><span>' + esc(options.modelName) + '</span>' : '') +
				'</div>';
		}
		html += '<div class="epc-cm-mod-table-wrap">';
		html += '<div class="epc-cm-mod-table-head">';
		html += renderModificationFilterSelect('L.', 'liter', filters.liters, 'L.');
		html += renderModificationFilterSelect('Model', 'trim', filters.trims, 'Model');
		html += renderModificationFilterSelect('Year', 'year', filters.years, 'Year');
		html += '<label class="epc-cm-mod-filter is-engine"><span class="epc-cm-mod-filter-label">Engine type</span><input type="search" class="form-control input-sm epc-cm-mod-engine-q" placeholder="Engine type" aria-label="Filter engine type"></label>';
		html += '<span class="epc-cm-mod-head-power">Power</span>';
		html += '<span class="epc-cm-mod-head-drive">Drive</span>';
		html += '</div>';
		html += '<div class="epc-cm-mod-table-body">';
		if (options.groupByFuel !== false) {
			var fuelGroups = groupModificationsByFuel(items);
			modificationFuelOrder().forEach(function (fuelKey) {
				var rows = fuelGroups[fuelKey];
				if (!rows || !rows.length) { return; }
				html += '<div class="epc-cm-mod-fuel-group" data-fuel="' + esc(fuelKey) + '">';
				html += '<div class="epc-cm-mod-fuel-head">' + esc(modificationFuelLabel(fuelKey)) + ' <span>' + rows.length + '</span></div>';
				rows.forEach(function (entry) {
					html += renderModificationTableRow(entry.item, entry.index, options);
				});
				html += '</div>';
			});
		} else {
			items.forEach(function (item, index) {
				html += renderModificationTableRow(item, index, options);
			});
		}
		html += '</div>';
		html += '<p class="epc-cm-mod-count">' + items.length + ' modifications</p>';
		html += '</div></div>';
		return html;
	}

	function filterModificationTableRows(container) {
		if (!container) { return 0; }
		var liter = (container.querySelector('.epc-cm-mod-filter-select[data-mod-filter="liter"]') || {}).value || '';
		var trim = (container.querySelector('.epc-cm-mod-filter-select[data-mod-filter="trim"]') || {}).value || '';
		var year = (container.querySelector('.epc-cm-mod-filter-select[data-mod-filter="year"]') || {}).value || '';
		var engineQ = ((container.querySelector('.epc-cm-mod-engine-q') || {}).value || '').toLowerCase();
		var visible = 0;
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-mod-row'), function (row) {
			var show = true;
			if (liter && row.getAttribute('data-liter') !== liter) { show = false; }
			if (show && trim && row.getAttribute('data-trim') !== trim) { show = false; }
			if (show && year && row.getAttribute('data-year') !== year) { show = false; }
			if (show && engineQ && row.getAttribute('data-engine').toLowerCase().indexOf(engineQ) === -1) { show = false; }
			row.style.display = show ? '' : 'none';
			if (show) { visible++; }
		});
		var count = container.querySelector('.epc-cm-mod-count');
		if (count) { count.textContent = visible + ' modifications'; }
		return visible;
	}

	function bindModificationTable(container, items, onSelect, tableOptions) {
		if (!container) { return; }
		tableOptions = tableOptions || {};
		var host = container.querySelector('.epc-cm-mod-window') || container;
		Array.prototype.forEach.call(host.querySelectorAll('.epc-cm-mod-row'), function (btn) {
			btn.onclick = function () {
				var rowIdx = parseInt(btn.getAttribute('data-index'), 10);
				var item = !isNaN(rowIdx) ? items[rowIdx] : null;
				if (!item && btn.getAttribute('data-parameter-idx')) {
					var want = text(btn.getAttribute('data-parameter-idx'));
					item = items.find(function (row) {
						var meta = row && row._carcatParam;
						return meta && text(meta.idx) === want;
					}) || null;
				}
				if (item && typeof onSelect === 'function') {
					onSelect(item, rowIdx, btn);
				}
			};
		});
		Array.prototype.forEach.call(host.querySelectorAll('.epc-cm-mod-filter-select, .epc-cm-mod-engine-q'), function (el) {
			el.onchange = el.oninput = function () { filterModificationTableRows(host); };
		});
		var backBtn = host.querySelector('[data-mod-back]');
		if (backBtn && typeof tableOptions.onBack === 'function') {
			backBtn.onclick = function () { tableOptions.onBack(); };
		}
	}

	function renderModificationGrid(items, options) {
		options = options || {};
		if (options.carModLayout === false) {
			var title = options.title || 'Select engine / modification';
			items = dedupeModifications(items);
			return '<p class="epc-vc-section-title">' + esc(title) + '</p>' +
				'<p class="epc-vc-list-count">' + items.length + ' modifications</p>' +
				'<div class="epc-cm-mod-grid">' + items.map(function (item, index) {
					var name = text(item.MODIFICATION || item.carName || item.title || item.name || 'Modification');
					var meta = [modificationPowerDisplay(item), item.FUEL_TYPE || item.fuel || ''].filter(Boolean).join(' · ');
					var selected = options.selectedId && modificationId(item) === parseInt(options.selectedId, 10);
					return '<div class="epc-cm-mod-card' + (selected ? ' is-selected' : '') + '" data-index="' + index + '" data-search="' + esc(name + ' ' + meta) + '">' +
						'<strong>' + esc(name) + '</strong>' +
						(meta ? '<small>' + esc(meta) + '</small>' : '') +
						'</div>';
				}).join('') + '</div>';
		}
		return renderModificationTable(items, options);
	}

	function bindModificationGrid(container, items, onSelect, gridOptions) {
		if (!container) { return; }
		items = dedupeModifications(items);
		if (container.querySelector('.epc-cm-mod-window')) {
			bindModificationTable(container, items, onSelect, gridOptions || {});
			return;
		}
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-mod-card'), function (card) {
			card.onclick = function () {
				var idx = parseInt(card.getAttribute('data-index'), 10);
				if (!isNaN(idx) && items[idx] && typeof onSelect === 'function') {
					onSelect(items[idx], idx, card);
				}
			};
		});
	}

	function renderArticleRow(item, options) {
		options = options || {};
		var art = articleNumber(item);
		var brand = articleBrand(item);
		var group = articleGroup(item);
		var src = options.showSource ? text(item.source || '') : '';
		var shopUrl = typeof options.partSearchUrl === 'function' ? options.partSearchUrl(brand, art) : '#';
		var productUrl = typeof options.productDetailUrl === 'function' ? options.productDetailUrl(brand, art) : '';
		var searchLabel = options.searchLabel || 'Price';
		var cartLabel = options.cartLabel || 'Cart';
		var warehouseOnly = options.warehouseOnly !== false && !!options.carModLayout;
		var checkLabel = options.checkPriceLabel || (warehouseOnly ? 'Check price' : searchLabel);
		if (options.carModLayout) {
			var detailHref = productUrl || shopUrl;
			return '<div class="epc-vc-article-row is-clickable" data-search="' + esc(art + ' ' + brand + ' ' + group) + '" data-product-url="' + esc(detailHref) + '">' +
				'<div class="epc-vc-article-thumb">' + supplierLogoHtml(item) + '</div>' +
				'<div><a class="epc-vc-article-link" href="' + esc(detailHref) + '"><span class="epc-vc-article-oem">' + esc(art) + '</span></a></div>' +
				'<div><a class="epc-vc-article-link" href="' + esc(detailHref) + '"><span class="epc-vc-article-brand">' + esc(brand) + '</span></a></div>' +
				'<div><a class="epc-vc-article-link" href="' + esc(detailHref) + '"><strong>' + esc(group) + '</strong></a>' +
				(src ? '<span class="epc-vc-source-tag">' + esc(src) + '</span>' : '') + '</div>' +
				'<div class="epc-vc-article-actions">' +
				'<a class="btn btn-xs btn-check-price" href="' + esc(shopUrl) + '" title="Check warehouse stock &amp; live price" onclick="event.stopPropagation();"><i class="fa fa-search"></i> ' + esc(checkLabel) + '</a>' +
				(options.showCart !== false ? '<a class="btn btn-xs btn-cart" href="' + esc(shopUrl) + '" title="Shop &amp; cart" onclick="event.stopPropagation();"><i class="fa fa-shopping-cart"></i>' + (cartLabel === 'Cart' ? '' : ' ' + esc(cartLabel)) + '</a>' : '') +
				'</div></div>';
		}
		return '<div class="epc-vc-article-row" data-search="' + esc(art + ' ' + brand + ' ' + group) + '">' +
			'<div class="epc-vc-article-thumb">' + supplierLogoHtml(item) + '</div>' +
			'<div><span class="epc-vc-article-oem">' + esc(art) + '</span> <span class="epc-vc-article-brand">' + esc(brand) + '</span>' +
			(group ? '<br><small>' + esc(group) + '</small>' : '') +
			(src ? '<span class="epc-vc-source-tag">' + esc(src) + '</span>' : '') + '</div>' +
			'<div class="epc-vc-article-actions">' +
			'<a class="btn btn-xs btn-success" href="' + esc(shopUrl) + '"><i class="fa fa-search"></i> ' + esc(searchLabel) + '</a>' +
			'<a class="btn btn-xs btn-cart" href="' + esc(shopUrl) + '"><i class="fa fa-shopping-cart"></i> ' + esc(cartLabel) + '</a>' +
			'</div></div>';
	}

	function bindMakeGrid(container, items, onSelect) {
		if (!container) { return; }
		var search = container.querySelector('.epc-vc-make-search');
		if (search) {
			search.oninput = function () {
				var term = (search.value || '').toLowerCase();
				Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-make-card'), function (card) {
					card.style.display = card.getAttribute('data-search').toLowerCase().indexOf(term) === -1 ? 'none' : '';
				});
			};
		}
		Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-make-card'), function (card) {
			card.onclick = function () {
				var idx = parseInt(card.getAttribute('data-index'), 10);
				if (!isNaN(idx) && items[idx] && typeof onSelect === 'function') {
					onSelect(items[idx], idx, card);
				}
			};
		});
	}

	function bindModelGrid(container, items, onSelect, gridOptions) {
		if (!container) { return; }
		gridOptions = gridOptions || {};
		function pickModelIndex(node) {
			var yearSelect = container.querySelector('.epc-cm-model-year-filter');
			var selectedYear = yearSelect ? (yearSelect.value || '') : '';
			var variantAttr = node.getAttribute('data-model-variant-indices') || '';
			var variantIndices = variantAttr ? variantAttr.split(',').map(function (v) { return parseInt(v, 10); }).filter(function (v) { return !isNaN(v); }) : [];
			if (variantIndices.length > 1) {
				return resolveModelSelectionIndex(items, variantIndices, selectedYear);
			}
			if (variantIndices.length === 1) {
				return variantIndices[0];
			}
			var idx = parseInt(node.getAttribute('data-index') || node.getAttribute('data-model-index'), 10);
			return isNaN(idx) ? -1 : idx;
		}
		Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-model-card'), function (card) {
			card.onclick = function () {
				var idx = pickModelIndex(card);
				if (idx >= 0 && items[idx] && typeof onSelect === 'function') {
					onSelect(items[idx], idx, card);
				}
			};
			card.onkeydown = function (event) {
				if (event.key !== 'Enter' && event.key !== ' ') { return; }
				event.preventDefault();
				var idx = pickModelIndex(card);
				if (idx >= 0 && items[idx] && typeof onSelect === 'function') {
					onSelect(items[idx], idx, card);
				}
			};
		});
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-model-letter-tab'), function (btn) {
			btn.onclick = function () {
				Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-model-letter-tab'), function (el) {
					el.classList.toggle('active', el === btn);
				});
				resetModelLetterIndexCollapse(container);
				filterModelGridRows(container);
			};
		});
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-model-letter-name'), function (btn) {
			btn.onclick = function () {
				var letter = btn.getAttribute('data-model-letter') || 'All';
				var tab = container.querySelector('.epc-cm-model-letter-tab[data-model-letter="' + letter + '"]');
				if (tab) {
					Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-model-letter-tab'), function (el) {
						el.classList.toggle('active', el === tab);
					});
					resetModelLetterIndexCollapse(container);
					filterModelGridRows(container);
				}
				var idx = pickModelIndex(btn);
				if (idx >= 0 && items[idx] && typeof onSelect === 'function') {
					onSelect(items[idx], idx, btn);
				}
			};
		});
		var showAllBtn = container.querySelector('.epc-cm-model-show-all');
		if (showAllBtn) {
			showAllBtn.onclick = function () {
				var letterIndex = container.querySelector('.epc-cm-model-letter-index');
				if (letterIndex) { letterIndex.classList.add('is-expanded'); }
				var wrap = showAllBtn.closest('.epc-cm-model-show-all-wrap');
				if (wrap) { wrap.style.display = 'none'; }
			};
		}
		var yearSelect = container.querySelector('.epc-cm-model-year-filter');
		if (yearSelect) {
			yearSelect.onchange = function () {
				resetModelLetterIndexCollapse(container);
				filterModelGridRows(container);
			};
		}
		var backBtn = container.querySelector('[data-model-back]');
		if (backBtn && typeof gridOptions.onBack === 'function') {
			backBtn.onclick = function () { gridOptions.onBack(); };
		}
		filterModelGridRows(container);
	}

	function bindCategoryGrid(container, onSelect, options) {
		if (!container) { return; }
		options = options || {};
		var search = container.querySelector('.epc-vc-cat-search');
		var grid = container.querySelector('.epc-vc-cat-grid');
		var showAllWrap = container.querySelector('.epc-cm-show-all-wrap');
		var flyoutTimer = null;
		function closeFlyouts(exceptWrap) {
			Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-cat-card-wrap.has-flyout'), function (wrap) {
				if (exceptWrap && wrap === exceptWrap) { return; }
				wrap.classList.remove('is-flyout-open');
				var fly = wrap.querySelector('.epc-cm-cat-flyout');
				if (fly) { fly.hidden = true; fly.setAttribute('aria-hidden', 'true'); }
			});
		}
		function openFlyout(wrap) {
			if (!wrap) { return; }
			closeFlyouts(wrap);
			wrap.classList.remove('is-flyout-left');
			wrap.classList.add('is-flyout-open');
			var fly = wrap.querySelector('.epc-cm-cat-flyout');
			if (fly) {
				fly.hidden = false;
				fly.setAttribute('aria-hidden', 'false');
				var rect = wrap.getBoundingClientRect();
				if (rect.right + 240 > window.innerWidth) {
					wrap.classList.add('is-flyout-left');
				}
			}
		}
		function applyCollapsedVisibility(term) {
			var expanded = grid && grid.classList.contains('is-expanded');
			Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-cat-card-wrap'), function (wrap) {
				var card = wrap.querySelector('.epc-vc-cat-card');
				if (!card) { return; }
				if (term) {
					var parentMatch = card.getAttribute('data-search').toLowerCase().indexOf(term) !== -1;
					var childMatch = false;
					Array.prototype.forEach.call(wrap.querySelectorAll('.epc-cm-cat-flyout-item'), function (btn) {
						if ((btn.getAttribute('data-sub-name') || '').toLowerCase().indexOf(term) !== -1) { childMatch = true; }
					});
					wrap.style.display = parentMatch || childMatch ? '' : 'none';
					if (childMatch && !parentMatch) { openFlyout(wrap); }
					return;
				}
				if (!expanded && card.classList.contains('is-collapsed')) {
					wrap.style.display = 'none';
					return;
				}
				wrap.style.display = '';
			});
			if (showAllWrap) { showAllWrap.style.display = term ? 'none' : ''; }
		}
		if (search) {
			search.oninput = function () { applyCollapsedVisibility((search.value || '').toLowerCase()); };
			applyCollapsedVisibility('');
		}
		var showAllBtn = container.querySelector('.epc-cm-show-all-sections');
		if (showAllBtn) {
			showAllBtn.onclick = function () {
				if (grid) { grid.classList.add('is-expanded'); }
				Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-cat-card.is-collapsed'), function (card) {
					card.classList.remove('is-collapsed');
				});
				if (showAllWrap) { showAllWrap.style.display = 'none'; }
				applyCollapsedVisibility(search ? (search.value || '').toLowerCase() : '');
			};
		}
		Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-cat-card-wrap.has-flyout'), function (wrap) {
			wrap.onmouseenter = function () {
				clearTimeout(flyoutTimer);
				flyoutTimer = setTimeout(function () { openFlyout(wrap); }, 120);
			};
			wrap.onmouseleave = function () {
				clearTimeout(flyoutTimer);
				flyoutTimer = setTimeout(function () {
					wrap.classList.remove('is-flyout-open');
					var fly = wrap.querySelector('.epc-cm-cat-flyout');
					if (fly) { fly.hidden = true; fly.setAttribute('aria-hidden', 'true'); }
				}, 180);
			};
		});
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-cat-flyout-item'), function (btn) {
			btn.onclick = function (e) {
				e.preventDefault();
				e.stopPropagation();
				if (typeof onSelect === 'function') {
					onSelect(btn.getAttribute('data-parent-str'), btn.getAttribute('data-sub-name'), btn, btn.getAttribute('data-sub-str'));
					return;
				}
				var href = btn.getAttribute('data-href');
				if (href) { window.location.href = href; }
			};
		});
		Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-cat-card'), function (card) {
			card.onclick = function (e) {
				if (e.target && e.target.closest && e.target.closest('.epc-cm-cat-flyout-item')) { return; }
				var wrap = card.closest('.epc-vc-cat-card-wrap.has-flyout');
				if (wrap) {
					e.preventDefault();
					openFlyout(wrap);
					return;
				}
				if (typeof onSelect !== 'function') { return; }
				if (card.hasAttribute && card.hasAttribute('href')) {
					e.preventDefault();
				}
				var strong = card.querySelector('strong');
				onSelect(card.getAttribute('data-str'), strong ? strong.textContent : '', card, null);
			};
		});
	}

	function collectBrandFilters(rows) {
		var brands = {};
		(rows || []).forEach(function (row) {
			var brand = articleBrand(row);
			if (!brand) { return; }
			brands[brand] = (brands[brand] || 0) + 1;
		});
		return Object.keys(brands).sort(function (a, b) { return a.localeCompare(b); }).map(function (brand) {
			return { brand: brand, count: brands[brand] };
		});
	}

	function sortArticles(rows, sortKey) {
		var list = (rows || []).slice();
		sortKey = sortKey || 'brand';
		list.sort(function (a, b) {
			var av = sortKey === 'article' ? articleNumber(a) : (sortKey === 'name' ? articleGroup(a) : articleBrand(a));
			var bv = sortKey === 'article' ? articleNumber(b) : (sortKey === 'name' ? articleGroup(b) : articleBrand(b));
			return text(av).localeCompare(text(bv));
		});
		return list;
	}

	function filterArticles(rows, activeBrands, term) {
		term = (term || '').toLowerCase();
		var brandSet = activeBrands && activeBrands.length ? activeBrands : null;
		return (rows || []).filter(function (row) {
			var brand = articleBrand(row);
			if (brandSet && brandSet.indexOf(brand) === -1) { return false; }
			if (!term) { return true; }
			var hay = (articleNumber(row) + ' ' + brand + ' ' + articleGroup(row)).toLowerCase();
			return hay.indexOf(term) !== -1;
		});
	}

	function renderArticleToolbar(options) {
		options = options || {};
		if (options.hideMeta) { return ''; }
		var view = options.view || 'list';
		var sort = options.sort || 'brand';
		var count = options.count || 0;
		return '<div class="epc-cm-articles-toolbar">' +
			'<div class="epc-cm-articles-meta"><strong>' + count + '</strong> parts · warehouse pricing only</div>' +
			'<div class="epc-cm-articles-controls">' +
			'<label class="epc-cm-sort-label">Sort <select class="form-control input-sm epc-cm-sort-select">' +
			'<option value="brand"' + (sort === 'brand' ? ' selected' : '') + '>By brand</option>' +
			'<option value="article"' + (sort === 'article' ? ' selected' : '') + '>By article</option>' +
			'<option value="name"' + (sort === 'name' ? ' selected' : '') + '>By name</option>' +
			'</select></label>' +
			'<div class="epc-cm-view-switch" role="group" aria-label="View mode">' +
			'<button type="button" class="epc-cm-view-btn' + (view === 'list' ? ' is-active' : '') + '" data-view="list" title="List view"><i class="fa fa-list"></i></button>' +
			'<button type="button" class="epc-cm-view-btn' + (view === 'card' ? ' is-active' : '') + '" data-view="card" title="Card view"><i class="fa fa-th-large"></i></button>' +
			'<button type="button" class="epc-cm-view-btn' + (view === 'compact' ? ' is-active' : '') + '" data-view="compact" title="Compact view"><i class="fa fa-bars"></i></button>' +
			'</div></div></div>';
	}

	function renderArticleFilterSidebar(filters, activeBrands, options) {
		options = options || {};
		if (!filters || filters.length < 2) { return ''; }
		activeBrands = activeBrands || [];
		var title = options.title || 'Filter by brand';
		return '<aside class="epc-cm-filter-box"><div class="epc-cm-filter-head">' + esc(title) + '</div>' +
			'<div class="epc-cm-filter-list">' + filters.map(function (f) {
				var checked = !activeBrands.length || activeBrands.indexOf(f.brand) !== -1;
				return '<label class="epc-cm-filter-item"><input type="checkbox" class="epc-cm-brand-filter" value="' + esc(f.brand) + '"' + (checked ? ' checked' : '') + '> ' +
					esc(f.brand) + ' <sup>' + f.count + '</sup></label>';
			}).join('') +
			(activeBrands.length ? '<button type="button" class="btn btn-link btn-xs epc-cm-filter-reset">Reset filter</button>' : '') +
			'</div></aside>';
	}

	function renderArticleCard(item, options) {
		options = options || {};
		var art = articleNumber(item);
		var brand = articleBrand(item);
		var group = articleGroup(item);
		var shopUrl = typeof options.partSearchUrl === 'function' ? options.partSearchUrl(brand, art) : '#';
		var productUrl = typeof options.productDetailUrl === 'function' ? options.productDetailUrl(brand, art) : shopUrl;
		var fitment = options.hideFitment ? '' : text(item.fitment || item.FITMENT || item.applicability || '');
		var checkLabel = options.checkPriceLabel || 'Check price';
		return '<div class="epc-cm-article-card" data-search="' + esc(art + ' ' + brand + ' ' + group + ' ' + fitment) + '" data-brand="' + esc(brand) + '">' +
			'<div class="epc-cm-article-card-thumb">' + supplierLogoHtml(item) + '</div>' +
			'<div class="epc-cm-article-card-body">' +
			'<a class="epc-vc-article-link" href="' + esc(productUrl) + '"><span class="epc-vc-article-oem">' + esc(art) + '</span></a>' +
			'<div class="epc-vc-article-brand">' + esc(brand) + '</div>' +
			'<strong>' + esc(group) + '</strong>' +
			(fitment ? '<div class="epc-cm-fitment-line">' + esc(fitment) + '</div>' : '') +
			'</div>' +
			'<div class="epc-cm-article-card-actions">' +
			'<a class="btn btn-xs btn-check-price" href="' + esc(shopUrl) + '"><i class="fa fa-search"></i> ' + esc(checkLabel) + '</a>' +
			(options.showCart !== false ? '<a class="btn btn-xs btn-cart" href="' + esc(shopUrl) + '"><i class="fa fa-shopping-cart"></i></a>' : '') +
			'</div></div>';
	}

	function renderArticleCompact(item, options) {
		options = options || {};
		var art = articleNumber(item);
		var brand = articleBrand(item);
		var group = articleGroup(item);
		var shopUrl = typeof options.partSearchUrl === 'function' ? options.partSearchUrl(brand, art) : '#';
		var productUrl = typeof options.productDetailUrl === 'function' ? options.productDetailUrl(brand, art) : shopUrl;
		var checkLabel = options.checkPriceLabel || 'Check price';
		return '<div class="epc-cm-article-compact" data-search="' + esc(art + ' ' + brand + ' ' + group) + '" data-brand="' + esc(brand) + '">' +
			'<div class="epc-cm-compact-oem"><a href="' + esc(productUrl) + '">' + esc(art) + '</a></div>' +
			'<div class="epc-cm-compact-brand">' + esc(brand) + '</div>' +
			'<div class="epc-cm-compact-name">' + esc(group) + '</div>' +
			'<div class="epc-cm-compact-actions"><a class="btn btn-xs btn-check-price" href="' + esc(shopUrl) + '">' + esc(checkLabel) + '</a></div></div>';
	}

	function renderArticleListBody(rows, options) {
		options = options || {};
		var view = options.view || 'list';
		var rowOpts = {
			partSearchUrl: options.partSearchUrl,
			productDetailUrl: options.productDetailUrl,
			carModLayout: options.carModLayout !== false,
			warehouseOnly: options.warehouseOnly !== false,
			checkPriceLabel: options.checkPriceLabel || 'Check price',
			cartLabel: options.cartLabel || 'Cart',
			showCart: options.showCart
		};
		if (options.partsListLayout && view === 'list') {
			return '<div class="epc-cm-tdlist">' + renderPartsListHead() +
				rows.map(function (r) { return renderPartsListRow(r, rowOpts); }).join('') + '</div>';
		}
		if (view === 'card') {
			return '<div class="epc-vc-articles is-card">' + rows.map(function (r) { return renderArticleCard(r, rowOpts); }).join('') + '</div>';
		}
		if (view === 'compact') {
			return '<div class="epc-vc-articles is-compact">' + rows.map(function (r) { return renderArticleCompact(r, rowOpts); }).join('') + '</div>';
		}
		return '<div class="epc-vc-articles">' + rows.map(function (r) { return renderArticleRow(r, rowOpts); }).join('') + '</div>';
	}

	function renderArticlesPanel(rows, panelOptions) {
		panelOptions = panelOptions || {};
		var sort = panelOptions.sort || 'brand';
		var view = panelOptions.view || 'list';
		var activeBrands = panelOptions.activeBrands || [];
		var term = panelOptions.term || '';
		var filters = collectBrandFilters(rows);
		var visible = filterArticles(sortArticles(rows, sort), activeBrands, term);
		var partsList = !!panelOptions.partsListLayout;
		var html = '<div class="epc-cm-articles-layout' + (filters.length > 1 ? ' has-filter' : '') + (partsList ? ' is-partslist' : '') + '">';
		html += renderArticleFilterSidebar(filters, activeBrands, { title: panelOptions.filterTitle || (partsList ? 'Filter by brand' : 'Manufacturers') });
		html += '<div class="epc-cm-articles-main">';
		html += renderArticleToolbar({ view: view, sort: sort, count: visible.length, hideMeta: partsList && view === 'list' });
		if (panelOptions.search !== false) {
			html += '<div class="epc-cm-articles-search"><input type="search" class="form-control epc-cm-articles-q" placeholder="Filter parts in list…" value="' + esc(term) + '"></div>';
		}
		html += renderArticleListBody(visible, panelOptions);
		html += '</div></div>';
		return { html: html, visible: visible, filters: filters };
	}

	function bindArticlesPanel(container, rows, panelOptions, onChange) {
		if (!container) { return; }
		panelOptions = panelOptions || {};
		function rerender() {
			var built = renderArticlesPanel(rows, panelOptions);
			var main = container.querySelector('.epc-cm-articles-main');
			if (main) {
				main.innerHTML = renderArticleToolbar({ view: panelOptions.view, sort: panelOptions.sort, count: built.visible.length, hideMeta: panelOptions.partsListLayout && panelOptions.view === 'list' }) +
					(panelOptions.search !== false ? '<div class="epc-cm-articles-search"><input type="search" class="form-control epc-cm-articles-q" placeholder="Filter parts in list…" value="' + esc(panelOptions.term || '') + '"></div>' : '') +
					renderArticleListBody(built.visible, panelOptions);
			} else {
				container.innerHTML = built.html;
			}
			wire();
			if (typeof onChange === 'function') { onChange(built.visible, panelOptions); }
		}
		function wire() {
			var sortSelect = container.querySelector('.epc-cm-sort-select');
			if (sortSelect) {
				sortSelect.onchange = function () {
					panelOptions.sort = sortSelect.value;
					rerender();
				};
			}
			Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-view-btn'), function (btn) {
				btn.onclick = function () {
					panelOptions.view = btn.getAttribute('data-view') || 'list';
					rerender();
				};
			});
			Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-brand-filter'), function (cb) {
				cb.onchange = function () {
					var selected = [];
					Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-brand-filter:checked'), function (c) {
						selected.push(c.value);
					});
					panelOptions.activeBrands = selected;
					rerender();
				};
			});
			var reset = container.querySelector('.epc-cm-filter-reset');
			if (reset) {
				reset.onclick = function () {
					panelOptions.activeBrands = [];
					rerender();
				};
			}
			var q = container.querySelector('.epc-cm-articles-q');
			if (q) {
				q.oninput = function () {
					panelOptions.term = q.value || '';
					rerender();
				};
			}
			if (typeof panelOptions.bindRows === 'function') {
				panelOptions.bindRows(container);
			}
		}
		wire();
	}

	function isInteractiveClickTarget(target) {
		if (!target || !target.closest) { return false; }
		return !!target.closest('a[href], button, input, select, textarea, label[for], .show_hide_button, .count_need_minus, .count_need_plus, .info_box, .epc-search-row-photo__btn, .td_add_to_cart, .epc-btn-cart, .epc-btn-quote');
	}

	function bindClickableRows(container, options) {
		options = options || {};
		if (!container) { return; }
		var selector = options.selector || '.epc-cm-tdlist-item.is-clickable, .epc-cm-article-card, .epc-cm-article-compact, .epc-vc-article-row.is-clickable';
		var skipSelector = options.skipSelector || 'a.btn, a.epc-cm-list-ask, a.btn-check-price, a.btn-cart, a.epc-vc-article-link';
		Array.prototype.forEach.call(container.querySelectorAll(selector), function (row) {
			if (row.getAttribute('data-epc-click-bound') === '1') { return; }
			row.setAttribute('data-epc-click-bound', '1');
			row.onclick = function (e) {
				if (isInteractiveClickTarget(e.target)) { return; }
				if (e.target && e.target.closest && e.target.closest(skipSelector)) { return; }
				var href = row.getAttribute('data-product-url') || row.getAttribute('data-epc-detail-url');
				if (!href) {
					var link = row.querySelector('a.epc-cm-tdname, a.epc-vc-article-link, .epc-cm-compact-oem a');
					href = link ? link.href : '';
				}
				if (href && href !== '#') {
					if (typeof options.onNavigate === 'function') {
						options.onNavigate(href, row, e);
					} else {
						window.location.href = href;
					}
				}
			};
		});
	}

	function bindSearchResultRows(container, options) {
		options = options || {};
		if (!container) { return; }
		var selector = options.selector || '#all_table_products tbody tr.epc-search-result-row, .epc-cross-missing-table tbody tr.epc-search-result-row, table.epc-cross-missing-table tbody tr.epc-search-result-row';
		Array.prototype.forEach.call(container.querySelectorAll(selector), function (row) {
			if (row.getAttribute('data-epc-click-bound') === '1') { return; }
			row.setAttribute('data-epc-click-bound', '1');
			row.onclick = function (e) {
				if (isInteractiveClickTarget(e.target)) { return; }
				var wrapId = row.getAttribute('data-epc-wrap-id');
				if (wrapId !== null && wrapId !== '') {
					var id = parseInt(wrapId, 10);
					if (!isNaN(id) && typeof window.wrap_states !== 'undefined' && window.wrap_states[id] === false && typeof window.show_hide_block === 'function') {
						window.show_hide_block(id, true);
						row.classList.add('is-expanded');
						return;
					}
				}
				var href = row.getAttribute('data-epc-detail-url') || row.getAttribute('data-product-url');
				if (href && href !== '#') {
					if (typeof options.onNavigate === 'function') {
						options.onNavigate(href, row, e);
					} else {
						window.location.href = href;
					}
				}
			};
		});
	}

	function bindSupplierPriceGrid(container, options) {
		options = options || {};
		if (!container) { return; }
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-supplier-row'), function (row) {
			if (row.getAttribute('data-epc-click-bound') === '1') { return; }
			var href = row.getAttribute('data-supplier-url') || '';
			if (!href) {
				var link = row.querySelector('a.btn-check-price, a[href]');
				href = link ? link.getAttribute('href') : '';
			}
			if (!href || href === '#') { return; }
			row.setAttribute('data-epc-click-bound', '1');
			row.classList.add('is-clickable');
			row.onclick = function (e) {
				if (isInteractiveClickTarget(e.target)) { return; }
				if (e.target && e.target.closest && e.target.closest('a.btn-check-price, a.btn')) { return; }
				if (typeof options.onNavigate === 'function') {
					options.onNavigate(href, row, e);
				} else {
					window.location.href = href;
				}
			};
		});
	}

	function renderSupplierPriceGrid(offers, options) {
		options = options || {};
		if (!offers || !offers.length) {
			return '<div class="epc-cm-supplier-grid is-empty"><p class="epc-vc-message">No warehouse offers loaded for this article. Use <strong>Check price</strong> to search live supplier stock.</p></div>';
		}
		var whUrl = typeof options.warehouseUrl === 'function' ? options.warehouseUrl : null;
		return '<div class="epc-cm-supplier-grid"><div class="epc-cm-supplier-grid-head">' +
			'<span>Supplier / warehouse</span><span>Availability</span><span>Action</span></div>' +
			offers.map(function (o) {
				var brand = text(o.brand || o.manufacturer || '');
				var art = text(o.article || options.article || '');
				var href = whUrl ? whUrl(brand, art) : text(o.url || '#');
				var label = text(o.label || o.warehouse || brand || 'Warehouse');
				var avail = text(o.availability || o.exist || o.stock || '—');
				return '<div class="epc-cm-supplier-row is-clickable" data-supplier-url="' + esc(href) + '">' +
					'<div class="epc-cm-supplier-name"><strong>' + esc(label) + '</strong>' +
					(brand ? '<small>' + esc(brand) + '</small>' : '') + '</div>' +
					'<div class="epc-cm-supplier-avail">' + esc(avail) + '</div>' +
					'<div class="epc-cm-supplier-action"><a class="btn btn-xs btn-check-price" href="' + esc(href) + '" onclick="event.stopPropagation();"><i class="fa fa-search"></i> Check price</a></div></div>';
			}).join('') + '</div>';
	}

	function renderBrandPickerDropdown(items, options) {
		options = options || {};
		var sections = options.sections && options.sections.length ? options.sections : DEFAULT_VEHICLE_SECTIONS;
		var section = text(options.section || 'passenger');
		var html = '<div class="epc-cm-mselect">';
		html += '<div class="epc-cm-mselect-main">';
		html += '<input type="search" class="form-control epc-cm-mselect-search" placeholder="Find brand" aria-label="Find brand">';
		html += '<div class="epc-cm-mselect-brands">' + (items || []).map(function (item, index) {
			var name = text(item.MANUFACTURER || item.name || '');
			return '<button type="button" class="epc-cm-mselect-brand" data-index="' + index + '" data-search="' + esc(name) + '">' +
				'<span class="epc-cm-mselect-brand-logo">' + manufacturerLogoHtml(item) + '</span>' +
				'<span class="epc-cm-mselect-brand-name">' + esc(name) + '</span></button>';
		}).join('') + '</div></div>';
		html += '<div class="epc-cm-mselect-types">' + sections.map(function (s) {
			var active = section === s.key ? ' is-active' : '';
			return '<button type="button" class="epc-cm-mselect-type' + active + '" data-section="' + esc(s.key) + '">' + esc(s.label) + '</button>';
		}).join('') + '</div></div>';
		return html;
	}

	function bindBrandPickerDropdown(container, items, onSelect, options) {
		if (!container) { return; }
		options = options || {};
		var host = container.querySelector('.epc-cm-mselect') || container;
		var search = host.querySelector('.epc-cm-mselect-search');
		if (search) {
			search.oninput = function () {
				var term = (search.value || '').toLowerCase();
				Array.prototype.forEach.call(host.querySelectorAll('.epc-cm-mselect-brand'), function (btn) {
					btn.style.display = btn.getAttribute('data-search').toLowerCase().indexOf(term) === -1 ? 'none' : '';
				});
			};
			search.onkeydown = function (e) {
				if (e.key === 'Escape') { e.stopPropagation(); }
			};
		}
		Array.prototype.forEach.call(host.querySelectorAll('.epc-cm-mselect-brand'), function (btn) {
			btn.onclick = function () {
				var idx = parseInt(btn.getAttribute('data-index'), 10);
				if (!isNaN(idx) && items[idx] && typeof onSelect === 'function') {
					onSelect(items[idx], idx, btn);
				}
			};
		});
		Array.prototype.forEach.call(host.querySelectorAll('.epc-cm-mselect-type'), function (btn) {
			btn.onclick = function (e) {
				e.stopPropagation();
				var sec = btn.getAttribute('data-section') || '';
				if (!sec || sec === options.section) { return; }
				Array.prototype.forEach.call(host.querySelectorAll('.epc-cm-mselect-type'), function (t) {
					t.classList.toggle('is-active', t === btn);
				});
				if (typeof options.onSection === 'function') {
					options.onSection(sec);
				}
			};
		});
	}

	function renderEnginePickerDropdown(items, options) {
		options = options || {};
		items = dedupeModifications(items);
		return '<div class="epc-cm-engine-dd-list">' + items.map(function (item, index) {
			var label = [
				modificationLiter(item),
				modificationTrimName(item),
				modificationYearDisplay(item),
				modificationPowerDisplay(item)
			].filter(Boolean).join(' · ');
			var selected = options.selectedId && modificationId(item) === parseInt(options.selectedId, 10);
			return '<button type="button" class="epc-cm-engine-dd-item' + (selected ? ' is-selected' : '') + '" data-index="' + index + '">' + esc(label || text(item.MODIFICATION || 'Engine')) + '</button>';
		}).join('') + '</div>';
	}

	function bindEnginePickerDropdown(container, items, onSelect) {
		if (!container) { return; }
		items = dedupeModifications(items);
		Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-engine-dd-item'), function (btn) {
			btn.onclick = function () {
				var idx = parseInt(btn.getAttribute('data-index'), 10);
				if (!isNaN(idx) && items[idx] && typeof onSelect === 'function') {
					onSelect(items[idx], idx, btn);
				}
			};
		});
	}

	function engineSearchYearRange(item) {
		if (!item) { return ''; }
		var from = normalizeCatalogYear(item.CI_FROM || item.year_from || item.YEAR_FROM || '');
		var to = normalizeCatalogYear(item.CI_TO || item.year_to || item.YEAR_TO || '');
		if (from && to && to !== '9999' && from !== to) { return from + '-' + to; }
		if (from) { return from + '-p.t.'; }
		return text(item.years || '');
	}

	function engineSearchFuelLabel(fuel) {
		fuel = text(fuel || '');
		if (!fuel) { return ''; }
		return fuel.charAt(0).toUpperCase() + fuel.slice(1).toLowerCase();
	}

	function engineSearchPowerText(kw, hp) {
		kw = cleanPowerNum(kw || '');
		hp = cleanPowerNum(hp || '');
		if (kw && hp) { return kw + 'Kw/' + hp + 'Hp'; }
		if (kw) { return kw + 'Kw'; }
		if (hp) { return hp + 'Hp'; }
		return '';
	}

	function engineSearchSeriesCode(item, seriesText) {
		var code = text(item && (item.MODEL_CODE || item.model_code || ''));
		if (code) {
			code = code.replace(/^\(|\)$/g, '').trim();
			return code ? '(' + code + ')' : '';
		}
		seriesText = text(seriesText || (item && (item.MODEL_SERIES || item.model || '')) || '');
		var m = seriesText.match(/\(([^)]+)\)/);
		return m ? '(' + m[1].trim() + ')' : text(item && item.series || '');
	}

	function buildEngineSearchGroupsFromRows(rows) {
		var groups = {};
		var order = [];
		(rows || []).forEach(function (row) {
			if (!row || typeof row !== 'object') { return; }
			var mfaId = row.MFA_ID || 0;
			var msId = row.MS_ID || 0;
			var years = engineSearchYearRange(row);
			var fuel = text(row.FUEL_TYPE || row.fuel || '');
			var drive = text(row.DRIVE_TYPE || row.drive || row.AXLE_CONFIGURATION || '');
			if (!drive) {
				var driveKind = modificationDriveKind(row);
				drive = driveKind === 'awd' ? 'All WD' : (driveKind === 'rwd' ? 'Rear WD' : 'Front WD');
			}
			var groupKey = mfaId + '|' + msId + '|' + years + '|' + fuel + '|' + drive;
			if (!groups[groupKey]) {
				groups[groupKey] = {
					make: text(row.MANUFACTURER || row.make || ''),
					model: modelDisplayName(row),
					series: engineSearchSeriesCode(row),
					years: years,
					fuel: fuel,
					drive: drive,
					MFA_ID: mfaId,
					MS_ID: msId,
					engines: []
				};
				order.push(groupKey);
			}
			var carId = row.PC_ID || row.CV_ID || row.MTB_ID || row.ID || row.carId || 0;
			groups[groupKey].engines.push({
				carId: carId,
				ID: carId || row.ID || 0,
				ENG_ID: row.ENG_ID || 0,
				MFA_ID: mfaId,
				MS_ID: msId,
				displacement: text(row.CAPACITY_LT || row.CAPACITY || row.displacement || modificationLiter(row)),
				power_kw: text(row.POWER_KW || row.POWER_KW_START || row.power_kw || ''),
				power_hp: text(row.POWER_PS || row.POWER_HP || row.POWER_PS_START || row.power_hp || ''),
				engine_code: text(row.ENGINE_CODE || row.ENG_CODE || row.engine_code || modificationEngineCode(row)),
				drive: drive,
				MODIFICATION: text(row.MODIFICATION || row.PASSENGER_CAR || row.carName || ''),
				FUEL_TYPE: fuel,
				CI_FROM: normalizeCatalogYear(row.CI_FROM || ''),
				CI_TO: normalizeCatalogYear(row.CI_TO || ''),
				MANUFACTURER: text(row.MANUFACTURER || ''),
				MODEL_SERIES: text(row.MODEL_SERIES || '')
			});
		});
		return order.map(function (key) { return groups[key]; });
	}

	function flattenEngineSearchVariant(variant, group) {
		var carId = variant.carId || variant.ID || variant.PC_ID || 0;
		return {
			PC_ID: carId,
			CV_ID: variant.CV_ID,
			MTB_ID: variant.MTB_ID,
			ID: carId || variant.ID || 0,
			carId: carId,
			ENG_ID: variant.ENG_ID || 0,
			MFA_ID: variant.MFA_ID || (group && group.MFA_ID) || 0,
			MS_ID: variant.MS_ID || (group && group.MS_ID) || 0,
			MANUFACTURER: text(variant.MANUFACTURER || (group && group.make) || ''),
			MODEL_SERIES: text(variant.MODEL_SERIES || (group && group.model) || ''),
			MODIFICATION: text(variant.MODIFICATION || ''),
			FUEL_TYPE: text(variant.FUEL_TYPE || (group && group.fuel) || ''),
			ENGINE_CODE: text(variant.engine_code || ''),
			POWER_KW: text(variant.power_kw || ''),
			POWER_PS: text(variant.power_hp || ''),
			CAPACITY_LT: text(variant.displacement || ''),
			CI_FROM: normalizeCatalogYear(variant.CI_FROM || ''),
			CI_TO: normalizeCatalogYear(variant.CI_TO || ''),
			DRIVE_TYPE: text(variant.drive || (group && group.drive) || '')
		};
	}

	function renderEngineSearchGroupHeader(group) {
		var title = [text(group.make || '').toUpperCase(), text(group.model || '').toUpperCase()].filter(Boolean).join(' ');
		var series = text(group.series || engineSearchSeriesCode(null, group.model || ''));
		var metaParts = [];
		if (group.years) { metaParts.push(group.years); }
		if (group.fuel) { metaParts.push(engineSearchFuelLabel(group.fuel)); }
		if (group.drive) { metaParts.push(group.drive); }
		return '<div class="epc-cm-engine-search-model">' +
			'<div class="epc-cm-engine-search-model-title">' +
			(title ? '<strong>' + esc(title) + '</strong>' : '') +
			(series ? ' <span class="epc-cm-engine-search-series">' + esc(series) + '</span>' : '') +
			'</div>' +
			(metaParts.length ? '<div class="epc-cm-engine-search-model-meta">' + esc(metaParts.join(', ')) + '</div>' : '') +
			'</div>';
	}

	function renderEngineSearchDropdown(code, groups) {
		code = text(code);
		groups = groups || [];
		var html = '<div class="epc-cm-engine-search-dd">' +
			'<div class="epc-cm-engine-search-dd-head">' +
			'<span class="epc-cm-engine-search-dd-term">' + esc(code) + '</span>' +
			'<button type="button" class="epc-cm-engine-search-dd-close" aria-label="Close">&times;</button>' +
			'</div><div class="epc-cm-engine-search-dd-body">';
		var rowIndex = 0;
		groups.forEach(function (group, groupIndex) {
			var engines = group.engines || [];
			if (!engines.length) { return; }
			html += '<div class="epc-cm-engine-search-group">';
			html += renderEngineSearchGroupHeader(group);
			engines.forEach(function (variant, variantIndex) {
				var disp = text(variant.displacement || modificationLiter(variant) || '');
				var power = engineSearchPowerText(variant.power_kw, variant.power_hp);
				var engineCode = text(variant.engine_code || code || '');
				var altClass = rowIndex % 2 === 1 ? ' is-alt' : '';
				html += '<button type="button" class="epc-cm-engine-search-variant' + altClass + '" data-group-index="' + groupIndex + '" data-variant-index="' + variantIndex + '">' +
					(disp ? '<span class="epc-cm-engine-search-disp">' + esc(disp) + '</span>' : '') +
					(power ? '<span class="epc-cm-engine-search-power">(' + esc(power) + ')</span>' : '') +
					(engineCode ? '<span class="epc-cm-engine-search-code">' + esc(engineCode) + '</span>' : '') +
					'</button>';
				rowIndex++;
			});
			html += '</div>';
		});
		html += '</div></div>';
		return html;
	}

	function bindEngineSearchDropdown(host, groups, onSelect, onClose) {
		if (!host) { return; }
		groups = groups || [];
		var closeBtn = host.querySelector('.epc-cm-engine-search-dd-close');
		if (closeBtn) {
			closeBtn.onclick = function (e) {
				e.preventDefault();
				host.hidden = true;
				if (typeof onClose === 'function') { onClose(); }
			};
		}
		Array.prototype.forEach.call(host.querySelectorAll('.epc-cm-engine-search-variant'), function (btn) {
			btn.onclick = function () {
				var gi = parseInt(btn.getAttribute('data-group-index'), 10);
				var vi = parseInt(btn.getAttribute('data-variant-index'), 10);
				if (isNaN(gi) || isNaN(vi) || !groups[gi] || !groups[gi].engines || !groups[gi].engines[vi]) { return; }
				var group = groups[gi];
				var variant = group.engines[vi];
				if (typeof onSelect === 'function') {
					onSelect(flattenEngineSearchVariant(variant, group), group);
				}
				host.hidden = true;
			};
		});
	}

	function initEngineSearchWidget(options) {
		options = options || {};
		var input = options.input;
		var dropdownHost = options.dropdownHost;
		var triggerBtn = options.triggerBtn;
		var apiUrl = options.apiUrl || '';
		if (!input || !dropdownHost) { return; }
		function hideDropdown() {
			dropdownHost.hidden = true;
		}
		function runSearch() {
			var code = (input.value || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
			if (code.length < 2 || code.length > 12) {
				if (typeof options.onInvalid === 'function') { options.onInvalid(); }
				return;
			}
			var section = typeof options.getSection === 'function' ? options.getSection() : (options.section || 'passenger');
			fetch(apiUrl + '?action=engine_search&section=' + encodeURIComponent(section) + '&code=' + encodeURIComponent(code), { credentials: 'same-origin' })
				.then(function (res) { return res.json(); })
				.then(function (payload) {
					var rows = payload.data || payload.results || [];
					var groups = payload.groups || [];
					if (!groups.length && rows.length) {
						groups = buildEngineSearchGroupsFromRows(rows);
					}
					if (!groups.length) {
						dropdownHost.innerHTML = '<div class="epc-cm-engine-search-dd is-empty"><div class="epc-cm-engine-search-dd-head">' +
							'<span class="epc-cm-engine-search-dd-term">' + esc(code) + '</span>' +
							'<button type="button" class="epc-cm-engine-search-dd-close" aria-label="Close">&times;</button>' +
							'</div><div class="epc-cm-engine-search-dd-empty">No engines found</div></div>';
						dropdownHost.hidden = false;
						bindEngineSearchDropdown(dropdownHost, [], null, hideDropdown);
						return;
					}
					dropdownHost.innerHTML = renderEngineSearchDropdown(code, groups);
					dropdownHost.hidden = false;
					bindEngineSearchDropdown(dropdownHost, groups, options.onSelect, hideDropdown);
				})
				.catch(function () {
					dropdownHost.innerHTML = '<div class="epc-cm-engine-search-dd is-empty"><div class="epc-cm-engine-search-dd-empty">Engine search unavailable</div></div>';
					dropdownHost.hidden = false;
				});
		}
		if (triggerBtn) { triggerBtn.onclick = runSearch; }
		input.onkeydown = function (e) {
			if (e.key === 'Enter') { e.preventDefault(); runSearch(); }
		};
	}

	var renderVehicleChrome = renderVehicleContextBar;
	var bindVehicleChrome = bindVehicleContextBar;

	global.epcVcCatalogUi = {
		text: text,
		esc: esc,
		CAR_MOD_CATEGORIES: CAR_MOD_CATEGORIES,
		CATEGORY_ICON_BASE: CATEGORY_ICON_BASE,
		setCategoryIconBase: setCategoryIconBase,
		sortCategoriesByOrder: sortCategoriesByOrder,
		DEFAULT_CATEGORY_ICONS: DEFAULT_CATEGORY_ICONS,
		manufacturerId: manufacturerId,
		modelId: modelId,
		categoryId: categoryId,
		categoryName: categoryName,
		normalizeCategoryItem: normalizeCategoryItem,
		normalizeCategories: normalizeCategories,
		extractUmapiCategoryEntries: extractUmapiCategoryEntries,
		vehicleCategoriesFromUmapi: vehicleCategoriesFromUmapi,
		resolvePresentationCategory: resolvePresentationCategory,
		resolveCategoryAliasByName: resolveCategoryAliasByName,
		normalizeCategoryNameKey: normalizeCategoryNameKey,
		supplierId: supplierId,
		articleBrand: articleBrand,
		articleNumber: articleNumber,
		articleGroup: articleGroup,
		modelYearRange: modelYearRange,
		modelYearDisplayForCard: modelYearDisplayForCard,
		normalizeCatalogYear: normalizeCatalogYear,
		cleanDate: cleanDate,
		modelDisplayName: modelDisplayName,
		modelSeriesCode: modelSeriesCode,
		modelLetter: modelLetter,
		modelNameForLetter: modelNameForLetter,
		stripMakePrefixFromModelName: stripMakePrefixFromModelName,
		modelYearDisplayLabel: modelYearDisplayLabel,
		filterModelGridRows: filterModelGridRows,
		manufacturerLogoUrl: manufacturerLogoUrl,
		supplierLogoUrl: supplierLogoUrl,
		modelImageUrl: modelImageUrl,
		modelImageFallback: modelImageFallback,
		umapiModelCdnUrls: umapiModelCdnUrls,
		manufacturerLogoHtml: manufacturerLogoHtml,
		supplierLogoHtml: supplierLogoHtml,
		modelImageHtml: modelImageHtml,
		categoryIcon: categoryIcon,
		categoryIconId: categoryIconId,
		categoryIconHtml: categoryIconHtml,
		renderCatalogHeading: renderCatalogHeading,
		renderStepPicker: renderStepPicker,
		bindStepPicker: bindStepPicker,
		renderMakeGrid: renderMakeGrid,
		renderModelGrid: renderModelGrid,
		renderCategoryGrid: renderCategoryGrid,
		categoryChildren: categoryChildren,
		renderCategoryFlyout: renderCategoryFlyout,
		renderCategoryTreeSidebar: renderCategoryTreeSidebar,
		bindCategoryTreeSidebar: bindCategoryTreeSidebar,
		renderCategoryWorkspace: renderCategoryWorkspace,
		bindCategoryWorkspace: bindCategoryWorkspace,
		renderArticlesWorkspace: renderArticlesWorkspace,
		bindArticlesWorkspace: bindArticlesWorkspace,
		buildVehicleContext: buildVehicleContext,
		renderVehicleContextBar: renderVehicleContextBar,
		bindVehicleContextBar: bindVehicleContextBar,
		isServicePartsCategory: isServicePartsCategory,
		renderPartsListHead: renderPartsListHead,
		renderPartsListRow: renderPartsListRow,
		renderPartsListCategoryHead: renderPartsListCategoryHead,
		parseCarcatYear: parseCarcatYear,
		carcatParamValue: carcatParamValue,
		normalizeCarcatCatalog: normalizeCarcatCatalog,
		normalizeCarcatModel: normalizeCarcatModel,
		normalizeCarcatModels: normalizeCarcatModels,
		normalizeCarcatParameterOption: normalizeCarcatParameterOption,
		normalizeCarcatCar: normalizeCarcatCar,
		normalizeCarcatCars: normalizeCarcatCars,
		normalizeCarcatGroup: normalizeCarcatGroup,
		normalizeCarcatGroups: normalizeCarcatGroups,
		normalizeModificationItem: normalizeModificationItem,
		normalizeModifications: normalizeModifications,
		dedupeModifications: dedupeModifications,
		modificationYearDisplay: modificationYearDisplay,
		modificationLiter: modificationLiter,
		modificationTrimName: modificationTrimName,
		modificationPowerDisplay: modificationPowerDisplay,
		modificationYearLongDisplay: modificationYearLongDisplay,
		modificationYearBannerDisplay: modificationYearBannerDisplay,
		modificationBreadcrumbLabel: modificationBreadcrumbLabel,
		modificationDriveLabel: modificationDriveLabel,
		modificationBodyLabel: modificationBodyLabel,
		modificationEngineCodesDisplay: modificationEngineCodesDisplay,
		modificationFuelPowerLong: modificationFuelPowerLong,
		vehicleSpecLine: vehicleSpecLine,
		vehicleBannerText: vehicleBannerText,
		modificationEngineCode: modificationEngineCode,
		modificationFuelCategory: modificationFuelCategory,
		modificationFuelLabel: modificationFuelLabel,
		modificationId: modificationId,
		renderModificationTable: renderModificationTable,
		renderModificationGrid: renderModificationGrid,
		renderArticleRow: renderArticleRow,
		bindMakeGrid: bindMakeGrid,
		bindModelGrid: bindModelGrid,
		bindModificationGrid: bindModificationGrid,
		bindCategoryGrid: bindCategoryGrid,
		collectBrandFilters: collectBrandFilters,
		sortArticles: sortArticles,
		filterArticles: filterArticles,
		renderArticleToolbar: renderArticleToolbar,
		renderArticleFilterSidebar: renderArticleFilterSidebar,
		renderArticleCard: renderArticleCard,
		renderArticleCompact: renderArticleCompact,
		renderArticleListBody: renderArticleListBody,
		renderArticlesPanel: renderArticlesPanel,
		bindArticlesPanel: bindArticlesPanel,
		bindClickableRows: bindClickableRows,
		bindSearchResultRows: bindSearchResultRows,
		bindSupplierPriceGrid: bindSupplierPriceGrid,
		renderSupplierPriceGrid: renderSupplierPriceGrid,
		DEFAULT_VEHICLE_SECTIONS: DEFAULT_VEHICLE_SECTIONS,
		vehicleSectionLabel: vehicleSectionLabel,
		renderBrandPickerDropdown: renderBrandPickerDropdown,
		bindBrandPickerDropdown: bindBrandPickerDropdown,
		renderEnginePickerDropdown: renderEnginePickerDropdown,
		bindEnginePickerDropdown: bindEnginePickerDropdown,
		renderEngineSearchDropdown: renderEngineSearchDropdown,
		bindEngineSearchDropdown: bindEngineSearchDropdown,
		buildEngineSearchGroupsFromRows: buildEngineSearchGroupsFromRows,
		flattenEngineSearchVariant: flattenEngineSearchVariant,
		initEngineSearchWidget: initEngineSearchWidget,
		renderVehicleChrome: renderVehicleChrome,
		bindVehicleChrome: bindVehicleChrome,
		isSavedCatalogResponse: isSavedCatalogResponse,
		updateSavedCatalogTag: updateSavedCatalogTag,
		savedCatalogDefaultMessage: savedCatalogDefaultMessage
	};

	function savedCatalogDefaultMessage() {
		return 'Saved catalog — showing last synced data.';
	}

	function isSavedCatalogResponse(data) {
		if (!data || typeof data !== 'object') { return false; }
		return !!(data.catalog_mode === 'saved_catalog' || data.source === 'cache' || data.source === 'database' || data.stale || data.api_offline || data.cata_cache);
	}

	function updateSavedCatalogTag(tagEl, data, defaultMessage) {
		if (!tagEl) { return; }
		var msg = defaultMessage || savedCatalogDefaultMessage();
		if (isSavedCatalogResponse(data)) {
			tagEl.classList.add('is-visible');
			tagEl.textContent = data.offline_message || msg;
		} else {
			tagEl.classList.remove('is-visible');
			tagEl.textContent = msg;
		}
	}

	function flushEpcVcCatalogUiQueue() {
		var queue = global.__epcVcCatalogUiQueue;
		if (!queue || !queue.length) { return; }
		global.__epcVcCatalogUiQueue = [];
		queue.forEach(function (fn) {
			if (typeof fn !== 'function') { return; }
			try {
				fn(global.epcVcCatalogUi);
			} catch (err) {
				if (global.console && typeof global.console.error === 'function') {
					global.console.error(err);
				}
			}
		});
	}

	global.whenEpcVcCatalogUiReady = function (fn) {
		if (typeof fn !== 'function') { return; }
		if (global.epcVcCatalogUi) {
			fn(global.epcVcCatalogUi);
			return;
		}
		global.__epcVcCatalogUiQueue = global.__epcVcCatalogUiQueue || [];
		global.__epcVcCatalogUiQueue.push(fn);
	};
	flushEpcVcCatalogUiQueue();
})(typeof window !== 'undefined' ? window : this);
