/**
 * CP SKU Media & Specs manager UI logic.
 */
(function (window, document) {
  'use strict';

  function el(tag, cls, html) {
    var n = document.createElement(tag);
    if (cls) n.className = cls;
    if (html != null) n.innerHTML = html;
    return n;
  }

  function qs(sel, root) {
    return (root || document).querySelector(sel);
  }

  function qsa(sel, root) {
    return Array.prototype.slice.call((root || document).querySelectorAll(sel));
  }

  function EpcSkuMedia(cfg) {
    this.cfg = cfg || {};
    this.endpoint = cfg.endpoint;
    this.csrf = cfg.csrf || '';
    this.root = qs(cfg.root || '#epc-sku-media');
    this.payload = null;
    this.profileId = parseInt(cfg.profileId || 0, 10) || 0;
    this.productId = parseInt(cfg.productId || 0, 10) || 0;
    this.brand = cfg.brand || '';
    this.article = cfg.article || '';
    this.meta = {
      photo_types: {},
      value_types: {},
      default_spec_types: []
    };
    if (!this.root) return;
    this.bind();
    this.boot();
  }

  EpcSkuMedia.prototype.toast = function (msg) {
    var t = qs('.epc-sku-media__toast', this.root);
    if (!t) {
      t = el('div', 'epc-sku-media__toast');
      this.root.appendChild(t);
    }
    t.textContent = msg;
    t.classList.add('is-on');
    clearTimeout(this._toastTimer);
    this._toastTimer = setTimeout(function () {
      t.classList.remove('is-on');
    }, 2200);
  };

  EpcSkuMedia.prototype.api = function (action, data, file) {
    var self = this;
    var fd = new FormData();
    fd.append('action', action);
    fd.append('csrf_guard_key', this.csrf);
    Object.keys(data || {}).forEach(function (k) {
      if (data[k] == null) return;
      fd.append(k, data[k]);
    });
    if (file) fd.append('photo', file);
    return fetch(this.endpoint, {
      method: 'POST',
      body: fd,
      credentials: 'same-origin'
    }).then(function (r) {
      return r.json();
    }).then(function (json) {
      if (!json || !json.ok) {
        throw new Error((json && json.error) || 'Request failed');
      }
      return json;
    }).catch(function (err) {
      self.toast(err.message || 'Error');
      throw err;
    });
  };

  EpcSkuMedia.prototype.bind = function () {
    var self = this;
    this.root.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-sku-action]');
      if (!btn) return;
      var act = btn.getAttribute('data-sku-action');
      if (act === 'search') self.refreshList();
      if (act === 'new') self.newProfile();
      if (act === 'save-profile') self.saveProfile();
      if (act === 'delete-profile') self.deleteProfile();
      if (act === 'pick') self.pickItem(btn);
      if (act === 'add-group') self.addGroup(btn.getAttribute('data-name'), btn.getAttribute('data-code'), btn.getAttribute('data-icon'));
      if (act === 'delete-group') self.deleteGroup(parseInt(btn.getAttribute('data-id'), 10) || 0);
      if (act === 'add-row') self.addRow(parseInt(btn.getAttribute('data-group'), 10) || 0);
      if (act === 'delete-row') self.deleteRow(parseInt(btn.getAttribute('data-id'), 10) || 0);
      if (act === 'delete-photo') self.deletePhoto(parseInt(btn.getAttribute('data-id'), 10) || 0);
      if (act === 'primary-photo') self.primaryPhoto(parseInt(btn.getAttribute('data-id'), 10) || 0);
      if (act === 'pick-file') qs('#epc-sku-photo-input', self.root).click();
    });
    this.root.addEventListener('change', function (e) {
      if (e.target && e.target.id === 'epc-sku-photo-input' && e.target.files && e.target.files[0]) {
        self.uploadPhoto(e.target.files[0]);
        e.target.value = '';
      }
    });
    var search = qs('#epc-sku-search', this.root);
    if (search) {
      search.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') {
          e.preventDefault();
          self.refreshList();
        }
      });
    }
  };

  EpcSkuMedia.prototype.boot = function () {
    var self = this;
    this.api('meta', {}).then(function (json) {
      self.meta.photo_types = json.photo_types || {};
      self.meta.value_types = json.value_types || {};
      self.meta.default_spec_types = json.default_spec_types || [];
      self.renderChips();
      return self.refreshList();
    }).then(function () {
      if (self.profileId > 0) return self.loadProfile(self.profileId);
      if (self.productId > 0 || self.brand || self.article) {
        return self.api('get', {
          product_id: self.productId,
          brand: self.brand,
          article: self.article
        }).then(function (json) {
          if (json.payload) {
            self.applyPayload(json.payload);
          } else {
            self.renderEditor(null);
            if (self.productId || self.brand || self.article) {
              qs('[name="brand"]', self.root).value = self.brand || '';
              qs('[name="article"]', self.root).value = self.article || '';
              qs('[name="product_id"]', self.root).value = self.productId || '';
            }
          }
        });
      }
      self.renderEditor(null);
    });
  };

  EpcSkuMedia.prototype.refreshList = function () {
    var self = this;
    var q = (qs('#epc-sku-search', this.root) || {}).value || '';
    return this.api('list', { q: q }).then(function (json) {
      var list = qs('#epc-sku-list', self.root);
      if (!list) return;
      list.innerHTML = '';
      var items = json.items || [];
      if (!items.length) {
        list.innerHTML = '<li class="epc-sku-media__empty">No matching brands / articles. Try a supplier article or create a new profile.</li>';
        return;
      }
      items.forEach(function (it) {
        var li = el('li');
        var b = el('button');
        b.setAttribute('type', 'button');
        b.setAttribute('data-sku-action', 'pick');
        b.setAttribute('data-id', String(it.id || 0));
        b.setAttribute('data-source', String(it.source || 'profile'));
        b.setAttribute('data-brand', String(it.brand || ''));
        b.setAttribute('data-article', String(it.article || it.article_show || ''));
        b.setAttribute('data-title', String(it.title || ''));
        b.setAttribute('data-product-id', String(it.product_id || 0));
        b.setAttribute('data-has-profile', it.has_profile ? '1' : '0');
        if (self.profileId > 0 && self.profileId === parseInt(it.id, 10)) b.classList.add('is-active');
        var title = (it.brand || '') + ' ' + (it.article || it.article_show || '');
        title = title.trim() || (it.title || ('SKU #' + (it.id || '')));
        var sourceLabel = it.source === 'supplier'
          ? ('Warehouse' + (it.warehouse ? ': ' + it.warehouse : ''))
          : (it.source === 'catalogue' ? 'Catalogue' : 'Media profile');
        var mediaBits = [];
        if (it.has_profile) {
          mediaBits.push((it.photo_count || 0) + ' photos');
          mediaBits.push((it.spec_count || 0) + ' specs');
        } else {
          mediaBits.push('No photos yet — click to add');
        }
        b.innerHTML =
          '<div><strong>' + self.esc(title) + '</strong> ' +
          '<span class="epc-sku-media__badge epc-sku-media__badge--' + self.esc(it.source || 'profile') + '">' +
          self.esc(sourceLabel) + '</span></div>' +
          '<div class="meta">' + self.esc(it.title || '') +
          (mediaBits.length ? ' · ' + mediaBits.join(' · ') : '') + '</div>';
        li.appendChild(b);
        list.appendChild(li);
      });
    });
  };

  EpcSkuMedia.prototype.pickItem = function (btn) {
    var id = parseInt(btn.getAttribute('data-id'), 10) || 0;
    var hasProfile = btn.getAttribute('data-has-profile') === '1';
    if (id > 0 && hasProfile) {
      return this.loadProfile(id);
    }
    var self = this;
    return this.api('ensure', {
      brand: btn.getAttribute('data-brand') || '',
      article: btn.getAttribute('data-article') || '',
      title: btn.getAttribute('data-title') || '',
      product_id: parseInt(btn.getAttribute('data-product-id'), 10) || 0
    }).then(function (json) {
      self.applyPayload(json.payload);
      return self.refreshList();
    });
  };

  EpcSkuMedia.prototype.esc = function (s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  };

  EpcSkuMedia.prototype.newProfile = function () {
    this.profileId = 0;
    this.payload = null;
    this.renderEditor(null);
  };

  EpcSkuMedia.prototype.loadProfile = function (id) {
    var self = this;
    return this.api('get', { profile_id: id }).then(function (json) {
      self.applyPayload(json.payload);
      self.refreshList();
    });
  };

  EpcSkuMedia.prototype.applyPayload = function (payload) {
    this.payload = payload;
    this.profileId = payload && payload.profile ? parseInt(payload.profile.id, 10) : 0;
    this.renderEditor(payload);
  };

  EpcSkuMedia.prototype.renderChips = function () {
    var wrap = qs('#epc-sku-type-chips', this.root);
    if (!wrap) return;
    wrap.innerHTML = '';
    (this.meta.default_spec_types || []).forEach(function (t) {
      var b = el('button');
      b.setAttribute('type', 'button');
      b.setAttribute('data-sku-action', 'add-group');
      b.setAttribute('data-name', t.name);
      b.setAttribute('data-code', t.code);
      b.setAttribute('data-icon', t.icon);
      b.innerHTML = '<i class="fa ' + t.icon + '"></i> ' + t.name;
      wrap.appendChild(b);
    });
  };

  EpcSkuMedia.prototype.renderEditor = function (payload) {
    var p = payload && payload.profile ? payload.profile : {};
    qs('[name="profile_id"]', this.root).value = p.id || '';
    qs('[name="product_id"]', this.root).value = p.product_id || this.productId || '';
    qs('[name="brand"]', this.root).value = p.brand || '';
    qs('[name="article"]', this.root).value = p.article || '';
    qs('[name="title"]', this.root).value = p.title || '';
    qs('[name="subtitle"]', this.root).value = p.subtitle || '';
    var status = qs('[name="status"]', this.root);
    if (status) status.value = p.status || 'active';

    var empty = qs('#epc-sku-editor-empty', this.root);
    var body = qs('#epc-sku-editor-body', this.root);
    if (!payload) {
      if (empty) empty.style.display = '';
      // keep form visible for create
    } else if (empty) {
      empty.style.display = 'none';
    }
    if (body) body.style.display = '';

    this.renderPhotos(payload ? payload.photos || [] : []);
    this.renderSpecs(payload ? payload.spec_groups || [] : []);
  };

  EpcSkuMedia.prototype.renderPhotos = function (photos) {
    var grid = qs('#epc-sku-photos', this.root);
    if (!grid) return;
    grid.innerHTML = '';
    if (!photos.length) {
      grid.innerHTML = '<div class="epc-sku-media__empty">No photos yet — upload as many as you need.</div>';
      return;
    }
    var self = this;
    var types = (this.payload && this.payload.photo_types) || this.meta.photo_types || {};
    photos.forEach(function (ph) {
      var card = el('div', 'epc-sku-media__photo');
      var typeLabel = types[ph.photo_type] || ph.photo_type || 'Product';
      card.innerHTML =
        '<img src="' + self.esc(ph.url) + '" alt="' + self.esc(ph.alt || '') + '">' +
        '<div class="body">' +
        (ph.is_primary == 1 || ph.is_primary === '1' ? '<span class="badge">Primary</span>' : '') +
        '<div><strong>' + self.esc(typeLabel) + '</strong></div>' +
        '<div style="font-size:12px;color:#64748b;">' + self.esc(ph.caption || ph.alt || '') + '</div>' +
        '<div style="display:flex;gap:6px;flex-wrap:wrap;">' +
        '<button type="button" class="epc-sku-media__btn epc-sku-media__btn--ghost epc-sku-media__btn--sm" data-sku-action="primary-photo" data-id="' + ph.id + '">Primary</button>' +
        '<button type="button" class="epc-sku-media__btn epc-sku-media__btn--danger epc-sku-media__btn--sm" data-sku-action="delete-photo" data-id="' + ph.id + '">Delete</button>' +
        '</div></div>';
      grid.appendChild(card);
    });
  };

  EpcSkuMedia.prototype.renderSpecs = function (groups) {
    var wrap = qs('#epc-sku-specs', this.root);
    if (!wrap) return;
    wrap.innerHTML = '';
    var self = this;
    var valueTypes = (this.payload && this.payload.value_types) || this.meta.value_types || {};
    if (!groups.length) {
      wrap.innerHTML = '<div class="epc-sku-media__empty">Add a specification type above, then fill unlimited rows.</div>';
      return;
    }
    groups.forEach(function (g) {
      var box = el('div', 'epc-sku-media__spec-type');
      var head = el('div', 'epc-sku-media__spec-type-h');
      head.innerHTML = '<h4><i class="fa ' + self.esc(g.icon || 'fa-list') + '"></i> ' + self.esc(g.name) + '</h4>' +
        '<div style="display:flex;gap:6px;">' +
        '<button type="button" class="epc-sku-media__btn epc-sku-media__btn--sm" data-sku-action="add-row" data-group="' + g.id + '"><i class="fa fa-plus"></i> Spec</button>' +
        '<button type="button" class="epc-sku-media__btn epc-sku-media__btn--ghost epc-sku-media__btn--sm" data-sku-action="delete-group" data-id="' + g.id + '">Remove type</button>' +
        '</div>';
      box.appendChild(head);
      var table = el('table', 'epc-sku-media__spec-table');
      table.innerHTML = '<thead><tr><th>Label</th><th>Type</th><th>Value</th><th>Unit</th><th></th></tr></thead>';
      var tb = el('tbody');
      (g.rows || []).forEach(function (row) {
        var tr = el('tr');
        var typeOpts = Object.keys(valueTypes).map(function (k) {
          return '<option value="' + k + '"' + (row.value_type === k ? ' selected' : '') + '>' + self.esc(valueTypes[k]) + '</option>';
        }).join('');
        tr.innerHTML =
          '<td><input type="text" data-row="' + row.id + '" data-field="label" value="' + self.esc(row.label) + '"></td>' +
          '<td><select data-row="' + row.id + '" data-field="value_type">' + typeOpts + '</select></td>' +
          '<td><input type="text" data-row="' + row.id + '" data-field="value" value="' + self.esc(row.value || '') + '"></td>' +
          '<td><input type="text" data-row="' + row.id + '" data-field="unit" value="' + self.esc(row.unit || '') + '" style="width:80px;"></td>' +
          '<td><button type="button" class="epc-sku-media__btn epc-sku-media__btn--ghost epc-sku-media__btn--sm" data-sku-action="delete-row" data-id="' + row.id + '">×</button></td>';
        tb.appendChild(tr);
      });
      if (!(g.rows || []).length) {
        tb.innerHTML = '<tr><td colspan="5" class="epc-sku-media__empty">No rows in this specification type yet.</td></tr>';
      }
      table.appendChild(tb);
      box.appendChild(table);
      wrap.appendChild(box);
    });

    qsa('[data-row][data-field]', wrap).forEach(function (input) {
      var timer = null;
      var handler = function () {
        clearTimeout(timer);
        timer = setTimeout(function () {
          self.saveRow(parseInt(input.getAttribute('data-row'), 10), wrap);
        }, 450);
      };
      input.addEventListener('change', handler);
      input.addEventListener('blur', handler);
    });
  };

  EpcSkuMedia.prototype.collectProfile = function () {
    return {
      profile_id: this.profileId || 0,
      product_id: (qs('[name="product_id"]', this.root) || {}).value || 0,
      brand: (qs('[name="brand"]', this.root) || {}).value || '',
      article: (qs('[name="article"]', this.root) || {}).value || '',
      title: (qs('[name="title"]', this.root) || {}).value || '',
      subtitle: (qs('[name="subtitle"]', this.root) || {}).value || '',
      status: (qs('[name="status"]', this.root) || {}).value || 'active'
    };
  };

  EpcSkuMedia.prototype.saveProfile = function () {
    var self = this;
    return this.api('save_profile', this.collectProfile()).then(function (json) {
      self.applyPayload(json.payload);
      self.toast('SKU profile saved');
      self.refreshList();
    });
  };

  EpcSkuMedia.prototype.deleteProfile = function () {
    if (!this.profileId) return;
    if (!window.confirm('Delete this SKU media profile and all photos/specs?')) return;
    var self = this;
    this.api('delete_profile', { profile_id: this.profileId }).then(function () {
      self.profileId = 0;
      self.payload = null;
      self.renderEditor(null);
      self.toast('Deleted');
      self.refreshList();
    });
  };

  EpcSkuMedia.prototype.uploadPhoto = function (file) {
    var self = this;
    var ensure = this.profileId ? Promise.resolve() : this.saveProfile();
    ensure.then(function () {
      return self.api('upload_photo', {
        profile_id: self.profileId,
        photo_type: (qs('#epc-sku-photo-type', self.root) || {}).value || 'product',
        caption: (qs('#epc-sku-photo-caption', self.root) || {}).value || '',
        alt: (qs('#epc-sku-photo-alt', self.root) || {}).value || ''
      }, file);
    }).then(function (json) {
      self.applyPayload(json.payload);
      self.toast('Photo uploaded');
      self.refreshList();
    });
  };

  EpcSkuMedia.prototype.deletePhoto = function (id) {
    var self = this;
    this.api('delete_photo', { photo_id: id, profile_id: this.profileId }).then(function (json) {
      self.applyPayload(json.payload);
      self.toast('Photo removed');
      self.refreshList();
    });
  };

  EpcSkuMedia.prototype.primaryPhoto = function (id) {
    var self = this;
    this.api('update_photo', { photo_id: id, profile_id: this.profileId, is_primary: 1 }).then(function (json) {
      self.applyPayload(json.payload);
      self.toast('Primary photo set');
    });
  };

  EpcSkuMedia.prototype.addGroup = function (name, code, icon) {
    var self = this;
    var ensure = this.profileId ? Promise.resolve() : this.saveProfile();
    ensure.then(function () {
      if (!name) {
        name = window.prompt('Specification type name', 'Custom');
        if (!name) return null;
        code = 'custom';
        icon = 'fa-list';
      }
      return self.api('add_spec_group', {
        profile_id: self.profileId,
        name: name,
        code: code || '',
        icon: icon || 'fa-list'
      });
    }).then(function (json) {
      if (!json) return;
      self.applyPayload(json.payload);
      self.toast('Specification type added');
      self.refreshList();
    });
  };

  EpcSkuMedia.prototype.deleteGroup = function (id) {
    if (!window.confirm('Remove this specification type and its rows?')) return;
    var self = this;
    this.api('delete_spec_group', { group_id: id, profile_id: this.profileId }).then(function (json) {
      self.applyPayload(json.payload);
      self.toast('Type removed');
      self.refreshList();
    });
  };

  EpcSkuMedia.prototype.addRow = function (groupId) {
    var self = this;
    var label = window.prompt('Specification label', '');
    if (!label) return;
    this.api('add_spec_row', {
      group_id: groupId,
      profile_id: this.profileId,
      label: label,
      value: '',
      value_type: 'text',
      unit: ''
    }).then(function (json) {
      self.applyPayload(json.payload);
      self.toast('Specification added');
      self.refreshList();
    });
  };

  EpcSkuMedia.prototype.saveRow = function (rowId, scope) {
    var fields = {};
    qsa('[data-row="' + rowId + '"]', scope || this.root).forEach(function (n) {
      fields[n.getAttribute('data-field')] = n.value;
    });
    var self = this;
    this.api('update_spec_row', {
      row_id: rowId,
      profile_id: this.profileId,
      label: fields.label || '',
      value: fields.value || '',
      value_type: fields.value_type || 'text',
      unit: fields.unit || ''
    }).then(function () {
      self.toast('Spec saved');
    });
  };

  EpcSkuMedia.prototype.deleteRow = function (id) {
    var self = this;
    this.api('delete_spec_row', { row_id: id, profile_id: this.profileId }).then(function (json) {
      self.applyPayload(json.payload);
      self.toast('Spec removed');
      self.refreshList();
    });
  };

  window.EpcSkuMedia = EpcSkuMedia;

  function epcSkuMediaBootFromDom() {
    var root = document.getElementById('epc-sku-media');
    if (!root || root.getAttribute('data-epc-booted') === '1') {
      return;
    }
    var cfg = Object.assign({}, window.EPC_SKU_MEDIA_CP || {});
    cfg.root = '#epc-sku-media';
    cfg.endpoint = cfg.endpoint || root.getAttribute('data-endpoint') || '/content/shop/catalogue/ajax_epc_sku_media.php';
    cfg.csrf = cfg.csrf || root.getAttribute('data-csrf') || '';
    cfg.profileId = parseInt(root.getAttribute('data-profile-id') || '0', 10) || 0;
    cfg.productId = parseInt(root.getAttribute('data-product-id') || '0', 10) || 0;
    cfg.brand = root.getAttribute('data-brand') || '';
    cfg.article = root.getAttribute('data-article') || '';
    root.setAttribute('data-epc-booted', '1');
    window.epcSkuMediaApp = new EpcSkuMedia(cfg);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', epcSkuMediaBootFromDom);
  } else {
    epcSkuMediaBootFromDom();
  }
})(window, document);
