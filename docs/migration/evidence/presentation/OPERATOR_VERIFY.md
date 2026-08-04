# Operator verify — presentation recheck

Honest gate: live ASP.NET product chrome is **not** PHP-parity until chrome + module function evidence pass. Checked-in `php-vs-aspnet-recheck.json` currently records `status=fail`, `cutoverAllowed=false`, `readyForPhpRemoval=false`.

## Offline CI floor

```bash
bash scripts/cloudpanel_run_presentation_recheck_operator.sh
```

Validates cached evidence only (no network). Expect PASS on the lock (`readyForPhpRemoval=false`), not chrome parity.

## Live soft probe (CloudPanel / www)

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
cd /opt/ecomae-aspnet-source
ECOMAE_PRESENTATION_LIVE=1 ECOMAE_PRESENTATION_SOFT=1 \
  bash scripts/cloudpanel_run_presentation_recheck_operator.sh
```

Soft mode exits 0 so evidence can refresh while still listing failures. Never invents `RELEASE_OWNER_APPROVAL.md` or `MODULE_FUNCTION_TEST_PASS.md`.

## Exact-route inventory

Checked-in floor (47 presentation nginx locations, `cutoverAllowed=false`):

`docs/migration/evidence/presentation/presentation-exact-routes.json`

```bash
python3 scripts/validate_presentation_hybrid_allowlist_sync.py
```

## Related

- Dual-sample suite: `bash scripts/cloudpanel_run_all_dual_sample_operators.sh`
- Tenant same-to-same: `docs/migration/evidence/tenant-safety/OPERATOR_VERIFY.md`
