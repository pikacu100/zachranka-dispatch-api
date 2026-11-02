# Záchranka Dispatch API

Ukázková backendová služba pro dispečink záchranných složek: evidence výjezdů a jednotek, přiřazování jednotek k výjezdům a živé notifikace dispečerům přes SignalR.

*A small dispatch backend for emergency services: incidents, response units, unit assignment and real-time updates over SignalR. ASP.NET Core 9, EF Core with PostgreSQL or MSSQL, Docker, xUnit, GitHub Actions.*

## Technologie

- **ASP.NET Core 9 Web API** (controllery, validace přes DataAnnotations, chyby jako ProblemDetails, Swagger)
- **Entity Framework Core 9** s migracemi pro **PostgreSQL** i **MSSQL**; poskytovatel se volí v konfiguraci
- **SignalR**: hub `/hubs/dispatch` posílá klientům události `IncidentCreated` a `IncidentUpdated`
- **Docker**: multi-stage Dockerfile (běží jako non-root uživatel) a `compose.yaml` s PostgreSQL a volitelně MSSQL
- **xUnit**: integrační testy přes `WebApplicationFactory` nad SQLite v paměti, včetně testu SignalR
- **GitHub Actions**: build a testy, potom sestavení image a smoke test proti skutečnému PostgreSQL i MSSQL

## Spuštění

**Bez Dockeru (SQLite):**

```bash
dotnet run --project src/Dispatch.Api
```

Swagger: http://localhost:5007/swagger

**Docker + PostgreSQL:**

```bash
docker compose up --build
```

Swagger: http://localhost:8080/swagger

**Docker + MSSQL:**

```bash
docker compose --profile sqlserver up --build
```

API nad MSSQL: http://localhost:8081/swagger. Při startu se automaticky aplikují migrace.

**Skript pro Windows** (spustí Docker, počká na API a volitelně naplní ukázková data):

```powershell
.\scripts\start.ps1 -Seed              # PostgreSQL + ukázkové jednotky a výjezdy
.\scripts\start.ps1 -SqlServer -Seed   # navíc MSSQL
.\scripts\start.ps1 -Reset -Seed       # smaže databáze a začne znovu
.\scripts\start.ps1 -Stop
```

**Ukázková data** do běžícího API (desítky výjezdů ve všech stavech a prioritách):

```powershell
.\scripts\seed.ps1                                  # API z launch.json (http://localhost:5007)
.\scripts\seed.ps1 -BaseUrl http://localhost:8080   # API v Dockeru
.\scripts\seed.ps1 -Incidents 60 -Units 30
```

**Testy:**

```bash
dotnet test
```

## API

| Metoda | Cesta | Popis |
|---|---|---|
| GET | `/api/units` | seznam jednotek |
| POST | `/api/units` | nová jednotka (`callSign`, `type`: Ambulance / Fire / Police) |
| GET | `/api/units/{id}` | detail jednotky |
| GET | `/api/incidents` | seznam výjezdů (nejnovější první) |
| POST | `/api/incidents` | nový výjezd (`title`, `description`, `address`, `priority`: Low / Medium / High / Critical) |
| GET | `/api/incidents/{id}` | detail výjezdu včetně přiřazených jednotek |
| POST | `/api/incidents/{id}/assignments` | přiřazení jednotky (`unitId`) |
| PUT | `/api/incidents/{id}/status` | změna stavu výjezdu (`status`) |
| GET | `/health` | stav aplikace včetně připojení k databázi |

### Stavy výjezdu

```
New ──(přiřazení jednotky)──> Dispatched ──> OnScene ──> Closed
 │                                │                        ^
 └────────────(planý poplach)─────┴──────(zrušení)─────────┘
```

- Přiřadit lze jen jednotku ve stavu `Available` a jen k neuzavřenému výjezdu. Jinak API vrátí `409 Conflict`.
- `OnScene` přepne přiřazené jednotky na `OnScene`.
- `Closed` uvolní jednotky zpět na `Available` a zapíše čas uvolnění i uzavření.

## Architektonická rozhodnutí

- **Tenké controllery, logika v servisách** (`IncidentService`, `UnitService`). Pravidla přechodů stavů jsou na jednom místě.
- **Doménové výjimky → ProblemDetails.** `NotFoundException` a `ConflictException` převádí `DomainExceptionHandler` na 404 a 409, controllery tak neřeší chyby ručně.
- **Jeden model, dva databázoví poskytovatelé.** `DispatchDbContext` obsahuje model a z něj dědí `PostgresDispatchDbContext` a `SqlServerDispatchDbContext`, každý s vlastní složkou migrací (`Data/Migrations/Postgres`, `Data/Migrations/SqlServer`). Zbytek aplikace pracuje jen s `DispatchDbContext`.
- **Enumy se ukládají jako text**, takže jsou data v databázi čitelná i bez aplikace.
- **Notifikace přes rozhraní** `IDispatchNotifier`. Servisy nezávisí přímo na SignalR a v testech jde notifikace nahradit.
- **`TimeProvider`** místo `DateTime.Now`, aby šel čas v testech řídit.

## Formátování

Kód formátuje [CSharpier](https://csharpier.com). Ve VS Code a Cursoru se formátuje při každém uložení (rozšíření `csharpier.csharpier-vscode`, nastavení v `.vscode/settings.json`). CI build shodí, pokud něco zformátované není.

```bash
dotnet tool restore
dotnet csharpier format .   # zformátuje vše
dotnet csharpier check .    # jen kontrola, jako v CI
```

## Přidání migrace

```bash
dotnet tool restore
cd src/Dispatch.Api
dotnet ef migrations add <Nazev> --context PostgresDispatchDbContext --output-dir Data/Migrations/Postgres
dotnet ef migrations add <Nazev> --context SqlServerDispatchDbContext --output-dir Data/Migrations/SqlServer
```

## Další kroky

- [ ] Filtrování a stránkování výjezdů (`?status=&priority=&page=`)
- [ ] Optimistic concurrency (row version), aby dva dispečeři nepřiřadili stejnou jednotku současně
- [ ] Autentizace dispečerů (JWT) a role
- [ ] Změna stavu jednotky (např. `OutOfService` při servisu vozidla)
