# PHP → Python migration plan (strangler fig)

Goal: move the platform to Python **incrementally** — new fast Python services take
over one hot path at a time while the PHP CMS keeps serving everything else.
Both stacks share the same MySQL database, so there is no big-bang cutover and
each step is independently shippable and reversible.

> Honest note on speed: most recent slowness (Cloudflare 524s, loadavg ~40) came
> from **database locks and unbounded work on the request path**, not from PHP
> itself. The Python services below are designed to make that class of bug
> impossible (hard per-query timeouts, no schema mutation at request time),
> which is where the real speed win comes from.
>
> Migration status: **Phases 0–2 are built and unit-tested in `pyapi/`.**
> Phases 3–4 (background worker + optional SSR) and the production cutover of
> live traffic remain — a full rewrite of a multi-tenant CMS is deliberately
> incremental and is **not** "done" until each endpoint has run under real
> traffic with the PHP fallback still in place. This is by design, not omission.

## Phase 0 — foundations (this PR)

- `pyapi/` FastAPI service, deployed behind nginx at `/pyapi/`
- Reads DB credentials from the existing `config.php` (same convention as
  `pyprices`), env overrides for containers
- Connection pool + **3s hard per-statement timeout** — a slow query returns an
  error instead of holding a worker for 100s
- Endpoints (read-only, hottest first):
  | Endpoint | Replaces (PHP) |
  |---|---|
  | `GET /pyapi/v1/search?article=…` | warehouse part search AJAX chain |
  | `GET /pyapi/v1/prices?key=…` | CP price lists listing (QTY incl. fallback) |
  | `GET /pyapi/v1/laximo/catalogs` | `api/laximo_proxy.php?action=catalogs` (DB reads) |
  | `GET /pyapi/health` | — (monitoring) |

### Deploy (CloudPanel host)

```bash
cd /home/ecomae/htdocs/www.ecomae.com
python3 -m venv pyapi-venv
pyapi-venv/bin/pip install -r pyapi/requirements.txt
pyapi-venv/bin/uvicorn pyapi.main:app --host 127.0.0.1 --port 8090 --workers 2
```

systemd unit (`/etc/systemd/system/pyapi.service`):

```ini
[Unit]
Description=ecomae pyapi (FastAPI)
After=network.target mysql.service

[Service]
User=ecomae
WorkingDirectory=/home/ecomae/htdocs/www.ecomae.com
ExecStart=/home/ecomae/htdocs/www.ecomae.com/pyapi-venv/bin/uvicorn pyapi.main:app --host 127.0.0.1 --port 8090 --workers 2
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
```

nginx (inside each tenant vhost, before the PHP location):

```nginx
location /pyapi/ {
    proxy_pass http://127.0.0.1:8090/pyapi/;
    proxy_read_timeout 10s;
    proxy_connect_timeout 2s;
}
```

Verify: `curl https://www.epartscart.com/pyapi/health` → `{"status": true, ...}`.

## Phase 1 — storefront search cutover  ✅ flag-ready

- `/pyapi/v1/search` (indexed `article_search`, excludes disabled lists)
- `/pyapi/v1/brands` (in-stock manufacturer grid, index-friendly GROUP BY)
- Cutover shim `pyapi/static/epc_pyapi_search.js` — loads only when
  `epc_pyapi_search=1` cookie / `window.EPC_PYAPI_SEARCH` is set, falls back to
  the PHP path on any error (instant per-user rollback)
- **Remaining:** flip the flag in the part-search UI and measure p95 < 1s

## Phase 2 — CP / ERP data APIs  ✅ done (endpoints + auth + tests)

Implemented and tested (`tests/pyapi/`):
- `GET /pyapi/v1/prices` — price lists with QTY fallback
- `GET /pyapi/v1/upload-history` — per-list or global upload history
- `GET /pyapi/v1/commerce/sources` — commerce S/P/L sources
- `GET /pyapi/v1/dashboard` — KPI tiles (price lists, orders, products, customers)
- `GET /pyapi/v1/orders` — paginated orders
- `GET /pyapi/v1/laximo/status` — sync snapshot
- **Auth** (`pyapi/auth.py`): validates the `admin_session` cookie against the
  `sessions` table (same rule as `epc_cp_auth_gate_is_admin`), OR a server-to-server
  `tech_key`. Storefront customer cookie helpers included for future use.
- **Remaining (integration):** have the CP grids fetch these via AJAX after first
  paint (HTML shell stays PHP). Mobile apps (PR #234) can point at these directly.

## Phase 3 — background jobs + native push  ✅ push dispatcher done

**Native push notifications wired to pyapi** (order alerts + low-stock):
- `pyapi/push.py` — device registry (`epc_push_devices`) + FCM HTTP v1 sender
  (FCM relays to APNs for iOS, so one transport covers Android + iOS). Safe
  no-op when FCM creds absent; tokens still register.
- Endpoints: `POST /pyapi/v1/push/register`, `/push/unregister` (admin session),
  `/push/test` (admin/key).
- `pyapi/worker.py` — long-lived loop (or `--once` for cron) that polls new
  orders + low-stock and dispatches push. Baselines to the current order id on
  first run so it never blasts the backlog; state in a small file.
- `pyapi/ops/push_setup.py` — creates `epc_push_devices` (ops-only DDL).
- App side: `pyapi/static/epc_push_register.js` requests permission, gets the
  FCM/APNs token in the Capacitor CP shell, and registers it with the admin
  session cookie.

Enable sending (once you have a Firebase project):
```bash
export PYAPI_FCM_PROJECT=your-firebase-project-id
export PYAPI_FCM_ACCESS_TOKEN="$(gcloud auth application-default print-access-token)"
python -m pyapi.ops.push_setup           # create device table
python -m pyapi.worker                    # start the dispatcher
```

systemd unit (`/etc/systemd/system/pyapi-worker.service`):
```ini
[Unit]
Description=ecomae pyapi push worker
After=network.target mysql.service

[Service]
User=ecomae
WorkingDirectory=/home/ecomae/htdocs/www.ecomae.com
Environment=PYAPI_FCM_PROJECT=your-firebase-project-id
ExecStart=/home/ecomae/htdocs/www.ecomae.com/pyapi-venv/bin/python -m pyapi.worker
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

**Remaining phase 3:** move the pyprices cron protocol itself into this worker
(retire the PHP↔CGI `pyprices-api.php` bridge); write `records_count` /
`article_search` at ingest time.

## Phase 3b — remaining background jobs

- Move pyprices cron protocol into a proper worker (Celery/APScheduler in the
  same venv), retiring the PHP↔CGI bridge (`pyprices-api.php`)
- Imports write `records_count`/`article_search` at ingest time, removing all
  request-path backfills for good

## Phase 4 — page rendering (only if still needed)

- Highest-traffic storefront pages (home, part search page) as FastAPI +
  Jinja2 SSR with the existing full-page cache semantics
- CP remains PHP until the API surface from phase 2 covers all grids/forms;
  then port screen-by-screen

## Rules that keep every phase safe

1. **One database.** Python never migrates schema at request time; ops scripts
   (`epc-*.php` or `pyapi/ops/`) own DDL.
2. **Hard timeouts everywhere.** 3s per statement in pyapi; nginx proxy_read 10s.
3. **PHP fallback stays** until a Python endpoint has run clean for 2 weeks
   under production traffic.
4. **No mixed writes** to the same table from both stacks within one phase —
   reads migrate first, then the writer moves in a single step.
