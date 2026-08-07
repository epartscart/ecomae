# FORCE_LIVE — LifeOS clients board (authoritative)

Live prove after the previous paste still showed the **pre-#945** binary
(`joinedAtUtc` stuck at `2026-08-07T12:33:23Z`, `/lifeos/clients/cp` 404, join still Blazor).
Likely cause: `cloudpanel_FORCE_LIVE_NOW.sh` exited early on the LifeOS cinematic **Git LFS** MP4 check before publish/restart.

## CloudPanel root — use THIS paste

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/lifeos-clients-public-no-login-7b3b/scripts/cloudpanel_FORCE_LIVE_LIFEOS_CLIENTS.sh)" 2>&1 | tee /root/force-live-lifeos-clients-v2.log
tail -40 /root/force-live-lifeos-clients-v2.log
```

Must print `RESULT=PASS — LifeOS clients board + join fetch live`.

## Working links after PASS

- https://lifeos.ecomae.com/lifeos/clients-board
- https://lifeos.ecomae.com/lifeos/clients/cp
- https://lifeos.ecomae.com/cp/lifeos-clients-app
