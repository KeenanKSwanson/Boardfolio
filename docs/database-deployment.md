# Separate database deployment

Build a migration bundle from the same source revision as the API release. Run it as a separate, blocking deployment job **once per database**, before rolling out the API. Do not run it independently on every API server. Production API startup does not apply migrations; local SQLite Development startup still does. Azure SQL is never migrated on application startup.

For the configured Azure deployment, see [Azure setup](azure-setup.md). Use `-Provider SqlServer` for Azure SQL or `-Provider Sqlite` (the default) for local SQLite. Each artifact contains only the selected context's migration history.

## Build and review

From the repository root, on a build machine with PowerShell 7 and the .NET 10 SDK:

```powershell
dotnet test Boardfolio.Api/Boardfolio.Api.slnx
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
./scripts/Build-DatabaseDeployment.ps1 -Runtime linux-x64 -OutputDirectory ./artifacts/release-001/database
dotnet publish Boardfolio.Api -c Release -o ./artifacts/release-001/api
if ($LASTEXITCODE -ne 0) { throw 'API publish failed' }
```

Use `win-x64` for a Windows deployment host. The script creates a framework-dependent EF bundle (`migrate` or `migrate.exe`), an apply script, a minimal configuration file, and `schema-review.sql`. The target needs PowerShell 7 and the .NET/ASP.NET Core 10 runtime. Restore/build needs access to the required NuGet packages. Existing artifact directories are not overwritten. If building fails, discard that incomplete artifact directory and build to a new one.

The build checks that the model has no changes missing a migration. Review the migration source and generated SQL before promotion; the script does not decide whether a change is destructive. Test the release against a representative staging database, not only an empty one.

`schema-review.sql` describes the complete schema from an empty database. **It is for review, not for execution against an existing database.** SQLite does not support EF's idempotent migration scripts; the bundle selects pending migrations using `__EFMigrationsHistory` instead.

## Apply in the deployment job

1. Take a verified database backup/snapshot and plan a maintenance window if required by the schema change. SQLite migrations can rebuild tables and block writes; stop writers for these deployments.
2. Supply `ConnectionStrings__Boardfolio` through the deployment environment/secret store. For SQLite, use the **absolute** path to the persistent database (for example `Data Source=/srv/boardfolio/boardfolio.db`). Ensure its parent directory exists. A relative path would resolve inside the artifact directory. The bundle must access the same database file as the API. For SQL Server artifacts, provide an explicit `Server` and `Database` using a deployment identity with schema permissions. For local Azure SQL deployment, `Authentication=Active Directory Interactive` can use the Entra administrator account. Do not use the Web App's read/write identity for schema changes.
3. Invoke the packaged script. Keep the bundle and its configuration file together:

```powershell
pwsh -NoProfile -File ./artifacts/release-001/database/Apply-DatabaseMigrations.ps1
if ($LASTEXITCODE -ne 0) { throw 'Stop deployment: database update failed' }
```

4. Only on success, roll out the matching API artifact and verify application health.

The script requires an explicit connection setting, forces Production mode, and propagates bundle failures. It does not print the connection string or pass it as a command-line argument. Use a schema-capable deployment identity for future managed databases, separate from the API runtime identity where supported. EF/provider diagnostic errors may contain database details; restrict access to deployment logs.

Already-applied migrations are skipped. Repeating the step is normally safe after success; a failure still requires investigation before retrying because a multi-migration release can be partially applied. The job has no automatic rollback. Prefer a reviewed forward fix; restoring a backup requires coordinating application version and data recovery. Never rewrite migrations already deployed to shared environments.

## Several servers or databases

- One shared database: serialize migration jobs for that database across releases; run one job, then deploy the API instances. EF's database migration lock is an additional safeguard, not a rollout scheduler.
- Independent databases: run the same release bundle against each database with its own connection setting, backup, and success check. Each has its own migration history.
- SQLite files on separate servers are separate databases, even when their paths are identical. SQLite is the local-development provider. Azure SQL uses the SQL Server provider and the separate `SqlServerBoardfolioDbContext` migrations.

The API has an Azure SQL production configuration, but no automated Azure/GitHub deployment pipeline is configured yet. These scripts provide the build/apply steps to plug into the eventual pipeline; they do not provision hosting or deploy the API.

See [EF Core deployment guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) for migration bundles and controlled production rollout.
