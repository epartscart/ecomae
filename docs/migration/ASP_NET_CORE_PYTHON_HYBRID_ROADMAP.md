# ASP.NET Core + Python Hybrid Migration Roadmap

Target architecture: ASP.NET Core is the primary web platform and owns routing, identity, authorization, tenant context, operator dashboards, HTML/API contracts, deployment, observability, and cutover control. Python remains a first-class sidecar for workloads where it is stronger or already proven in production, especially price/catalog processing, ingestion, search normalization, data science, and high-volume asynchronous automation.

## Rule of ownership

- ASP.NET Core owns customer-facing and operator-facing application surfaces: storefront pages, CP, ERP, BOS, public APIs, auth policy, tenant routing, and cutover decisions.
- Python owns specialized compute/data workloads when it gives a clear operational advantage: price ingestion, article normalization, catalog enrichment, bulk ETL, ML/scoring, supplier data cleanup, async worker passes, and integrations that benefit from Python libraries.
- PHP is only a temporary compatibility fallback during migration. No new business feature should be implemented in PHP.

## Current Python strengths already present

The existing `pyapi` FastAPI service is already aligned with the hybrid target for price and catalog-adjacent work:

- FastAPI sidecar entrypoint under `/pyapi/*`.
- Price/search normalization with uppercase alphanumeric article keys.
- Storefront part search over `shop_docpart_prices_data`.
- CP/ERP price-list, upload-history, dashboard, and order read endpoints.
- Price ingest and URL refresh worker capabilities.
- Shared database/session compatibility with the legacy platform during migration.

## Keep/build Python where Python is stronger

Use Python for these slices:

1. **Price ingestion and supplier ETL**
   - CSV/XLSX/TXT supplier feeds.
   - Dirty data cleanup and normalization.
   - Bulk transforms before database write.
   - Duplicate detection and supplier confidence scoring.

2. **Catalog/search enrichment**
   - Article normalization.
   - Cross-brand matching.
   - Laximo/catalog cache refresh and enrichment.
   - Search ranking experiments.

3. **Analytics and forecasting**
   - Demand prediction.
   - Low-stock prediction.
   - Supplier price anomaly detection.
   - Margin and inventory optimization.

4. **Async automation**
   - Long-running imports.
   - Batch URL/source refresh.
   - Push notification fan-out.
   - Report generation and export preparation.

5. **AI/ML integrations**
   - Product matching.
   - Description cleanup.
   - Fraud/risk scoring.
   - Support-ticket classification.

## Keep/build ASP.NET Core where ASP.NET Core is stronger

Use ASP.NET Core for these slices:

1. **Primary web/API platform**
   - Storefront pages and API gateway behavior.
   - CP, ERP, and BOS user interfaces.
   - Stable route ownership and endpoint versioning.

2. **Security and tenancy**
   - Authentication and session bridge retirement.
   - Authorization policies.
   - Tenant resolution and tenant-scoped permissions.
   - API key policy enforcement.

3. **Transactional business workflows**
   - Orders.
   - Invoices.
   - Payments.
   - User/account administration.
   - ERP financial flows.
   - BOS privileged admin workflows.

4. **Production cutover control**
   - Route cutover policy.
   - Migration readiness and parity endpoints.
   - Diagnostics-only deployment.
   - Exact-route proxy control and rollback.

## Integration contract between ASP.NET Core and Python

ASP.NET Core should call Python only through explicit internal APIs or queues, never by importing Python into ASP.NET Core processes.

Approved integration patterns:

- HTTP sidecar calls to `http://127.0.0.1:8090/pyapi/*` for synchronous internal reads.
- Queue/job handoff for long-running ingest/enrichment work.
- Shared database tables only where ownership is documented and writes are idempotent.
- Shared trace IDs, request IDs, and tenant IDs across ASP.NET Core and Python logs.

Required headers for internal calls:

- `X-EcomAE-Request-Id`
- `X-EcomAE-Tenant-Id`
- `X-EcomAE-Caller: aspnet-platform`

## Cutover order

1. Keep current Python price/search paths running where they are already stable.
2. Move PHP route ownership to ASP.NET Core first.
3. For price/catalog routes, ASP.NET Core becomes the public API/controller layer and delegates specialized ingest/search/enrichment to Python.
4. Replace PHP cron/setup scripts with either ASP.NET Core workers or Python workers based on the ownership rule above.
5. Remove PHP after route inventory shows zero PHP-only routes and all worker jobs have non-PHP replacements.

## Production rule

The final target is **0% PHP**, not **100% C# only**. The desired production state is:

- ASP.NET Core: primary platform and route owner.
- Python: approved sidecar for price/catalog/data/AI/ETL workloads.
- PHP: removed from request handling, cron jobs, deploy requirements, and runtime dependencies.
