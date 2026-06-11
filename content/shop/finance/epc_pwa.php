<?php
/**
 * Mobile layer: PWA (Progressive Web App) + mobile REST API contract.
 *
 * Makes the storefront / tenant CP / ERP installable on Android & iOS:
 *   - a per-tenant web app manifest (icon, name, theme, standalone display)
 *   - a service worker (offline app-shell cache + network-first for data)
 *   - the <head> tags + registration script to wire it in
 *   - a documented mobile REST API route map (served by the existing inbound
 *     API layer with per-tenant keys + scopes), which the optional native
 *     Android/iOS wrapper (Capacitor, see mobile/) calls.
 *
 * Additive: nothing is auto-published to app stores; the PWA is opt-in per
 * tenant. Pure functions (no DB) so they are unit-testable.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_pwa_manifest')) {
    /**
     * Build a web app manifest for a tenant.
     *
     * @param array<string,mixed> $tenant {name, short_name, theme_color,
     *                                      background_color, start_url, icon}
     * @return string JSON manifest
     */
    function epc_pwa_manifest(array $tenant): string
    {
        $name = (string) ($tenant['name'] ?? 'ecomae ERP');
        $short = (string) ($tenant['short_name'] ?? (function_exists('mb_substr') ? mb_substr($name, 0, 12) : substr($name, 0, 12)));
        $theme = (string) ($tenant['theme_color'] ?? '#0b1020');
        $bg = (string) ($tenant['background_color'] ?? '#0b1020');
        $start = (string) ($tenant['start_url'] ?? '/cp/');
        $icon = (string) ($tenant['icon'] ?? '/cp/assets/erp/icon-512.png');
        $icon192 = (string) ($tenant['icon192'] ?? '/cp/assets/erp/icon-192.png');

        $manifest = array(
            'name' => $name,
            'short_name' => $short,
            'description' => $name . ' — ERP, e-commerce & accounting',
            'start_url' => $start,
            'scope' => '/',
            'display' => 'standalone',
            'orientation' => 'any',
            'theme_color' => $theme,
            'background_color' => $bg,
            'lang' => (string) ($tenant['lang'] ?? 'en'),
            'dir' => (string) ($tenant['dir'] ?? 'ltr'),
            'icons' => array(
                array('src' => $icon192, 'sizes' => '192x192', 'type' => 'image/png', 'purpose' => 'any maskable'),
                array('src' => $icon, 'sizes' => '512x512', 'type' => 'image/png', 'purpose' => 'any maskable'),
            ),
        );
        return json_encode($manifest, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    }
}

if (!function_exists('epc_pwa_service_worker')) {
    /**
     * Service worker source. App-shell is cache-first; everything else is
     * network-first with an offline fallback, so the app opens offline and
     * shows fresh data when online.
     *
     * @param array<int,string> $shell URLs to precache
     */
    function epc_pwa_service_worker(array $shell = array()): string
    {
        $shell = $shell ?: array('/cp/', '/cp/offline.html');
        $version = 'epc-pwa-v1';
        $list = json_encode(array_values($shell), JSON_UNESCAPED_SLASHES);
        return <<<JS
const CACHE = '{$version}';
const SHELL = {$list};
self.addEventListener('install', (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', (e) => {
  e.waitUntil(caches.keys().then((keys) => Promise.all(
    keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))
  )).then(() => self.clients.claim()));
});
self.addEventListener('fetch', (e) => {
  const req = e.request;
  if (req.method !== 'GET') return;
  e.respondWith(
    fetch(req).then((res) => {
      const copy = res.clone();
      caches.open(CACHE).then((c) => c.put(req, copy));
      return res;
    }).catch(() => caches.match(req).then((m) => m || caches.match('/cp/offline.html')))
  );
});
JS;
    }
}

if (!function_exists('epc_pwa_head_tags')) {
    /**
     * <head> tags + service-worker registration for a page.
     */
    function epc_pwa_head_tags(string $manifestUrl = '/cp/manifest.webmanifest', string $swUrl = '/cp/sw.js', string $themeColor = '#0b1020'): string
    {
        $m = htmlspecialchars($manifestUrl, ENT_QUOTES);
        $s = htmlspecialchars($swUrl, ENT_QUOTES);
        $t = htmlspecialchars($themeColor, ENT_QUOTES);
        return "<link rel=\"manifest\" href=\"$m\">\n"
            . "<meta name=\"theme-color\" content=\"$t\">\n"
            . "<meta name=\"mobile-web-app-capable\" content=\"yes\">\n"
            . "<meta name=\"apple-mobile-web-app-capable\" content=\"yes\">\n"
            . "<meta name=\"apple-mobile-web-app-status-bar-style\" content=\"black-translucent\">\n"
            . "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, viewport-fit=cover\">\n"
            . "<script>if('serviceWorker' in navigator){navigator.serviceWorker.register('$s').catch(function(){});}</script>";
    }
}

if (!function_exists('epc_mobile_api_routes')) {
    /**
     * The mobile REST API contract. These routes are served by the existing
     * inbound API layer (per-tenant key + scope); the native wrapper / PWA
     * calls them. Returned as a documented map for the app + the guide.
     *
     * @return array<int,array{method:string,path:string,scope:string,desc:string}>
     */
    function epc_mobile_api_routes(): array
    {
        return array(
            array('method' => 'POST', 'path' => '/api/v1/auth/login', 'scope' => 'public', 'desc' => 'Exchange credentials for a scoped mobile token'),
            array('method' => 'GET', 'path' => '/api/v1/dashboard', 'scope' => 'dashboard.read', 'desc' => 'KPI tiles + chart series for the home dashboard'),
            array('method' => 'GET', 'path' => '/api/v1/orders', 'scope' => 'sales.read', 'desc' => 'List orders (paginated)'),
            array('method' => 'POST', 'path' => '/api/v1/orders', 'scope' => 'sales.write', 'desc' => 'Create an order / quote'),
            array('method' => 'GET', 'path' => '/api/v1/products', 'scope' => 'inventory.read', 'desc' => 'Catalogue + live stock'),
            array('method' => 'POST', 'path' => '/api/v1/stock/count', 'scope' => 'inventory.write', 'desc' => 'Submit a stock count (barcode scan)'),
            array('method' => 'GET', 'path' => '/api/v1/customers', 'scope' => 'crm.read', 'desc' => 'Customer list + AR balance'),
            array('method' => 'GET', 'path' => '/api/v1/invoices/{id}/pdf', 'scope' => 'finance.read', 'desc' => 'Download an invoice/e-invoice PDF'),
        );
    }
}

if (!function_exists('epc_capacitor_config')) {
    /**
     * Capacitor config for the native Android/iOS wrapper around the PWA.
     *
     * @param array<string,mixed> $tenant {app_id, app_name, server_url}
     */
    function epc_capacitor_config(array $tenant): string
    {
        $cfg = array(
            'appId' => (string) ($tenant['app_id'] ?? 'com.ecomae.erp'),
            'appName' => (string) ($tenant['app_name'] ?? 'ecomae ERP'),
            'webDir' => 'www',
            'bundledWebRuntime' => false,
            'server' => array(
                'url' => (string) ($tenant['server_url'] ?? 'https://www.ecomae.com/cp/'),
                'cleartext' => false,
            ),
            'plugins' => array(
                'PushNotifications' => array('presentationOptions' => array('badge', 'sound', 'alert')),
                'BarcodeScanner' => array(),
            ),
        );
        return json_encode($cfg, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
    }
}
