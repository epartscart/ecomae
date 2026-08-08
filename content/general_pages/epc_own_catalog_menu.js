/**
 * ePartsCart Catalog of products mega menu (PHP dp_menu / showCatalogMenu twin).
 * Loads /storefront/catalogue/tree and renders #dp_menu.
 */
(function () {
  'use strict';

  var loaded = false;
  var loading = false;
  var scrollY = 0;
  var TREE_URL = '/storefront/catalogue/tree';
  var PLACEHOLDER = '/content/files/images/no_image.png';

  function $(sel, root) {
    return (root || document).querySelector(sel);
  }

  function ensureFon() {
    var fon = $('.fon-catalog');
    if (!fon) {
      fon = document.createElement('div');
      fon.className = 'fon-catalog';
      var header = $('header.epc-nero-header') || $('header');
      if (header && header.parentNode) {
        header.parentNode.insertBefore(fon, header.nextSibling);
      } else {
        document.body.appendChild(fon);
      }
      fon.addEventListener('click', function () {
        window.showCatalogMenu();
      });
    }
    return fon;
  }

  function imageUrl(image) {
    if (!image) return PLACEHOLDER;
    return '/content/files/images/catalogue_images/' + encodeURIComponent(image);
  }

  function countLinks(nodes) {
    var n = 0;
    (nodes || []).forEach(function (node) {
      n += 1 + countLinks(node.data || []);
    });
    return n;
  }

  function renderLinks(nodes, level) {
    level = level || 0;
    var html = '';
    var showCnt = 0;
    var linkCnt = 0;
    if (level === 0) {
      var total = countLinks(nodes);
      if (total < 40 && total > 15) showCnt = Math.ceil(total / 2);
      else if (total > 40) showCnt = Math.ceil(total / 3);
      else showCnt = total;
      html += '<div class="column_box_line">';
    }
    level += 1;
    (nodes || []).forEach(function (cat) {
      if (level === 1) {
        html += '<div class="box_line">';
      }
      var cls = level === 1 ? 'one_line' : 'two_line';
      var style = level > 2 ? ' style="margin-left:' + 15 * (level - 1) + 'px;"' : '';
      var href = cat.href || ('/storefront/own-catalog-app?url=' + encodeURIComponent(cat.url || ''));
      html += '<a class="' + cls + '"' + style + ' href="' + href + '">' + escapeHtml(cat.value || cat.alias || '') + '</a>';
      if ((cat.data || []).length) {
        html += renderLinks(cat.data, level);
      }
      if (level === 1) {
        html += '</div>';
      }
      linkCnt += 1;
      if (level === 1 && showCnt > 0 && linkCnt > showCnt) {
        linkCnt = 0;
        html += '</div><div class="column_box_line">';
      }
    });
    if (level === 1) {
      html += '</div>';
    }
    return html;
  }

  function escapeHtml(s) {
    return String(s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function renderTree(tree) {
    var host = $('#dp_menu .vertical-tabs-right');
    if (!host) return;
    if (!tree || !tree.length) {
      host.innerHTML =
        '<div class="epc-own-cat-empty">' +
        '<p>Own catalog categories are loading or empty.</p>' +
        '<p><a href="/storefront/own-catalog-app">Open own catalog</a> · ' +
        '<a href="/storefront/search-app?mode=name">Search by name</a></p></div>';
      return;
    }

    var tabs = '<div class="vertical-tab-list"><ul class="nav">';
    var panes = '<div class="tab-content" style="position:relative;padding-top:10px;">';
    tree.forEach(function (cat, i) {
      var active = i === 0 ? ' active' : '';
      var hasChildren = (cat.data || []).length > 0 || (cat.childCount || 0) > 0;
      var img = imageUrl(cat.image);
      var label = escapeHtml(cat.value || cat.alias || ('Category #' + cat.id));
      if (hasChildren) {
        tabs +=
          '<li class="' +
          active.trim() +
          '"><a class="count_ch" href="#category_' +
          cat.id +
          '" data-toggle="tab" data-hover="tab">' +
          '<table><tr><td style="padding-right:10px;"><img style="max-width:30px;max-height:30px;" src="' +
          img +
          '" alt=""/></td><td style="width:100%;">' +
          label +
          '</td></tr></table></a></li>';
        panes +=
          '<div style="overflow:hidden;" class="tab-pane' +
          active +
          '" id="category_' +
          cat.id +
          '">' +
          renderLinks(cat.data || [], 0) +
          '</div>';
      } else {
        var href = cat.href || ('/storefront/own-catalog-app?url=' + encodeURIComponent(cat.url || ''));
        tabs +=
          '<li class="' +
          active.trim() +
          '"><a href="' +
          href +
          '">' +
          '<table><tr><td style="padding-right:10px;"><img style="max-width:30px;max-height:30px;" src="' +
          img +
          '" alt=""/></td><td style="width:100%;">' +
          label +
          '</td></tr></table></a></li>';
        panes +=
          '<div style="overflow:hidden;" class="tab-pane' +
          active +
          '" id="category_' +
          cat.id +
          '"><div class="box_line"><a class="one_line" href="' +
          href +
          '">Browse ' +
          label +
          '</a></div></div>';
      }
    });
    tabs += '</ul></div>';
    panes += '</div>';
    host.innerHTML = tabs + panes;

    host.querySelectorAll('[data-hover="tab"]').forEach(function (el) {
      el.addEventListener('mouseenter', function () {
        var href = el.getAttribute('href') || '';
        if (href.charAt(0) !== '#') return;
        host.querySelectorAll('.vertical-tab-list li').forEach(function (li) {
          li.classList.remove('active');
        });
        host.querySelectorAll('.tab-pane').forEach(function (pane) {
          pane.classList.remove('active');
        });
        if (el.parentElement) el.parentElement.classList.add('active');
        var pane = host.querySelector(href);
        if (pane) pane.classList.add('active');
      });
    });
  }

  function loadTree(cb) {
    if (loaded) {
      if (cb) cb();
      return;
    }
    if (loading) return;
    loading = true;
    fetch(TREE_URL, { credentials: 'same-origin' })
      .then(function (r) {
        return r.json();
      })
      .then(function (data) {
        loaded = true;
        loading = false;
        renderTree((data && data.tree) || []);
        if (cb) cb();
      })
      .catch(function () {
        loading = false;
        renderTree([]);
        if (cb) cb();
      });
  }

  window.showCatalogMenu = function () {
    ensureFon();
    var menu = document.getElementById('dp_menu');
    var fon = $('.fon-catalog');
    if (!menu) {
      window.location.href = '/storefront/own-catalog-app';
      return false;
    }

    var open = menu.style.display === 'block' || menu.classList.contains('is-open');
    if (open) {
      menu.style.display = 'none';
      menu.classList.remove('is-open');
      if (fon) fon.style.display = 'none';
      document.body.style.position = '';
      document.body.style.top = '0px';
      document.body.style.overflow = '';
      window.scrollTo(0, parseInt(scrollY || '0', 10) || 0);
      return false;
    }

    loadTree(function () {
      scrollY = window.pageYOffset || 0;
      document.body.style.overflow = 'hidden';
      menu.style.display = 'block';
      menu.classList.add('is-open');
      if (fon) fon.style.display = 'block';
      var header = $('header.epc-nero-header') || $('header');
      var headerH = header ? header.offsetHeight : 80;
      menu.style.maxHeight = Math.max(240, window.innerHeight - headerH - 20) + 'px';
      document.body.style.position = 'fixed';
      document.body.style.top = '-' + scrollY + 'px';
    });
    return false;
  };

  document.addEventListener('DOMContentLoaded', function () {
    ensureFon();
    document.querySelectorAll('.header-cat-btn').forEach(function (btn) {
      btn.addEventListener('click', function (e) {
        e.preventDefault();
        window.showCatalogMenu();
      });
    });
  });
})();
