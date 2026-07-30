using EcomAE.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<MigrationWorkerJobCatalog>();
builder.Services.AddHostedService<MigrationWorkerPlaceholder>();

var host = builder.Build();
host.Run();
