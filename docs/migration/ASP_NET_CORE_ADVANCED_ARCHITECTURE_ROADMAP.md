# ASP.NET Core Advanced Architecture Roadmap

This roadmap captures the modern ASP.NET Core practices required after the migration foundation is merged. These items are architectural standards and future implementation tracks; they do not remove PHP fallback or approve broad production cutover by themselves.

## 1. Advanced architecture and microservices

- **Clean / Hexagonal Architecture:** Keep domain logic independent from ASP.NET Core, databases, queues, and UI code by using ports/adapters and clear application boundaries.
- **CQRS with MediatR-style pipelines:** Separate commands that mutate state from queries that read state, and route cross-cutting validation, authorization, logging, and telemetry through explicit pipelines.
- **Event-driven systems:** Prefer asynchronous events for internal workflows that do not require immediate response coupling. Per Enterprise BOS law, use **Apache Kafka 4** as the primary messaging platform; RabbitMQ 4 is the documented alternative.
- **Migration rule:** Begin as a modular ASP.NET Core platform beside PHP, then split services only when route parity, data ownership, and operational runbooks are proven.

## 2. Hyper-performance and memory tuning

- **Zero-allocation hot paths:** Use `Span<T>`, `Memory<T>`, and `ReadOnlySpan<char>` for parsing, normalization, and high-frequency transformations after profiling proves value.
- **Object pooling:** Use `ArrayPool<T>` and ASP.NET Core `ObjectPool<T>` for expensive reusable buffers or serializers only where benchmarks show allocation pressure.
- **Benchmarking:** Add BenchmarkDotNet projects for critical tenant-routing, price lookup, catalog normalization, and API-key parsing paths before optimizing.
- **Diagnostics and profiling:** Use `dotnet-trace`, `dotnet-dump`, `dotnet-counters`, and production-safe profiling to investigate memory leaks, thread-pool starvation, GC pressure, and CPU bottlenecks.

## 3. Cloud-native and orchestration readiness

- **Docker:** Add multi-stage Dockerfiles for platform and worker hosts after the diagnostics-only CloudPanel deployment is stable.
- **Kubernetes:** Design manifests for Pods, Services, Ingress, ConfigMaps, Secrets, readiness/liveness probes, and Horizontal Pod Autoscaling only after container images and health checks are stable.
- **Minimal APIs:** Keep endpoint modules small and explicit. Use Minimal APIs for low-overhead route migration where they preserve clarity and testability.
- **Native AOT:** Evaluate Native AOT only for isolated services that do not require unsupported reflection-heavy dependencies; do not make it mandatory for the first migration foundation.

## 4. Advanced data and multi-tenant partitioning

- **Tenant-aware data routing:** Resolve tenant context before database access and keep route/database selection explicit and auditable.
- **Database sharding:** Plan shard maps by tenant tier, region, and load profile. Dedicated databases can be reserved for premium or high-volume tenants while shared clusters can serve lower tiers.
- **Distributed caching:** Use Redis or Garnet for cross-node cache state once ASP.NET Core runs beyond one process.
- **Cache stampede protection:** Add per-key locking, jittered expirations, stale-while-revalidate, and backpressure for hot catalog/price keys.

## 5. Advanced security and zero-trust architecture

- **OAuth2 / OpenID Connect:** Plan centralized identity with OAuth2/OIDC for SSO and secure token exchange. Evaluate Duende IdentityServer or a managed identity provider before implementation.
- **Refresh-token safety:** Require rotation, revocation, replay detection, secure storage, and audit logging for long-lived sessions.
- **Dynamic policy-based authorization:** Replace static role-only checks with data-driven policies that evaluate tenant, company, department, feature entitlement, device/session risk, and contextual constraints.
- **Zero-trust defaults:** Authenticate every request, authorize every resource access, keep least privilege for workers, and never trust tenant identity from an unvalidated route or header.

## Acceptance gates before production route cutover

- Architecture changes must include unit/integration tests and migration parity evidence.
- Performance optimizations must include benchmark or profiler evidence.
- Cloud-native changes must preserve `/health`, readiness, rollback, and PHP fallback behavior.
- Data partitioning changes must include tenant-isolation tests and rollback plans.
- Security changes must include threat-model notes and policy tests.

## 6. Principal-level systems engineering track

These topics are advanced engineering tracks for specialist owners after the foundation is stable. They must be introduced through measured experiments, design reviews, rollback plans, and production-safe observability rather than broad rewrites.

### Operating system and framework internals

- **Roslyn, IL, and compiler behavior:** Inspect generated IL and decompiled binaries with tools such as ILSpy or IL utilities before relying on compiler transformations in performance-sensitive paths, especially async/await state machines and iterator-heavy workflows.
- **Garbage Collector tuning:** Profile Server GC behavior, allocation rates, Large Object Heap fragmentation, and pause times before changing runtime knobs; document tenant-load evidence for any segment, heap, or container memory tuning.
- **Kestrel transport internals:** Keep standard Kestrel HTTP transports for the migration foundation. Any custom transport, Linux `epoll`, or Windows IOCP experiment must be isolated behind benchmark evidence and a reversible deployment flag.

### Low-level zero-allocation and hardware-aware engineering

- **SIMD/vectorization:** Use `System.Runtime.Intrinsics` only for proven hot paths such as report filters, search vectors, or numeric aggregation where BenchmarkDotNet shows material throughput gains.
- **Unmanaged interop:** Treat `NativeMemory.Alloc`, unsafe pointers, and C/C++ interop as restricted techniques requiring code ownership, memory-safety review, leak tests, and fallback managed implementations.
- **CPU cache-aware design:** For high-volume tenant analytics, evaluate data-oriented layouts and contiguous primitives when profiling shows object graph traversal or cache misses dominate request cost.

### Native AOT and low-level compilation operations

- **Trimming and Native AOT:** Evaluate Native AOT for isolated ASP.NET Core services after dependency reflection, serialization, source generation, and dynamic-code compatibility are proven.
- **Cloud footprint measurement:** Require cold-start, resident memory, CPU, and error-rate comparisons before adopting Native AOT for scale-out workloads.

### Reliability and chaos engineering

- **Polly resilience pipelines:** Use policy composition for timeout, retry, circuit-breaker, hedging, bulkhead, and rate-limit behavior so tenant-group failures degrade locally instead of cascading across the platform.
- **Chaos experiments:** Run pod termination, Redis/cache outage, database latency, network partition, and CPU pressure drills only in approved test/staging clusters until rollback and alerting are proven.

### Advanced telemetry, observability, and eBPF

- **OpenTelemetry trace context:** Use `Activity`, `DiagnosticSource`, and OpenTelemetry conventions so request IDs propagate through CloudPanel/Nginx, ASP.NET Core routes, worker queues, database calls, and parity reports.
- **eBPF observability:** Use Linux eBPF only for production-safe, read-only kernel-level visibility into networking, file I/O, CPU scheduling, and tenant resource patterns; never require privileged probes for application correctness.

## Principal-level acceptance gates

- Low-level runtime changes require benchmark evidence, profiler traces, and a rollback plan.
- Unsafe or unmanaged memory code requires ownership, review, leak testing, and managed fallbacks.
- Chaos experiments require isolated environments, explicit blast-radius limits, and verified alerting.
- Kernel/eBPF instrumentation must be optional observability, not a request-processing dependency.
