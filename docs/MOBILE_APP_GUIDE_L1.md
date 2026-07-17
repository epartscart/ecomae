# Mobile App — Complete Level 1 Guide (ecomae / CP)

Step-by-step guide to build, install and test the ecomae mobile apps for **all
areas**. This is **Level 1**: the app wraps the live responsive web product and
adds installability, offline shell, and push notifications. (Level 2 — deep
native screens / offline data — comes later.)

Store submission (Play Store / App Store) is **out of scope here** — we do that
together afterwards. This guide takes you all the way to a working app on a real
device.

---

## 0. The four apps (areas)

| # | Area | Capacitor target | Opens | Suggested bundle id |
|---|------|------------------|-------|---------------------|
| 1 | **ecomae admin CP** | `ecomae-cp` | `www.ecomae.com/cp/` | `com.ecomae.cp` |
| 2 | **Tenant CP** (eParts Cart) | `tenant-cp` | `www.epartscart.com/cp/` | `com.epartscart.cp` |
| 3 | **ERP** | `erp` | `www.ecomae.com/cp/shop/finance/erp` | `com.ecomae.erp` |
| 4 | **Tenant storefront** | `storefront` | `www.epartscart.com/en/` | `com.epartscart.store` |

Two delivery layers, both already in the repo:
- **Layer A — PWA:** installable from the phone browser, zero tooling. Best for an instant check.
- **Layer B — Native (Capacitor):** real `.apk`/`.aab` (Android) and `.ipa` (iOS) for device testing and, later, the stores. Project: `mobile/ecomae-app/`.

Login is the **existing web session** (admin cookie for CP/ERP, customer cookie for storefront) — no separate app login to build.

---

## 1. Prerequisites

### For Layer A (PWA) — nothing
Just a phone with Chrome (Android) or Safari (iOS) and the site deployed over HTTPS.

### For Layer B (Native)
| Tool | Android | iOS |
|------|---------|-----|
| Node.js 18+ | ✅ | ✅ |
| Android Studio (+ SDK) | ✅ | — |
| macOS + Xcode 15+ | — | ✅ (iOS builds need a Mac) |
| A physical phone or emulator/simulator | ✅ | ✅ |

Check Node:
```bash
node --version   # must be >= 18
```

---

## 2. Level 1 — Layer A: install the PWA (all four areas)

Do this per area by opening its URL. Works today once the site is deployed.

### Android (Chrome)
1. Open the area URL (e.g. `https://www.epartscart.com/cp/`).
2. Log in.
3. Tap **⋮ menu → Install app** (or **Add to Home screen**).
4. Confirm. An icon appears on the home screen; it launches full-screen.

### iOS (Safari)
1. Open the area URL in **Safari** (not Chrome — iOS only installs PWAs from Safari).
2. Log in.
3. Tap the **Share** button → **Add to Home Screen** → **Add**.
4. The icon appears; it launches full-screen like an app.

### What proves it works
- Launches with no browser address bar (standalone).
- App icon is the ecomae/eParts icon, not a screenshot.
- With network off, opening it shows the **offline screen** (not a browser error).

> The CP/ERP PWA is served by `cp/manifest.webmanifest`, `cp/sw.js`,
> `cp/offline.html` (via `cp/epc_cp_pwa_assets.php`). The storefront PWA is at
> the site root. Verify after deploy:
> ```bash
> curl -sI https://www.epartscart.com/cp/manifest.webmanifest   # 200 application/manifest+json
> curl -sI https://www.epartscart.com/cp/sw.js                  # 200, Service-Worker-Allowed: /cp/
> ```

---

## 3. Level 1 — Layer B: build the native app

All commands run inside the project folder:
```bash
cd mobile/ecomae-app
npm install            # one time; pulls Capacitor + plugins
```

### 3.1 Pick the area and generate its config
```bash
# choose ONE: ecomae-cp | tenant-cp | erp | storefront
npm run config:tenant-cp
```
This writes `capacitor.config.json` (appId, server URL, status bar, push) and
`www/index.html` (splash + redirect into the live site; external links open in
the system browser). Re-run with a different target to switch apps.

White-label a specific tenant without editing files:
```bash
APP_ID=com.acme.cp APP_NAME="Acme CP" SERVER_URL="https://cp.acme.com/cp/" \
  KEEP_HOST=acme.com node build-config.mjs tenant-cp
```

### 3.2 Android — build & run on a device (no account needed)
```bash
npm run add:android         # first time only — creates android/
npm run open:android        # opens Android Studio
```
In Android Studio:
1. Enable **Developer options → USB debugging** on the phone, plug it in (accept the prompt).
2. Pick your device in the toolbar → press **Run ▶**. The app installs and opens.
3. To share a test build: **Build → Build Bundle(s)/APK(s) → Build APK(s)**, then send the `.apk` to sideload.

