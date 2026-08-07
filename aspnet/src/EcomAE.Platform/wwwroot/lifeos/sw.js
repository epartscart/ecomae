/**
 * LifeOS™ companion PWA — network-first shell for /lifeos/mobile.
 * Scaffold: caches the companion shell for offline reopen; APIs stay online.
 */
const CACHE = 'lifeos-companion-shell-v1';
const PRECACHE = [
  '/lifeos/mobile?clientId=test-amina&source=pwa',
  '/lifeos/manifest.webmanifest',
  '/lifeos/icons/lifeos-pwa-192.svg',
  '/lifeos/icons/lifeos-pwa-512.svg',
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE).then((cache) => cache.addAll(PRECACHE)).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') return;
  const url = new URL(event.request.url);
  if (url.origin !== self.location.origin) return;
  if (!url.pathname.startsWith('/lifeos/')) return;

  // Never cache join/companion JSON or POST-like API GETs with query mutators beyond shell.
  const isApi =
    url.pathname === '/lifeos/directory' ||
    url.pathname === '/lifeos/companion' ||
    url.pathname.indexOf('/lifeos/companion/') === 0 ||
    url.pathname === '/lifeos/join' && url.search.includes('json');

  if (isApi) {
    event.respondWith(fetch(event.request));
    return;
  }

  event.respondWith(
    fetch(event.request)
      .then((res) => {
        const copy = res.clone();
        if (res.ok && (url.pathname.startsWith('/lifeos/mobile') || url.pathname.startsWith('/lifeos/icons') || url.pathname.endsWith('.webmanifest'))) {
          caches.open(CACHE).then((c) => c.put(event.request, copy));
        }
        return res;
      })
      .catch(() =>
        caches.match(event.request).then((cached) => {
          if (cached) return cached;
          return caches.match('/lifeos/mobile?clientId=test-amina&source=pwa');
        })
      )
  );
});
