# Enterprise BOS Cloud Platform - Technology & Architecture Instructions

> **Canonical project law.** Every technical decision in this repository must follow
> these instructions. Track live vs target status in
> `docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md`. Scaffolding notes live in
> `docs/migration/ASPNET_TARGET_STACK_SCAFFOLDING_NOTES.md`. Zero-PHP progress is tracked
> separately and must not be used to claim full Enterprise BOS stack readiness.

These instructions are mandatory for all migration and feature work.

You are building a large-scale Enterprise Business Operating System (BOS) designed for millions of users, thousands of organizations, multi-tenant SaaS operation, cloud-native deployment, high availability, AI capabilities, blockchain integration, enterprise security, and long-term maintainability.

## 1. Core backend platform

Use:

- .NET 10 LTS.
- ASP.NET Core 10.
- C# 14.
- Entity Framework Core 10.

ASP.NET Core is the main application platform and the single source of truth for all enterprise business functionality.

Do not introduce Java Spring Boot, Node.js backend, Go backend, PHP, or other backend frameworks unless explicitly requested.

## 2. ASP.NET Core responsibilities

All enterprise application responsibilities must be implemented in ASP.NET Core.

### Enterprise core

- Business logic.
- Domain services.
- Enterprise workflows.
- Business rules.
- Approval processes.
- Transaction processing.
- Financial logic.
- Organization management.
- Multi-tenancy.
- User management.
- Role management.
- Permission management.

### API layer

- REST APIs.
- GraphQL APIs only when required.
- API versioning.
- API documentation.
- External integrations.
- Webhooks.

### Security

- Authentication.
- Authorization.
- Identity management.
- OAuth 2.1.
- OpenID Connect.
- JWT.
- MFA integration.
- RBAC.
- ABAC.
- Security policies.

### Platform services

- Notifications.
- Background workers.
- Scheduling.
- Audit logging.
- Reporting.
- Document management.
- File management.
- Configuration management.
- Administration portal services.
- Integration services.

ASP.NET Core owns the enterprise application.

## 3. AI platform

Use Python only for AI-related workloads.

Technology:

- Python 3.13+.
- FastAPI.

Python responsibilities:

- Artificial intelligence.
- Machine learning.
- Large language models.
- Generative AI.
- OCR.
- NLP.
- Computer vision.
- Image processing.
- Document intelligence.
- Recommendation systems.
- Predictive analytics.
- Forecasting.
- Data science.
- AI agents.
- AI search.
- Speech processing.

AI services must be independent services.

Communication path:

```text
ASP.NET Core -> Python AI Service
```

Use REST APIs or gRPC.

Rules:

- Python must not contain core business rules.
- Python must not own enterprise transactions.
- Python must not directly control user permissions.
- Python must not directly access the frontend.
- Python must not directly modify business data unless explicitly required by an approved architecture decision.

## 4. Blockchain integration

Blockchain is an integration layer only.

Use blockchain for:

- Smart contracts.
- Immutable records.
- Digital identity verification.
- Asset verification.
- Supply chain tracking.
- Tokenization.
- Digital signatures.
- Blockchain transactions.

Possible technologies:

- Hyperledger Fabric 3.x for enterprise blockchain.
- Solidity smart contracts for EVM blockchain.

Rules:

- Business logic remains in ASP.NET Core.
- Blockchain is not the primary database.
- Blockchain data must be accessed through secure ASP.NET Core-controlled services.
- Frontend must not communicate directly with blockchain nodes.

## 5. Database architecture

Primary database:

- PostgreSQL 17.

Alternative database:

- SQL Server 2025.

Rules:

- ASP.NET Core owns all database operations.
- Entity Framework Core 10 is the primary ORM.
- Database access must follow repository and domain patterns.
- Python services must not directly modify business data.

## 6. Caching

Use Redis 8 for:

- Distributed caching.
- Session management.
- Performance optimization.
- Rate limiting.
- Temporary data.

