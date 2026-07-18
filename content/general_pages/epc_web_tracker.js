/**
 * ECOM AE / tenant first-party website tracker.
 * Captures pageviews, clicks, search, engagement, UTM, device — batches via sendBeacon.
 */
(function () {
  'use strict';
  var CFG = window.EPC_WEB_TRACKER || null;
  if (!CFG || !CFG.endpoint || !CFG.site_key) return;
  if (navigator.doNotTrack === '1' || window.doNotTrack === '1') return;
  // Never track CP / admin shells.
  try {
    if (/\/cp(\/|$)/i.test(location.pathname) || /(?:^|\.)cp\./i.test(location.hostname)) return;
  } catch (e) {}

  var STORAGE_S = 'epc_wt_sid';
  var STORAGE_V = 'epc_wt_vid';
  var MAX_CLICKS_PAGE = 80;
  var FLUSH_MS = 4000;

  function uuid() {
    try {
      if (crypto && crypto.randomUUID) return crypto.randomUUID();
    } catch (e) {}
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
      var r = (Math.random() * 16) | 0;
      var v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }

  function getCookie(n) {
    var m = document.cookie.match(new RegExp('(?:^|; )' + n.replace(/([.$?*|{}()[\]\\/+^])/g, '\\$1') + '=([^;]*)'));
    return m ? decodeURIComponent(m[1]) : '';
  }
  function setCookie(n, v, days) {
    var d = new Date();
    d.setTime(d.getTime() + days * 864e5);
    document.cookie = n + '=' + encodeURIComponent(v) + '; path=/; expires=' + d.toUTCString() + '; SameSite=Lax';
  }

  function sid() {
    var s = getCookie(STORAGE_S);
    if (!s) {
      try { s = sessionStorage.getItem(STORAGE_S) || ''; } catch (e) {}
    }
    if (!s) s = uuid();
    setCookie(STORAGE_S, s, 1);
    try { sessionStorage.setItem(STORAGE_S, s); } catch (e) {}
    return s;
  }
  function vid() {
    var v = getCookie(STORAGE_V);
    if (!v) {
      try { v = localStorage.getItem(STORAGE_V) || ''; } catch (e) {}
    }
    if (!v) v = uuid();
    setCookie(STORAGE_V, v, 400);
    try { localStorage.setItem(STORAGE_V, v); } catch (e) {}
    return v;
  }

  function qs(name) {
    try {
      return new URLSearchParams(location.search).get(name) || '';
    } catch (e) {
      return '';
    }
  }

  function utm() {
    return {
      source: qs('utm_source') || qs('source') || '',
      medium: qs('utm_medium') || '',
      campaign: qs('utm_campaign') || '',
      term: qs('utm_term') || '',
      content: qs('utm_content') || ''
    };
  }

  function loadTimeMs() {
    try {
      var n = performance && performance.timing;
      if (n && n.navigationStart && n.loadEventEnd) {
        var t = n.loadEventEnd - n.navigationStart;
        if (t > 0 && t < 600000) return t;
      }
      var entries = performance.getEntriesByType && performance.getEntriesByType('navigation');
      if (entries && entries[0] && entries[0].loadEventEnd) {
        return Math.round(entries[0].loadEventEnd);
      }
    } catch (e) {}
    return 0;
  }

  function scrollPct() {
    var el = document.documentElement;
    var body = document.body;
    var scrollTop = window.pageYOffset || el.scrollTop || body.scrollTop || 0;
    var height = Math.max(el.scrollHeight, body.scrollHeight) - (window.innerHeight || 0);
    if (height <= 0) return 100;
    return Math.max(0, Math.min(100, Math.round((scrollTop / height) * 100)));
  }

  function cssPath(el) {
    if (!el || !el.tagName) return '';
    var parts = [];
    var cur = el;
    var depth = 0;
    while (cur && cur.nodeType === 1 && depth < 6) {
      var part = cur.tagName.toLowerCase();
      if (cur.id) {
        part += '#' + cur.id.replace(/\s+/g, '');
        parts.unshift(part);
        break;
      }
      if (cur.className && typeof cur.className === 'string') {
        var cls = cur.className.trim().split(/\s+/).slice(0, 2).join('.');
        if (cls) part += '.' + cls;
      }
      parts.unshift(part);
      cur = cur.parentElement;
      depth++;
    }
    return parts.join(' > ').slice(0, 500);
  }

  function textOf(el) {
    if (!el) return '';
    var t = (el.innerText || el.textContent || el.value || el.getAttribute('aria-label') || el.getAttribute('title') || '').trim();
    return t.replace(/\s+/g, ' ').slice(0, 200);
  }

  var state = {
    session_uid: sid(),
    visitor_uid: vid(),
    started: Date.now(),
    pageStarted: Date.now(),
    scrollMax: 0,
    clickCount: 0,
    pageviews: [],
    events: [],
    flushedPv: 0,
    flushedEv: 0,
    currentPath: location.pathname + location.search
  };

  function pushPageview(extra) {
    extra = extra || {};
    state.pageviews.push({
      path: location.pathname,
      query: (location.search || '').replace(/^\?/, '').slice(0, 1000),
      title: (document.title || '').slice(0, 250),
      referrer: document.referrer || '',
      ts: Math.floor(Date.now() / 1000),
      load_time_ms: loadTimeMs(),
      time_on_page_ms: 0,
      scroll_max_pct: state.scrollMax,
      viewport_w: window.innerWidth || 0,
      viewport_h: window.innerHeight || 0
    });
  }

  function updateLastPvTiming() {
    if (!state.pageviews.length) return;
    var last = state.pageviews[state.pageviews.length - 1];
    last.time_on_page_ms = Math.max(0, Date.now() - state.pageStarted);
    last.scroll_max_pct = state.scrollMax;
  }

  function pushEvent(ev) {
    if (!ev || !ev.type) return;
    ev.ts = ev.ts || Math.floor(Date.now() / 1000);
    ev.path = ev.path || location.pathname;
    state.events.push(ev);
    if (state.events.length > 120) {
      state.events = state.events.slice(-80);
    }
  }

  function payload() {
    updateLastPvTiming();
    var pv = state.pageviews.slice(state.flushedPv);
    var ev = state.events.slice(state.flushedEv);
    if (!pv.length && !ev.length) return null;
    return {
      site_key: CFG.site_key,
      hostname: CFG.hostname || location.hostname,
      session_uid: state.session_uid,
      visitor_uid: state.visitor_uid,
      user_id: CFG.user_id || 0,
      is_registered: !!CFG.is_registered,
      referrer: document.referrer || '',
      utm: utm(),
      screen_w: (window.screen && screen.width) || 0,
      screen_h: (window.screen && screen.height) || 0,
      language: (navigator.language || '').slice(0, 32),
      timezone: (function () {
        try { return Intl.DateTimeFormat().resolvedOptions().timeZone || ''; } catch (e) { return ''; }
      })(),
      duration_ms: Math.max(0, Date.now() - state.started),
      pageviews: pv,
      events: ev,
      v: CFG.v || '1'
    };
  }

  function flush(sync) {
    var body = payload();
    if (!body) return;
    var pvLen = body.pageviews.length;
    var evLen = body.events.length;
    state.flushedPv += pvLen;
    state.flushedEv += evLen;
    var data = JSON.stringify(body);
    var ok = false;
    try {
      if (navigator.sendBeacon) {
        ok = navigator.sendBeacon(CFG.endpoint, new Blob([data], { type: 'application/json' }));
      }
    } catch (e) {}
    if (!ok) {
      try {
        var xhr = new XMLHttpRequest();
        xhr.open('POST', CFG.endpoint, !sync);
        xhr.setRequestHeader('Content-Type', 'application/json');
        xhr.send(data);
      } catch (e2) {}
    }
  }

  // Initial pageview
  pushPageview();

  // URL search params as search events
  (function captureUrlSearch() {
    var keys = ['article', 'q', 'search', 'query', 'sku', 'oem', 'keyword', 's'];
    for (var i = 0; i < keys.length; i++) {
      var val = qs(keys[i]);
      if (val && val.length >= 2) {
        pushEvent({ type: 'search', search: val.slice(0, 500), search_ctx: 'url_' + keys[i] });
      }
    }
  })();

  // Scroll depth
  var scrollT = null;
  window.addEventListener('scroll', function () {
    state.scrollMax = Math.max(state.scrollMax, scrollPct());
    if (scrollT) return;
    scrollT = setTimeout(function () { scrollT = null; }, 400);
  }, { passive: true });

  // Clicks
  document.addEventListener('click', function (e) {
    if (state.clickCount >= MAX_CLICKS_PAGE) return;
    var t = e.target;
    if (!t) return;
    // Climb to interactive element
    var el = t;
    var hops = 0;
    while (el && hops < 4) {
      var tag = (el.tagName || '').toLowerCase();
      if (tag === 'a' || tag === 'button' || tag === 'input' || tag === 'select' || tag === 'textarea' || tag === 'label' ||
          (el.getAttribute && (el.getAttribute('role') === 'button' || el.onclick))) {
        break;
      }
      el = el.parentElement;
      hops++;
    }
    if (!el || !el.tagName) el = t;
    var tag = (el.tagName || '').toLowerCase();
    var href = '';
    try { href = el.href || el.getAttribute('href') || ''; } catch (err) {}
    state.clickCount++;
    pushEvent({
      type: 'click',
      x: Math.round(e.clientX || 0),
      y: Math.round(e.clientY || 0),
      page_x: Math.round(e.pageX || 0),
      page_y: Math.round(e.pageY || 0),
      tag: tag,
      id: (el.id || '').slice(0, 120),
      class: (typeof el.className === 'string' ? el.className : '').slice(0, 240),
      text: textOf(el),
      href: (href || '').slice(0, 1000),
      name: (el.getAttribute && (el.getAttribute('name') || el.getAttribute('data-name') || '') || '').slice(0, 120),
      css: cssPath(el)
    });
    if (href && /^https?:\/\//i.test(href)) {
      try {
        var u = new URL(href, location.href);
        if (u.hostname && u.hostname !== location.hostname) {
          pushEvent({ type: 'outbound', href: href.slice(0, 1000), text: textOf(el), tag: tag });
        }
      } catch (err2) {}
    }
  }, true);

  // Search form submits
  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!form || form.tagName !== 'FORM') return;
    var q = '';
    var inputs = form.querySelectorAll('input[type="search"], input[type="text"], input:not([type]), input[name*="article"], input[name*="search"], input[name*="q"], input[name*="sku"]');
    for (var i = 0; i < inputs.length; i++) {
      var v = (inputs[i].value || '').trim();
      if (v.length >= 1) { q = v; break; }
    }
    if (!q) return;
    var ctx = (form.id || form.getAttribute('name') || form.className || 'form').toString().slice(0, 60);
    pushEvent({ type: 'search', search: q.slice(0, 500), search_ctx: ctx, tag: 'form' });
  }, true);

  // Engagement pings
  setInterval(function () {
    state.scrollMax = Math.max(state.scrollMax, scrollPct());
    pushEvent({
      type: 'engagement',
      meta: { scroll: state.scrollMax, alive_ms: Date.now() - state.pageStarted }
    });
    flush(false);
  }, 30000);

  setInterval(function () { flush(false); }, FLUSH_MS);

  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'hidden') flush(true);
  });
  window.addEventListener('pagehide', function () { flush(true); });
  window.addEventListener('beforeunload', function () { flush(true); });

  // SPA-ish path changes (history API)
  var lastPath = location.pathname + location.search;
  setInterval(function () {
    var cur = location.pathname + location.search;
    if (cur !== lastPath) {
      updateLastPvTiming();
      flush(false);
      lastPath = cur;
      state.pageStarted = Date.now();
      state.scrollMax = 0;
      state.clickCount = 0;
      pushPageview();
    }
  }, 800);

  // First flush shortly after load
  setTimeout(function () { flush(false); }, 1200);
})();
