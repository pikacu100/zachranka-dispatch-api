<#
.SYNOPSIS
  Starts the API and database in Docker. Migrations create the database schema
  on API startup; -Seed then fills it with sample data via scripts\seed.ps1.

.EXAMPLE
  .\scripts\start.ps1               # PostgreSQL, http://localhost:8080/swagger
  .\scripts\start.ps1 -Seed         # ...plus sample data
  .\scripts\start.ps1 -SqlServer    # also MSSQL, http://localhost:8081/swagger
  .\scripts\start.ps1 -Reset -Seed  # delete the database volumes and start fresh
  .\scripts\start.ps1 -Stop         # stop everything
#>
param(
    [switch]$SqlServer,
    [switch]$Seed,
    [switch]$Reset,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$profileArgs = @('--profile', 'sqlserver')
if (-not $SqlServer) { $profileArgs = @() }

# Docker writes progress to stderr. With $ErrorActionPreference = 'Stop', Windows PowerShell 5.1
# turns those lines into terminating errors when output is redirected, so run docker with
# 'Continue' and rely on its exit code instead.
function Invoke-Docker {
    $ErrorActionPreference = 'Continue'
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw "docker $($args -join ' ') failed (exit code $LASTEXITCODE)." }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is not installed. Install Docker Desktop (winget install Docker.DockerDesktop), restart, and run this again."
}

$ErrorActionPreference = 'Continue'
docker info *> $null
$dockerRunning = $LASTEXITCODE -eq 0
$ErrorActionPreference = 'Stop'
if (-not $dockerRunning) {
    throw "Docker is not running. Start Docker Desktop and wait until it says 'Engine running'."
}

if ($Stop) {
    Invoke-Docker compose --profile sqlserver down
    return
}

if ($Reset) {
    Write-Host "Deleting containers and database volumes..."
    Invoke-Docker compose --profile sqlserver down -v
}

Write-Host "Building and starting containers (the first run downloads images and takes a few minutes)..."
Invoke-Docker compose @profileArgs up -d --build --wait

function Wait-Healthy([string]$baseUrl) {
    for ($i = 0; $i -lt 60; $i++) {
        try {
            if ((Invoke-RestMethod "$baseUrl/health") -eq 'Healthy') { return }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "API at $baseUrl did not become healthy. See: docker compose logs api"
}

function Add-SampleData([string]$baseUrl) {
    if ((Invoke-RestMethod "$baseUrl/api/units").Count -gt 0) {
        Write-Host "  $baseUrl already has data, skipping seed (use -Reset to start fresh)."
        return
    }
    & "$PSScriptRoot\seed.ps1" -BaseUrl $baseUrl
}

$apis = @('http://localhost:8080')
if ($SqlServer) { $apis += 'http://localhost:8081' }

foreach ($api in $apis) {
    Wait-Healthy $api
    if ($Seed) { Add-SampleData $api }
}

Write-Host ""
Write-Host "Running:"
Write-Host "  PostgreSQL: board http://localhost:8080/   API http://localhost:8080/swagger   (DB localhost:5432, user/password/db = dispatch)"
if ($SqlServer) {
    Write-Host "  MSSQL:      board http://localhost:8081/   API http://localhost:8081/swagger   (DB localhost,1433, sa / Dispatch_Passw0rd!)"
}
Write-Host "Stop with: .\scripts\start.ps1 -Stop"
