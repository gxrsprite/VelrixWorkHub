[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [string]$Database = 'VelrixWorkHub_Probe',
    [string]$ConnectionString,
    [switch]$KeepDatabase
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw '缺少 sqlcmd，无法创建 SQL Server 临时回归数据库。'
}

if ($Database -notmatch '^[A-Za-z0-9_]+$') {
    throw '临时数据库名只允许使用字母、数字和下划线。'
}

$createdDatabase = $false
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $databaseId = sqlcmd -S $Server -E -d master -h -1 -W -Q "SELECT DB_ID(N'$Database')" -b | Out-String
    if ($databaseId.Trim() -notmatch '^\d+$') {
        sqlcmd -S $Server -E -d master -Q "CREATE DATABASE [$Database]" -b
        $createdDatabase = $true
    }
    $ConnectionString = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
}

try {
    & (Join-Path $projectRoot 'scripts\test-workflow-postgresql.ps1') -DatabaseType SqlServer -ConnectionString $ConnectionString
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    if ($createdDatabase -and -not $KeepDatabase) {
        sqlcmd -S $Server -E -d master -Q "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END" -b
    }
}
