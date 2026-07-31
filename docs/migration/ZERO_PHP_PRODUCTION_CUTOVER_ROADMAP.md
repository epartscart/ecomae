# Zero-PHP Production Cutover Roadmap

## Cutover Principles
1. **Never Broad Proxy First:** Always start diagnostics-only by proxying only `/health` and `/migration/*`.
2. **Never Disable Fallback:** Production deployment must maintain active PHP fallback (`RequirePhpFallback=true`).
3. **Stateless API First:** The first exact match route group to cut over is Group 1: public stateless lookup and catalog status APIs.

## Phased Cutover Sequence
* **Phase 1: Diagnostics-Only** (Expose `/health` and `/migration/*` only).
* **Phase 2: Shadow Public APIs** (Shadow `/api/v1` routes with complete telemetry logging).
* **Phase 3: Parity Verification** (Validate DB-backed price offer lookups).
* **Phase 4: Privileged CP/ERP/BOS Shells** (Scoped/isolated routes transition).
* **Phase 5: Storefront and Workers** (Distributed locking and final cutover).
