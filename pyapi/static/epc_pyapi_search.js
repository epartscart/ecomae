/**
 * Optional storefront cutover shim (Phase 1 of the migration).
 *
 * Loads ONLY when the feature flag is on (cookie epc_pyapi_search=1 or a global
 * window.EPC_PYAPI_SEARCH === true). Sends warehouse part lookups to the Python
 * service and falls back to the existing PHP flow on any error, so it is safe to
 * enable per-user and roll back instantly.
 *
 * Wire-up (not enabled by default):
 *   <script src="/pyapi/static/epc_pyapi_search.js" defer></script>
 * then call EpcPyapiSearch.lookup(article).then(render) from the search UI.
 */
(function () {
  'use strict';

  function flagOn() {
    if (window.EPC_PYAPI_SEARCH === true) return true;
    return document.cookie.split(';').some(function (c) {
      return c.trim() === 'epc_pyapi_search=1';
    });
  }

  var EpcPyapiSearch = {
    enabled: flagOn(),

    /**
     * @param {string} article
     * @param {number} [limit]
     * @returns {Promise<{status:boolean, rows:Array, source:string}>}
     */
    lookup: function (article, limit) {
      var url = '/pyapi/v1/search?article=' + encodeURIComponent(article || '') +
        '&limit=' + encodeURIComponent(limit || 100);
      return fetch(url, { credentials: 'same-origin' })
        .then(function (r) {
          if (!r.ok) throw new Error('pyapi ' + r.status);
          return r.json();
        })
        .then(function (data) {
          data.source = 'pyapi';
          return data;
        })
        .catch(function () {
          // Fallback signal — caller keeps its existing PHP AJAX path.
          return { status: false, rows: [], source: 'fallback' };
        });
    },
  };

  window.EpcPyapiSearch = EpcPyapiSearch;
})();
