# Menu + field parity — built vs pending

**Locks:** `cutoverAllowed=false`, `readyForPhpRemoval=false`, `aspNetInteractiveComplete=0`.

## Menus (no missing items)

| Surface | Catalog | ASP.NET coverage |
|---|---:|---|
| CP features | 405 | 404 digest-contract + 1 PHP-only holdout (`cp-debug-console`) |
| ERP categories/areas/tabs | 9 / 35 / 154 | 100% digest-contract + hybrid deeplink |
| BOS sections/modules | 11 / 99 | 100% |
| Storefront surfaces | 13 | 100% |
| **Total** | **726** | **725 digest / 1 holdout** |

Every menu item has an ASP.NET digest/app route or intentional PHP deeplink. **No menu item is missing from the catalog board.**

## Fields

- Digest contracts: summary + hybrid list + list + object stems now have field floors (see `*-item-field-floor.json`, `list-digest-item-field-floor.json`, `storefront-profile-object-field-floor.json`).
- CP integrations field parity (payments, carriers, Amazon/marketplace, hub) already merged via PR #809.

## Writes / interactive (still pending for 0 PHP)

| Pack | Actions | Status |
|---|---:|---|
| CP module-ajax + classic forms | 394 | Dry-run gates 100%; live writes PHP |
| ERP `ajax_erp.php` | 321 | Dedicated dry-runs; live writes PHP |
| BOS `ajax_epc_bos.php` | 231 | Dry-runs; live writes PHP |
| Module-ajax dual-sample goldens | 20 curated | `writes=0`; need PHP-side paired samples on CloudPanel |

## Path to decommission PHP

1. CloudPanel: authenticated digest + module-ajax field dual-samples  
2. Exact-route shadows green on www + tenants  
3. Human `RELEASE_OWNER_APPROVAL.md` (never invent)  
4. Only then `cutoverAllowed` / `readyForPhpRemoval` / interactive complete  

Honest path stays **99%** until those gates clear — UI/menus are catalog-complete; interactive write cutover is not.

## Sprint update

- Module-ajax dedicated dry-run goldens expanded to **254** (writes=0).
- Storefront digest shadows inventory **7/7** (incl. checkout).
- Marketing `/marketing/*` probe: 37 routes inventoried; live shadows blocked awaiting CloudPanel install.
