# ecomae — Project SOP, Rules, Protocols, Principles & Security Law

**Status:** Canonical operating law for the ecomae multi-tenant platform.  
**Audience:** Operators, release owners, CloudPanel admins, developers, agents.  
**Related law:** `docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md` · `docs/migration/TENANT_MIGRATION_SAFETY.md` · `docs/TENANT_SCALE_1000.md`

> **Honesty lock:** This SOP does **not** claim the product is unhackable or that PHP can be removed.  
> Today: `cutoverAllowed=false`, `readyForPhpRemoval=false`, `aspNetInteractiveComplete=0`.  
> Never invent `RELEASE_OWNER_APPROVAL.md`, module-function PASS, or presentation `status=pass`.

---

## 1. Mission principles (non-negotiable)

| # | Principle | Meaning |
|---|---|---|
| P1 | **Tenant data sovereignty** | Every tenant’s commerce, ERP, users, orders, KYC, and bank data belong **only** to that tenant. |
| P2 | **Confidentiality by default** | Secrets, PII, documents, and financial detail are withheld unless a role **and** tenant context both authorize them. |
| P3 | **Least privilege** | Users, APIs, jobs, and digests get the minimum capabilities required — never fleet-wide credentials on a tenant request. |
| P4 | **Defense in depth** | Host → TLS → auth → ACL → tenant DB resolve → query scope → audit. One broken layer must not expose another tenant. |
| P5 | **Same-to-same until cutover** | Live tenants keep PHP look/function until dual-sample + staged exact-route + human approval. |
| P6 | **No invented readiness** | Scaffold % ≠ UX cutover. Weighted Zero-PHP % ≠ ReadyToRemovePhp. |
| P7 | **Fail closed** | Missing tenant, session, cookie, or approval → deny / refuse — never degrade into shared data. |
| P8 | **Auditability** | Privileged actions and isolation incidents must be attributable (who/when/what/tenant). |

---

## 2. Hard rules (must never violate)

### 2.1 Tenant isolation

1. **New tenants MUST use dedicated MySQL** (`dedicated_db=1` / `scale_policy=dedicated_mysql`).  
   Shared `docpart` is **legacy exception only**, never the default for new onboardings.
2. **No cross-tenant reads or writes.** Queries must resolve the tenant’s own DB (or, for platform registry only, the platform DB with `site_key` filters).
3. **Hostname → tenant resolve is mandatory** before any business data access (`epc_portal_load_tenant_by_host` / ASP.NET equivalent when live).
4. **Never reuse another tenant’s `db_user` / `db_password` / connection** for a different host.
5. **Super-CP / BOS fleet tools** may list tenant **metadata** from the platform registry; they must **open one tenant DB at a time** for data ops — never union customer tables across tenants.
6. **If a non–eParts client is still on shared `docpart`**, containment (`epc_tenant_data_guard`) MUST hide orders/bank from the shared spare-parts corpus and treat isolation as a **P0 incident**, not steady state.
7. **`company_id` / site filters** inside a tenant DB are additional scoping — they do **not** replace dedicated-DB isolation between tenants.
8. **Digests and Blazor apps MUST NOT** open `OpenAsync(null)` against a shared connection and return another tenant’s rows. Until ASP.NET has a live DB-backed registry + per-tenant credentials, digests stay **www scaffolding** and PHP remains authoritative for tenant product data.

### 2.2 Confidentiality & PII

1. Passwords, API secrets, tokens, MFA secrets, payment credentials, raw KYC documents → **never** in digests, logs, dual-samples, or error messages.
2. Emails, phones, passport numbers, addresses → omit or mask in read digests unless the surface is an authenticated same-tenant admin tool that PHP already exposes.
3. Export/CSV/PDF of sensitive data → PHP-authoritative paths with role checks until ASP.NET cutover is approved.
4. Dual-sample / evidence artifacts → redacted; no live cookies or secrets committed to git.
5. Browser storage / analytics → no secrets; Clarity/gtag must not carry session tokens.

### 2.3 Cutover & PHP removal

