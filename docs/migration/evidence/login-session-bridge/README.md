# Login / session bridge dual-sample evidence (Batch 3)

**Policy:** ASP.NET may mint PHP-compatible MySQL session cookies when `EcomAE__SecretSuccession` is set. Product chrome and BOS `$_SESSION` modules remain PHP-authoritative. `cutoverAllowed` / `readyForPhpRemoval` stay **false**.

## Decision gate (locked)

| Path | Role |
| --- | --- |
| `/cp/login`, `/erp/login`, `/storefront/login` | Opt-in PHP-compatible cookie mint (`admin_*` or `session`/`u_id`) |
| `/bos/login` | Same **admin cookie** bridge for digests + `/bos/app` only |
| `/BOS/?action=login` | **PHP-authoritative** native `$_SESSION['epc_bos_context']` — not replaced by Batch 3 |

Social / demo / shared-ERP picker / rate-limit / password hash upgrade remain PHP.

## Capture (CloudPanel)

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
bash scripts/cloudpanel_verify_secret_succession_configured.sh   # never prints secret
bash scripts/cloudpanel_find_and_redeploy.sh
# Loopback preferred:
export ECOMAE_ASPNET_BASE_URL=http://127.0.0.1:5100
export ECOMAE_LOGIN_CONTACT='…'
export ECOMAE_LOGIN_PASSWORD='…'
export ECOMAE_OVERWRITE_LOGIN_SAMPLES=1
bash scripts/cloudpanel_capture_login_cookie_dual_samples.sh
python3 scripts/compare_login_cookie_dual_samples.py \
  --samples-dir docs/migration/evidence/login-session-bridge \
  --out docs/migration/evidence/login-session-bridge/compare-result.json
```

Without credentials the capture script writes **contract stubs** (cookie names + expected probe kinds) so CI/foundation checks have a stable floor.

## Pass meaning

- Admin surfaces: `admin_session` + `admin_u_id` (or probe `kind=Admin`)
- Storefront: `session` + `u_id` (or probe `kind=Customer`)
- Customer mint formula: `md5(contact + userId + time + secret)` + `last_activiti_time`
- Result JSON always has `"cutoverAllowed": false`
