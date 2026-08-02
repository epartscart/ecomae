namespace EcomAE.Platform.Migration;

public sealed class PythonSidecarCatalogReporter : IPythonSidecarCatalogReporter
{
    public PythonSidecarCatalogReport BuildReport()
    {
        PythonSidecarWorkload[] workloads =
        [
            new(
                "price-ingest",
                "Supplier price ingestion and normalization",
                "python-ai-service",
                "pyapi foundation present; future scope narrowed to independent AI-service processing behind ASP.NET Core",
                "ASP.NET Core owns authenticated routes, validation, database transactions, CRUD writes, and import orchestration; Python returns AI processing results only.",
                "Python is stronger for OCR/document intelligence, ML matching, anomaly detection, AI search, forecasting, and supplier confidence scoring.",
                ["stateless request contract", "no direct frontend access", "ASP.NET-owned persistence", "ASP.NET-to-Python trace correlation"]),
            new(
                "catalog-enrichment",
                "Catalog AI enrichment and search intelligence",
                "python-ai-service",
                "article normalization exists; AI enrichment, AI search, and ranking suggestions remain behind ASP.NET Core",
                "ASP.NET Core owns public catalog API shape, tenant policy, database reads/writes, cache headers, and cutover decisions.",
                "Python is stronger for cross-brand ML matching, AI search, ranking experiments, recommendation models, and enrichment models.",
                ["PHP response parity", "stateless enrichment response", "tenant-safe ASP.NET cache contract", "rollback to PHP route"]),
            new(
                "analytics-forecasting",
                "Demand, stock, supplier, and margin AI analytics",
                "python-ai-service",
                "planned",
                "ASP.NET Core owns dashboards, permissions, export APIs, database transactions, and audit logging.",
                "Python is stronger for forecasting, anomaly detection, notebooks-to-production pipelines, and ML model iteration.",
                ["model/version registry", "read-only database role", "dashboard parity", "alert thresholds"]),
            new(
                "async-automation",
                "Long-running automation and export preparation",
                "aspnet-worker-with-optional-python-helper",
                "worker catalog foundation present; ASP.NET Core orchestrates jobs and may call Python helpers",
                "ASP.NET Core owns schedule visibility, run status APIs, tenant scoping, persistence, and operator controls.",
                "Python is used only for stateless AI-service helpers; ASP.NET Core workers own orchestration and transactions.",
                ["job ownership record", "retry policy", "dead-letter handling", "operator runbook"]),
            new(
                "ai-ml-integrations",
                "AI/ML/LLM matching, document intelligence, and risk scoring",
                "python-ai-service",
                "planned",
                "ASP.NET Core owns request authorization, rate limits, audit trail, and customer-facing responses.",
                "Python is stronger for model serving, vector/data processing, text cleanup, and rapid ML experimentation.",
                ["privacy review", "model quality gate", "fallback behavior", "tenant audit log"])
        ];

        return new PythonSidecarCatalogReport(
            "ASP.NET Core 10 primary platform with Python as an independent AI microservice; PHP removed after parity cutover.",
            "No new PHP features; PHP remains fallback only until each route/job is owned by ASP.NET Core, with Python used only for approved independent AI-service helpers.",
            workloads,
            [
                "ASP.NET Core remains the public route owner and policy boundary.",
                "Python AI services are called only through internal REST APIs or gRPC; Python does not directly access the frontend.",
                "Every Python call carries request ID, tenant ID, and caller identity supplied by ASP.NET Core.",
                "ASP.NET Core owns authentication, authorization, database transactions, CRUD, persistence, and final API responses."
            ],
            [
                "Expose an ASP.NET Core price API facade that owns validation/auth/database access and optionally calls Python for independent AI enrichment.",
                "Add a Python AI-service health/parity check comparing PHP, ASP.NET Core, and pyapi-derived AI results.",
                "Move one supplier import from PHP cron to an ASP.NET Core worker that may call Python for stateless OCR/document intelligence/ML scoring.",
                "Add route inventory ownership tags for aspnet-core, aspnet-with-python-ai-helper, deleted, and php-fallback."
            ]);
    }
}
