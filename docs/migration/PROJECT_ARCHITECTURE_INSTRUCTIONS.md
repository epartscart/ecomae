# EcomAE Enterprise BOS Migration Architecture Instructions

## Target platform ownership

- **ASP.NET Core 10 / .NET 10 LTS / C# 14 / EF Core 10** is the primary enterprise backend and the single source of truth for business functionality.
- **Python 3.13+ FastAPI** is allowed only as an independent AI microservice for AI, ML, LLM, OCR, NLP, computer vision, document intelligence, recommendations, forecasting, AI search, and speech workloads.
- **PHP is temporary fallback only** until each exact route and job has ASP.NET parity evidence, smoke evidence, rollback evidence, and cutover approval.

## Ownership boundaries

ASP.NET Core owns business logic, domain services, workflows, transactions, financial logic, multi-tenancy, users, roles, permissions, REST APIs, optional GraphQL, auth, authorization, identity, OAuth/OpenID/JWT/MFA/RBAC/ABAC, background workers, scheduling, reporting, audit logs, document/file services, integrations, configuration, administration, monitoring, and health checks.

Python must not own enterprise transactions, user permissions, frontend access, or direct business database CRUD unless explicitly approved for an AI-only use case.

Frontend clients must communicate with ASP.NET Core, not PHP or Python. Blockchain integrations, if present, are external integrations only; business decisions remain in ASP.NET Core.

## Migration gates

Do not claim 90%, 95%, or 100% completion until the readiness scripts pass with production evidence. Do not remove PHP fallback until all tracked PHP route/job items are live on ASP.NET Core or formally removed with rollback approval and production smoke results.

Current truthful zero-PHP status is recorded in `docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md`.
