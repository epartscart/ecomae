# Intelligence Platform + LifeOS — CloudPanel deploy paste

PR **#921** (IP + LifeOS Parts 1–2) is already on `main`. Later LifeOS Spec Parts 3–10 / IP login polish land on follow-up branches — after each merge, republish `:5100`.

**Merge alone does nothing live.** Run as **root** on CloudPanel.

## A) After this branch merges to `main` (recommended)

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae || { echo REPO_NOT_FOUND; exit 1; }
git fetch origin main
git checkout -f main
git reset --hard origin/main
export ECOMAE_BRANCH=main
bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live-ip-lifeos.log
grep -E 'RESULT=|ERROR|FAIL' /root/force-live-ip-lifeos.log | tail -20
```

## B) Deploy this branch before merge (prove early)

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae || { echo REPO_NOT_FOUND; exit 1; }
git fetch origin cursor/lifeos-spec-complete-ip-login-7b3b
git checkout -f cursor/lifeos-spec-complete-ip-login-7b3b
git reset --hard origin/cursor/lifeos-spec-complete-ip-login-7b3b
export ECOMAE_BRANCH=cursor/lifeos-spec-complete-ip-login-7b3b
bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live-ip-lifeos.log
grep -E 'RESULT=|ERROR|FAIL' /root/force-live-ip-lifeos.log | tail -20
```

## C) One-liner from GitHub raw (main)

```bash
ECOMAE_BRANCH=main bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_NOW.sh)" 2>&1 | tee /root/force-live-ip-lifeos.log
```

## CP system guide (after FORCE_LIVE)

- Visual + all frontend/backend links + chapters: `https://www.ecomae.com/cp/lifeos-guide-app`
- IP hub: `https://www.ecomae.com/ip` · login: `https://www.ecomae.com/ip/login`
- LifeOS home: `https://lifeos.ecomae.com/` · preview: `https://www.ecomae.com/lifeos`

## Prove after RESULT=PASS

```bash
curl -sI https://www.ecomae.com/ip/login | head -5
curl -sI https://www.ecomae.com/ip | head -5
curl -sI https://www.ecomae.com/cp/lifeos-guide-app | head -5
curl -sI https://www.ecomae.com/bos/login | head -5
curl -s https://www.ecomae.com/lifeos/spec | head -c 400; echo
curl -s https://www.ecomae.com/lifeos/architecture | head -c 400; echo
```

- Login: `https://www.ecomae.com/ip/login` — **same Super-CP operator email/password as** `/bos/login`
- Hub: `https://www.ecomae.com/ip/app`
- LifeOS: `https://lifeos.ecomae.com/` (needs DNS/TLS) or `https://www.ecomae.com/lifeos`

Hard-refresh (Ctrl+Shift+R) after deploy.
