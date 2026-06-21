<?php
defined('_ASTEXE_') or die('No access');
$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') ? rtrim($multilang_params['lang_href'], '/') : '/en';
$epc_umapi_chpu_on = !empty($DP_Config->chpu_search_config['chpu_search_on']);
$epc_umapi_chpu_parts_url = !empty($DP_Config->chpu_search_config['level_1']['url']) ? $DP_Config->chpu_search_config['level_1']['url'] : 'parts';
$epc_umapi_chpu_brands_url = !empty($DP_Config->chpu_search_config['level_2']['mode_1']['url']) ? $DP_Config->chpu_search_config['level_2']['mode_1']['url'] : 'brands';
$epc_umapi_chpu_slash_code = isset($DP_Config->chpu_search_config['slash_code']) ? $DP_Config->chpu_search_config['slash_code'] : '%2F';
?>
<style>
.epc-umapi { margin: 0 0 30px; }
.epc-umapi-panel { background: #fff; border: 1px solid #e1e7ef; border-radius: 8px; padding: 16px; }
.epc-umapi-title { margin: 0 0 14px; font-size: 24px; font-weight: 700; color: #172536; }
.epc-umapi-tabs { display: flex; gap: 10px; flex-wrap: wrap; margin: 0 0 18px; }
.epc-umapi-tab { border: 1px solid #d7dee9; background: #fff; border-radius: 6px; padding: 10px 14px; cursor: pointer; font-weight: 600; }
.epc-umapi-tab.active { background: #2b78d6; border-color: #2b78d6; color: #fff; }
.epc-umapi-toolbar { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; margin-bottom: 16px; }
.epc-umapi-toolbar input { min-width: 260px; max-width: 420px; }
.epc-umapi-vin-group { flex: 1 1 360px; max-width: 560px; }
.epc-umapi-vin-group input { text-transform: uppercase; letter-spacing: .04em; max-width: none; width: 100%; }
.epc-umapi-steps { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 15px; color: #6d7786; }
.epc-umapi-step { background: #f1f4f8; border: 0; border-radius: 20px; padding: 6px 10px; font-size: 13px; color: #6d7786; }
.epc-umapi-step.active { background: #dcecff; color: #12579f; }
.epc-umapi-step.clickable { cursor: pointer; color: #1e67b1; }
.epc-umapi-summary { margin: -4px 0 14px; color: #657184; font-size: 13px; }
.epc-umapi-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 12px; }
.epc-umapi-card { background: #fff; border: 1px solid #e1e7ef; border-radius: 8px; min-height: 76px; padding: 12px; cursor: pointer; transition: .15s ease; text-align: left; }
.epc-umapi-card:hover { border-color: #2b78d6; box-shadow: 0 4px 14px rgba(36,80,130,.12); }
.epc-umapi-card strong { display: block; color: #172536; }
.epc-umapi-card small { color: #758195; }
.epc-umapi-logo { max-height: 30px; max-width: 70px; margin-bottom: 8px; }
.epc-country-flag { width: 22px; height: 16px; object-fit: cover; border: 1px solid #e0e5ed; border-radius: 2px; margin-right: 5px; vertical-align: -3px; }
.epc-umapi-list { display: grid; gap: 9px; }
.epc-umapi-row { background: #fff; border: 1px solid #e1e7ef; border-radius: 8px; padding: 12px; cursor: pointer; }
.epc-umapi-row:hover { border-color: #2b78d6; }
.epc-umapi-count { float: right; color: #657184; font-size: 13px; margin-top: 8px; }
.epc-umapi-alpha { display: flex; flex-wrap: wrap; gap: 6px; margin: 0 0 14px; }
.epc-umapi-alpha a { display: inline-flex; min-width: 30px; min-height: 30px; align-items: center; justify-content: center; border: 1px solid #e1e7ef; border-radius: 6px; color: #1e67b1; background: #fff; font-weight: 600; }
.epc-umapi-alpha a:hover { text-decoration: none; border-color: #2b78d6; background: #f4f8ff; }
.epc-umapi-section { margin-top: 18px; }
.epc-umapi-section:first-child { margin-top: 0; }
.epc-umapi-section-title { display: flex; align-items: center; gap: 8px; margin: 0 0 10px; font-size: 18px; font-weight: 700; color: #172536; }
.epc-umapi-section-title span { color: #657184; font-size: 13px; font-weight: 400; }
.epc-umapi-manufacturer-grid { grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); }
.epc-umapi-manufacturer-card { min-height: 110px; display: flex; flex-direction: column; justify-content: center; align-items: center; gap: 6px; text-align: center; border-radius: 10px; }
.epc-umapi-manufacturer-card .epc-umapi-logo { margin: 0; max-height: 34px; max-width: 92px; }
.epc-umapi-manufacturer-card small { min-height: 18px; }
.epc-umapi-tree ul { list-style: none; padding-left: 18px; }
.epc-umapi-tree li { margin: 5px 0; }
.epc-umapi-tree button { border: 0; background: transparent; color: #1e67b1; padding: 3px 0; text-align: left; }
.epc-umapi-products { display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 8px; }
.epc-umapi-products button { white-space: normal; text-align: left; }
.epc-umapi-table { width: 100%; border-collapse: collapse; background: #fff; }
.epc-umapi-table th, .epc-umapi-table td { border: 1px solid #e1e7ef; padding: 8px; vertical-align: top; }
.epc-umapi-table th { background: #f5f7fa; }
.epc-umapi-message { padding: 14px; background: #fff8e1; border: 1px solid #f0d98a; border-radius: 8px; }
.epc-umapi-loader { padding: 18px; text-align: center; color: #657184; }
@media (max-width: 767px) {
    .epc-umapi-grid { grid-template-columns: repeat(auto-fill, minmax(135px, 1fr)); }
    .epc-umapi-toolbar input { min-width: 100%; }
}
</style>

<div class="epc-umapi" id="epc-umapi" data-lang-href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>">
    <h1 class="epc-umapi-title">Epart Catalog</h1>
    <div class="epc-umapi-tabs">
        <button type="button" class="epc-umapi-tab" data-section="passenger">Passenger</button>
        <button type="button" class="epc-umapi-tab" data-section="commercial">Commercial</button>
        <button type="button" class="epc-umapi-tab" data-section="motorbike">Motorbike</button>
    </div>

    <div class="epc-umapi-toolbar">
        <div class="input-group epc-umapi-vin-group">
            <input type="search" class="form-control" id="epc-umapi-vin" placeholder="VIN, e.g. WBAXG1103CDW29096" maxlength="17" autocomplete="off">
            <span class="input-group-btn"><button class="btn btn-success" type="button" id="epc-umapi-vin-btn">VIN search</button></span>
        </div>
        <input type="search" class="form-control" id="epc-umapi-filter" placeholder="Filter current list">
        <div class="input-group" style="max-width:420px;">
            <input type="search" class="form-control" id="epc-umapi-article" placeholder="Search article / OEM / analog">
            <span class="input-group-btn"><button class="btn btn-primary" type="button" id="epc-umapi-article-btn">Search</button></span>
        </div>
    </div>

    <div class="epc-umapi-steps" id="epc-umapi-steps"></div>
    <div class="epc-umapi-panel" id="epc-umapi-output"></div>
</div>

<script>
(function () {
    'use strict';

    var root = document.getElementById('epc-umapi');
    if (!root) {
        return;
    }

    var output = document.getElementById('epc-umapi-output');
    var steps = document.getElementById('epc-umapi-steps');
    var filterInput = document.getElementById('epc-umapi-filter');
    var articleInput = document.getElementById('epc-umapi-article');
    var articleButton = document.getElementById('epc-umapi-article-btn');
    var vinInput = document.getElementById('epc-umapi-vin');
    var vinButton = document.getElementById('epc-umapi-vin-btn');
    var langHref = root.getAttribute('data-lang-href') || '';
    var epcUmapiChpuOn = <?php echo $epc_umapi_chpu_on ? 'true' : 'false'; ?>;
    var epcUmapiChpuPartsUrl = <?php echo json_encode($epc_umapi_chpu_parts_url, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
    var epcUmapiChpuBrandsUrl = <?php echo json_encode($epc_umapi_chpu_brands_url, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
    var epcUmapiChpuSlashCode = <?php echo json_encode($epc_umapi_chpu_slash_code, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
    var state = {
        section: 'passenger',
        manufacturer: null,
        model: null,
        modification: null,
        categories: null,
        vehicleType: ''
    };

    var sectionLabels = {
        passenger: 'Passenger',
        commercial: 'Commercial',
        motorbike: 'Motorbike'
    };

    function api(action, params) {
        var query = new URLSearchParams();
        query.set('action', action);
        query.set('section', state.section);
        query.set('language', 'en');
        query.set('region', 'WWW');
        if (state.vehicleType && ['models', 'modifications', 'categories', 'products', 'articles'].indexOf(action) !== -1) {
            query.set('vehicle_type', state.vehicleType);
        }
        Object.keys(params || {}).forEach(function (key) {
            if (params[key] !== undefined && params[key] !== null && params[key] !== '') {
                query.set(key, params[key]);
            }
        });
        return fetch('/api/umapi_proxy.php?' + query.toString(), { credentials: 'same-origin' }).then(function (response) {
            return response.json().then(function (data) {
                if (!response.ok) {
                    throw data;
                }
                return data;
            });
        });
    }

    function text(value) {
        return String(value === null || value === undefined ? '' : value);
    }

    function esc(value) {
        return text(value).replace(/[&<>"']/g, function (ch) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[ch];
        });
    }

    function cleanDate(value) {
        value = text(value);
        return value ? value.replace(/-01$/, '').replace(/-00$/, '') : '';
    }

    function countryCode(country) {
        var key = text(country).trim().toLowerCase();
        var map = {
            'япония': 'jp', 'japan': 'jp',
            'германия': 'de', 'germany': 'de',
            'сша': 'us', 'usa': 'us', 'united states': 'us',
            'италия': 'it', 'italy': 'it',
            'великобритания': 'gb', 'great britain': 'gb', 'united kingdom': 'gb',
            'китай': 'cn', 'china': 'cn',
            'франция': 'fr', 'france': 'fr',
            'южная корея': 'kr', 'south korea': 'kr',
            'россия': 'ru', 'russia': 'ru',
            'румыния': 'ro', 'romania': 'ro',
            'чехия': 'cz', 'czech republic': 'cz',
            'швеция': 'se', 'sweden': 'se',
            'украина': 'ua', 'ukraine': 'ua',
            'бельгия': 'be', 'belgium': 'be',
            'бразилия': 'br', 'brazil': 'br',
            'индия': 'in', 'india': 'in',
            'турция': 'tr', 'turkey': 'tr',
            'иран': 'ir', 'iran': 'ir',
            'чехословакия': 'cz',
            'испания': 'es', 'spain': 'es',
            'сербия': 'rs', 'serbia': 'rs',
            'аргентина': 'ar', 'argentina': 'ar',
            'австрия': 'at', 'austria': 'at',
            'ссср': 'ru'
        };
        return map[key] || '';
    }

    function flagHtml(country) {
        var code = countryCode(country);
        if (!code) {
            return esc(country || '');
        }
        return '<img class="epc-country-flag" alt="" src="https://flagcdn.com/24x18/' + code + '.png">' + esc(country || '');
    }

    function apiItems(data) {
        if (Array.isArray(data)) {
            return data;
        }
        if (data && Array.isArray(data.data)) {
            return data.data;
        }
        return [];
    }

    function apiObject(data) {
        if (!data || typeof data !== 'object') {
            return data;
        }
        if (data.data && typeof data.data === 'object' && !Array.isArray(data.data)) {
            return data.data;
        }
        return data;
    }

    function loading(message) {
        output.innerHTML = '<div class="epc-umapi-loader">' + esc(message || 'Loading catalog data...') + '</div>';
    }

    function showError(error) {
        var message = error && error.message ? error.message : 'Catalog data is temporarily unavailable.';
        output.innerHTML = '<div class="epc-umapi-message">' + esc(message) + '</div>';
    }

    function setHash(section) {
        if (location.hash !== '#/' + section) {
            location.hash = '#/' + section;
        }
    }

    function syncTabs() {
        Array.prototype.forEach.call(document.querySelectorAll('.epc-umapi-tab'), function (tab) {
            tab.classList.toggle('active', tab.getAttribute('data-section') === state.section);
        });
    }

    function renderSteps(active) {
        var items = [
            ['manufacturers', sectionLabels[state.section]],
            ['models', state.manufacturer ? state.manufacturer.MANUFACTURER : 'Model'],
            ['modifications', state.model ? state.model.MODEL_SERIES : 'Modification'],
            ['categories', state.modification ? getModificationTitle(state.modification) : 'Parts'],
        ];
        steps.innerHTML = items.map(function (item) {
            var canClick = item[0] === 'manufacturers' || (item[0] === 'models' && state.manufacturer) || (item[0] === 'modifications' && state.model) || (item[0] === 'categories' && state.modification);
            return '<button type="button" class="epc-umapi-step ' + (item[0] === active ? 'active ' : '') + (canClick ? 'clickable' : '') + '" data-step="' + esc(item[0]) + '">' + esc(item[1]) + '</button>';
        }).join('');
        Array.prototype.forEach.call(steps.querySelectorAll('[data-step]'), function (button) {
            button.onclick = function () {
                var step = button.getAttribute('data-step');
                if (step === 'manufacturers') {
                    loadManufacturers();
                } else if (step === 'models' && state.manufacturer) {
                    loadModels(state.manufacturer);
                } else if (step === 'modifications' && state.model) {
                    loadModifications(state.model);
                } else if (step === 'categories' && state.modification && state.categories) {
                    renderCategories(state.categories);
                }
            };
        });
    }

    function getModificationId(item) {
        return item.PC_ID || item.CV_ID || item.MTB_ID || item.ID || item.MOD_ID || '';
    }

    function getModificationTitle(item) {
        return item.PASSENGER_CAR || item.COMMERCIAL_VEHICLE || item.MOTORBIKE || item.MODIFICATION || item.DES || item.MODEL_SERIES || 'Selected vehicle';
    }

    function chooseVehicleType(item) {
        var types = item && item.EPART_TYPES ? item.EPART_TYPES : [];
        return types.length ? types[0] : '';
    }

    function listSummary(label, items) {
        return '<div class="epc-umapi-summary">' + esc(label) + '<span class="epc-umapi-count">' + esc((items || []).length) + ' found</span></div>';
    }

    function popularFlag(item) {
        if (state.section === 'commercial') {
            return item.POPULAR_CV;
        }
        if (state.section === 'motorbike') {
            return item.POPULAR_MTB;
        }
        return item.POPULAR_PC;
    }

    function isPopularManufacturer(item) {
        return String(popularFlag(item)) === '1';
    }

    function manufacturerLetter(item) {
        var name = text(item.MANUFACTURER).trim();
        var letter = name ? name.charAt(0).toUpperCase() : '#';
        return /^[A-Z]$/.test(letter) ? letter : '0-9';
    }

    function manufacturerCard(item, index) {
        var logo = item.IS_LOGO ? '<img class="epc-umapi-logo" alt="" src="https://image.umapi.ru/MANUFACTURERS/' + encodeURIComponent(item.MFA_ID) + '.png" onerror="this.style.display=\'none\';">' : '';
        return '<div class="epc-umapi-card epc-umapi-manufacturer-card" data-index="' + index + '" data-search="' + esc(item.MANUFACTURER + ' ' + item.COUNTRY) + '">' +
            logo + '<strong>' + esc(item.MANUFACTURER) + '</strong><small>' + flagHtml(item.COUNTRY || '') + '</small></div>';
    }

    function manufacturerGrid(list, allItems) {
        return '<div class="epc-umapi-grid epc-umapi-manufacturer-grid">' + list.map(function (item) {
            return manufacturerCard(item, allItems.indexOf(item));
        }).join('') + '</div>';
    }

    function applyFilter() {
        var term = filterInput.value.toLowerCase();
        Array.prototype.forEach.call(output.querySelectorAll('[data-search]'), function (node) {
            node.style.display = node.getAttribute('data-search').toLowerCase().indexOf(term) === -1 ? 'none' : '';
        });
    }

    function renderManufacturers(items) {
        items = apiItems(items);
        renderSteps('manufacturers');
        if (!items || !items.length) {
            output.innerHTML = '<div class="epc-umapi-message">No manufacturers found.</div>';
            return;
        }
        var popular = items.filter(isPopularManufacturer);
        var other = items.filter(function (item) { return !isPopularManufacturer(item); });
        var groups = {};
        other.forEach(function (item) {
            var letter = manufacturerLetter(item);
            if (!groups[letter]) {
                groups[letter] = [];
            }
            groups[letter].push(item);
        });
        var letters = Object.keys(groups).sort();
        var html = listSummary('Select vehicle brand. Popular manufacturers are shown first, then all other manufacturers alphabetically.', items);
        if (popular.length) {
            html += '<div class="epc-umapi-section"><h3 class="epc-umapi-section-title">Popular manufacturers <span>' + esc(popular.length) + '</span></h3>' + manufacturerGrid(popular, items) + '</div>';
        }
        if (letters.length) {
            html += '<div class="epc-umapi-section"><h3 class="epc-umapi-section-title">Other manufacturers <span>A-Z</span></h3><div class="epc-umapi-alpha">' +
                letters.map(function (letter) { return '<a href="#epc-letter-' + esc(letter) + '">' + esc(letter) + '</a>'; }).join('') + '</div></div>';
            letters.forEach(function (letter) {
                html += '<div class="epc-umapi-section" id="epc-letter-' + esc(letter) + '"><h3 class="epc-umapi-section-title">' + esc(letter) + ' <span>' + esc(groups[letter].length) + '</span></h3>' + manufacturerGrid(groups[letter], items) + '</div>';
            });
        }
        output.innerHTML = html;
        bindIndexed('.epc-umapi-card', items, loadModels);
        applyFilter();
    }

    function renderModels(items) {
        items = apiItems(items);
        renderSteps('models');
        if (!items || !items.length) {
            output.innerHTML = '<div class="epc-umapi-message">No models found for this manufacturer.</div>';
            return;
        }
        output.innerHTML = listSummary('Select model series for ' + (state.manufacturer ? state.manufacturer.MANUFACTURER : 'selected brand') + '.', items) + '<div class="epc-umapi-list">' + items.map(function (item, index) {
            var years = cleanDate(item.CI_FROM) + (item.CI_TO ? ' - ' + cleanDate(item.CI_TO) : '');
            return '<div class="epc-umapi-row" data-index="' + index + '" data-search="' + esc(item.MODEL_SERIES + ' ' + years) + '">' +
                '<strong>' + esc(item.MODEL_SERIES) + '</strong><br><small>' + esc(years) + '</small></div>';
        }).join('') + '</div>';
        bindIndexed('.epc-umapi-row', items, loadModifications);
        applyFilter();
    }

    function renderModifications(items) {
        items = apiItems(items);
        renderSteps('modifications');
        if (!items || !items.length) {
            output.innerHTML = '<div class="epc-umapi-message">No modifications found for this model.</div>';
            return;
        }
        output.innerHTML = listSummary('Select exact vehicle modification.', items) + '<div class="epc-umapi-list">' + items.map(function (item, index) {
            var meta = [
                cleanDate(item.CI_FROM) + (item.CI_TO ? ' - ' + cleanDate(item.CI_TO) : ''),
                item.POWER_KW ? item.POWER_KW + ' kW' : '',
                item.CAPACITY_LT ? item.CAPACITY_LT + ' L' : '',
                item.FUEL_TYPE || ''
            ].filter(Boolean).join(' | ');
            return '<div class="epc-umapi-row" data-index="' + index + '" data-search="' + esc(getModificationTitle(item) + ' ' + meta) + '">' +
                '<strong>' + esc(getModificationTitle(item)) + '</strong><br><small>' + esc(meta) + '</small></div>';
        }).join('') + '</div>';
        bindIndexed('.epc-umapi-row', items, loadCategories);
        applyFilter();
    }

    function renderCategoryTree(nodes, level) {
        if (!nodes || !nodes.length || level > 5) {
            return '';
        }
        return '<ul>' + nodes.map(function (node) {
            var children = node.CHILD || node.children || [];
            return '<li data-search="' + esc(node.DES || '') + '">' +
                '<button type="button" data-category="' + esc(node.CATEGORY_ID) + '">' + esc(node.DES || 'Category') + '</button>' +
                renderCategoryTree(children, level + 1) +
            '</li>';
        }).join('') + '</ul>';
    }

    function renderCategories(data) {
        data = apiObject(data) || {};
        renderSteps('categories');
        if (!data || (!data.root && !data.quic)) {
            output.innerHTML = '<div class="epc-umapi-message">No categories found for this vehicle.</div>';
            return;
        }
        var quick = data.quic || [];
        var html = '';
        if (quick.length) {
            html += '<h3>Popular groups</h3><div class="epc-umapi-grid">' + quick.map(function (item) {
                var id = item.CATEGORY_IDS && item.CATEGORY_IDS.length ? item.CATEGORY_IDS[0] : item.CATEGORY_ID;
                return '<div class="epc-umapi-card" data-category="' + esc(id) + '" data-search="' + esc(item.DES) + '"><strong>' + esc(item.DES) + '</strong></div>';
            }).join('') + '</div>';
        }
        html += '<h3>All categories</h3><div class="epc-umapi-tree">' + renderCategoryTree(data.root || [], 0) + '</div>';
        output.innerHTML = html;
        bindCategoryButtons();
        applyFilter();
    }

    function renderProducts(items, categoryId) {
        items = apiItems(items);
        renderSteps('categories');
        if (!items || !items.length) {
            output.innerHTML = '<div class="epc-umapi-message">No product groups found for this category.</div>';
            return;
        }
        output.innerHTML = '<button type="button" class="btn btn-default" id="epc-umapi-back-categories">Back to categories</button><h3>Product groups</h3><div class="epc-umapi-products">' +
            items.map(function (item) {
                var id = item.PT_ID || item.PT_IDS || item.ID;
                var label = item.PT_DES || item.DES || item.PRODUCT_GROUP || item.PRODUCT || id;
                return '<button type="button" class="btn btn-default" data-product="' + esc(id) + '" data-search="' + esc(label) + '">' + esc(label) + '</button>';
            }).join('') + '</div>';
        document.getElementById('epc-umapi-back-categories').onclick = function () { renderCategories(state.categories); };
        Array.prototype.forEach.call(output.querySelectorAll('[data-product]'), function (button) {
            button.onclick = function () { loadArticles(button.getAttribute('data-product')); };
        });
        applyFilter();
    }

    function renderArticles(data) {
        var items = apiItems(data);
        if (!items || !items.length) {
            output.innerHTML = '<div class="epc-umapi-message">No articles found.</div>';
            return;
        }
        output.innerHTML = '<button type="button" class="btn btn-default" id="epc-umapi-back-categories">Back to categories</button><h3>Articles</h3>' +
            '<div class="epc-umapi-summary">Last step: click Search to open the shop price result for the selected brand and part number.</div>' +
            '<table class="epc-umapi-table"><thead><tr><th>Brand</th><th>Article</th><th>Name</th><th>Action</th></tr></thead><tbody>' +
            items.map(articleRow).join('') + '</tbody></table>';
        document.getElementById('epc-umapi-back-categories').onclick = function () { renderCategories(state.categories); };
    }

    function normalizeArticleForUrl(article) {
        return String(article || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
    }
    function shopSearchUrl(brand, article) {
        var articleNorm = normalizeArticleForUrl(article);
        var brandName = String(brand || '').trim();
        if (!articleNorm) {
            return langHref + '/shop/part_search';
        }
        if (epcUmapiChpuOn) {
            if (!brandName) {
                return langHref + '/' + epcUmapiChpuPartsUrl + '/' + epcUmapiChpuBrandsUrl + '/' + encodeURIComponent(articleNorm);
            }
            var manufacturerAlias = brandName.split('/').join(epcUmapiChpuSlashCode);
            return langHref + '/' + epcUmapiChpuPartsUrl + '/' + encodeURIComponent(manufacturerAlias) + '/' + encodeURIComponent(articleNorm);
        }
        var legacyUrl = langHref + '/shop/part_search?article=' + encodeURIComponent(articleNorm);
        if (brandName) {
            legacyUrl += '&brend=' + encodeURIComponent(brandName);
        }
        return legacyUrl;
    }
    function articleRow(item) {
        var brand = item.SUP_BRAND || item.BRAND || item.SUPPLIER || item.MANUFACTURER || '';
        var article = item.ART_ARTICLE_NR || item.ARTICLE || item.ART_NUMBER || item.OEN || '';
        var name = item.ART_PRODUCT_NAME || item.PRODUCT_NAME || item.DES || item.NAME || '';
        var searchUrl = shopSearchUrl(brand, article);
        return '<tr data-search="' + esc([brand, article, name].join(' ')) + '">' +
            '<td>' + esc(brand) + '</td><td>' + esc(article) + '</td><td>' + esc(name) + '</td>' +
            '<td><a class="btn btn-xs btn-primary" href="' + esc(searchUrl) + '" title="Search ' + esc(brand) + ' ' + esc(article) + ' in shop">Search</a></td></tr>';
    }

    function renderBrandSearch(items, article) {
        items = apiItems(items);
        if (!items || !items.length) {
            output.innerHTML = '<div class="epc-umapi-message">No brand found for this article.</div>';
            return;
        }
        output.innerHTML = '<h3>Choose brand for ' + esc(article) + '</h3><div class="epc-umapi-grid">' +
            items.map(function (item, index) {
                var brand = item.BRAND || item.SUP_BRAND || item.MANUFACTURER || item.BRAND_NAME || item.DES || '';
                return '<div class="epc-umapi-card" data-index="' + index + '" data-search="' + esc(brand) + '"><strong>' + esc(brand) + '</strong></div>';
            }).join('') + '</div>';
        bindIndexed('.epc-umapi-card', items, function (item) {
            var brand = item.BRAND || item.SUP_BRAND || item.MANUFACTURER || item.BRAND_NAME || item.DES || '';
            loading('Loading analogs...');
            api('analogs', { article: article, brand: brand }).then(renderArticles).catch(showError);
        });
    }

    function bindIndexed(selector, items, handler) {
        Array.prototype.forEach.call(output.querySelectorAll(selector), function (node) {
            node.onclick = function () {
                handler(items[parseInt(node.getAttribute('data-index'), 10)]);
            };
        });
    }

    function bindCategoryButtons() {
        Array.prototype.forEach.call(output.querySelectorAll('[data-category]'), function (node) {
            node.onclick = function () { loadProducts(node.getAttribute('data-category')); };
        });
    }

    function loadManufacturers() {
        state.manufacturer = null;
        state.model = null;
        state.modification = null;
        state.categories = null;
        state.vehicleType = '';
        syncTabs();
        renderSteps('manufacturers');
        loading('Loading all manufacturers...');
        api('manufacturers', {}).then(renderManufacturers).catch(showError);
    }

    function loadModels(item) {
        state.manufacturer = item;
        state.model = null;
        state.modification = null;
        state.vehicleType = chooseVehicleType(item);
        loading('Loading models...');
        api('models', { MFA_ID: item.MFA_ID }).then(renderModels).catch(showError);
    }

    function loadModifications(item) {
        state.model = item;
        state.modification = null;
        loading('Loading modifications...');
        api('modifications', { MS_ID: item.MS_ID }).then(renderModifications).catch(showError);
    }

    function loadCategories(item) {
        state.modification = item;
        loading('Loading categories...');
        api('categories', { ID: getModificationId(item) }).then(function (data) {
            state.categories = apiObject(data) || {};
            renderCategories(state.categories);
        }).catch(showError);
    }

    function loadProducts(categoryId) {
        if (!state.modification) {
            return;
        }
        loading('Loading product groups...');
        api('products', { CATEGORY_ID: categoryId, ID: getModificationId(state.modification) }).then(function (items) {
            renderProducts(items, categoryId);
        }).catch(showError);
    }

    function loadArticles(productId) {
        if (!state.modification) {
            return;
        }
        loading('Loading articles...');
        api('articles', { PT_IDS: productId, ID: getModificationId(state.modification), limit: 30, offset: 0 }).then(renderArticles).catch(showError);
    }


    function normalizeVin(value) {
        return String(value || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
    }

    function vinArray(node) {
        if (!node) { return []; }
        if (Array.isArray(node)) { return node; }
        if (node.array && Array.isArray(node.array)) { return node.array; }
        return [];
    }

    function mapVinSection(vehicle) {
        var type = String((vehicle && (vehicle.linkageTargetType || vehicle.subLinkageTargetType)) || 'P').toUpperCase();
        if (type === 'V' || type === 'CV' || type === 'C') { return 'commercial'; }
        if (type === 'M' || type === 'MTB' || type === 'B') { return 'motorbike'; }
        return 'passenger';
    }

    function applyVinVehicle(vehicle, manufacturers, models) {
        var manu = manufacturers.find(function (item) { return String(item.manuId) === String(vehicle.manuId); }) || {};
        var model = models.find(function (item) { return String(item.modelId) === String(vehicle.modelId); }) || {};
        state.section = mapVinSection(vehicle);
        setHash(state.section);
        syncTabs();
        state.manufacturer = {
            MFA_ID: vehicle.manuId,
            MANUFACTURER: manu.manuName || 'Vehicle',
            EPART_TYPES: [state.section === 'commercial' ? 'CV' : (state.section === 'motorbike' ? 'Motorcycle' : 'PC')]
        };
        state.vehicleType = chooseVehicleType(state.manufacturer) || (state.section === 'commercial' ? 'CV' : (state.section === 'motorbike' ? 'Motorcycle' : 'PC'));
        state.model = {
            MS_ID: vehicle.modelId,
            MODEL_SERIES: model.modelName || vehicle.carName || 'Model',
            MFA_ID: vehicle.manuId
        };
        loading('Loading vehicle modification...');
        api('modifications', { MS_ID: vehicle.modelId }).then(function (items) {
            var list = Array.isArray(items) ? items : [];
            var mod = list.find(function (row) {
                return String(getModificationId(row)) === String(vehicle.carId);
            });
            if (!mod) {
                mod = {
                    PC_ID: vehicle.carId,
                    PASSENGER_CAR: vehicle.vehicleTypeDescription || vehicle.carName || 'Vehicle',
                    MS_ID: vehicle.modelId,
                    MFA_ID: vehicle.manuId,
                    TYPE: state.vehicleType
                };
            }
            state.modification = mod;
            loadCategories(mod);
        }).catch(showError);
    }

    function renderVinVehiclePicker(vehicles, manufacturers, models, vin) {
        renderSteps('manufacturers');
        output.innerHTML = '<h3>Select vehicle for VIN ' + esc(vin) + '</h3><div class="epc-umapi-list">' +
            vehicles.map(function (vehicle, index) {
                var label = vehicle.carName || vehicle.vehicleTypeDescription || ('Vehicle ' + (index + 1));
                return '<div class="epc-umapi-row" data-vin-index="' + index + '"><strong>' + esc(label) + '</strong></div>';
            }).join('') + '</div>';
        Array.prototype.forEach.call(output.querySelectorAll('[data-vin-index]'), function (node) {
            node.onclick = function () {
                applyVinVehicle(vehicles[parseInt(node.getAttribute('data-vin-index'), 10)], manufacturers, models);
            };
        });
    }

    function vinSearch() {
        var vin = normalizeVin(vinInput ? vinInput.value : '');
        if (vin.length < 11) {
            if (vinInput) { vinInput.focus(); }
            showError({ message: 'Enter a valid VIN (11-17 characters).' });
            return;
        }
        if (vinInput) { vinInput.value = vin; }
        loading('Decoding VIN and loading catalog...');
        api('vin', { vin: vin }).then(function (payload) {
            var data = payload && payload.data ? payload.data : payload;
            var vehicles = vinArray(data && data.matchingVehicles);
            var manufacturers = vinArray(data && data.matchingManufacturers);
            var models = vinArray(data && data.matchingModels);
            if (!vehicles.length) {
                showError({ message: 'No vehicle found for this VIN.' });
                return;
            }
            if (vehicles.length === 1) {
                applyVinVehicle(vehicles[0], manufacturers, models);
                return;
            }
            renderVinVehiclePicker(vehicles, manufacturers, models, vin);
        }).catch(showError);
    }

    function initVinFromQuery() {
        var params = new URLSearchParams(location.search);
        var vin = normalizeVin(params.get('vin') || '');
        if (!vin) { return false; }
        if (vinInput) { vinInput.value = vin; }
        vinSearch();
        return true;
    }

    function articleSearch() {
        var article = articleInput.value.trim();
        if (!article) {
            articleInput.focus();
            return;
        }
        loading('Searching article...');
        api('brands', { article: article }).then(function (items) {
            renderBrandSearch(items, article);
        }).catch(showError);
    }

    function sectionFromHash() {
        var value = (location.hash || '#/passenger').replace('#/', '');
        return sectionLabels[value] ? value : 'passenger';
    }

    Array.prototype.forEach.call(document.querySelectorAll('.epc-umapi-tab'), function (tab) {
        tab.onclick = function () {
            state.section = tab.getAttribute('data-section');
            setHash(state.section);
            loadManufacturers();
        };
    });
    filterInput.oninput = applyFilter;
    if (vinButton) { vinButton.onclick = vinSearch; }
    if (vinInput) {
        vinInput.onkeydown = function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                vinSearch();
            }
        };
    }
    articleButton.onclick = articleSearch;
    articleInput.onkeydown = function (event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            articleSearch();
        }
    };
    window.addEventListener('hashchange', function () {
        var section = sectionFromHash();
        if (section !== state.section) {
            state.section = section;
            loadManufacturers();
        }
    });

    state.section = sectionFromHash();
    syncTabs();
    if (!initVinFromQuery()) {
        loadManufacturers();
    }
})();
</script>