1. Broad nginx cutovers of `/`, `/api`, `/cp`, `/erp`, `/bos`, `/storefront` on tenant vhosts are **forbidden**.
2. Exact-route shadows default to **www.ecomae.com** only.
3. Named live tenants refuse ASP.NET hybrid unless unlocked with `ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES` **and** dual-sample evidence.
4. `ReadyToRemovePhp=true` only when the decommission checklist is fully green **and** human `RELEASE_OWNER_APPROVAL.md` contains `APPROVED_TO_REMOVE_PHP_FALLBACK`.
5. PHP source deletion is a **separate human-owned PR** after runtime decommission — never agent-automated.

### 2.4 Security operations

1. No production secrets in repo, PRs, chat, or evidence JSON.
2. Confirm env flags (`ECOMAE_CONFIRM_*=YES`) are required for destructive or shadow-install operators.
3. Dry-run write endpoints must keep `writes=0` and refuse `confirm_writes` until interactive cutover is explicitly approved.
4. Rate limits / API daily quotas must remain enforced; scaffolds are not a substitute for live throttle.
5. Security incidents involving cross-tenant exposure → immediate containment (isolate DB, revoke sessions, rotate credentials) before feature work continues.

---

## 3. Architecture security layers

```
┌─────────────────────────────────────────────────────────────┐
│ L0  Edge: TLS, HSTS, host allowlists, nginx exact-route only │
├─────────────────────────────────────────────────────────────┤
│ L1  App: security headers, session cookies, CSRF (PHP forms) │
├─────────────────────────────────────────────────────────────┤
│ L2  AuthN: admin / customer / BOS / API-client identity      │
├─────────────────────────────────────────────────────────────┤
│ L3  AuthZ: groups, modules_access ACL, capabilities          │
├─────────────────────────────────────────────────────────────┤
│ L4  Tenant resolve: hostname → site_key → dedicated DB creds │
├─────────────────────────────────────────────────────────────┤
│ L5  Data: per-tenant MySQL (+ company_id where applicable)   │
├─────────────────────────────────────────────────────────────┤
│ L6  Audit: epc_boc_audit / ERP audit / isolation audit       │
└─────────────────────────────────────────────────────────────┘
```

| Layer | Controls in repo today | Operator duty |
|---|---|---|
| L0 | Nginx site safety (`scripts/ecomae_nginx_site_safety.py`), exact-route shadow examples | Never broad `location /` on tenant vhosts |
| L1 | `SecurityHeadersMiddleware` (nosniff, SAMEORIGIN, Referrer-Policy, Permissions-Policy, HSTS) | Keep HTTPS everywhere; do not weaken headers for convenience |
| L2–L3 | PHP sessions + ASP.NET `DbBackedLegacySessionValidator` | Prefer short sessions; revoke on role change |
| L4–L5 | `epc_portal_resolve_tenant_db`, dedicated MySQL default, `epc_tenant_data_guard` | Onboard dedicated only; isolate shared_docpart leftovers |
| L6 | Isolation audit digest `/cp/isolation-audit`, BOS compliance | Run isolation audits after onboard/migrate |

---

## 4. Tenant data isolation protocol

### 4.1 Onboarding (mandatory)

1. Super-CP → Tenant Hub → Onboard.  
2. Scale policy = **Dedicated MySQL (recommended)** — do not opt out without written exception.  
3. Provision unique `db_name` / `db_user` / `db_password`.  
4. Enqueue `tenant_warmup_pdo` (best effort).  
5. Verify hostname resolves to that DB only.  
6. Record industry_code / packs; do not copy another tenant’s DB.

### 4.2 Runtime resolve (every request)

1. Identify host.  
2. Load portal tenant row from **platform** registry.  
3. Open **that tenant’s** credentials (never a neighbor’s).  
4. Reject or degrade-closed if resolve fails.  
5. For shared_docpart degraded clients: activate data guard; hide shared orders/bank; escalate isolation job.

### 4.3 Isolation verification (SOP cadence)

| Cadence | Action |
|---|---|
| After every onboard | Connect with tenant creds; confirm `DATABASE()` is tenant DB |
| Weekly (fleet) | Sample isolation audit / anomaly probes |
| Before any shadow on tenant host | `bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh` |
| Before claiming ReadyToRemovePhp | Full dual-sample + isolation regression + approval |

### 4.4 Forbidden patterns (cross-tenant leak vectors)

