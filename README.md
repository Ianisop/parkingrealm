# parkingrealm

ParkingRealm består af en ASP.NET Core minimal API (`ParkingRealm/`) med SQL Server via Entity Framework Core og Identity, og en React-frontend (`parking-realm/`).

## Forudsætninger

- .NET 10 SDK
- Node.js og npm
- SQL Server LocalDB (følger med Visual Studio) eller en anden SQL Server. Standardforbindelsen i `ParkingRealm/appsettings.Development.json` bruger `(localdb)\MSSQLLocalDB`.
- EF Core-værktøjet installeres første gang lokalt fra repoets rod: `dotnet tool install dotnet-ef --tool-path .tools --version 10.0.14`

## Database

Databasen oprettes ud fra C#-modellerne med EF Core migrations. Kør fra roden af repoet:

```powershell
.\.tools\dotnet-ef.exe database update --project ParkingRealm
```

Ny migration efter en modelændring, fra repoets rod: `.\.tools\dotnet-ef.exe migrations add <Navn> --project ParkingRealm --output-dir Data/Migrations`.

Brug user-secrets eller miljøvariablen `ConnectionStrings__DefaultConnection` til en anden forbindelsesstreng. Læg aldrig adgangskoder i `appsettings*.json`.

## Kør API'et

```powershell
cd ParkingRealm
dotnet run --launch-profile http
```

API'et lytter på `http://localhost:5198`. Eksempler på kald ligger i `ParkingRealm/ParkingRealm.http`.

## Kør React-appen

```powershell
cd parking-realm
npm install
npm start
```

Appen kører på `http://localhost:3000` (den eneste origin API'et tillader i Development). API-adressen læses fra `REACT_APP_API_URL` og er som standard `http://localhost:5198`. Sæt den i en `.env.local` i `parking-realm/` for at pege et andet sted hen:

```
REACT_APP_API_URL=http://localhost:5198
```

## Login

Login bruger en cookie (Identity cookie authentication). API'et sætter en HttpOnly-cookie ved login, og React sender den med hvert kald via `credentials: 'include'`.

| Kald | Beskrivelse |
| --- | --- |
| `POST /api/auth/register` | Opret bruger |
| `POST /api/auth/login` | Log ind med `{ email, password }`. `200` og cookie, `401` ved forkerte oplysninger, `400` ved manglende felter |
| `GET /api/auth/me` | Den loggede ind bruger. `401` hvis ikke logget ind |

Cookie-login forudsætter, at React-appen og API'et ligger på samme *site*. Lokalt er `localhost:3000` og `localhost:5198` samme site, og API'et tillader kun `http://localhost:3000` med credentials i Development. Brug samme domæne/reverse proxy i produktion. Deployment på forskellige sites kræver særskilt konfiguration af både login- og antiforgery-cookies samt CORS og er ikke standardopsætningen.

Login og `/api/auth/me` returnerer også `isAdministrator`. Almindelige brugere får ingen administratorrolle. Der er endnu ikke logout eller lockout.

## Booking

| Kald | Beskrivelse |
| --- | --- |
| `GET /api/parking-spaces` | Offentlig, aktuel reservationsbaseret status med stabile ID'er og todimensionale `row`/`column`-koordinater |
| `GET /api/parking-spaces?start=...&end=...` | Status for det angivne tidsinterval |
| `GET /api/vehicles` | Den indloggede brugers køretøjer |
| `POST /api/vehicles` | Registrer eget køretøj med `{ "registrationNumber": "AB12345" }` |
| `GET /api/reservations` | Brugerens egne aktive og annullerede reservationer |
| `POST /api/reservations` | Opret booking med køretøj, tidsinterval og automatisk tildeling eller selvvalg |
| `POST /api/reservations/{id}/cancel` | Annuller egen aktive reservation; historikken bevares |
| `GET /api/antiforgery` | Hent `requestToken` som indlogget bruger; svaret sætter også en antiforgery-cookie |
| `GET /api/admin/booking-statistics?start=...&end=...` | Kun administrator: antal, aktive/annullerede og daglige tællinger |

Alle `POST`-kald under vehicles/reservations kræver login-cookien, antiforgery-cookien og headeren `X-CSRF-TOKEN` med den hentede `requestToken`. React-klienten håndterer dette automatisk. Ved direkte API-kald skal cookies bevares mellem login, tokenhentning og skrivning.

Eksempel på bookingens JSON-body (erstat ID'erne med værdier returneret af API'et og vælg et fremtidigt interval):

```json
{
  "vehicleId": 1,
  "start": "2099-01-01T10:00:00Z",
  "end": "2099-01-01T11:00:00Z",
  "mode": "manual",
  "parkingSpaceId": 1
}
```

Ved automatisk tildeling bruges `"mode": "automatic"` og `"parkingSpaceId": null`. API'et validerer ejerskab og tid, kontrollerer ledighed igen under en SQL Server-transaktion og bekræfter først efter commit. Overlappende bookinger på samme plads forhindres; et interval, der starter præcis når det foregående slutter, er tilladt. `400` betyder ugyldige kriterier eller token, `401` manglende login, `403` manglende administratorrettighed, og `409` ingen egnet plads eller konflikt. Annullering af en ukendt eller en anden brugers reservation giver `404`; gentagen annullering giver `409`.

Status er baseret på reservationer og er **ikke** en måling af fysisk belægning. Der er ingen sensorintegration. Oversigten opdateres ved ændrede kriterier, manuel opdatering og booking/annullering. Alle API-tider kræver en eksplicit tidszone og normaliseres til UTC. Statistikken tæller reservationer med start i rapportintervallet og grupperer efter UTC-startdato; `hasData: false` betyder intet datagrundlag, ikke en gyldig trend.

## Provisionering og deployment

1. Tag backup og anvend EF-migrations. Migrationen tilføjer bookingtabeller uden at fjerne eksisterende konti.
2. Indlæs det faktiske parkeringshus med [operatørens inventory-script](ParkingRealm/Data/Provisioning/README.md). Databasen er tom for pladser, indtil dette gøres; der opfindes ikke et standardlayout.
3. Registrer en administratorkonto normalt og følg [administrator-provisionering](ParkingRealm/Data/Provisioning/Administrator.md). Den betroede operatørkommando `dotnet run --project ParkingRealm --no-launch-profile -- --grant-admin admin@example.com` tildeler rollen og afslutter uden at starte webserveren. Kontoen skal logge ind igen bagefter. Der er ingen offentlig rolletildeling.
4. Deploy API og React sammen. Kontrollér login, antiforgery, to konkurrerende bookinger, annullering og administratoradgang mod SQL Server.
5. Ved rollback fjernes bookingadgangen/UI først; behold de additive tabeller og reservationernes historik til genopretning. Drop ikke bookingdata automatisk.

## Tests

```powershell
dotnet test ParkingRealm.Tests      # bruger en midlertidig LocalDB-database, ParkingRealm_Tests
cd parking-realm; npm test
```