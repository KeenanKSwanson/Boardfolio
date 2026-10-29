# Boardfolio on Azure

The production configuration connects to `boardfolio` on `boardfolio-sql-dev.database.windows.net` using the Web App's system-assigned managed identity. No database password is stored in the project. Development continues to use SQLite. This change does not copy local SQLite accounts or data into Azure.

## Azure settings

For App Service `boardfolio-api`:

1. Enable **Identity > System assigned** and save.
2. Keep **Environment variables > ASPNETCORE_ENVIRONMENT** set to `Production` (the default if absent).
3. Remove any old `ConnectionStrings__Boardfolio` app setting or `Boardfolio` connection-string entry that points to SQLite or contains a different authentication method. Azure settings override the shipped production configuration.
4. If you prefer to configure the database in the portal, set these App Service app settings:

| Name | Value |
| --- | --- |
| `Database__Provider` | `SqlServer` |
| `ConnectionStrings__Boardfolio` | `Server=tcp:boardfolio-sql-dev.database.windows.net,1433;Database=boardfolio;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;` |

The Azure SQL server firewall must allow every outbound IP listed for the Web App. Recheck these rules if the App Service plan or outbound IPs change. Your computer's current IP must also be allowed to run migrations locally.

Sign in to the `boardfolio` database as the server's Microsoft Entra administrator and grant the Web App access (already completed for this deployment):

```sql
CREATE USER [boardfolio-api] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [boardfolio-api];
ALTER ROLE db_datawriter ADD MEMBER [boardfolio-api];
```

Do not rerun `CREATE USER` if it already exists. The SQL server's own managed identity is separate from the Web App identity and is not required for this connection setup.

## Create the database tables before publishing

The Web App's read/write identity cannot create schema. Production startup deliberately does not run migrations. Apply the SQL Server migrations once using an administrator or a dedicated deployment identity with schema permissions.

From the project root, on your computer, use your Microsoft Entra administrator sign-in. Interactive authentication opens a sign-in prompt; it is for local migration deployment, not for the hosted API:

```powershell
dotnet tool restore
$env:ConnectionStrings__Boardfolio = 'Server=tcp:boardfolio-sql-dev.database.windows.net,1433;Database=boardfolio;Authentication=Active Directory Interactive;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
try {
    dotnet tool run dotnet-ef database update --project Boardfolio.Api --context SqlServerBoardfolioDbContext
    if ($LASTEXITCODE -ne 0) { throw 'Migration failed. Do not publish the API.' }
}
finally {
    Remove-Item Env:ConnectionStrings__Boardfolio -ErrorAction SilentlyContinue
}
```

For a repeatable release artifact, use the separate bundle workflow:

```powershell
pwsh -File ./scripts/Build-DatabaseDeployment.ps1 -Provider SqlServer -Runtime win-x64 -OutputDirectory ./artifacts/azure-sql-release-001
```

Review `schema-review.sql`, set the deployment connection string in the environment, and run the artifact's `Apply-DatabaseMigrations.ps1`. See [database deployment](database-deployment.md). A migration bundle is specific to its selected provider and context.

## Publish and verify

Publish `Boardfolio.Api` in Release configuration to the existing `boardfolio-api` App Service using Visual Studio's Azure App Service publish target or an authenticated deployment workflow. Publishing files locally alone does not deploy them to Azure.

After deployment, request `GET /api/switches` using the Web App's actual Overview URL. A `200` response containing the seeded Cherry switches verifies an anonymous database query. Then register a test account, sign in, and create/read a build to verify writes and ownership. `/docs` and `/openapi/v1.json` are enabled in the shipped Production configuration. The reference and schema are public. The Test Request button is hidden outside Development to prevent accidental submissions through the documentation; API endpoints remain callable by clients and private operations still require bearer authentication. Local Development keeps the button enabled. Requests submitted locally still write to the database configured for that local API. Set the App Service app setting `ApiDocumentation__Enabled=false` to disable both documentation endpoints in Production.

A sleeping serverless database may take time to resume. If it has exhausted its monthly free allowance and overage billing is disabled, it remains paused until the allowance resets.

App Service's default ASP.NET Core Data Protection key storage persists keys in its HOME directory for the deployment slot. Preserve that storage; moving to a different slot or host requires planning key sharing so existing bearer tokens remain usable.

## Maintaining both providers

Use `--context BoardfolioDbContext` for SQLite migrations and `--context SqlServerBoardfolioDbContext` for SQL Server migrations. Keep both migration sets up to date for shared model changes. SQL Server cost amounts use `decimal(28,8)`; review numeric range and fractional precision before importing existing data.

References: [EF migrations with multiple providers](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers), [passwordless Azure SQL with EF Core](https://learn.microsoft.com/en-us/azure/azure-sql/database/azure-sql-dotnet-entity-framework-core-quickstart), [App Service outbound IPs](https://learn.microsoft.com/en-us/azure/app-service/overview-inbound-outbound-ips).

## Updating the API

For the current catalog pagination update, deploy the prepared ZIP:

```powershell
az login
az webapp deploy --resource-group boardfolio-dev --name boardfolio-api --src-path C:/Dev/Boardfolio/artifacts/boardfolio-api-release-005.zip --type zip
```

For future changes, build into a new output directory so removed files are not carried forward. Run from `C:/Dev/Boardfolio`:

```powershell
$releaseName = 'boardfolio-api-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$publishDirectory = Join-Path (Get-Location) "artifacts/$releaseName"
$packagePath = "$publishDirectory.zip"
dotnet publish Boardfolio.Api/Boardfolio.Api.csproj --configuration Release --output $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Do not deploy.' }
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $packagePath
az webapp deploy --resource-group boardfolio-dev --name boardfolio-api --src-path $packagePath --type zip
if ($LASTEXITCODE -ne 0) { throw 'Deployment failed. Check the Azure deployment logs.' }
```

The catalog pagination update has no database schema changes, so no migrations are required. `/api/switches` now returns a paginated object rather than a raw array; see [mobile catalog caching](switch-catalog.md). For future schema changes, generate/review/apply the SQL Server migrations before deploying the matching API. Publishing the API does not erase Azure SQL data.

After a successful update, visit `/docs`, `/openapi/v1.json`, and `/api/switches` using the Web App's actual default domain. Keep `ASPNETCORE_ENVIRONMENT=Production`; documentation no longer requires Development mode.
