# ASP.NET Core + Python Hybrid Migration Roadmap

Target architecture: ASP.NET Core is the primary web platform and owns routing, identity, authorization, tenant context, operator dashboards, HTML/API contracts, deployment, observability, and cutover control. Python remains a separate stateless microservice only for AI, ML, OCR, image processing, predictive analytics, automation scripts, and data-processing helpers explicitly invoked by ASP.NET Core.

## Rule of ownership

- ASP.NET Core owns customer-facing and operator-facing application surfaces, REST APIs, business logic, authentication, authorization, database access, CRUD operations, file handling, payment integration, logging, background-job orchestration, tenant routing, and cutover decisions.
- Python owns only delegated stateless AI/data-processing work: AI, ML, NLP, recommendations, OCR, image processing, PDF text extraction, predictive analytics, AI report generation, and automation/data-processing helpers.
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

1. **AI/data-processing assistance for price and supplier data**
   - Data cleanup, parsing assistance, duplicate detection, OCR/PDF extraction, and confidence scoring.
   - ASP.NET Core remains responsible for validation, database transactions, CRUD writes, and import orchestration.

2. **Catalog/search AI enrichment**
   - Article normalization.
   - Cross-brand matching.
   - Catalog enrichment suggestions returned to ASP.NET Core.
   - Search ranking experiments.

3. **Analytics and forecasting**
   - Demand prediction.
   - Low-stock prediction.
   - Supplier price anomaly detection.
   - Margin and inventory optimization.

4. **Async automation**
   - Automation/data-processing helpers explicitly invoked by ASP.NET Core.
   - AI report generation and export preparation.
   - ASP.NET Core remains responsible for job orchestration, persistence, and audit logging.

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

- HTTP sidecar calls to `http://127.0.0.1:8090/pyapi/*` for stateless AI/data-processing results.
- Queue/job handoff for long-running ingest/enrichment work.
- No direct Python database CRUD unless an explicit architecture decision grants a tightly scoped exception.
- Shared trace IDs, request IDs, and tenant IDs across ASP.NET Core and Python logs; ASP.NET Core owns final validation and persistence.

Required headers for internal calls:

- `X-EcomAE-Request-Id`
- `X-EcomAE-Tenant-Id`
- `X-EcomAE-Caller: aspnet-platform`

## Cutover order

1. Keep current Python price/search paths running where they are already stable.
2. Move PHP route ownership to ASP.NET Core first.
3. For AI/data-processing needs in price/catalog routes, ASP.NET Core remains the API/controller/database owner and delegates only stateless processing to Python.
4. Replace PHP cron/setup scripts with ASP.NET Core workers; those workers may call Python for stateless AI/data-processing helper work.
5. Remove PHP after route inventory shows zero PHP-only routes and all worker jobs have non-PHP replacements.

## Production rule

The final target is **0% PHP**, not **100% C# only**. The desired production state is:

- ASP.NET Core: primary platform, route owner, API owner, database owner, business-logic owner, and job orchestrator.
- Python: approved stateless AI/data-processing microservice only.
- PHP: removed from request handling, cron jobs, deploy requirements, and runtime dependencies.
