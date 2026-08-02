using EcomAE.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<MigrationWorkerJobCatalog>();
builder.Services.AddSingleton<ZeroPhpBatchOneWorkerReplacementCatalog>();
builder.Services.AddSingleton<IMigrationWorkerJobRunner, MigrationWorkerJobRunner>();
builder.Services.AddSingleton<IMigrationWorkerSchedulePlanner, MigrationWorkerSchedulePlanner>();
builder.Services.AddHostedService<MigrationWorkerPlaceholder>();

var host = builder.Build();
host.Run();
