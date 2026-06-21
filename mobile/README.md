# ecomae Mobile (Android & iOS)

Two layers, both additive and opt-in. **Nothing is auto-published to app stores.**

## 1. PWA (works today on Android & iOS)

The storefront, tenant CP and ERP are installable as a Progressive Web App:

- **Manifest** — generated per tenant by `epc_pwa_manifest()` (name, icon, theme,
  `display: standalone`, language/direction). Serve at `/cp/manifest.webmanifest`.
- **Service worker** — `epc_pwa_service_worker()` (app-shell cache-first,
  data network-first, offline fallback to `mobile/offline.html`). Serve at `/cp/sw.js`.
- **Head wiring** — `epc_pwa_head_tags()` adds the `<link rel="manifest">`,
  theme-color + Apple meta tags, and registers the service worker.

Install: open the site on a phone → browser menu → **Add to Home Screen**.
It then launches full-screen like an app and opens offline (cached views).

## 2. Native wrapper (Play Store / App Store) via Capacitor

A thin native shell around the PWA for store listings, native push and
camera/barcode scan (POS + stock counts).

```bash
npm init -y
npm install @capacitor/core @capacitor/cli @capacitor/android @capacitor/ios
npx cap init        # use the values from epc_capacitor_config()
mkdir -p www && echo "<script>location.href='https://www.ecomae.com/cp/'</script>" > www/index.html
npx cap add android
npx cap add ios
npx cap open android   # build/sign in Android Studio  -> Play Store
npx cap open ios       # build/sign in Xcode           -> App Store
```

`capacitor.config.json` is produced by `epc_capacitor_config($tenant)` (appId,
appName, server URL, push + barcode plugins).

**Store submission needs your accounts/credentials:**
- Google Play: a Google Play Console developer account.
- Apple App Store: an Apple Developer Program account + signing certs.

Provide those when ready and the build can be signed and submitted.

## 3. Mobile REST API

The app talks to the existing inbound API layer (per-tenant key + scope). The
route contract is `epc_mobile_api_routes()` (login, dashboard, orders, products,
stock count, customers, invoice PDF).
