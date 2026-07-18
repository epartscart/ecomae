/**
 * Laximo Catalog - car-mod.com style storefront UI
 * Integrates with /api/laximo_proxy.php (Syncron-style cached API)
 * Flow: Brands → Vehicle Selection (Wizard/VIN) → Categories → Units → Parts → Aftermarket
 */

var Laximo = {
    apiUrl: '/api/laximo_proxy.php',
    containerId: 'Laximo_container',
    breadcrumbs: [],
    currentCatalog: null,
    currentVehicle: null,
    currentSsd: null,
    structureMode: 'unified', // 'unified' or 'manufacturer'
    wizardState: {},
    alphaFilter: 'all',

    // Initialize
    init: function() {
        this.loadCatalogs();
    },

    // Show loading spinner
    showLoading: function(msg) {
        var el = document.getElementById(this.containerId);
        if (el) {
            el.innerHTML = '<div class="laximo-loading"><div class="spinner"></div><p>' + (msg || 'Loading...') + '</p></div>';
        }
    },

    // Show error
    showError: function(msg, critical) {
        var el = document.getElementById(this.containerId);
        if (el) {
            el.innerHTML = '<div class="laximo-error' + (critical ? ' critical' : '') + '">' + msg + '</div>';
        }
    },

    // API call
    ajax: function(params, callback) {
        var self = this;
        var url = this.apiUrl + '?action=' + encodeURIComponent(params.action);
        var action = params.action;
        delete params.action;
        for (var key in params) {
            if (params.hasOwnProperty(key) && params[key] !== undefined && params[key] !== null && params[key] !== '') {
                url += '&' + encodeURIComponent(key) + '=' + encodeURIComponent(params[key]);
            }
        }
        jQuery.ajax({
            type: 'GET',
            url: url,
            dataType: 'json',
            success: function(data) {
                if (data && (data.success || data.status)) {
                    // Normalize response: extract data array from action-specific keys
                    var normalized = {success: true, data: [], raw: data};
                    if (data.data !== undefined && data.data !== null) {
                        normalized.data = data.data;
                    } else if (data.catalogs) {
                        normalized.data = data.catalogs;
                    } else if (data.vehicles) {
                        normalized.data = data.vehicles;
                    } else if (data.categories) {
                        normalized.data = data.categories;
                    } else if (data.units) {
                        normalized.data = data.units;
                    } else if (data.details) {
                        normalized.data = data.details;
                    } else if (data.quick_groups) {
                        normalized.data = data.quick_groups;
                    } else if (data.groups) {
                        normalized.data = data.groups;
                    } else if (data.parts) {
                        normalized.data = data.parts;
                    } else if (data.results) {
                        normalized.data = data.results;
                    } else if (data.references) {
                        normalized.data = data.references;
                    } else if (data.refs) {
                        normalized.data = data.refs;
                    } else if (data.aftermarket) {
                        normalized.data = data.aftermarket;
                    } else if (data.steps) {
                        normalized.data = {steps: data.steps, vehicles: data.vehicles || []};
                    } else if (data.wizard) {
                        normalized.data = data.wizard;
                    } else if (data.applicability) {
                        normalized.data = data.applicability;
                    }
                    callback(normalized);
                } else {
                    self.showError(data && data.error ? data.error : 'Unknown error');
                }
            },
            error: function(xhr) {
                self.showError('Connection error. Please try again.', true);
            }
        });
    },

    // Build breadcrumb HTML
    renderBreadcrumbs: function() {
        if (this.breadcrumbs.length === 0) return '';
        var html = '<div class="laximo-breadcrumbs">';
        html += '<a onclick="Laximo.loadCatalogs()">Catalogs</a>';
        for (var i = 0; i < this.breadcrumbs.length; i++) {
            html += '<span class="separator">&rsaquo;</span>';
            if (i < this.breadcrumbs.length - 1) {
                html += '<a onclick="' + this.breadcrumbs[i].onclick + '">' + this.escHtml(this.breadcrumbs[i].label) + '</a>';
            } else {
                html += '<span class="current">' + this.escHtml(this.breadcrumbs[i].label) + '</span>';
            }
        }
        html += '</div>';
        return html;
    },

    escHtml: function(s) {
        if (!s) return '';
        return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
    },

    // ===== STEP 1: Load Catalogs (Brands) =====
    loadCatalogs: function() {
        var self = this;
        this.breadcrumbs = [];
        this.currentCatalog = null;
        this.currentVehicle = null;
        this.showLoading('Loading catalogs...');
        this.ajax({action: 'catalogs'}, function(data) {
            self.renderCatalogs(data.data || []);
        });
    },

    renderCatalogs: function(catalogs) {
        var self = this;
        // Collect first letters for alpha filter
        var letters = {};
        for (var i = 0; i < catalogs.length; i++) {
            var fl = (catalogs[i].name || '').charAt(0).toUpperCase();
            if (fl) letters[fl] = true;
        }
        var sortedLetters = Object.keys(letters).sort();

        var html = '';
        // Section 1: VIN / Frame search
        html += '<section class="laximo-section laximo-section--vin" aria-label="VIN search">';
        html += '<h2 class="laximo-section__title">1. Search by VIN / Frame</h2>';
        html += '<p class="laximo-section__lead">Enter a chassis VIN or frame number to find the matching vehicle catalog.</p>';
        html += '<div class="laximo-vin-search">';
        html += '<label for="laximo_vin_input">VIN / Frame number</label>';
        html += '<div class="input-group" style="display:flex;max-width:480px;">';
        html += '<input type="text" id="laximo_vin_input" placeholder="Enter VIN or Frame number..." style="flex:1;border:1px solid #ddd;padding:8px 12px;border-radius:4px 0 0 4px;font-size:14px;" />';
        html += '<button type="button" class="laximo-search-bar btn-search" onclick="Laximo.searchVin()" style="background:#337ab7;color:#fff;border:1px solid #337ab7;padding:8px 16px;border-radius:0 4px 4px 0;cursor:pointer;">Search</button>';
        html += '</div></div></section>';

        // Section 2: Brand / OEM catalogs
        html += '<section class="laximo-section laximo-section--brands" aria-label="Brand catalogs">';
        html += '<h2 class="laximo-section__title">2. Browse by manufacturer</h2>';
        html += '<p class="laximo-section__lead">Choose a brand to open the OEM parts catalog wizard.</p>';
        html += '<div class="laximo-alpha-filter">';
        html += '<span class="alpha-btn all' + (this.alphaFilter === 'all' ? ' active' : '') + '" onclick="Laximo.filterBrands(\'all\')">ALL</span>';
        for (var j = 0; j < sortedLetters.length; j++) {
            html += '<span class="alpha-btn' + (this.alphaFilter === sortedLetters[j] ? ' active' : '') + '" onclick="Laximo.filterBrands(\'' + sortedLetters[j] + '\')">' + sortedLetters[j] + '</span>';
        }
        html += '</div>';

        html += '<div class="laximo-brands-grid">';
        for (var k = 0; k < catalogs.length; k++) {
            var cat = catalogs[k];
            var catName = cat.name || cat.brand || '';
            var catCode = cat.code || '';
            var firstLetter = catName.charAt(0).toUpperCase();
            var hidden = (this.alphaFilter !== 'all' && firstLetter !== this.alphaFilter) ? ' style="display:none;"' : '';
            var icon = cat.icon_url || (cat.icon ? 'https://cdn.laximo.net/images/catalogs/' + cat.icon : '');
            html += '<div class="laximo-brand-card" data-letter="' + firstLetter + '"' + hidden + ' onclick="Laximo.selectCatalog(\'' + self.escHtml(catCode) + '\', \'' + self.escHtml(catName) + '\')">';
            html += '<div class="brand-logo">';
            if (icon) {
                html += '<img src="' + self.escHtml(icon) + '" alt="' + self.escHtml(catName) + '" onerror="this.parentNode.innerHTML=\'<span class=brand-letter>' + firstLetter + '</span>\'" />';
            } else {
                html += '<span class="brand-letter">' + firstLetter + '</span>';
            }
            html += '</div>';
            html += '<div class="brand-name">' + self.escHtml(catName) + '</div>';
            html += '</div>';
        }
        html += '</div></section>';

        if (!catalogs.length) {
            html += '<div class="laximo-error">No manufacturer catalogs returned. Please try again later.</div>';
        }

        document.getElementById(this.containerId).innerHTML = html;
    },

    filterBrands: function(letter) {
        this.alphaFilter = letter;
        var cards = document.querySelectorAll('.laximo-brand-card');
        var btns = document.querySelectorAll('.laximo-alpha-filter .alpha-btn');
        for (var i = 0; i < btns.length; i++) {
            btns[i].classList.remove('active');
            if ((letter === 'all' && btns[i].textContent === 'ALL') || btns[i].textContent === letter) {
                btns[i].classList.add('active');
            }
        }
        for (var j = 0; j < cards.length; j++) {
            if (letter === 'all' || cards[j].getAttribute('data-letter') === letter) {
                cards[j].style.display = '';
            } else {
                cards[j].style.display = 'none';
            }
        }
    },

    // ===== VIN Search =====
    searchVin: function() {
        var vin = document.getElementById('laximo_vin_input').value.trim();
        if (!vin || vin.length < 5) {
            alert('Please enter a valid VIN or frame number (at least 5 characters).');
            return;
        }
        var self = this;
        this.showLoading('Searching by VIN: ' + this.escHtml(vin) + '...');
        this.ajax({action: 'find_vehicle', vin: vin}, function(data) {
            self.renderVinResults(data.data || [], vin);
        });
    },

    renderVinResults: function(vehicles, vin) {
        this.breadcrumbs = [{label: 'VIN: ' + vin, onclick: "Laximo.searchVinDirect('" + this.escHtml(vin) + "')"}];
        var html = this.renderBreadcrumbs();

        if (vehicles.length === 0) {
            html += '<div class="laximo-error">No vehicles found for VIN: ' + this.escHtml(vin) + '</div>';
            document.getElementById(this.containerId).innerHTML = html;
            return;
        }

        html += '<h4>Vehicles found for VIN: ' + this.escHtml(vin) + '</h4>';
        html += '<div class="laximo-vehicles-list">';
        for (var i = 0; i < vehicles.length; i++) {
            var v = vehicles[i];
            html += '<div class="laximo-vehicle-item" onclick="Laximo.selectVehicle(\'' + this.escHtml(v.catalog) + '\', \'' + this.escHtml(v.vehicleid) + '\', \'' + this.escHtml(v.ssd || '') + '\', \'' + this.escHtml(v.name || v.brand || '') + '\')">';
            html += '<div class="vehicle-name">' + this.escHtml(v.name || v.brand || 'Vehicle') + '</div>';
            html += '<div class="vehicle-info">' + this.escHtml(v.catalog || '') + (v.date ? ' &middot; ' + this.escHtml(v.date) : '') + '</div>';
            html += '</div>';
        }
        html += '</div>';
        document.getElementById(this.containerId).innerHTML = html;
    },

    searchVinDirect: function(vin) {
        document.getElementById('laximo_vin_input') || this.showLoading();
        var self = this;
        this.showLoading('Searching by VIN: ' + this.escHtml(vin) + '...');
        this.ajax({action: 'find_vehicle', vin: vin}, function(data) {
            self.renderVinResults(data.data || [], vin);
        });
    },

    // ===== STEP 2: Select Catalog → Wizard =====
    selectCatalog: function(code, name) {
        this.currentCatalog = code;
        this.breadcrumbs = [{label: name, onclick: "Laximo.selectCatalog('" + this.escHtml(code) + "', '" + this.escHtml(name) + "')"}];
        this.wizardState = {};
        this.loadWizard(code);
    },

    loadWizard: function(catalog, ssd) {
        var self = this;
        this.showLoading('Loading vehicle selection...');
        var params = {action: 'wizard', catalog: catalog};
        if (ssd) params.ssd = ssd;
        this.ajax(params, function(data) {
            var payload = self.normalizeWizardPayload(data.data, data.raw);
            self.renderWizard(payload, catalog);
        });
    },

    normalizeWizardPayload: function(payload, raw) {
        var out = {steps: [], vehicles: []};
        if (!payload && raw) {
            payload = raw;
        }
        if (!payload) {
            return out;
        }
        // {type:'wizard'|'vehicles', items:[...], steps?:[]}
        if (payload.type === 'vehicles') {
            out.vehicles = payload.items || payload.vehicles || [];
            out.steps = payload.steps || [];
            return out;
        }
        if (payload.type === 'wizard') {
            out.steps = payload.items || payload.steps || [];
            out.vehicles = payload.vehicles || [];
            return out;
        }
        if (Object.prototype.toString.call(payload) === '[object Array]') {
            // Heuristic: vehicle rows have vehicleid; wizard steps have options
            if (payload.length && (payload[0].vehicleid || payload[0].VehicleId)) {
                out.vehicles = payload;
            } else {
                out.steps = payload;
            }
            return out;
        }
        out.steps = payload.steps || (payload.wizard && payload.wizard.steps) || [];
        out.vehicles = payload.vehicles || [];
        if ((!out.steps || !out.steps.length) && raw && raw.steps) {
            out.steps = raw.steps;
        }
        if ((!out.vehicles || !out.vehicles.length) && raw && raw.vehicles) {
            out.vehicles = raw.vehicles;
        }
        return out;
    },

    renderWizard: function(wizardData, catalog) {
        var normalized = this.normalizeWizardPayload(wizardData, arguments[2] || null);
        var steps = normalized.steps || [];
        var vehicles = normalized.vehicles || [];
        var html = this.renderBreadcrumbs();

        html += '<div class="laximo-search-bar">';
        html += '<p style="margin:0 0 8px;font-size:13px;color:#555;">Identify the vehicle first, then search parts by name (e.g. oil filter) in the unified catalog.</p>';
        html += '</div>';

        if (steps.length > 0) {
            html += '<div class="laximo-wizard">';
            html += '<h4 style="margin:0 0 15px;font-size:15px;">Select your vehicle</h4>';
            for (var i = 0; i < steps.length; i++) {
                var step = steps[i];
                var determined = step.determined === true || step.determined === 'true' || step.determined === '1';
                html += '<div class="laximo-wizard-step">';
                html += '<label>' + this.escHtml(step.name || 'Step ' + (i + 1)) + '</label>';
                html += '<select id="laximo_wizard_' + i + '" onchange="Laximo.wizardNext(' + i + ')" ' + (determined ? 'disabled' : '') + '>';
                html += '<option value="">-- Select --</option>';
                if (step.options) {
                    for (var j = 0; j < step.options.length; j++) {
                        var opt = step.options[j];
                        var selected = (step.value && (step.value === opt.key || step.value === opt.value)) ? ' selected' : '';
                        html += '<option value="' + this.escHtml(opt.key || opt.value) + '" data-ssd="' + this.escHtml(opt.ssd || '') + '"' + selected + '>' + this.escHtml(opt.value || opt.name || opt.key) + '</option>';
                    }
                }
                html += '</select>';
                html += '</div>';
            }
            html += '</div>';
        }

        if (vehicles.length > 0) {
            html += '<h4 style="margin:20px 0 10px;font-size:15px;">Select vehicle variant</h4>';
            html += '<div class="laximo-vehicles-list">';
            for (var k = 0; k < vehicles.length; k++) {
                var v = vehicles[k];
                html += '<div class="laximo-vehicle-item" onclick="Laximo.selectVehicle(\'' + this.escHtml(catalog) + '\', \'' + this.escHtml(v.vehicleid || v.id) + '\', \'' + this.escHtml(v.ssd || '') + '\', \'' + this.escHtml(v.name || '') + '\')">';
                html += '<div class="vehicle-name">' + this.escHtml(v.name || 'Vehicle') + '</div>';
                html += '<div class="vehicle-info">' + (v.date ? this.escHtml(v.date) : '') + (v.description ? ' &middot; ' + this.escHtml(v.description) : '') + '</div>';
                html += '</div>';
            }
            html += '</div>';
        }

        if (!steps.length && !vehicles.length) {
            html += '<div class="laximo-error">No vehicle selection steps returned for this catalog. Try VIN search, or another brand.</div>';
        }

        document.getElementById(this.containerId).innerHTML = html;
    },

    wizardNext: function(stepIndex) {
        var sel = document.getElementById('laximo_wizard_' + stepIndex);
        if (!sel || !sel.value) return;
        var ssd = sel.options[sel.selectedIndex].getAttribute('data-ssd') || '';
        var self = this;
        this.showLoading('Loading next step...');
        this.ajax({action: 'wizard_next', catalog: this.currentCatalog, ssd: ssd, step: stepIndex, value: sel.value}, function(data) {
            var payload = self.normalizeWizardPayload(data.data, data.raw);
            self.renderWizard(payload, self.currentCatalog);
        });
    },

    // ===== STEP 3: Select Vehicle → Load Categories =====
    selectVehicle: function(catalog, vehicleid, ssd, name) {
        this.currentCatalog = catalog;
        this.currentVehicle = vehicleid;
        this.currentSsd = ssd;
        if (this.breadcrumbs.length < 1 || this.breadcrumbs[this.breadcrumbs.length - 1].label !== name) {
            // Keep catalog breadcrumb if exists, add vehicle
            if (this.breadcrumbs.length === 0) {
                this.breadcrumbs.push({label: catalog, onclick: "Laximo.selectCatalog('" + this.escHtml(catalog) + "', '" + this.escHtml(catalog) + "')"});
            }
            this.breadcrumbs.push({label: name || 'Vehicle', onclick: "Laximo.selectVehicle('" + this.escHtml(catalog) + "', '" + this.escHtml(vehicleid) + "', '" + this.escHtml(ssd) + "', '" + this.escHtml(name) + "')"});
        }
        this.structureMode = 'unified';
        this.loadCategories();
    },

    loadCategories: function() {
        var self = this;
        var unified = this.structureMode === 'unified';
        this.showLoading(unified ? 'Loading unified groups...' : 'Loading manufacturer categories...');
        var params = {
            action: unified ? 'quick_groups' : 'categories',
            catalog: this.currentCatalog,
            vehicle_id: this.currentVehicle,
            vehicleid: this.currentVehicle,
            ssd: this.currentSsd
        };
        this.ajax(params, function(data) {
            self.renderCategories(data.data || []);
        });
    },

    renderCategories: function(categories) {
        var html = this.renderBreadcrumbs();
        if (!categories) categories = [];

        // Structure toggle (Unified / Manufacturer)
        html += '<div class="laximo-structure-toggle">';
        html += '<div class="toggle-btn' + (this.structureMode === 'unified' ? ' active' : '') + '" onclick="Laximo.switchStructure(\'unified\')">Unified Structure</div>';
        html += '<div class="toggle-btn' + (this.structureMode === 'manufacturer' ? ' active' : '') + '" onclick="Laximo.switchStructure(\'manufacturer\')">Manufacturer Structure</div>';
        html += '</div>';

        // Part search (unified name search → OEM, then DOC analogs)
        html += '<div class="laximo-search-bar" style="padding:10px 15px;">';
        html += '<label style="display:block;font-size:12px;color:#666;margin-bottom:6px;">Search parts by name in this vehicle (unified catalog)</label>';
        html += '<div class="input-group" style="display:flex;max-width:400px;">';
        html += '<input type="text" id="laximo_part_search_input" placeholder="e.g. oil filter, brake pad..." style="flex:1;border:1px solid #ddd;padding:8px 12px;border-radius:4px 0 0 4px;font-size:13px;" onkeydown="if(event.key===\'Enter\'){Laximo.searchParts();}" />';
        html += '<button onclick="Laximo.searchParts()" style="background:#337ab7;color:#fff;border:1px solid #337ab7;padding:8px 14px;border-radius:0 4px 4px 0;cursor:pointer;font-size:13px;">Search</button>';
        html += '</div></div>';

        if (this.structureMode === 'unified') {
            html += '<div class="laximo-quick-groups">';
            if (!categories.length) {
                html += '<p style="color:#999;padding:12px;">No unified groups for this vehicle. Switch to Manufacturer Structure, or search by part name above.</p>';
            }
            for (var i = 0; i < categories.length; i++) {
                var cat = categories[i];
                var gid = cat.quickgroupid || cat.groupid || cat.categoryid || cat.id || '';
                html += '<div class="laximo-quick-group" onclick="Laximo.loadQuickDetails(\'' + this.escHtml(gid) + '\', \'' + this.escHtml(cat.name || '') + '\')">';
                html += this.escHtml(cat.name || cat.synonimname || 'Group');
                if (cat.count) html += ' <small style="color:#999;">(' + cat.count + ')</small>';
                html += '</div>';
            }
            html += '</div>';
        } else {
            html += '<div class="laximo-catalog-layout">';
            html += '<div class="laximo-catalog-tree">';
            html += this.renderTree(categories);
            html += '</div>';
            html += '<div class="laximo-catalog-content" id="laximo_units_area">';
            html += '<p style="color:#999;padding:20px;">Select a category from the tree to view units.</p>';
            html += '</div></div>';
        }

        document.getElementById(this.containerId).innerHTML = html;
    },

    renderTree: function(items, level) {
        level = level || 0;
        if (!items || items.length === 0) return '';
        var html = '';
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            var hasChildren = item.children && item.children.length > 0;
            html += '<div class="laximo-tree-item" style="padding-left:' + (level * 16) + 'px;" onclick="Laximo.loadUnits(\'' + this.escHtml(item.categoryid || item.id || '') + '\', \'' + this.escHtml(item.name || '') + '\')">';
            if (hasChildren) {
                html += '<span class="tree-toggle" onclick="event.stopPropagation();Laximo.toggleTree(this);">+</span> ';
            } else {
                html += '<span class="tree-toggle">&bull;</span> ';
            }
            html += this.escHtml(item.name || 'Category');
            html += '</div>';
            if (hasChildren) {
                html += '<div class="laximo-tree-children">';
                html += this.renderTree(item.children, level + 1);
                html += '</div>';
            }
        }
        return html;
    },

    toggleTree: function(el) {
        var next = el.parentNode.nextElementSibling;
        if (next && next.classList.contains('laximo-tree-children')) {
            if (next.classList.contains('open')) {
                next.classList.remove('open');
                el.textContent = '+';
            } else {
                next.classList.add('open');
                el.textContent = '-';
            }
        }
    },

    switchStructure: function(mode) {
        this.structureMode = mode;
        this.loadCategories();
    },

    // ===== STEP 4: Load Units =====
    loadUnits: function(categoryid, name) {
        var self = this;
        var targetEl = document.getElementById('laximo_units_area');
        if (targetEl) {
            targetEl.innerHTML = '<div class="laximo-loading"><div class="spinner"></div><p>Loading units...</p></div>';
        } else {
            this.showLoading('Loading units...');
        }
        this.ajax({
            action: 'units',
            catalog: this.currentCatalog,
            vehicle_id: this.currentVehicle,
            vehicleid: this.currentVehicle,
            ssd: this.currentSsd,
            category_id: categoryid,
            categoryid: categoryid
        }, function(data) {
            self.renderUnits(data.data || [], name, targetEl);
        });
    },

    renderUnits: function(units, catName, targetEl) {
        var html = '<h4 style="margin:0 0 15px;font-size:14px;">' + this.escHtml(catName || 'Units') + '</h4>';
        if (units.length === 0) {
            html += '<p style="color:#999;">No units found in this category.</p>';
        } else {
            html += '<div class="laximo-units-grid">';
            for (var i = 0; i < units.length; i++) {
                var u = units[i];
                html += '<div class="laximo-unit-card" onclick="Laximo.loadUnitDetails(\'' + this.escHtml(u.unitid || u.id || '') + '\', \'' + this.escHtml(u.ssd || this.currentSsd || '') + '\', \'' + this.escHtml(u.name || '') + '\')">';
                if (u.imageurl) {
                    html += '<img src="' + this.escHtml(u.imageurl) + '" alt="' + this.escHtml(u.name) + '" onerror="this.style.display=\'none\'" />';
                }
                html += '<div class="unit-name">' + this.escHtml(u.name || 'Unit') + '</div>';
                if (u.code) html += '<div class="unit-code">' + this.escHtml(u.code) + '</div>';
                html += '</div>';
            }
            html += '</div>';
        }
        if (targetEl) {
            targetEl.innerHTML = html;
        } else {
            document.getElementById(this.containerId).innerHTML = this.renderBreadcrumbs() + html;
        }
    },

    // ===== STEP 5: Load Unit Details (Parts) =====
    loadUnitDetails: function(unitid, ssd, name) {
        var self = this;
        this.showLoading('Loading parts...');
        this.ajax({
            action: 'unit_details',
            catalog: this.currentCatalog,
            unit_id: unitid,
            unitid: unitid,
            ssd: ssd || this.currentSsd
        }, function(data) {
            self.renderUnitDetails(data.data || [], name);
        });
    },

    renderUnitDetails: function(parts, unitName) {
        var html = this.renderBreadcrumbs();
        html += '<h4 style="margin:0 0 15px;font-size:15px;">' + this.escHtml(unitName || 'Parts') + '</h4>';

        if (parts.length === 0) {
            html += '<p style="color:#999;">No parts found in this unit.</p>';
        } else {
            html += '<table class="laximo-parts-table">';
            html += '<thead><tr><th>#</th><th>OEM Number</th><th>Name</th><th>Note</th><th>Cross-ref</th></tr></thead>';
            html += '<tbody>';
            for (var i = 0; i < parts.length; i++) {
                var p = parts[i];
                html += '<tr>';
                html += '<td>' + (i + 1) + '</td>';
                html += '<td><span class="part-oem" onclick="Laximo.partRefs(\'' + this.escHtml(p.oem || p.number || '') + '\', \'' + this.escHtml(this.currentCatalog) + '\')">' + this.escHtml(p.oem || p.number || '-') + '</span></td>';
                html += '<td class="part-name">' + this.escHtml(p.name || '-') + '</td>';
                html += '<td>' + this.escHtml(p.note || p.description || '') + '</td>';
                html += '<td><span class="part-aftermarket" onclick="Laximo.loadAftermarket(\'' + this.escHtml(p.oem || p.number || '') + '\', \'' + this.escHtml(p.name || '') + '\')">Analogs</span></td>';
                html += '</tr>';
            }
            html += '</tbody></table>';
        }

        document.getElementById(this.containerId).innerHTML = html;
    },

    // ===== Quick Group Details =====
    loadQuickDetails: function(groupid, name) {
        var self = this;
        this.showLoading('Loading parts for: ' + this.escHtml(name) + '...');
        this.ajax({
            action: 'quick_details',
            catalog: this.currentCatalog,
            vehicle_id: this.currentVehicle,
            vehicleid: this.currentVehicle,
            ssd: this.currentSsd,
            group_id: groupid,
            groupid: groupid
        }, function(data) {
            self.renderUnitDetails(data.data || [], name);
        });
    },

    // ===== Part Search =====
    searchParts: function() {
        var input = document.getElementById('laximo_part_search_input');
        if (!input || !input.value.trim()) {
            alert('Please enter a part name to search.');
            return;
        }
        if (!this.currentVehicle) {
            alert('Identify a vehicle first (VIN or wizard), then search parts by name.');
            return;
        }
        var query = input.value.trim();
        var self = this;
        this.showLoading('Searching for: ' + this.escHtml(query) + '...');
        this.ajax({
            action: 'part_search',
            catalog: this.currentCatalog,
            vehicle_id: this.currentVehicle || '',
            vehicleid: this.currentVehicle || '',
            ssd: this.currentSsd || '',
            q: query,
            query: query
        }, function(data) {
            var rows = data.data || [];
            if (!Array.isArray(rows)) {
                rows = [];
            }
            self.renderSearchResults(rows, query);
        });
    },

    renderSearchResults: function(results, query) {
        var html = this.renderBreadcrumbs();
        html += '<h4 style="margin:0 0 15px;font-size:15px;">Search results for: "' + this.escHtml(query) + '"</h4>';

        if (results.length === 0) {
            html += '<div class="laximo-error">No parts found for "' + this.escHtml(query) + '". Try a different search term.</div>';
        } else {
            html += '<table class="laximo-parts-table">';
            html += '<thead><tr><th>#</th><th>OEM Number</th><th>Name</th><th>Catalog</th><th>Cross-ref</th></tr></thead>';
            html += '<tbody>';
            for (var i = 0; i < results.length; i++) {
                var p = results[i];
                html += '<tr>';
                html += '<td>' + (i + 1) + '</td>';
                html += '<td><span class="part-oem" onclick="Laximo.partRefs(\'' + this.escHtml(p.oem || p.number || '') + '\', \'' + this.escHtml(p.catalog || this.currentCatalog) + '\')">' + this.escHtml(p.oem || p.number || '-') + '</span></td>';
                html += '<td class="part-name">' + this.escHtml(p.name || '-') + '</td>';
                html += '<td>' + this.escHtml(p.catalog || '') + '</td>';
                html += '<td><span class="part-aftermarket" onclick="Laximo.loadAftermarket(\'' + this.escHtml(p.oem || p.number || '') + '\', \'' + this.escHtml(p.name || '') + '\')">Analogs</span></td>';
                html += '</tr>';
            }
            html += '</tbody></table>';
        }

        document.getElementById(this.containerId).innerHTML = html;
    },

    // ===== Part References (cross-catalog) =====
    partRefs: function(oem, catalog) {
        var self = this;
        this.showLoading('Loading part references for ' + this.escHtml(oem) + '...');
        this.ajax({
            action: 'part_refs',
            oem: oem,
            catalog: catalog
        }, function(data) {
            self.renderPartRefs(data.data || [], oem);
        });
    },

    renderPartRefs: function(refs, oem) {
        var html = this.renderBreadcrumbs();
        html += '<h4 style="margin:0 0 15px;font-size:15px;">References for OEM: ' + this.escHtml(oem) + '</h4>';
        html += '<a onclick="Laximo.loadAftermarket(\'' + this.escHtml(oem) + '\', \'\')" style="color:#28a745;cursor:pointer;font-size:13px;margin-bottom:15px;display:inline-block;">View aftermarket analogs &rarr;</a>';

        if (refs.length === 0) {
            html += '<div class="laximo-error">No cross-references found for this part.</div>';
        } else {
            html += '<table class="laximo-parts-table">';
            html += '<thead><tr><th>Catalog</th><th>OEM Number</th><th>Name</th><th>Applicability</th></tr></thead>';
            html += '<tbody>';
            for (var i = 0; i < refs.length; i++) {
                var r = refs[i];
                html += '<tr>';
                html += '<td>' + this.escHtml(r.catalog || '-') + '</td>';
                html += '<td class="part-oem">' + this.escHtml(r.oem || r.number || '-') + '</td>';
                html += '<td>' + this.escHtml(r.name || '-') + '</td>';
                html += '<td>' + this.escHtml(r.applicability || '') + '</td>';
                html += '</tr>';
            }
            html += '</tbody></table>';
        }

        document.getElementById(this.containerId).innerHTML = html;
    },

    // ===== Aftermarket (DOC service) =====
    loadAftermarket: function(oem, name) {
        var self = this;
        this.showLoading('Loading aftermarket analogs for ' + this.escHtml(oem) + '...');
        this.ajax({
            action: 'aftermarket',
            oem: oem
        }, function(data) {
            self.renderAftermarket(data.data || [], oem, name);
        });
    },

    flattenAftermarket: function(items) {
        var out = [];
        if (!items) return out;
        if (!Array.isArray(items)) {
            // Nested XML→JSON blob: try common shapes
            if (items.detail) {
                items = Array.isArray(items.detail) ? items.detail : [items.detail];
            } else if (items.FindOEM && items.FindOEM.detail) {
                items = Array.isArray(items.FindOEM.detail) ? items.FindOEM.detail : [items.FindOEM.detail];
            } else {
                return out;
            }
        }
        for (var i = 0; i < items.length; i++) {
            var am = items[i] || {};
            var brand = am.brand || am.manufacturer || (am['@attributes'] && (am['@attributes'].manufacturer || am['@attributes'].brand)) || '';
            var number = am.number || am.oem || am.formattedoem || (am['@attributes'] && (am['@attributes'].oem || am['@attributes'].formattedoem)) || '';
            var name = am.name || (am['@attributes'] && am['@attributes'].name) || '';
            if (number || brand) {
                out.push({
                    brand: brand,
                    number: number,
                    name: name,
                    is_replacement: !!am.is_replacement,
                    replacement_type: am.replacement_type || '',
                    rate: am.rate || ''
                });
            }
        }
        return out;
    },

    renderAftermarket: function(items, oem, partName) {
        var rows = this.flattenAftermarket(items);
        var html = this.renderBreadcrumbs();
        html += '<h4 style="margin:0 0 5px;font-size:15px;">Aftermarket analogs for OEM: ' + this.escHtml(oem) + '</h4>';
        if (partName) html += '<p style="color:#666;font-size:13px;margin-bottom:15px;">' + this.escHtml(partName) + '</p>';

        if (rows.length === 0) {
            html += '<div class="laximo-error">No aftermarket cross-references found for this part. Please try again later.</div>';
        } else {
            html += '<div class="laximo-aftermarket">';
            html += '<h4>Cross-references / Analogs / Replacements (' + rows.length + ')</h4>';
            html += '<table class="laximo-parts-table"><thead><tr><th>Brand</th><th>Number</th><th>Name</th><th>Type</th></tr></thead><tbody>';
            for (var i = 0; i < rows.length; i++) {
                var am = rows[i];
                html += '<tr>';
                html += '<td class="am-brand">' + this.escHtml(am.brand || '-') + '</td>';
                html += '<td class="am-number"><a href="/parts/brands/' + encodeURIComponent(am.number) + '">' + this.escHtml(am.number || '-') + '</a></td>';
                html += '<td class="am-name">' + this.escHtml(am.name || '') + '</td>';
                html += '<td>' + this.escHtml(am.is_replacement ? (am.replacement_type || 'replacement') : 'OEM match') + (am.rate ? ' · rate ' + this.escHtml(String(am.rate)) : '') + '</td>';
                html += '</tr>';
            }
            html += '</tbody></table></div>';
        }

        document.getElementById(this.containerId).innerHTML = html;
    }
};

// Auto-init when document is ready
jQuery(document).ready(function() {
    if (document.getElementById('Laximo_container')) {
        Laximo.init();
    }
});