- Selecting from `shop_orders` / users / accounting on shared `docpart` for a non-owner host without guard.  
- ASP.NET digest using a single `TenantRegistry` connection string while ignoring tenant `DatabaseName` / credentials.  
- BOS “list all tenants” UI that then queries one shared commerce schema for all sites.  
- Logging full SQL rows with PII into shared log ships without tenant redaction.  
- Backup/restore of tenant A into tenant B hostname without credential rewrite.  
- Giving a support engineer a platform root password for “quick fix” on a live tenant DB without ticket + time-bound access.

---

## 5. Confidentiality protocol (data classes)

| Class | Examples | Handling |
|---|---|---|
| **S0 — Secrets** | DB passwords, API keys, MFA secrets, payment tokens | Vault / env only; never digests; rotate on leak |
| **S1 — Regulated PII** | Passport, national ID, KYC files, AML notes | Role + tenant scoped; omit from digests; encrypt at rest where PHP modules support it |
| **S2 — Customer PII** | Email, phone, address | Mask in list digests; full only in authorized same-tenant tools |
| **S3 — Financial** | Invoices, bank lines, credit limits | Same-tenant admin/ERP roles only |
| **S4 — Operational meta** | site_key, hostname, industry_code, status | Allowed in Super-CP/BOS registry views |

**Digest rule:** Prefer S4 + aggregated KPIs. If a digest must list S2/S3 fields, document why and keep PHP authoritative until cutover approval.

---

## 6. Access control protocol

### 6.1 Surfaces

| Surface | Who | Session |
|---|---|---|
| Storefront | Customer | Customer cookie; own orders/garage/cart only |
| CP | Tenant admin | Admin session + CP capabilities |
| ERP | Tenant finance/ops | Admin + `erp` capability + module ACL |
| BOS / Super-CP | Platform operators | Highest privilege — still **one tenant DB at a time** for data |
| Public API | API client | Hash auth + daily quota; tenant-bound client |

### 6.2 Rules

1. Capability checks on every ASP.NET MapGet/MapPost for CP/ERP/BOS/storefront digests.  
2. Nested `modules_access` ACL for admin tools — no “all modules” default for client admins.  
3. API clients: active flag, hash, quota consume, usage log — no anonymous catalog write.  
4. Impersonation / tenant switch (BOS): audit log required; never leave a sticky wrong-tenant cookie.

---

## 7. Application security protocol

### 7.1 Transport & headers

- HTTPS everywhere (HSTS `max-age=31536000; includeSubDomains` when HTTPS).  
- `X-Content-Type-Options: nosniff`  
- `X-Frame-Options: SAMEORIGIN`  
- `Referrer-Policy: strict-origin-when-cross-origin`  
- Restrictive `Permissions-Policy`  
- CSP: not forced on ASP.NET today (Blazor/SignalR compatibility) — any future CSP must be staged on www first.

### 7.2 Input / output

- Parameterized SQL only (no string-concat table names from request input).  
- Report-center table names only from **allowlisted registry** entries.  
- Validate/encode all HTML output (PHP `htmlspecialchars` / Razor encoding).  
- File/LFI surfaces (e.g. debug console) → metadata-only allowlists; never arbitrary path read.

### 7.3 Write safety during migration

- Wave-B / module ajax dry-runs: `writes=0`, refuse `confirm_writes`.  
- Interactive writes remain PHP until interactive cutover evidence exists.  
- Workers: dry-run placeholders must not fan out live writes across thousands of tenants.

### 7.4 Dependency & host hardening (ops)

- Patch OS / PHP-FPM / MySQL / ASP.NET runtime on a fixed cadence.  
- MySQL: least-privilege users per tenant; no `GRANT ALL ON *.*` for app users.  
- Raise `max_connections` from measured workers × pool — not guesswork.  
- Separate platform registry DB credentials from tenant app credentials.  
- Backups encrypted; restore tested per tenant; access logged.

---

## 8. Incident response SOP (cross-tenant / theft suspicion)

