<#
.SYNOPSIS
  Fills a running API with sample units and incidents in every state.
  Goes through the REST API, so it works with any database provider and follows the dispatch rules
  (a unit can only be on one active incident at a time).

.EXAMPLE
  .\scripts\seed.ps1                                   # API from launch.json (http://localhost:5007)
  .\scripts\seed.ps1 -BaseUrl http://localhost:8080    # API in Docker
  .\scripts\seed.ps1 -Incidents 60 -Units 20
#>
param(
    [string]$BaseUrl = 'http://localhost:5007',
    [int]$Incidents = 40,
    [int]$Units = 25
)

$ErrorActionPreference = 'Stop'

function Invoke-Api([string]$method, [string]$path, $body) {
    $params = @{ Uri = "$BaseUrl$path"; Method = $method }
    if ($null -ne $body) {
        $params.ContentType = 'application/json; charset=utf-8'
        $params.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json))
    }
    Invoke-RestMethod @params
}

try { Invoke-RestMethod "$BaseUrl/health" | Out-Null }
catch { throw "API at $BaseUrl is not running. Start it (F5 or .\scripts\start.ps1) and try again." }

# --- Units --------------------------------------------------------------------------------------

$unitTemplates = @(
    @{ prefix = 'RZP'; type = 'Ambulance' },
    @{ prefix = 'RLP'; type = 'Ambulance' },
    @{ prefix = 'HZS'; type = 'Fire' },
    @{ prefix = 'PCR'; type = 'Police' }
)
$takenCallSigns = @{}
foreach ($u in (Invoke-Api GET '/api/units')) { $takenCallSigns[$u.callSign] = $true }

$number = 100
for ($i = 0; $i -lt $Units; $i++) {
    $template = $unitTemplates[$i % $unitTemplates.Count]
    do { $number++; $callSign = "$($template.prefix) $number" } while ($takenCallSigns.ContainsKey($callSign))
    $takenCallSigns[$callSign] = $true
    Invoke-Api POST '/api/units' @{ callSign = $callSign; type = $template.type } | Out-Null
}

# --- Incidents ----------------------------------------------------------------------------------

$titles = @(
    'Dopravní nehoda, 2 osobní auta', 'Bolest na hrudi', 'Bezvědomí na ulici', 'Pád ze schodů',
    'Požár kontejneru', 'Požár bytu, kouř z okna', 'Dušnost, astmatik', 'Poranění ruky při práci',
    'Srážka chodce s autem', 'Únik plynu', 'Křeče, epileptický záchvat', 'Pád z kola',
    'Alergická reakce po bodnutí', 'Strom spadlý na silnici', 'Rvačka před barem', 'Porod v autě',
    'Otrava houbami', 'Zaklíněná osoba ve vozidle', 'Popálení horkou vodou', 'Kolaps ve sportovní hale'
)
$descriptions = @(
    $null, 'Volající je na místě.', 'Pacient komunikuje.', 'Pacient nekomunikuje.',
    'Na místě je více zraněných.', 'Přístup přes dvůr.', 'Volá svědek události.'
)
$streets = @(
    'Brno, Hlinky 12', 'Brno, Kounicova 5', 'Brno, Masarykova 30', 'Brno, Bašty 2',
    'Žďár nad Sázavou, Nádražní 5', 'Žďár nad Sázavou, Horní 18', 'Jihlava, Masarykovo nám. 1',
    'Jihlava, Žižkova 40', 'Olomouc, Horní nám. 7', 'Blansko, Pražská 12', 'Velké Meziříčí, Náměstí 3',
    'Nové Město na Moravě, Vratislavovo nám. 9', 'D1, km 162 směr Praha', 'Bystřice nad Pernštejnem, Masarykovo nám. 1'
)
$priorities = @('Low', 'Medium', 'Medium', 'High', 'High', 'Critical')
# Target mix of final states; Dispatched/OnScene also need a free unit, otherwise the incident stays New.
$states = @('Closed', 'Closed', 'Closed', 'Closed', 'New', 'New', 'Dispatched', 'Dispatched', 'OnScene', 'OnScene')

$counts = @{ New = 0; Dispatched = 0; OnScene = 0; Closed = 0 }

for ($i = 0; $i -lt $Incidents; $i++) {
    $incident = Invoke-Api POST '/api/incidents' @{
        title       = Get-Random $titles
        description = Get-Random $descriptions
        address     = Get-Random $streets
        priority    = Get-Random $priorities
    }

    $target = Get-Random $states
    if ($target -ne 'New') {
        # Parentheses make Windows PowerShell 5.1 enumerate the JSON array before filtering.
        $free = @((Invoke-Api GET '/api/units') | Where-Object status -eq 'Available')
        if ($free.Count -eq 0) {
            $target = 'New'
        }
        else {
            # Serious incidents sometimes get a second unit.
            $unitCount = if ($incident.priority -in 'High', 'Critical' -and $free.Count -ge 2 -and (Get-Random 3) -eq 0) { 2 } else { 1 }
            foreach ($unit in ($free | Get-Random -Count $unitCount)) {
                Invoke-Api POST "/api/incidents/$($incident.id)/assignments" @{ unitId = $unit.id } | Out-Null
            }
            if ($target -in 'OnScene', 'Closed') {
                Invoke-Api PUT "/api/incidents/$($incident.id)/status" @{ status = 'OnScene' } | Out-Null
            }
            if ($target -eq 'Closed') {
                Invoke-Api PUT "/api/incidents/$($incident.id)/status" @{ status = 'Closed' } | Out-Null
            }
        }
    }
    $counts[$target]++
    Write-Progress -Activity "Seeding $BaseUrl" -Status "$($i + 1) / $Incidents incidents" -PercentComplete (100 * ($i + 1) / $Incidents)
}
Write-Progress -Activity "Seeding $BaseUrl" -Completed

Write-Host "Added $Units units and $Incidents incidents to $BaseUrl"
Write-Host ("  New: {0}  Dispatched: {1}  OnScene: {2}  Closed: {3}" -f $counts.New, $counts.Dispatched, $counts.OnScene, $counts.Closed)
