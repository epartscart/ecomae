# ASP.NET Core + Python Hybrid Migration Roadmap

Target architecture: ASP.NET Core is the primary web platform and owns routing, identity, authorization, tenant context, operator dashboards, HTML/API contracts, deployment, observability, and cutover control. Python remains a separate independent AI microservice only for AI, ML, LLMs, generative AI, OCR, NLP, computer vision, image processing, document intelligence, recommendation systems, predictive analytics, forecasting, data science, AI agents, AI search, and speech processing.

## Rule of ownership

- ASP.NET Core 10 owns customer-facing and operator-facing application surfaces, REST APIs, GraphQL when required, business logic, authentication, authorization, identity, database access through EF Core 10, CRUD operations, file/document handling, payment and financial processing, notifications, reporting, audit logs, background workers, scheduling, tenant routing, and cutover decisions.
- Python owns only delegated AI-service work: AI, ML, LLMs, generative AI, NLP, recommendations, OCR, computer vision, image processing, document intelligence, predictive analytics, forecasting, data science, AI agents, AI search, and speech processing.
- PHP is only a temporary compatibility fallback during migration. No new business feature should be implemented in PHP.

## Current Python surface (legacy — not AI-only compliant)

Existing `pyapi` / `pyprices` FastAPI helpers include **business** endpoints (search, prices, orders, dashboard, ingest writes). Under Enterprise BOS law these are **temporary legacy**, not the target hybrid:

- Keep them only until ASP.NET Core exact-route parity replaces them.
- Do **not** expand Python business APIs, transactions, or permission control.
- New Python work must be AI/ML/OCR/LLM/vision/data-science sidecars called by ASP.NET Core only.
- FastAPI remains acceptable for those AI sidecars under `/pyapi/*` when ownership is AI-only.

## Keep/build Python where Python is stronger

Use Python only for these AI-service slices:

1. **AI assistance for price and supplier intelligence**
   - AI matching, anomaly detection, OCR/document extraction, supplier confidence scoring, and predictive analytics.
   - ASP.NET Core 10 remains responsible for validation, database transactions, CRUD writes, import orchestration, and final decisions.

2. **Catalog/search AI enrichment**
   - Article normalization.
   - Cross-brand matching.
   - AI enrichment suggestions returned to ASP.NET Core.
   - AI search and ranking experiments.

3. **AI analytics and forecasting**
   - Demand prediction.
   - Low-stock prediction.
   - Supplier price anomaly detection.
   - Margin and inventory optimization.

4. **Async automation**
   - AI agents and AI-assisted automation explicitly invoked by ASP.NET Core.
   - AI report generation and document intelligence.
   - ASP.NET Core remains responsible for job orchestration, persistence, audit logging, and business outcomes.

5. **AI/ML/LLM integrations**
   - Product matching.
   - Description cleanup.
   - Fraud/risk scoring.
   - Support-ticket classification.

## Keep/build ASP.NET Core where ASP.NET Core is stronger

Use ASP.NET Core 10 for these slices:

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

ASP.NET Core should call Python AI services only through explicit internal REST APIs or gRPC, never by importing Python into ASP.NET Core processes.

Approved integration patterns:

- Internal REST calls to Python/FastAPI services for stateless AI results.
- ASP.NET Core worker orchestration that may call Python AI services for long-running AI processing.
- No direct Python database CRUD unless an explicit architecture decision grants a tightly scoped exception.
- Shared trace IDs, request IDs, and tenant IDs across ASP.NET Core and Python logs; ASP.NET Core owns final validation and persistence.

Required headers for internal calls:

- `X-EcomAE-Request-Id`
- `X-EcomAE-Tenant-Id`
- `X-EcomAE-Caller: aspnet-platform`

## Cutover order

1. Keep current Python price/search paths running where they are already stable.
2. Move PHP route ownership to ASP.NET Core first.
3. For AI needs in price/catalog routes, ASP.NET Core remains the API/controller/database owner and delegates only stateless AI inference/processing to Python.
4. Replace PHP cron/setup scripts with ASP.NET Core workers; those workers may call Python for stateless AI-service work only.
5. Remove PHP after route inventory shows zero PHP-only routes and all worker jobs have non-PHP replacements.

## Production rule

The final target is **0% PHP**, not **100% C# only**. The desired production state is:

- ASP.NET Core: primary platform, route owner, API owner, database owner, business-logic owner, and job orchestrator.
- Python: approved independent AI microservice only.
- PHP: removed from request handling, cron jobs, deploy requirements, and runtime dependencies.
