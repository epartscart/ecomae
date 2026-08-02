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
