#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $OutputDirectory,
    [Parameter(Mandatory)][ValidateSet('win-x64', 'linux-x64', 'linux-arm64', 'osx-arm64')][string] $Runtime,
    [ValidateSet('Sqlite', 'SqlServer')][string] $Provider = 'Sqlite'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'Boardfolio.Api/Boardfolio.Api.csproj'
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) {
    throw 'Choose a new output directory for this release; existing deployment artifacts will not be overwritten.'
}

function Invoke-DotNet([string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Database deployment build failed (exit $LASTEXITCODE). Do not deploy this output directory." }
}

$previousEnvironment = $env:DOTNET_ENVIRONMENT
$previousAspEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousConnection = $env:ConnectionStrings__Boardfolio
$previousProvider = $env:Database__Provider
$context = if ($Provider -eq 'SqlServer') { 'SqlServerBoardfolioDbContext' } else { 'BoardfolioDbContext' }
Push-Location $repoRoot
try {
    # Design-time tooling only: no application startup migration or database update.
    $env:DOTNET_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:Database__Provider = $Provider
    $env:ConnectionStrings__Boardfolio = if ($Provider -eq 'SqlServer') {
        'Server=localhost;Database=BoardfolioDesignTime;Integrated Security=True;Encrypt=True'
    } else { 'Data Source=:memory:' }
    New-Item -ItemType Directory -Path $output | Out-Null
    Invoke-DotNet -Arguments @('tool', 'restore')
    Invoke-DotNet -Arguments @('ef', 'migrations', 'has-pending-model-changes', '--project', $project, '--context', $context, '--configuration', 'Release')
    Invoke-DotNet -Arguments @('ef', 'migrations', 'script', '0', '--project', $project, '--context', $context, '--configuration', 'Release', '--no-build', '--output', (Join-Path $output 'schema-review.sql'))
    $bundleName = if ($Runtime.StartsWith('win-')) { 'migrate.exe' } else { 'migrate' }
    Invoke-DotNet -Arguments @('ef', 'migrations', 'bundle', '--project', $project, '--context', $context, '--configuration', 'Release', '--no-build', '--target-runtime', $Runtime, '--output', (Join-Path $output $bundleName))
    $Provider | Set-Content -LiteralPath (Join-Path $output 'database-provider.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Apply-DatabaseMigrations.ps1') -Destination $output
    # The EF bundle expects appsettings.json alongside it. Never copy development secrets.
    '{}' | Set-Content -LiteralPath (Join-Path $output 'appsettings.json')
    Write-Host "Database deployment artifacts built in $output"
}
finally {
    $env:DOTNET_ENVIRONMENT = $previousEnvironment
    $env:ASPNETCORE_ENVIRONMENT = $previousAspEnvironment
    $env:ConnectionStrings__Boardfolio = $previousConnection
    $env:Database__Provider = $previousProvider
    Pop-Location
}

