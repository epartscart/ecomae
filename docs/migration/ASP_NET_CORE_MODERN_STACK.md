# ASP.NET Core Modern Stack Confirmation

## Stack Components
* **Target Framework:** .NET 10.0 (pins .NET 10 SDK band in `global.json`).
* **Test Engine:** xUnit running under visual studio runner with `Microsoft.NET.Test.Sdk` integration.
* **Worker Services:** Distributed worker host utilizing hosted background jobs and dead-letter lock queues.
* **Web Framework:** ASP.NET Core Minimal APIs / MVC Controllers with customizable middleware pipelines.