### 3.3 iOS — build & run on a device (Mac required)
```bash
npm run add:ios             # first time only — creates ios/
npm run open:ios            # opens Xcode
```
In Xcode:
1. Select the project → **Signing & Capabilities** → pick your Apple ID team (a **free** Apple ID works for on-device testing; the build expires after 7 days).
2. Connect your iPhone, select it as the run target → press **Run ▶**.
3. On the phone: **Settings → General → VPN & Device Management** → trust your developer certificate (first run only).

### 3.4 After any web/config change
```bash
npm run sync                # copy web + config into android/ and ios/
```
Because the app loads the live site, most content changes need **no rebuild** —
just reopen the app. Rebuild only when you change `capacitor.config.json`,
plugins, or icons.

---

## 4. Level 1 — Push notifications (order + low-stock alerts)

> **Requires the pyapi push PR merged + deployed** (files `pyapi/push.py`,
> `pyapi/worker.py`, `pyapi/ops/push_setup.py`, `pyapi/static/epc_push_register.js`).
> The PWA and native builds in §2–§3 work without this; push is an add-on step.

Backend is the pyapi service (`pyapi/push.py`, endpoints under `/pyapi/v1/push/*`,
dispatcher `pyapi/worker.py`). The app registers its device token automatically.

### 4.1 Wire the app to register tokens
Ensure the CP shell loads the registration script (already provided):
```html
<script src="/pyapi/static/epc_push_register.js" defer></script>
```
On a real device it asks permission, gets the FCM/APNs token, and registers it
with your admin session. No-ops harmlessly in a browser.

### 4.2 Turn on sending (server, one time)
Requires a Firebase project (FCM). iOS also needs an APNs key uploaded to Firebase.
```bash
export PYAPI_FCM_PROJECT=your-firebase-project-id
export PYAPI_FCM_ACCESS_TOKEN="$(gcloud auth application-default print-access-token)"
python -m pyapi.ops.push_setup     # creates epc_push_devices
python -m pyapi.worker             # starts the order/low-stock dispatcher
```
Until these are set, token registration still works and sending is a safe no-op.

### 4.3 Verify
```bash
# send a test push to all registered devices (needs tech_key or admin session)
curl -X POST "https://www.epartscart.com/pyapi/v1/push/test?key=TECH_KEY" \
  -H 'Content-Type: application/json' -d '{"title":"Hello","body":"pyapi push works"}'
```
Then place a test order — the worker should push a **New order** alert within its
poll interval (default 30s).

---

## 5. Level 1 test checklist (per area)

Run through this for each of the four apps you build:

- [ ] App launches full-screen (no browser chrome).
- [ ] Correct icon + name on the home screen / launcher.
- [ ] Login works and persists across app restarts (session cookie retained).
- [ ] Core screens load: CP dashboard / ERP dashboard / storefront home / part search.
- [ ] External links (e.g. supplier sites) open in the system browser, not inside the app.
- [ ] Offline: opening with no network shows the offline screen, not a crash.
- [ ] (If push enabled) test push arrives; new-order alert arrives after a test order.
- [ ] Back button (Android) navigates within the app, doesn't kill it unexpectedly.

Capture a screen recording of each for the review pass.

---

## 6. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| PWA install option missing (Android) | Site must be HTTPS + manifest + SW reachable. Check the two `curl -sI` commands in §2. |
| iOS won't "Add to Home Screen" | Must use **Safari**; Chrome/Firefox on iOS can't install PWAs. |
| Native app shows white screen | `SERVER_URL` unreachable or not HTTPS. Re-run `npm run config:<target>` and confirm the URL loads in a browser. |
| iOS build won't sign | Set your team under Signing & Capabilities; free Apple ID = 7-day builds. |
| Push never arrives | `PYAPI_FCM_PROJECT` / access token not set, or worker not running. `/pyapi/v1/push/test` returns `reason: not_configured` when unset. |
| Logged out immediately in app | The WebView must keep cookies; confirm the app opens the real HTTPS host (not `capacitor://`). Our config uses `server.url`, which keeps cookies. |

---

## 7. Files this guide uses (already in the repo)

- `mobile/ecomae-app/` — Capacitor project (targets.json, build-config.mjs, package.json)
- `cp/manifest.webmanifest`, `cp/sw.js`, `cp/offline.html`, `cp/assets/app/icon-*.svg` — CP/ERP PWA
- `manifest.webmanifest`, `sw.js`, `icons/` — storefront PWA
- `pyapi/push.py`, `pyapi/worker.py`, `pyapi/ops/push_setup.py`, `pyapi/static/epc_push_register.js` — push
- `docs/MOBILE_APPS.md` — condensed reference

---

## 8. What we do together later (store side — not now)

- Google Play Console account → upload signed `.aab` → internal testing → production.
- Apple Developer Program → TestFlight → App Store review.
- Store icons, screenshots, privacy-policy URLs, app descriptions per app.
- Optional deep links (`assetlinks.json` / `apple-app-site-association`).

When you're ready, we take the working device builds from §3 and push them to the
stores — nothing above needs to change.
