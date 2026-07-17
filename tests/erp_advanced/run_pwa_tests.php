<?php
/**
 * CLI tests for the mobile / PWA layer (epc_pwa). No DB required.
 *
 *   php tests/erp_advanced/run_pwa_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_pwa.php';

$pass_count = 0;
$fail_count = 0;
function check(string $label, bool $cond): void
{
    global $pass_count, $fail_count;
    if ($cond) {
        $pass_count++;
        echo "  PASS  $label\n";
    } else {
        $fail_count++;
        echo "  FAIL  $label\n";
    }
}
function section(string $t): void
{
    echo "\n== $t ==\n";
}

section('Web app manifest');
$mJson = epc_pwa_manifest(array('name' => 'Al Noor ERP', 'theme_color' => '#101935', 'lang' => 'ar', 'dir' => 'rtl'));
$m = json_decode($mJson, true);
check('valid JSON manifest', is_array($m));
check('name carried', $m['name'] === 'Al Noor ERP');
check('display standalone (installable)', $m['display'] === 'standalone');
check('theme color carried', $m['theme_color'] === '#101935');
check('rtl/lang carried for Arabic tenant', $m['lang'] === 'ar' && $m['dir'] === 'rtl');
check('has 192 + 512 icons', count($m['icons']) === 2);
check('icons maskable', strpos($m['icons'][0]['purpose'], 'maskable') !== false);
check('start_url default', $m['start_url'] === '/cp/');
$mDef = json_decode(epc_pwa_manifest(array()), true);
check('defaults applied when empty', $mDef['name'] === 'ecomae ERP' && $mDef['display'] === 'standalone');

section('Service worker (offline)');
$sw = epc_pwa_service_worker(array('/cp/', '/cp/offline.html', '/cp/assets/erp/app.css'));
check('install caches the shell', strpos($sw, "caches.open(CACHE)") !== false && strpos($sw, 'addAll(SHELL)') !== false);
check('activate cleans old caches', strpos($sw, 'caches.delete') !== false);
check('fetch handler present', strpos($sw, "addEventListener('fetch'") !== false);
check('offline fallback to offline.html', strpos($sw, '/cp/offline.html') !== false);
check('only GET cached', strpos($sw, "req.method !== 'GET'") !== false);
check('shell list embedded', strpos($sw, 'app.css') !== false);

section('Head tags + SW registration');
$head = epc_pwa_head_tags('/cp/manifest.webmanifest', '/cp/sw.js', '#0b1020');
check('links manifest', strpos($head, 'rel="manifest"') !== false);
check('theme-color meta', strpos($head, 'name="theme-color"') !== false);
check('apple mobile capable', strpos($head, 'apple-mobile-web-app-capable') !== false);
check('viewport with viewport-fit', strpos($head, 'viewport-fit=cover') !== false);
check('registers service worker', strpos($head, "serviceWorker") !== false && strpos($head, "register('/cp/sw.js')") !== false);

section('Mobile REST API contract');
$routes = epc_mobile_api_routes();
check('has multiple routes', count($routes) >= 6);
check('every route has method/path/scope', (function () use ($routes) {
    foreach ($routes as $r) {
        if (empty($r['method']) || empty($r['path']) || !isset($r['scope'])) {
            return false;
        }
    }
    return true;
})());
$paths = array_map(static function ($r) {
    return $r['path'];
}, $routes);
check('login route exists', in_array('/api/v1/auth/login', $paths, true));
check('dashboard route exists', in_array('/api/v1/dashboard', $paths, true));
check('barcode stock-count route (native camera)', in_array('/api/v1/stock/count', $paths, true));
check('login is public scope', (function () use ($routes) {
    foreach ($routes as $r) {
        if ($r['path'] === '/api/v1/auth/login') {
            return $r['scope'] === 'public';
        }
    }
    return false;
})());

section('Capacitor native wrapper config');
$cap = json_decode(epc_capacitor_config(array('app_id' => 'com.alnoor.erp', 'app_name' => 'Al Noor', 'server_url' => 'https://alnoor.example/cp/')), true);
check('valid JSON config', is_array($cap));
check('appId carried', $cap['appId'] === 'com.alnoor.erp');
check('server url carried', $cap['server']['url'] === 'https://alnoor.example/cp/');
check('push notifications plugin', isset($cap['plugins']['PushNotifications']));
check('barcode scanner plugin', isset($cap['plugins']['BarcodeScanner']));
check('default appId when empty', json_decode(epc_capacitor_config(array()), true)['appId'] === 'com.ecomae.erp');

section('CP PWA served assets (installable CP + ERP)');
$root = dirname(__DIR__, 2);
check('cp manifest file exists', is_file($root . '/cp/manifest.webmanifest'));
$cpM = json_decode((string) file_get_contents($root . '/cp/manifest.webmanifest'), true);
check('cp manifest valid JSON', is_array($cpM));
check('cp manifest standalone', ($cpM['display'] ?? '') === 'standalone');
check('cp manifest scope /cp/', ($cpM['scope'] ?? '') === '/cp/');
check('cp manifest has 2 icons', isset($cpM['icons']) && count($cpM['icons']) === 2);
check('cp service worker file exists', is_file($root . '/cp/sw.js'));
check('cp sw scoped to /cp/', strpos((string) file_get_contents($root . '/cp/sw.js'), "/cp/") !== false);
check('cp offline shell exists', is_file($root . '/cp/offline.html'));
check('cp app icon 192 exists', is_file($root . '/cp/assets/app/icon-192.svg'));
check('cp app icon 512 exists', is_file($root . '/cp/assets/app/icon-512.svg'));

$assetServer = (string) file_get_contents($root . '/cp/epc_cp_pwa_assets.php');
check('asset server maps manifest', strpos($assetServer, "/cp/manifest.webmanifest") !== false);
check('asset server maps sw.js', strpos($assetServer, "/cp/sw.js") !== false);
check('asset server sets Service-Worker-Allowed', strpos($assetServer, 'Service-Worker-Allowed') !== false);

$cpIndex = (string) file_get_contents($root . '/cp/index.php');
check('cp index serves PWA assets early', strpos($cpIndex, 'epc_cp_pwa_maybe_serve_asset()') !== false);

$cpDesktop = (string) file_get_contents($root . '/cp/templates/bootstrap_admin/desktop.php');
check('CP desktop wires PWA head tags', strpos($cpDesktop, 'epc_pwa_head_tags(') !== false);
$erpDesktop = (string) file_get_contents($root . '/cp/templates/bootstrap_admin/erp_desktop.php');
check('ERP desktop wires PWA head tags', strpos($erpDesktop, 'epc_pwa_head_tags(') !== false);

section('Capacitor native project (Android + iOS)');
check('native project package.json exists', is_file($root . '/mobile/ecomae-app/package.json'));
check('native targets.json exists', is_file($root . '/mobile/ecomae-app/targets.json'));
$targets = json_decode((string) file_get_contents($root . '/mobile/ecomae-app/targets.json'), true);
check('targets valid JSON', is_array($targets));
check('has ecomae CP target', isset($targets['ecomae-cp']['appId']));
check('has tenant CP target', isset($targets['tenant-cp']['appId']));
check('has ERP target', isset($targets['erp']['appId']));
check('has storefront target', isset($targets['storefront']['appId']));
check('config generator exists', is_file($root . '/mobile/ecomae-app/build-config.mjs'));
$pkg = json_decode((string) file_get_contents($root . '/mobile/ecomae-app/package.json'), true);
check('capacitor android dep', isset($pkg['dependencies']['@capacitor/android']));
check('capacitor ios dep', isset($pkg['dependencies']['@capacitor/ios']));

echo "\n========================================\n";
echo "PWA/MOBILE TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
