#Requires -Version 7.0
[CmdletBinding()]
param(
    [string] $ArtifactDirectory = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Boardfolio)) {
    throw 'Set ConnectionStrings__Boardfolio from deployment configuration before applying migrations. No default database is allowed.'
}
$artifactPath = (Resolve-Path -LiteralPath $ArtifactDirectory).Path
$providerFile = Join-Path $artifactPath 'database-provider.txt'
$provider = if (Test-Path -LiteralPath $providerFile) { (Get-Content -LiteralPath $providerFile -Raw).Trim() } else { 'Sqlite' }
if ($provider -notin @('Sqlite', 'SqlServer')) { throw 'Invalid database provider in deployment artifact.' }
# Do not silently create a SQLite database relative to the bundle.
$settings = [System.Data.Common.DbConnectionStringBuilder]::new()
try { $settings.ConnectionString = $env:ConnectionStrings__Boardfolio }
catch { throw 'ConnectionStrings__Boardfolio is not a valid connection string.' }
if ($provider -eq 'Sqlite') {
    $databasePath = $null
    foreach ($key in @('Data Source', 'DataSource', 'Filename')) {
        if ($settings.ContainsKey($key)) { $databasePath = [string]$settings[$key]; break }
    }
    if ([string]::IsNullOrWhiteSpace($databasePath) -or -not [System.IO.Path]::IsPathFullyQualified($databasePath) -or
        ($settings.ContainsKey('Mode') -and $settings['Mode'] -eq 'Memory')) {
        throw 'This SQLite migration step requires an absolute database file path. Relative and in-memory targets are not allowed.'
    }
    if (-not (Test-Path -LiteralPath (Split-Path $databasePath -Parent) -PathType Container)) {
        throw 'The database parent directory must exist before deployment.'
    }
} else {
    $server = if ($settings.ContainsKey('Server')) { $settings['Server'] } elseif ($settings.ContainsKey('Data Source')) { $settings['Data Source'] } else { $null }
    $database = if ($settings.ContainsKey('Database')) { $settings['Database'] } elseif ($settings.ContainsKey('Initial Catalog')) { $settings['Initial Catalog'] } else { $null }
    if ([string]::IsNullOrWhiteSpace($server) -or [string]::IsNullOrWhiteSpace($database)) {
        throw 'SQL Server migrations require an explicit Server and Database in ConnectionStrings__Boardfolio.'
    }
}
$bundleName = if ($IsWindows) { 'migrate.exe' } else { 'migrate' }
$bundle = Join-Path $artifactPath $bundleName
if (-not (Test-Path -LiteralPath $bundle -PathType Leaf)) {
    throw "Migration bundle not found: $bundle"
}

$previousEnvironment = $env:DOTNET_ENVIRONMENT
$previousAspEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousProvider = $env:Database__Provider
Push-Location $artifactPath
try {
    $env:DOTNET_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:Database__Provider = $provider
    # The bundle reads configuration from the environment. Do not expose secrets in arguments.
    & $bundle
    if ($LASTEXITCODE -ne 0) {
        throw "Database migration failed (exit $LASTEXITCODE). Stop the API rollout and investigate."
    }
    Write-Host 'Database migrations completed successfully. The API rollout may continue.'
}
finally {
    $env:DOTNET_ENVIRONMENT = $previousEnvironment
    $env:ASPNETCORE_ENVIRONMENT = $previousAspEnvironment
    $env:Database__Provider = $previousProvider
    Pop-Location
}
