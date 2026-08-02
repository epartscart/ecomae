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
                "python-ai-data-sidecar",
                "pyapi foundation present; future scope narrowed to stateless processing behind ASP.NET Core",
                "ASP.NET Core owns authenticated routes, validation, database transactions, CRUD writes, and import orchestration; Python returns processing results only.",
                "Python is stronger for OCR/PDF extraction, data cleanup suggestions, duplicate detection, ML matching, and supplier confidence scoring.",
                ["stateless request contract", "no direct frontend access", "ASP.NET-owned persistence", "ASP.NET-to-Python trace correlation"]),
            new(
                "catalog-enrichment",
                "Catalog enrichment and search normalization",
                "python-ai-data-sidecar",
                "article normalization exists; AI enrichment and ranking suggestions remain behind ASP.NET Core",
                "ASP.NET Core owns public catalog API shape, tenant policy, database reads/writes, cache headers, and cutover decisions.",
                "Python is stronger for article cleanup suggestions, cross-brand ML matching, ranking experiments, and enrichment models.",
                ["PHP response parity", "stateless enrichment response", "tenant-safe ASP.NET cache contract", "rollback to PHP route"]),
            new(
                "analytics-forecasting",
                "Demand, stock, supplier, and margin analytics",
                "python-ai-data-sidecar",
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
                "Python is used only for stateless automation/data-processing helpers; ASP.NET Core workers own orchestration and transactions.",
                ["job ownership record", "retry policy", "dead-letter handling", "operator runbook"]),
            new(
                "ai-ml-integrations",
                "AI/ML matching, cleanup, and risk scoring",
                "python-ai-data-sidecar",
                "planned",
                "ASP.NET Core owns request authorization, rate limits, audit trail, and customer-facing responses.",
                "Python is stronger for model serving, vector/data processing, text cleanup, and rapid ML experimentation.",
                ["privacy review", "model quality gate", "fallback behavior", "tenant audit log"])
        ];

        return new PythonSidecarCatalogReport(
            "ASP.NET Core primary platform with Python as a stateless AI/data-processing microservice; PHP removed after parity cutover.",
            "No new PHP features; PHP remains fallback only until each route/job is owned by ASP.NET Core, with Python used only for approved stateless AI/data-processing helpers.",
            workloads,
            [
                "ASP.NET Core remains the public route owner and policy boundary.",
                "Python microservices are called only through internal REST APIs or gRPC; Python does not directly access the frontend.",
                "Every Python call carries request ID, tenant ID, and caller identity supplied by ASP.NET Core.",
                "ASP.NET Core owns authentication, authorization, database transactions, CRUD, persistence, and final API responses."
            ],
            [
                "Expose an ASP.NET Core price API facade that owns validation/auth/database access and optionally calls Python for stateless AI enrichment.",
                "Add a Python helper health/parity check comparing PHP, ASP.NET Core, and pyapi-derived processing results.",
                "Move one supplier import from PHP cron to an ASP.NET Core worker that may call Python for stateless parsing/OCR/ML scoring.",
                "Add route inventory ownership tags for aspnet-core, aspnet-with-python-helper, deleted, and php-fallback."
            ]);
    }
}
