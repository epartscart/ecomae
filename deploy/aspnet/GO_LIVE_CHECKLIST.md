# ASP.NET Core Route-by-Route Go-Live Checklist

Use this checklist for every individual ASP.NET Core route or surface. Do not use it to approve broad `/cp`, `/erp`, `/bos`, `/api`, or storefront catch-all traffic.

## Required evidence before changing proxy traffic

- [ ] PHP route remains healthy and rollback-tested.
- [ ] ASP.NET Core route responds on `127.0.0.1:5100`.
- [ ] `/migration/readiness` has no blocker for this route.
- [ ] `/migration/data-parity` confirms database/read-model parity for this route.
- [ ] `/migration/cutover-validation` confirms fallback, monitoring, and approval gates.
- [ ] Live smoke passes from an allowed network without exposing secrets.
- [ ] Business owner approves route behavior and response shape.
- [ ] Nginx change is exact-match only, for example `location = /api/v1/catalog/status`.
- [ ] `scripts/verify_aspnet_proxy_guardrails.sh` passes after the Nginx snippet is added.

## Cutover window steps

1. Snapshot current Nginx/CloudPanel site configuration.
2. Add the exact-route ASP.NET Core proxy block.
3. Run `sudo nginx -t`.
4. Reload Nginx.
5. Request the route and confirm `X-EcomAE-Target-Runtime` and `X-EcomAE-PHP-Fallback` diagnostics when available.
6. Watch PHP, ASP.NET Core, Nginx, and database logs for errors.
7. Record the timestamp, route, approver, and rollback command.

## Immediate rollback

1. Remove the exact-route ASP.NET Core proxy block.
2. Run `sudo nginx -t`.
3. Reload Nginx.
4. Confirm the PHP route responds again.
5. Keep parity telemetry for incident review.
