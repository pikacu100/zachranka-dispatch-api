<#
.SYNOPSIS
  Starts the API and database in Docker. Migrations create the database schema
  on API startup; -Seed then fills it with sample units and incidents.

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

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is not installed. Install Docker Desktop (winget install Docker.DockerDesktop), restart, and run this again."
}

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Docker is not running. Start Docker Desktop and wait until it says 'Engine running'."
}

if ($Stop) {
    docker compose --profile sqlserver down
    return
}

if ($Reset) {
    Write-Host "Deleting containers and database volumes..."
    docker compose --profile sqlserver down -v
}

Write-Host "Building and starting containers (the first run downloads images and takes a few minutes)..."
docker compose @profileArgs up -d --build --wait
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed. See: docker compose logs" }

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

    $units = @(
        @{ callSign = 'RZP 101'; type = 'Ambulance' },
        @{ callSign = 'RZP 102'; type = 'Ambulance' },
        @{ callSign = 'RLP 201'; type = 'Ambulance' },
        @{ callSign = 'HZS CAS 30'; type = 'Fire' },
        @{ callSign = 'PCR 301'; type = 'Police' }
    ) | ForEach-Object { Invoke-RestMethod "$baseUrl/api/units" -Method Post -ContentType 'application/json' -Body ($_ | ConvertTo-Json) }

    $incidents = @(
        @{ title = 'Dopravní nehoda, 2 osobní auta'; address = 'Brno, Hlinky 12'; priority = 'High' },
        @{ title = 'Bolest na hrudi, muž 65 let'; address = 'Žďár nad Sázavou, Nádražní 5'; priority = 'Critical' },
        @{ title = 'Požár kontejneru'; address = 'Olomouc, Masarykova 20'; priority = 'Medium' },
        @{ title = 'Pád z kola, odřeniny'; address = 'Jihlava, Masarykovo nám. 1'; priority = 'Low' }
    ) | ForEach-Object { Invoke-RestMethod "$baseUrl/api/incidents" -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes(($_ | ConvertTo-Json))) }

    # A few assignments so the data has every state: Dispatched, OnScene, Closed and New.
    $assign = { param($incident, $unit) Invoke-RestMethod "$baseUrl/api/incidents/$($incident.id)/assignments" -Method Post -ContentType 'application/json' -Body (@{ unitId = $unit.id } | ConvertTo-Json) | Out-Null }
    $status = { param($incident, $s) Invoke-RestMethod "$baseUrl/api/incidents/$($incident.id)/status" -Method Put -ContentType 'application/json' -Body (@{ status = $s } | ConvertTo-Json) | Out-Null }

    & $assign $incidents[0] $units[0]
    & $assign $incidents[0] $units[4]
    & $assign $incidents[1] $units[2]
    & $status $incidents[1] 'OnScene'
    & $assign $incidents[2] $units[3]
    & $status $incidents[2] 'Closed'

    Write-Host "  Seeded $($units.Count) units and $($incidents.Count) incidents into $baseUrl"
}

$apis = @('http://localhost:8080')
if ($SqlServer) { $apis += 'http://localhost:8081' }

foreach ($api in $apis) {
    Wait-Healthy $api
    if ($Seed) { Add-SampleData $api }
}

Write-Host ""
Write-Host "Running:"
Write-Host "  PostgreSQL API: http://localhost:8080/swagger   (DB: localhost:5432, user/password/db = dispatch)"
if ($SqlServer) {
    Write-Host "  MSSQL API:      http://localhost:8081/swagger   (DB: localhost,1433, sa / Dispatch_Passw0rd!)"
}
Write-Host "Stop with: .\scripts\start.ps1 -Stop"
