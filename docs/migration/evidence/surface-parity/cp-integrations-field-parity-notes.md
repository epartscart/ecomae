# CP integrations field parity (same-to-same)

cutoverAllowed=false · readyForPhpRemoval=false · aspNetInteractiveComplete=0

## Surfaces closed

| Surface | Before | After |
| --- | --- | --- |
| `/cp/carriers` | Wrong table `epc_erp_carriers` | `epc_carrier_accounts` + `epc_carrier_shipments` + catalog region/blurb |
| `/cp/payment-gateways` | Missing `anable` | `anable`=Enabled, `active`=Default; secrets still omitted |
| `/cp/integrations` | `epc_webhooks` only | Integrations Hub catalog (`key`/`label`/`blurb`/`category`/`configureUrl`) |
| `/cp/marketplace-channels` | DB rows only | + catalog `family`/`region`/`api`/`blurb` (Amazon etc.) |
| Blazor apps (5) | Stack-revealing “Open PHP / JSON digest / Not a cutover” | Product chrome: Configure / Manage only |

## Write gates

- Payments / channels / logistics dry-runs now allowlist PHP ajax actions.
- classic_form dedicated gates: `logistics_carriers`, `obtaining_mode`, `epc_integrations_hub`, `payments_configure`.

## Honest path

HonestCompletionPct stays **99**. Never invent cutover/100%/RELEASE_OWNER_APPROVAL.
