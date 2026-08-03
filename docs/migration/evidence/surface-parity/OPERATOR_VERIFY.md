# Operator verify — digest dual samples

Surface/storefront digests (35 stems including `/cp/orders-digest`).

```bash
# Offline migration contract floor (no admin cookie)
bash scripts/cloudpanel_run_digest_dual_sample_operator.sh

# Allowlist sync
python3 scripts/validate_surface_digest_allowlist_sync.py
```

Live capture (CloudPanel, admin cookie required):

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
# ECOMAE_ADMIN_COOKIE_HEADER set by smoke issuer / platform.env
bash scripts/cloudpanel_run_digest_dual_sample_operator.sh
```

Expect `cutoverAllowed=false`. Never invent `RELEASE_OWNER_APPROVAL.md`.