## 7. Messaging and event architecture

Primary messaging platform:

- Apache Kafka 4.

Alternative messaging platform:

- RabbitMQ 4.

Use messaging for:

- Domain events.
- Async processing.
- Integration events.
- Notifications.
- Long-running workflows.

Implement event-driven architecture where appropriate.

## 8. Search platform

Use OpenSearch 3 for:

- Enterprise search.
- Document search.
- Logs.
- Analytics search.

## 9. File and object storage

Use cloud object storage:

- Azure Blob Storage, or
- Amazon S3.

Self-hosted option:

- MinIO.

Store documents, images, videos, attachments, and backups in object storage.

## 10. Frontend architecture

Frontend options:

- Angular 20 with TypeScript 5.9.
- React 19 with TypeScript 5.9.

Rules:

- Frontend communicates only with ASP.NET Core APIs.
- Frontend must never directly access databases.
- Frontend must never directly access Python services.
- Frontend must never directly access blockchain nodes.

## 11. Architecture principles

Follow:

- Clean Architecture.
- Domain-Driven Design (DDD).
- SOLID principles.
- CQRS where beneficial.
- Repository Pattern.
- Dependency Injection.
- Event-Driven Architecture.
- Modular Architecture.
- API-first design.

Development approach:

- Start with a modular monolith.
- Extract into microservices only when scaling or business boundaries require it.
- Avoid unnecessary microservice complexity.

## 12. Cloud-native requirements

The system must support:

- Docker 28.
- Kubernetes 1.34.
- Horizontal scaling.
- Auto scaling.
- Multi-region deployment.
- High availability.
- Disaster recovery.
- Zero-downtime deployment.
- Blue/green deployment.
- Rolling updates.

## 13. DevOps and deployment

CI/CD options:

- GitHub Actions, or
- Azure DevOps.

Deployment target:

- Kubernetes.
- Helm.
- GitOps.
- Argo CD.

## 14. API gateway

Use:

- YARP (ASP.NET Core Reverse Proxy), or
- Kong Gateway.

Gateway responsibilities:

- Routing.
- Authentication enforcement.
- Rate limiting.
- API policies.

## 15. Observability

Monitoring:

- OpenTelemetry.
- Prometheus.
- Grafana.

Logging:

- Serilog.
- Seq.

Requirements:

- Distributed tracing.
- Metrics.
- Centralized logs.
- Health monitoring.
- Performance monitoring.

## 16. Security requirements

Implement:

- Zero Trust Architecture.
- Encryption at rest.
- Encryption in transit.
- Secure secrets management.
- API rate limiting.
- Audit trails.
- Security logging.
- Vulnerability scanning.

Secrets:

- HashiCorp Vault, or
- Azure Key Vault.

## 17. General technology decision guide

| Requirement | Technology owner |
| --- | --- |
| Business logic | ASP.NET Core 10 |
| Enterprise APIs | ASP.NET Core 10 |
| Database | ASP.NET Core 10 + EF Core 10 |
| Authentication | ASP.NET Core 10 |
| Authorization | ASP.NET Core 10 |
| Workflow engine | ASP.NET Core 10 |
| Financial processing | ASP.NET Core 10 |
| AI | Python 3.13+ |
| Machine learning | Python |
| OCR | Python |
| Computer vision | Python |
| Data science | Python |
| LLM services | Python |
| Smart contracts | Blockchain platform |
| Blockchain transactions | Blockchain layer |

## Final architecture goal

Build a secure, scalable, cloud-native Enterprise BOS platform that is enterprise-grade, multi-tenant, highly available, AI-enabled, blockchain-ready, secure by design, maintainable for decades, and capable of supporting global business operations.

Every technical decision must prioritize:

1. Scalability.
2. Security.
3. Performance.
4. Maintainability.
5. Extensibility.
6. Enterprise reliability.
