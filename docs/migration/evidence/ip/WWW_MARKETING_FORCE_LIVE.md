# FORCE_LIVE — www.ecomae.com marketing home (authoritative)

PR **#949** is on `main` but merge does **not** republish `:5100` or refresh nginx.
Live www stays on the stale binary / STOP_PRODUCT_PHP snip that sent `/` → storefront
and left marketing CSS on dead PHP/`epc-static` URLs.

## CloudPanel root — use THIS paste

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/force-live-www-marketing-7b3b/scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh)" 2>&1 | tee /root/force-live-www-marketing.log
tail -50 /root/force-live-www-marketing.log
```

After this PR merges to main you may use:

```bash
ECOMAE_BRANCH=main bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh)" 2>&1 | tee /root/force-live-www-marketing.log
```

Must print `RESULT=PASS — www.ecomae.com marketing home styled via platform-assets`.

## Working checks after PASS

- https://www.ecomae.com/ shows animated epm-hub (not concatenated nav / katakana dump)
- https://www.ecomae.com/platform-assets/epc_ecomae_platform_marketing.css → `200 text/css`
- Home HTML contains `ecomae-chrome-surface: marketing` and `/platform-assets/epc_ecomae_platform_marketing.css`
