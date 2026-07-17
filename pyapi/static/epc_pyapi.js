/**
 * pyapi client helpers — one file for both features.
 *
 * 1) Storefront search cutover (flag-gated):
 *      window.EpcPyapiSearch.lookup(article) → pyapi, PHP fallback on error.
 *      Enable per user: cookie epc_pyapi_search=1 or window.EPC_PYAPI_SEARCH=true.
 *
 * 2) Native push registration (Capacitor app only):
 *      Asks permission, gets the FCM/APNs token, registers it with the admin
 *      session cookie at /pyapi/v1/push/register. No-ops in a plain browser.
 *
 *   <script src="/pyapi/static/epc_pyapi.js" defer></script>
 */
(function () {
  'use strict';

  // ── 1. Search cutover ──
  function searchFlagOn() {
    if (window.EPC_PYAPI_SEARCH === true) return true;
    return document.cookie.split(';').some(function (c) {
      return c.trim() === 'epc_pyapi_search=1';
    });
  }

  window.EpcPyapiSearch = {
    enabled: searchFlagOn(),
    lookup: function (article, limit) {
      var url = '/pyapi/v1/search?article=' + encodeURIComponent(article || '') +
        '&limit=' + encodeURIComponent(limit || 100);
      return fetch(url, { credentials: 'same-origin' })
        .then(function (r) {
          if (!r.ok) throw new Error('pyapi ' + r.status);
          return r.json();
        })
        .then(function (data) { data.source = 'pyapi'; return data; })
        .catch(function () {
          return { status: false, rows: [], source: 'fallback' };
        });
    },
  };

  // ── 2. Push registration (Capacitor only) ──
  function registerToken(token, platform) {
    return fetch('/pyapi/v1/push/register', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: token, platform: platform, app: 'cp' }),
    }).catch(function () {});
  }

  function initPush() {
    if (!window.Capacitor || !Capacitor.Plugins || !Capacitor.Plugins.PushNotifications) {
      return;
    }
    var Push = Capacitor.Plugins.PushNotifications;
    var platform = (Capacitor.getPlatform && Capacitor.getPlatform()) || 'android';

    Push.addListener('registration', function (t) {
      if (t && t.value) registerToken(t.value, platform);
    });
    Push.addListener('registrationError', function () {});
    Push.requestPermissions().then(function (res) {
      if (res && res.receive === 'granted') Push.register();
    }).catch(function () {});
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initPush, { once: true });
  } else {
    initPush();
  }
})();
