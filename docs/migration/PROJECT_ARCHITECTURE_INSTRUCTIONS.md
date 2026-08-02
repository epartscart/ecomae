# Project Architecture Instructions

These instructions are mandatory for future migration and feature work.

## Primary rule

Use **ASP.NET Core** as the primary backend and use **Python** only for AI, machine-learning, OCR, image processing, predictive analytics, automation scripts, and data-processing tasks that are explicitly delegated by ASP.NET Core.

ASP.NET Core is the main application. Python is an independent stateless AI/data-processing microservice.

## Use ASP.NET Core for

- User authentication and authorization with JWT/Identity or the approved migration bridge.
- REST APIs and API gateway behavior.
- Business logic and transactional workflows.
- Database access through Entity Framework Core or approved .NET data-access boundaries.
- CRUD operations.
- File uploads and downloads.
- Payment integration.
- Role and permission management.
- API validation.
- Logging and audit trails.
- Background jobs and job orchestration.
- Security controls.
- Frontend communication.
- Tenant routing and tenant-scoped policy enforcement.
- Production cutover, rollback, parity, and diagnostics control.

## Use Python for

- Artificial intelligence (AI).
- Machine-learning models.
- Natural-language processing (NLP).
- Recommendation engines.
- Data analysis.
- Image processing.
- OCR.
- PDF text extraction.
- AI-assisted report generation.
- Predictive analytics.
- Automation scripts and data-processing helpers explicitly invoked by ASP.NET Core.

## Communication rules

- Keep Python as a separate microservice.
- ASP.NET Core calls Python through REST APIs or gRPC.
- Python does not directly access the frontend.
- ASP.NET Core manages authentication, authorization, tenant policy, API validation, and database transactions.
- Python is stateless and returns only processing results.
- Python calls must include request correlation and tenant context supplied by ASP.NET Core.

Required service-call metadata:

- `X-EcomAE-Request-Id`
- `X-EcomAE-Tenant-Id`
- `X-EcomAE-Caller: aspnet-platform`

## Database rules

- ASP.NET Core owns the database.
- ASP.NET Core owns schema migrations, database transactions, and CRUD writes.
- Python receives only the required input data from ASP.NET Core.
- Python returns computed/processed results to ASP.NET Core.
- Python must not perform CRUD operations unless a specific architecture decision record explicitly grants that exception.
- Any exception must define idempotency, retry, audit, rollback, and least-privilege database access.

## Decision matrix

| Feature type | Runtime owner |
| --- | --- |
| Business logic | ASP.NET Core |
| Authentication | ASP.NET Core |
| Authorization and roles | ASP.NET Core |
| Database access and transactions | ASP.NET Core |
| CRUD operations | ASP.NET Core |
| REST APIs | ASP.NET Core |
| API gateway | ASP.NET Core |
| Payment integration | ASP.NET Core |
| File upload/download | ASP.NET Core |
| Logging and audit | ASP.NET Core |
| Background job orchestration | ASP.NET Core |
| Frontend communication | ASP.NET Core |
| AI/ML | Python microservice |
| NLP | Python microservice |
| OCR | Python microservice |
| Image processing | Python microservice |
| Data science and predictive analytics | Python microservice |
| AI report generation | Python microservice |
| Automation/data-processing helper | Python microservice when explicitly invoked by ASP.NET Core |

## Clean architecture rule

Design every feature so ASP.NET Core remains the system of record and application boundary. Python may process data, infer, classify, extract, score, enrich, or recommend, but ASP.NET Core validates the request, authorizes the user, controls database transactions, and returns the final response to the frontend.
