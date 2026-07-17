#!/usr/bin/env node
/**
 * Generate capacitor.config.json + www/index.html for a chosen target.
 *
 *   node build-config.mjs <target>
 *   target ∈ ecomae-cp | tenant-cp | erp | storefront   (see targets.json)
 *
 * Env overrides (for white-label tenant builds):
 *   APP_ID, APP_NAME, SERVER_URL, THEME_COLOR, BACKGROUND_COLOR, KEEP_HOST
 */
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const targetName = process.argv[2] || 'tenant-cp';
const targets = JSON.parse(readFileSync(join(here, 'targets.json'), 'utf8'));

if (!targets[targetName]) {
  console.error(`Unknown target "${targetName}". Options: ${Object.keys(targets).join(', ')}`);
  process.exit(1);
}

const t = targets[targetName];
const cfg = {
  appId: process.env.APP_ID || t.appId,
  appName: process.env.APP_NAME || t.appName,
  webDir: 'www',
  server: {
    url: process.env.SERVER_URL || t.serverUrl,
    cleartext: false,
    androidScheme: 'https',
  },
  backgroundColor: process.env.BACKGROUND_COLOR || t.backgroundColor,
  plugins: {
    StatusBar: { style: 'DARK', backgroundColor: process.env.THEME_COLOR || t.themeColor },
    PushNotifications: { presentationOptions: ['badge', 'sound', 'alert'] },
  },
};

writeFileSync(join(here, 'capacitor.config.json'), JSON.stringify(cfg, null, 2) + '\n');

const keepHost = process.env.KEEP_HOST || t.keepHost;
const serverUrl = cfg.server.url;
const themeColor = process.env.THEME_COLOR || t.themeColor;

// Splash + immediate redirect into the live site. External links open in the
// system browser (via @capacitor/browser) so the app WebView stays on our host.
const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
<title>${cfg.appName}</title>
<style>
  html,body{margin:0;height:100%;background:${cfg.backgroundColor};color:#e5e7eb;
    font-family:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
  .wrap{height:100%;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:18px}
  .spin{width:44px;height:44px;border-radius:50%;border:4px solid rgba(255,255,255,.15);
    border-top-color:${themeColor};animation:s 0.9s linear infinite}
  @keyframes s{to{transform:rotate(360deg)}}
  p{font-size:14px;color:#9ca3af;margin:0}
</style>
</head>
<body>
<div class="wrap"><div class="spin"></div><p>Loading ${cfg.appName}…</p></div>
<script>
  var TARGET = ${JSON.stringify(serverUrl)};
  var KEEP = ${JSON.stringify(keepHost)};
  function go(){ location.replace(TARGET); }
  document.addEventListener('DOMContentLoaded', function(){
    if (window.Capacitor && Capacitor.Plugins && Capacitor.Plugins.App) {
      // Keep in-app navigation on our host; open everything else externally.
      document.addEventListener('click', function(ev){
        var a = ev.target; while (a && a.tagName !== 'A') a = a.parentElement;
        if (!a || !a.href) return;
        try {
          var u = new URL(a.href, location.href);
          var host = (u.hostname||'').replace(/^www\\./,'');
          if (host === KEEP || host.slice(-(KEEP.length+1)) === '.'+KEEP) return;
          if (u.protocol !== 'http:' && u.protocol !== 'https:') return;
          ev.preventDefault();
          var b = Capacitor.Plugins.Browser;
          if (b && b.open) b.open({ url: u.href }); else window.open(u.href,'_blank');
        } catch(e){}
      }, true);
    }
    setTimeout(go, 300);
  });
</script>
</body>
</html>
`;

mkdirSync(join(here, 'www'), { recursive: true });
writeFileSync(join(here, 'www', 'index.html'), html);

console.log(`✓ Configured "${targetName}"`);
console.log(`  appId:  ${cfg.appId}`);
console.log(`  server: ${cfg.server.url}`);
console.log('  Next: npm run add:android && npm run add:ios && npm run open:android');
