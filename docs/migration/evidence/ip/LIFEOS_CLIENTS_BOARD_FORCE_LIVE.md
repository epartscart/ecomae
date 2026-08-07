# FORCE_LIVE — LifeOS clients board + join fetch (after #945 / follow-up)

Merge alone does **not** republish Kestrel `:5100`. Live still served the pre-#945 binary
(`/lifeos/clients/cp` → 404, join still Blazor `__internal_preventDefault_onsubmit`).

## CloudPanel root paste

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin main && git checkout -f main && git reset --hard origin/main
export ECOMAE_BRANCH=main
bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live-lifeos-clients.log
grep -E 'RESULT=|ERROR|FAIL' /root/force-live-lifeos-clients.log | tail -20
```

## Prove

```bash
curl -sL https://lifeos.ecomae.com/lifeos/clients/cp | head
curl -sL -o /dev/null -w '%{http_code}\n' https://lifeos.ecomae.com/lifeos/clients-board
curl -sL -o /dev/null -w '%{http_code}\n' https://lifeos.ecomae.com/cp/lifeos-clients-app
curl -sL https://lifeos.ecomae.com/lifeos/join | grep -F '/lifeos/join.js'
curl -sL https://lifeos.ecomae.com/lifeos/join.js | grep -F "fetch('/lifeos/join'"
```

Expected: JSON 200 with `totalClients`, board/CP HTML 200 (no login redirect), join page loads `join.js`.
