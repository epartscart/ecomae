/**
 * ecomae Control Panel PWA service worker (scope: /cp/).
 * Network-first for everything (admin data must be fresh); offline HTML
 * fallback for navigations so the installed app opens without connectivity.
 */
const CACHE_NAME = 'ecomae-cp-shell-v1';
const OFFLINE_URL = '/cp/offline.html';

self.addEventListener('install', function (event) {
  event.waitUntil(
    caches.open(CACHE_NAME).then(function (cache) {
      return cache.addAll([OFFLINE_URL]).catch(function () {});
    }).then(function () {
      return self.skipWaiting();
    })
  );
});

self.addEventListener('activate', function (event) {
  event.waitUntil(
    caches.keys().then(function (keys) {
      return Promise.all(
        keys.filter(function (k) { return k !== CACHE_NAME; })
            .map(function (k) { return caches.delete(k); })
      );
    }).then(function () { return self.clients.claim(); })
  );
});

self.addEventListener('fetch', function (event) {
  var req = event.request;
  if (req.method !== 'GET') {
    return;
  }
  var url = new URL(req.url);
  if (url.origin !== self.location.origin) {
    return;
  }
  // Only manage the /cp/ scope.
  if (url.pathname.indexOf('/cp/') !== 0) {
    return;
  }
  event.respondWith(
    fetch(req).catch(function () {
      return caches.match(req).then(function (cached) {
        if (cached) {
          return cached;
        }
        if (req.mode === 'navigate' || (req.headers.get('accept') || '').indexOf('text/html') !== -1) {
          return caches.match(OFFLINE_URL);
        }
        return new Response('', { status: 503 });
      });
    })
  );
});
