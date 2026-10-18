# ecomae ERP — Hybrid deployment

Hybrid keeps ERP on the customer server and Super CP in the cloud. The site
agent talks **outbound HTTPS only**. Masters (customers, items, tax, staff,
price lists, chart of accounts) belong to the cloud. Transactions (orders,
invoices, payments, stock, journals) belong to the site. Conflicts are queued
for review — never silently overwritten. The agent **does not invent a send**
and **does not invent a MySQL user**.

Download both packs from Super CP `/cp/deployment-models-app`:

| Pack | Zip |
|------|-----|
| On premises (ERP install) | `/cp/deployment-models/setup-pack?kind=on-premises` |
| Hybrid (this folder) | `/cp/deployment-models/setup-pack?kind=hybrid` |

## Quick start (Windows PC)

1. Download `ecomae-hybrid-setup.zip` from Super CP → Deployment models.
2. Unzip. Double-click **`setup.exe`** (or `setup.cmd`).
3. Enter site key, enrollment (`enr_…`), and installation (`inst_…`) from Super CP register.
4. Do not invent a database password. Leave it empty to use JSON state, or paste an operator DSN.

## Quick start (Linux server)

```bash
# 1) Install ERP with the on-premises pack (license required).
sudo ./on-premises/install.sh --license YOUR_KEY --domain erp.yourcompany.com

# 2) Super CP registers the site (operator, confirmWrites=true):
#    POST https://www.ecomae.com/cp/hybrid-sync/register
#    { "siteKey":"acme", "cloudBaseUrl":"https://www.ecomae.com", "confirmWrites":true }

# 3) Install the sync agent next to the ERP (operator-supplied DSN — never empty-password).
sudo ./hybrid/install.sh \
  --site-key acme \
  --cloud-url https://www.ecomae.com \
  --enrollment enr_XXXX \
  --installation inst_XXXX \
  --erp-dir /opt/ecomae
```

`install.sh` copies the agent, writes `.env` from the flags you pass, creates
local inbox/outbox tables **only when `LOCAL_DSN` is set**, and starts the
systemd unit or the Compose overlay. If you omit `--dsn`, the agent uses
`./state/outbox.json` and `./state/inbox.json` — it will not guess a password.

## Files

| File | Purpose |
|------|---------|
| `install.sh` | Server installer for the sync agent (optionally chains the on-prem pack) |
| `.env.example` | Site key, cloud URL, enrollment, optional operator DSN |
| `docker-compose.hybrid.yml` | Compose overlay that runs the agent |
| `sync-agent/sync-agent.py` | Outbound HTTPS pull / ingest / ack / cycle |
| `sync-agent/ownership.json` | Cloud-owned masters vs site-owned transactions |
| `sync-agent/local-schema.sql` | Site `epc_hybrid_sync_*` inbox/outbox/conflict tables |
| `systemd/ecomae-hybrid-sync.service` | Loop the agent every five minutes |

## How the database synchronizes

1. Super CP (or a later client-erp job) **ingests** `cloud-to-hybrid` master envelopes into the platform change-log (`epc_hybrid_sync_envelopes`). Shop tables are not copied onto the platform database.
2. The site agent **pulls** pending master envelopes. An empty pull is success — it does not invent a send. After the agent writes `state/inbox.json` (and the site inbox table when a DSN is supplied) it **acks** those envelope ids.
3. The site ERP (or an operator) writes transaction rows into `epc_hybrid_sync_outbox` or `state/outbox.json`. The agent **ingests** them as `hybrid-to-cloud`.
4. Ownership or version clashes become rows on `epc_hybrid_sync_conflicts` (`resolution=queued`).

Cloud endpoints (Super host):

- `POST /cp/hybrid-sync/register`
- `POST /cp/hybrid-sync/pull`
- `POST /cp/hybrid-sync/ingest`
- `POST /cp/hybrid-sync/ack`
- `GET  /cp/hybrid-sync/status`

Writes stay dry-run unless `confirmWrites=true`.

## Manual agent

```bash
cd /opt/ecomae/hybrid
cp .env.example .env   # fill SITE_KEY, CLOUD_BASE_URL, ENROLLMENT_REQUEST_ID, INSTALLATION_ID
python3 sync-agent/sync-agent.py cycle
```