| Step | Action | Owner |
|---|---|---|
| 1 | **Contain** — disable affected host shadow; freeze API clients; revoke admin sessions | On-call |
| 2 | **Isolate** — force dedicated DB if shared_docpart; rotate DB passwords | Platform |
| 3 | **Scope** — which `site_key`s, tables, time window | Security |
| 4 | **Preserve** — logs, audit rows, collision evidence (no silent DELETE) | Security |
| 5 | **Notify** — affected tenants / DPO as required by jurisdiction | Release owner |
| 6 | **Remediate** — patch path, add regression test, update this SOP if gap found | Eng |
| 7 | **Close** — written postmortem; no production reopen until isolation re-verified | Release owner |

**Never** “fix forward” by widening shared DB access.

---

## 9. Migration & cutover SOP (summary)

Full detail: `docs/migration/TENANT_MIGRATION_SAFETY.md`, residual board  
`docs/migration/evidence/decommission/public-probes/www-zero-php-residual-board.json`.

**Before any PHP cut:**

1. www shadows green (marketing 37/37, storefront digests 7/7).  
2. Authenticated digest + ajax dual-samples with cookies.  
3. Presentation recheck `status=pass` (do not invent).  
4. Functional live-smoke 7/7 captured.  
5. Same-to-same on named tenants + industry hosts.  
6. Isolation regression (dedicated DB + no cross-tenant queries).  
7. Scale controls: session cache, per-tenant ASP.NET connections, pool caps.  
8. Human `RELEASE_OWNER_APPROVAL.md`.  
9. Runtime decommission only with `ECOMAE_CONFIRM_PHP_DECOMMISSION=YES`.  
10. Source deletion = separate human PR.

**Refuse scripts:**

```bash
bash scripts/cloudpanel_php_decommission_gated.sh delete-php-source
# Expect: readyToRemovePhp=False … REFUSE (until checklist truly green)
```

---

## 10. Verification commands (operators)

```bash
# Offline contracts (safe)
python3 scripts/validate_surface_digest_allowlist_sync.py
python3 scripts/validate_presentation_hybrid_allowlist_sync.py
python3 scripts/compare_digest_dual_samples.py --contract-only
cd aspnet && dotnet test tests/EcomAE.Platform.Tests/EcomAE.Platform.Tests.csproj --nologo

# Live tenant still PHP-primary
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh

# Presentation recheck (must become pass before chrome cutover)
bash scripts/cloudpanel_probe_php_presentation_parity.sh

# Tenant scale / isolation tests
php tests/erp_advanced/run_tenant_scale_tests.php

# PHP removal gate (must REFUSE until ready)
bash scripts/cloudpanel_php_decommission_gated.sh delete-php-source
```

---

## 11. Roles & responsibilities

| Role | Duty |
|---|---|
| **Release owner** | Sole authority for `RELEASE_OWNER_APPROVAL.md` and PHP removal confirm flags |
| **Platform / BOS operator** | Fleet onboard, isolation audits, credential rotation |
| **Tenant admin** | Same-tenant CP/ERP only; no registry credentials |
| **Developer / agent** | Follow this SOP + architecture instructions; never invent approval/PASS |
| **On-call** | Incident containment within minutes for cross-tenant suspicion |

---

## 12. Current honest gaps (must close for “very high” confidentiality on ASP.NET path)

These are **documented debt**, not permission to ignore isolation on PHP:

1. ASP.NET tenant registry is still largely **config/seed**, not live DB registry for all hosts.  
2. Many digests call `OpenAsync(null)` — unsafe as a primary multi-tenant data path until fixed.  
3. Session/ACL validation is uncached multi-query — harden before high concurrency.  
4. Redis / rate-limit / Polly scaffolds are **not** live.  
5. Legacy `shared_docpart` tenants remain a residual isolation risk (guard = containment).  
6. Interactive compliance/industry UX remains PHP-authoritative.

**Until the above are closed + dual-sample + approval: PHP stays primary; do not cut PHP.**

---

## 13. Document control

| Item | Value |
|---|---|
| Document | `docs/PROJECT_SOP_SECURITY_TENANT_ISOLATION.md` |
| Supersedes | Ad-hoc chat instructions for security/isolation |
| Must update when | Isolation model, cutover gates, or secret handling changes |
| Companion | `PROJECT_ARCHITECTURE_INSTRUCTIONS.md`, `TENANT_MIGRATION_SAFETY.md`, `TENANT_SCALE_1000.md` |

**End of SOP.** Violations of tenant isolation or confidentiality are release-blocking defects.
