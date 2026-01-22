# Introduction

- Implements the lifestyle checker as described at: https://github.com/airelogic/tech-test-portal/tree/main/T2-Lifestyle-Checker

## Development deployment steps:
- Configure host and port in appsettings.json
- For VSCode development copy files in VsCodeConfig into .vscode folder in project root. 
- To debug using HTTP ensure the environment variable "ASPNETCORE_ENVIRONMENT": "Development"

```powershell
dotnet test Test/HealthTest.Test/HealthTest.Test.csproj
```
or in VS Code 
Run Task: Terminal → Run Task → test

```powershell
dotnet run
```

## Production deployment steps
IMPORTANT: The questionare involves personal sensitive data. It should only be used behind HTTPS 
1) Install prerequisites: IIS (Web Server role) + .NET 8 Hosting Bundle on the Windows server.
2) Publish the app: dotnet publish -c Release -o ./publish.
3) Ensure web.config/ANCM present (publish normally adds it); choose InProcess hosting (recommended) or OutOfProcess (IIS as reverse proxy to Kestrel).
4) Create an IIS Site pointing to the publish folder, set App Pool to No Managed Code and proper identity/permissions.

For in-process hosting (best performance), you can ensure the csproj includes (optional — publish usually configures this automatically):
```
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>
</PropertyGroup>
```

Hello Kestrel web app (minimal)

Files added:
- [Program.cs](Program.cs)
- [appsettings.cs](appsettings.cs)
- [HealthTest.csproj](HealthTest.csproj)

Run:

```powershell
dotnet run
```

Configuration:
- Change host/port in `appsettings.cs` (the embedded JSON string) under `Host` and `Port`.


