# Same-to-same look parity (product chrome)

**Locks:** `cutoverAllowed=false`, `readyForPhpRemoval=false`, `aspNetInteractiveComplete=0`.

## Goal

CP, ERP, BOS, storefront, dashboard, and marketing product chrome must not advertise PHP vs ASP.NET, hybrid cutover, JSON digests, or stack badges. Tenants should not be able to identify a look gap versus live PHP hubs.

## Changes

- Stripped stack-revealing CTAs and notes across ~130 product Blazor apps.
- Shared chrome: `PhpCpDesktopChrome`, `PhpErpDesktopChrome`, `PhpBosDesktopChrome`, `PhpStorefrontDesktopChrome`, `PhpHybridWorkspaceFrame`, `PhpHybridModuleDirectory`, `LegacyAdminLoginForm`.
- Marketing overviews: no “Open PHP … (hybrid)” / dual-sample / ASP.NET scaffold footers in visible copy.
- Hero CTAs use product labels (`Open module`, `Open dashboard`, etc.) instead of `Open PHP …`.
- Floor evidence: `same-to-same-look-gap-floor.json` (`forbiddenHits=0`).

## Validation

```bash
python3 scripts/validate_same_to_same_look_gaps.py
```

Operator consoles (`MigrationCompareConsole`, `ZeroPhpConsole`) and on-prem installer remain excluded — they may still name stacks.
