using EcomAE.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<MigrationWorkerPlaceholder>();

var host = builder.Build();
host.Run();
