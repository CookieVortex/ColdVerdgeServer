# ColdVerdge Server

ASP.NET Core backend for ColdVerdge with PostgreSQL and Entity Framework Core.

## Requirements

- .NET SDK 10.0.302 or compatible 10.0 patch
- Docker Desktop
- Git

## First local start

```powershell
Copy-Item .env.example .env
notepad .env

docker compose up -d
dotnet tool restore
dotnet restore
dotnet build
dotnet tool run dotnet-ef database update --project src/ColdVerdge.Infrastructure --startup-project src/ColdVerdge.Api
dotnet run --project src/ColdVerdge.Api
```

The API listens locally on `http://localhost:5153` with the current launch profile.

Health check:

```text
http://localhost:5153/api/health
```

## Project layout

```text
src/ColdVerdge.Api             HTTP API and controllers
src/ColdVerdge.Application     Application layer
src/ColdVerdge.Domain          Domain entities
src/ColdVerdge.Infrastructure  EF Core, PostgreSQL, migrations
tests/ColdVerdge.Tests         Automated tests
```

## Database migrations

Create a migration:

```powershell
dotnet tool run dotnet-ef migrations add MigrationName --project src/ColdVerdge.Infrastructure --startup-project src/ColdVerdge.Api --output-dir Persistence/Migrations
```

Apply migrations:

```powershell
dotnet tool run dotnet-ef database update --project src/ColdVerdge.Infrastructure --startup-project src/ColdVerdge.Api
```

## Git workflow

Initialize and create the first commit:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/Initialize-Git.ps1
```

Connect a private GitHub repository:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/Initialize-Git.ps1 -RemoteUrl "https://github.com/YOUR_NAME/ColdVerdgeServer.git"
```

For later changes:

```powershell
git status
git add .
git commit -m "Describe the server change"
git push
```

## Secrets

Do not commit `.env`, database passwords, AWS credentials, JWT keys, certificates, or production connection strings. Local ASP.NET secrets are stored through `dotnet user-secrets` and are not part of this repository.
