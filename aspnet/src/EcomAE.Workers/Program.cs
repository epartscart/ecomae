using EcomAE.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<MigrationWorkerJobCatalog>();
builder.Services.AddSingleton<IMigrationWorkerDryRunEvidenceProvider, MigrationWorkerDryRunEvidenceProvider>();
builder.Services.AddSingleton<IMigrationWorkerJobRunner, MigrationWorkerJobRunner>();
builder.Services.AddSingleton<IMigrationWorkerBatchDryRunReporter, MigrationWorkerBatchDryRunReporter>();
builder.Services.AddSingleton<IMigrationWorkerSchedulePlanner, MigrationWorkerSchedulePlanner>();
builder.Services.AddHostedService<MigrationWorkerPlaceholder>();

var host = builder.Build();
host.Run();
