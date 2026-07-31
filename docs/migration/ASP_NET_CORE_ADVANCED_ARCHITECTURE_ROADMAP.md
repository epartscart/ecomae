# ASP.NET Core Advanced Architecture Roadmap

## Core Architecture Design
1. **Multi-Tenant Context Middleware:** Every request resolves `TenantContext` containing `Host`, `Path`, `Surface`, and `TenantMode`.
2. **Stateless API Routing:** Unified routing under `EcomAeRoutes` to handle request matching, normalization, and logging.
3. **Database-Backed Parity Repositories:** Standard SQL mapping to transition from mock providers to MariaDB/MySQL.

## Future Milestones
* Active MySQL tenant registry integration.
* Distributed locking and stateful background jobs with dead-letter retry policies.
* full CP, ERP, and BOS functional ports under unified ASP.NET Core controllers.
