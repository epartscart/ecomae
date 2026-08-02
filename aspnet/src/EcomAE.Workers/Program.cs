using EcomAE.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<MigrationWorkerJobCatalog>();
builder.Services.AddSingleton<ZeroPhpBatchOneWorkerReplacementCatalog>();
builder.Services.AddSingleton<ZeroPhpBatchOneWorkerReplacementRunner>();
builder.Services.AddSingleton<ZeroPhpBatchOneWorkerParityReporter>();
builder.Services.AddSingleton<ZeroPhpBatchOneWorkerDryRunEvidenceManifest>();
builder.Services.AddSingleton<ZeroPhpBatchOneWorkerDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerDryRunEvidenceProvider, MigrationWorkerDryRunEvidenceProvider>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, PriceImportDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, SitemapDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, BackupDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, NotificationsDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, ErpReportsDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, CurrencyLiveRatesDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, DemoExpireDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, PlatformJobsDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, SeoSitemapPingDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, SeoSitemapWarmDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, UaeTaxLegislationDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, ApaiBackgroundJobsDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, FulfillmentQueueDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, ApaiSyncCategoriesDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, IntegrationsCleanupDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, ProductExistLimitDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, CacheWarmupDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobDryRunExecutor, ImportOrchestratorDryRunExecutor>();
builder.Services.AddSingleton<IMigrationWorkerJobRunner>(sp => new MigrationWorkerJobRunner(
    sp.GetRequiredService<MigrationWorkerJobCatalog>(),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<IMigrationWorkerDryRunEvidenceProvider>(),
    sp.GetServices<IMigrationWorkerJobDryRunExecutor>()));
builder.Services.AddSingleton<IMigrationWorkerBatchDryRunReporter, MigrationWorkerBatchDryRunReporter>();
builder.Services.AddSingleton<IMigrationWorkerSchedulePlanner, MigrationWorkerSchedulePlanner>();
builder.Services.AddHostedService<MigrationWorkerPlaceholder>();

var host = builder.Build();
host.Run();
