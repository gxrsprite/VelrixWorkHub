[CmdletBinding()]
param(
    [string]$ConnectionString,
    [ValidateSet('PostgreSQL', 'SqlServer')]
    [string]$DatabaseType = 'PostgreSQL'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$probeProject = Join-Path $projectRoot 'tests\VelrixWorkHub.Workflow.PostgreSqlProbe\VelrixWorkHub.Workflow.PostgreSqlProbe.csproj'

if ([string]::IsNullOrWhiteSpace($ConnectionString) -and $DatabaseType -eq 'PostgreSQL') {
    $settingsPath = Join-Path $projectRoot 'src\VelrixWorkHub.Web\appsettings.json'
    $settings = Get-Content -Raw $settingsPath | ConvertFrom-Json
    $ConnectionString = $settings.ConnectionStrings.Default
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw '缺少数据库连接字符串。PostgreSQL 可使用项目配置；SqlServer 必须通过 -ConnectionString 显式提供。'
}

$previous = $env:VELRIX_WORKHUB_POSTGRES_CONNECTION
$previousDatabaseType = $env:VELRIX_WORKHUB_WORKFLOW_PROBE_DATABASE_TYPE
try {
    $env:VELRIX_WORKHUB_POSTGRES_CONNECTION = $ConnectionString
    $env:VELRIX_WORKHUB_WORKFLOW_PROBE_DATABASE_TYPE = $DatabaseType
    dotnet run --project $probeProject --configuration Debug
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL Workflow 探针失败，退出码：$LASTEXITCODE" }
}
finally {
    $env:VELRIX_WORKHUB_POSTGRES_CONNECTION = $previous
    $env:VELRIX_WORKHUB_WORKFLOW_PROBE_DATABASE_TYPE = $previousDatabaseType
}
