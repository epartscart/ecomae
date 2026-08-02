# PHP Decommission Readiness

This document tracks the final Zero-PHP gate. It does **not** authorize PHP removal.

## Status

`blocked-not-ready-for-php-removal`

Live diagnostic: `GET /migration/php-decommission-readiness`

## Why the last 5% remains

Weighted Zero-PHP completion can reach 95% with inventory ownership, digests, auth, workers dry-run scaffolding, and surface shells. The final 5% is **PHP runtime decommission** and stays at 0% until:

1. Every tracked route/job has green PHP-vs-ASP.NET parity evidence
2. Exact-route staging smoke artifacts are attached
3. Only approved `location =` nginx shadows are promoted
4. Release-owner written approval exists to disable PHP fallback
5. Rollback commands are validated

## Must remain until the final gate

- PHP-FPM
- PHP cron / schedulers
- PHP rewrites and docroot PHP entrypoints
- PHP source dependencies used as authoritative fallback

## Forbidden shortcuts

- Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront nginx cutover
- Claiming 100% Zero-PHP because dry-run catalogs exist
- Deleting PHP before parity/shadow/live evidence is complete
