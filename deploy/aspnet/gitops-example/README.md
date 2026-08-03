# GitOps example (DESIGN ONLY)

CloudPanel VM remains the current host. These manifests are **not applied** by deploy scripts.

- `cutoverAllowed=false`
- `readyForPhpRemoval=false`
- PHP product chrome remains authoritative
- Exact-route Nginx/YARP allowlists only — never catch-all `/api` `/cp` `/erp` `/bos` `/`

Do not invent `RELEASE_OWNER_APPROVAL.md`.
