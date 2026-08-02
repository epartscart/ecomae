# ASP.NET Core Modern Stack Confirmation

This migration targets ASP.NET Core, not legacy ASP.NET / System.Web / .NET Framework.

## Required runtime direction

- Use ASP.NET Core hosted by Kestrel behind CloudPanel/Nginx.
- Target the SDK band pinned by `aspnet/global.json` and project target frameworks under `aspnet/`.
- Keep PHP as the authoritative fallback until parity and route-level cutover evidence is approved.
- Use exact-route proxying only; do not add broad catch-all cutovers for `/api`, `/cp`, `/erp`, `/bos`, or storefront routes.

## Explicitly out of scope

- Do not introduce legacy ASP.NET Web Forms, MVC 5, Web API 2, System.Web, Global.asax, Web.config application hosting, or .NET Framework-only libraries.
- Do not deploy under IIS as the primary hosting assumption for this CloudPanel rollout.
- Do not rename the work to plain "ASP.NET" in operator-facing migration docs; use "ASP.NET Core" for clarity.

## Verification expectations

- `aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj` must remain an ASP.NET Core web SDK project.
- `aspnet/src/EcomAE.Workers/EcomAE.Workers.csproj` must remain a modern .NET worker project.
- `tests/aspnet_migration/run_foundation_checks.sh` must keep checking the ASP.NET Core deployment/runbook artifacts and proxy guardrails.
