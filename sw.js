/**
 * eParts Cart PWA — network-first with offline fallback (Phase 1).
 */
const CACHE_NAME = 'epc-pwa-shell-v1';
const OFFLINE_HTML =
  '<!DOCTYPE html><html lang="en"><head><meta charset="utf-8"/>' +
  '<meta name="viewport" content="width=device-width,initial-scale=1"/>' +
  '<title>eParts Cart — offline</title>' +
  '<style>body{font-family:system-ui,sans-serif;margin:2rem;color:#111827}' +
  'h1{color:#dc2626;font-size:1.25rem}</style></head><body>' +
  '<h1>eParts Cart</h1><p>You are offline. Reconnect to browse parts and place orders.</p>' +
  '</body></html>';

self.addEventListener('install', function (event) {
  self.skipWaiting();
});

self.addEventListener('activate', function (event) {
  event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', function (event) {
  if (event.request.method !== 'GET') {
    return;
  }
  var url = new URL(event.request.url);
  if (url.origin !== self.location.origin) {
    return;
  }
  event.respondWith(
    fetch(event.request).catch(function () {
      return caches.match(event.request).then(function (cached) {
        if (cached) {
          return cached;
        }
        if (event.request.mode === 'navigate' || (event.request.headers.get('accept') || '').indexOf('text/html') !== -1) {
          return new Response(OFFLINE_HTML, {
            status: 503,
            headers: { 'Content-Type': 'text/html; charset=utf-8' },
          });
        }
        return new Response('', { status: 503 });
      });
    })
  );
});
