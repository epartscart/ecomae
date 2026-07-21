<?php
defined('_ASTEXE_') or die('No access');
$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') ? rtrim($multilang_params['lang_href'], '/') : '/en';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
$epc_brands_prices_visible = epc_storefront_prices_visible_for_user();
$epc_brands_login_cta = epc_storefront_prices_login_cta_html($multilang_params ?? null);
echo epc_storefront_prices_styles();
?>
<style>
.epc-brands { margin-bottom: 30px; }
.epc-brands-controls { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; margin: 0 0 14px; }
.epc-brands-controls input { max-width: 380px; }
.epc-brands-tabs { display: flex; gap: 8px; flex-wrap: wrap; margin: 0 0 12px; }
.epc-brands-tabs button, .epc-brands-letters button { border: 1px solid #d8e0ea; background: #fff; border-radius: 4px; padding: 7px 10px; }
.epc-brands-tabs button.active, .epc-brands-letters button.active { background: #2b78d6; border-color: #2b78d6; color: #fff; }
.epc-brands-letters { display: flex; gap: 5px; flex-wrap: wrap; margin: 0 0 18px; }
.epc-brands-summary { color: #697586; margin-bottom: 12px; }
.epc-brands-grid { columns: 4 210px; column-gap: 24px; }
.epc-brand-item { break-inside: avoid; display: flex; width: 100%; min-height: 46px; gap: 9px; align-items: center; padding: 6px 0; border: 0; border-bottom: 1px dotted #e1e7ef; background: transparent; color: #172536; text-align: left; }
.epc-brand-item:hover { color: #2b78d6; }
.epc-brand-item strong { font-weight: 600; }
.epc-brand-item small { color: #758195; }
.epc-brand-logo-wrap { align-items: center; display: inline-flex; flex: 0 0 42px; height: 28px; justify-content: center; position: relative; width: 42px; }
.epc-brand-logo { background: #fff; border: 1px solid #eef2f7; border-radius: 3px; display: block; height: 28px; object-fit: contain; width: 42px; }
.epc-brand-logo-fallback { align-items: center; background: #f1f5f9; border: 1px solid #e2e8f0; border-radius: 3px; color: #64748b; display: inline-flex; font-size: 11px; font-weight: 800; height: 28px; justify-content: center; left: 0; position: absolute; text-transform: uppercase; top: 0; width: 42px; }
.epc-brand-item.has-logo-loaded .epc-brand-logo-fallback { display: none; }
.epc-brand-item.is-logo-missing .epc-brand-logo { display: none; }
.epc-brand-item.is-logo-missing .epc-brand-logo-fallback { display: inline-flex; }
.epc-country-flag { width: 22px; height: 16px; object-fit: cover; border: 1px solid #e0e5ed; border-radius: 2px; margin-right: 5px; vertical-align: -3px; }
.epc-brand-parts-head { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; margin-bottom: 12px; }
.epc-brand-parts-table { width: 100%; border-collapse: collapse; background: #fff; }
.epc-brand-parts-table th, .epc-brand-parts-table td { border: 1px solid #e1e7ef; padding: 8px; vertical-align: top; }
.epc-brand-parts-table th { background: #f5f7fa; }
.epc-brands-message { padding: 14px; background: #fff8e1; border: 1px solid #f0d98a; border-radius: 8px; }
.epc-brands-loader { padding: 18px; color: #657184; text-align: center; }
@media (max-width: 767px) {
    .epc-brands-controls input { max-width: 100%; width: 100%; }
}
</style>

<div class="epc-brands" id="epc-brands" data-umapi-href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>/umapi_catalog" data-prices-visible="<?php echo $epc_brands_prices_visible ? '1' : '0'; ?>">
    <div class="epc-brands-controls">
        <input type="search" class="form-control" id="epc-brands-search" placeholder="Search brand name, manufacturer, or country">
        <a class="btn btn-default" href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>/umapi_catalog">Open Epart Catalog</a>
    </div>
    <div class="epc-brands-tabs">
        <button type="button" data-mode="parts" class="active">Parts brands</button>
        <button type="button" data-mode="vehicles">Vehicle manufacturers</button>
    </div>
    <div class="epc-brands-letters" id="epc-brands-letters"></div>
    <div class="epc-brands-summary" id="epc-brands-summary"></div>
    <div id="epc-brands-output"></div>
</div>

<script>
(function () {
    'use strict';

    var root = document.getElementById('epc-brands');
    if (!root) {
        return;
    }

    var search = document.getElementById('epc-brands-search');
    var letters = document.getElementById('epc-brands-letters');
    var summary = document.getElementById('epc-brands-summary');
    var output = document.getElementById('epc-brands-output');
    var mode = 'parts';
    var activeLetter = 'A';
    var cache = { parts: null, vehicles: null };
    var pricesVisible = root.getAttribute('data-prices-visible') === '1';
    var priceLoginCta = <?php echo json_encode($epc_brands_login_cta, JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
    var alphabet = ['All', '0-9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'];

    function esc(value) {
        return String(value === null || value === undefined ? '' : value).replace(/[&<>"']/g, function (ch) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[ch];
        });
    }

    function countryCode(country) {
        var key = String(country || '').trim().toLowerCase();
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

    function logoProxyUrl(kind, id) {
        if (!id) {
            return '';
        }
        return '/api/umapi_image.php?kind=' + encodeURIComponent(kind) + '&id=' + encodeURIComponent(id);
    }

    function flagHtml(country) {
        var code = countryCode(country);
        if (!code) {
            return esc(country || '');
        }
        return '<img class="epc-country-flag" alt="" src="https://flagcdn.com/24x18/' + code + '.png">' + esc(country || '');
    }

    function api(action, params) {
        var query = new URLSearchParams();
        query.set('action', action);
        query.set('language', 'en');
        query.set('region', 'WWW');
        Object.keys(params || {}).forEach(function (key) {
            query.set(key, params[key]);
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

    function normalizeLetter(name) {
        var first = (name || '').trim().charAt(0).toUpperCase();
        return /^[A-Z]$/.test(first) ? first : (/^[0-9]$/.test(first) ? '0-9' : '#');
    }

    function showLoading(text) {
        output.innerHTML = '<div class="epc-brands-loader">' + esc(text || 'Loading brands...') + '</div>';
        summary.innerHTML = '';
    }

    function showError(error) {
        var message = error && error.message ? error.message : 'Brands are temporarily unavailable.';
        output.innerHTML = '<div class="epc-brands-message">' + esc(message) + '</div>';
    }

    function buildLetters(items) {
        var available = {};
        items.forEach(function (item) {
            available[normalizeLetter(item.name)] = true;
        });
        letters.innerHTML = alphabet.map(function (letter) {
            var disabled = letter !== 'All' && !available[letter] ? ' disabled' : '';
            return '<button type="button" data-letter="' + letter + '"' + disabled + ' class="' + (letter === activeLetter ? 'active' : '') + '">' + letter + '</button>';
        }).join('');
        Array.prototype.forEach.call(letters.querySelectorAll('button'), function (button) {
            button.onclick = function () {
                if (button.disabled) {
                    return;
                }
                activeLetter = button.getAttribute('data-letter');
                render();
            };
        });
    }

    function filteredItems() {
        var items = cache[mode] || [];
        var term = search.value.trim().toLowerCase();
        return items.filter(function (item) {
            var letter = normalizeLetter(item.name);
            var letterMatch = activeLetter === 'All' || letter === activeLetter;
            var termMatch = !term || item.search.indexOf(term) !== -1;
            return letterMatch && termMatch;
        });
    }

    function brandLogoHtml(item) {
        var initial = esc((item.name || '?').trim().charAt(0) || '?');
        var fallback = '<span class="epc-brand-logo-fallback" aria-hidden="true">' + initial + '</span>';
        if (!item.logo) {
            return '<span class="epc-brand-logo-wrap">' + fallback + '</span>';
        }
        return '<span class="epc-brand-logo-wrap"><img class="epc-brand-logo" alt="" loading="eager" decoding="async" src="' + esc(item.logo) + '" onload="var p=this.closest(\'.epc-brand-item\');if(p){p.classList.add(\'has-logo-loaded\');}" onerror="var p=this.closest(\'.epc-brand-item\');if(p){p.classList.add(\'is-logo-missing\');}this.remove();">' + fallback + '</span>';
    }

    function render() {
        var items = cache[mode] || [];
        buildLetters(items);
        var shown = filteredItems();
        summary.innerHTML = 'Showing ' + shown.length + ' of ' + items.length + ' available ' + (mode === 'parts' ? 'parts brands' : 'vehicle manufacturers') + '.';
        if (!shown.length) {
            output.innerHTML = '<div class="epc-brands-message">No brands found. Enter at least 3 symbols or choose another letter.</div>';
            return;
        }
        output.innerHTML = '<div class="epc-brands-grid">' + shown.map(function (item) {
            var detail = item.detail ? '<br><small>' + (item.country ? flagHtml(item.country) + ' | ' + esc(item.detail) : esc(item.detail)) + '</small>' : '';
            var logoClass = item.logo ? '' : ' is-logo-missing';
            return '<button type="button" class="epc-brand-item' + logoClass + '" data-brand-id="' + esc(item.id || '') + '" data-brand="' + esc(item.name) + '">' + brandLogoHtml(item) + '<span><strong>' + esc(item.name) + '</strong>' + detail + '</span></button>';
        }).join('') + '</div>';
        Array.prototype.forEach.call(output.querySelectorAll('.epc-brand-item'), function (item) {
            item.onclick = function () {
                var brand = item.getAttribute('data-brand');
                if (mode === 'parts') {
                    loadBrandParts(brand, 0);
                } else {
                    location.href = root.getAttribute('data-umapi-href') || '/umapi_catalog';
                }
            };
        });
    }

    function brandPriceCell(row) {
        if (!pricesVisible) {
            return '***';
        }
        return esc(row.price || '');
    }

    function brandStockCell(row) {
        if (!pricesVisible) {
            return '***';
        }
        return esc(row.exist || '');
    }

    function renderBrandParts(data) {
        var brand = data.brand || '';
        var rows = data.data || [];
        summary.innerHTML = 'Showing part numbers for ' + esc(brand) + ': ' + rows.length + ' of ' + (data.rows || 0) + '.';
        if (!rows.length) {
            output.innerHTML = '<div class="epc-brand-parts-head"><button type="button" class="btn btn-default" id="epc-brands-back">Back to brands</button><strong>' + esc(brand) + '</strong></div>' +
                '<div class="epc-brands-message">No loaded price-list part numbers found for this brand yet. Upload supplier price data for this brand, or use the site part search by entering a known article number.</div>';
            document.getElementById('epc-brands-back').onclick = render;
            return;
        }
        var priceHeader = pricesVisible ? 'Price from' : 'Price';
        output.innerHTML = '<div class="epc-brand-parts-head"><button type="button" class="btn btn-default" id="epc-brands-back">Back to brands</button><strong>' + esc(brand) + '</strong></div>' +
            '<table class="epc-brand-parts-table"><thead><tr><th>Brand</th><th>Part number</th><th>Name</th><th>Stock</th><th>' + priceHeader + '</th><th></th></tr></thead><tbody>' +
            rows.map(function (row) {
                var article = row.article_show || row.article || '';
                var searchUrl = (root.getAttribute('data-umapi-href') || '/umapi_catalog').replace('/umapi_catalog', '/shop/part_search') + '?article=' + encodeURIComponent(article);
                return '<tr><td>' + esc(row.manufacturer || brand) + '</td><td><strong>' + esc(article) + '</strong></td><td>' + esc(row.name || '') + '</td><td>' + brandStockCell(row) + '</td><td>' + brandPriceCell(row) + '</td><td><a class="btn btn-xs btn-primary" href="' + esc(searchUrl) + '">Search price</a></td></tr>';
            }).join('') + '</tbody></table>';
        document.getElementById('epc-brands-back').onclick = render;
    }

    function loadBrandParts(brand, offset) {
        showLoading('Loading part numbers for ' + brand + '...');
        api('brand_parts', { brand: brand, limit: 100, offset: offset || 0 }).then(renderBrandParts).catch(showError);
    }

    function loadParts(offset, all, rows) {
        return api('suppliers', { limit: 100, offset: offset }).then(function (data) {
            var batch = data.data || [];
            rows = data.rows || rows || batch.length;
            all = all.concat(batch.map(function (item) {
                var name = item.SUP_BRAND || item.SUP_FULL_NAME || '';
                var detail = item.SUP_FULL_NAME && item.SUP_FULL_NAME !== name ? item.SUP_FULL_NAME : '';
                return {
                    id: item.SUP_ID,
                    name: name,
                    detail: detail,
                    logo: item.SUP_ID ? logoProxyUrl('supplier', item.SUP_ID) : '',
                    search: (name + ' ' + detail + ' ' + item.SUP_ID).toLowerCase()
                };
            }));
            if (all.length < rows && batch.length) {
                return loadParts(offset + batch.length, all, rows);
            }
            cache.parts = all.sort(function (a, b) { return a.name.localeCompare(b.name); });
            render();
        });
    }

    function vehicleRequest(section) {
        return api('manufacturers', { section: section }).then(function (items) {
            return (items || []).map(function (item) {
                var name = item.MANUFACTURER || '';
                var detail = item.TYPE || '';
                return {
                    id: item.MFA_ID,
                    name: name,
                    detail: detail,
                    country: item.COUNTRY || '',
                    logo: item.MFA_ID ? logoProxyUrl('manufacturer', item.MFA_ID) : '',
                    search: (name + ' ' + detail + ' ' + (item.MANUFACTURER_RU || '')).toLowerCase()
                };
            });
        });
    }

    function loadVehicles() {
        return Promise.all([vehicleRequest('passenger'), vehicleRequest('commercial'), vehicleRequest('motorbike')]).then(function (groups) {
            var byId = {};
            groups.forEach(function (group) {
                group.forEach(function (item) {
                    byId[item.id] = item;
                });
            });
            cache.vehicles = Object.keys(byId).map(function (id) { return byId[id]; }).sort(function (a, b) { return a.name.localeCompare(b.name); });
            render();
        });
    }

    function ensureLoaded() {
        if (cache[mode]) {
            render();
            return;
        }
        showLoading(mode === 'parts' ? 'Loading available parts brands...' : 'Loading vehicle manufacturers...');
        (mode === 'parts' ? loadParts(0, [], 0) : loadVehicles()).catch(showError);
    }

    Array.prototype.forEach.call(root.querySelectorAll('.epc-brands-tabs button'), function (button) {
        button.onclick = function () {
            mode = button.getAttribute('data-mode');
            activeLetter = 'A';
            Array.prototype.forEach.call(root.querySelectorAll('.epc-brands-tabs button'), function (item) {
                item.classList.toggle('active', item === button);
            });
            ensureLoaded();
        };
    });
    search.oninput = render;
    ensureLoaded();
})();
</script>