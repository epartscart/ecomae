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

## Phase 1 — cut storefront search over to pyapi

- Point the storefront part-search JS at `/pyapi/v1/search` (feature flag:
  `epc_pyapi_search=1` cookie/config), PHP endpoint stays as fallback
- Add `/pyapi/v1/brands` (cached brands list) and `/pyapi/v1/manufacturers`
- Measure: p95 click→result must be **< 1s**; roll back per-flag if not

## Phase 2 — CP data APIs

- CP prices module fetches `/pyapi/v1/prices` via AJAX after first paint
  (HTML shell stays PHP; data grid becomes API-driven)
- Upload history, commerce sources list, Laximo status → pyapi
- Session auth: pyapi validates the `admin_session` cookie against the
  `sessions` table (same rule as `epc_cp_auth_gate_is_admin`)

## Phase 3 — background jobs

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
