# Mobile apps — Android & iOS (all surfaces)

Every surface is now mobile-app deliverable via two layers:

| Surface | Installable PWA | Native store app (Capacitor) |
|---|---|---|
| ecomae admin CP (`www.ecomae.com/cp`) | ✅ | ✅ target `ecomae-cp` |
| Tenant CP (`www.epartscart.com/cp`) | ✅ | ✅ target `tenant-cp` |
| ERP (platform + client shells) | ✅ | ✅ target `erp` |
| Tenant storefront (`www.epartscart.com`) | ✅ (already live) | ✅ target `storefront` |

The web UIs are already responsive (viewport + media queries in CP/ERP/storefront
CSS), so both layers wrap the real product — no separate mobile UI to maintain.

---

## Layer 1 — Installable PWA (works immediately, no store account)

Users open the site on a phone → browser menu → **Add to Home Screen**. It then
launches full-screen like a native app and opens offline (cached shell).

What was added:
- **CP/ERP manifest** `cp/manifest.webmanifest` (standalone, scope `/cp/`, indigo theme, 192/512 icons)
- **CP/ERP service worker** `cp/sw.js` (network-first for fresh admin data, offline fallback `cp/offline.html`)
- **Icons** `cp/assets/app/icon-192.svg`, `icon-512.svg`
- **Static serving** `cp/epc_cp_pwa_assets.php` — serves the above early in `cp/index.php` (before the heavy bootstrap), with `Service-Worker-Allowed: /cp/`
- **Head wiring** `epc_pwa_head_tags()` injected into `cp/templates/bootstrap_admin/desktop.php` (CP) and `erp_desktop.php` (ERP)
- Storefront PWA was already live (`manifest.webmanifest`, `sw.js`, wired in `templates/nero/desktop.php`)

Verify after deploy:
```bash
curl -sI https://www.epartscart.com/cp/manifest.webmanifest   # 200, application/manifest+json
curl -sI https://www.epartscart.com/cp/sw.js                  # 200, Service-Worker-Allowed: /cp/
```
Then on a phone, load `/cp/`, log in, and use **Add to Home Screen**.

---

## Layer 2 — Native store apps (Google Play + Apple App Store) via Capacitor

A thin native shell wraps the live site (same session-cookie login), adding a
store listing, native push, and camera/barcode where needed. Project lives in
`mobile/ecomae-app/`.

### One-time per machine
Requires Node 18+, Android Studio (Android) and Xcode on macOS (iOS).

```bash
cd mobile/ecomae-app
npm install
```

### Build any of the four apps
```bash
# pick a target: ecomae-cp | tenant-cp | erp | storefront
npm run config:tenant-cp        # writes capacitor.config.json + www/index.html
npm run add:android             # first time only
npm run add:ios                 # first time only (macOS)
npm run open:android            # build & sign in Android Studio  → Play Store (.aab)
npm run open:ios                # build & sign in Xcode           → App Store
```

White-label a tenant without editing `targets.json`:
```bash
APP_ID=com.acme.cp APP_NAME="Acme CP" SERVER_URL="https://cp.acme.com/cp/" \
  KEEP_HOST=acme.com node build-config.mjs tenant-cp
```

Targets are defined in `mobile/ecomae-app/targets.json`:

| Target | appId | Opens |
|---|---|---|
| `ecomae-cp` | `com.ecomae.cp` | `www.ecomae.com/cp/` |
| `tenant-cp` | `com.epartscart.cp` | `www.epartscart.com/cp/` |
| `erp` | `com.ecomae.erp` | `www.ecomae.com/cp/shop/finance/erp` |
| `storefront` | `com.epartscart.store` | `www.epartscart.com/en/` |

`build-config.mjs` writes `capacitor.config.json` (server URL, status bar, push)
and `www/index.html` (splash + redirect that keeps in-app navigation on your host
and opens external links in the system browser).

### What only you can provide (store accounts)
- **Google Play**: Play Console developer account → upload the signed `.aab`.
- **Apple App Store**: Apple Developer Program account + signing certificates → Xcode Archive → App Store Connect.
- App store icons/screenshots and privacy-policy URL (storefront already has legal pages).

### Optional deep links / universal links
If you want `https://…/cp/orders` to open the app directly, add:
- Android: `/.well-known/assetlinks.json` (SHA-256 of the signing cert)
- iOS: `/.well-known/apple-app-site-association` (Team ID + bundle ID)

Not required for install or normal use — add when you want link-to-app handoff.

---

## Auth
Both layers reuse the existing web sessions — no new login stack needed:
- Storefront: `session` + `u_id` cookies (`DP_User`)
- CP / ERP: `admin_session` cookie (password / email OTP / Google all set it)

A future offline-native mode could use the documented mobile token contract in
`epc_mobile_api_routes()` + `pyapi/`, but the WebView wrapper works today with
cookies.
