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

Booking samme dag kræver ikke en dags varsel: start skal blot være efter det aktuelle tidspunkt. Siden udfylder start med næste femminutters tidspunkt med mindst ét minuts buffer og slut med én time senere. **Book i dag** nulstiller til dette tidspunkt; tæt på midnat kan næste tidspunkt være i morgen. Genvejene **1 time**, **2 timer** og **4 timer** ændrer varigheden, og slut kan også angives manuelt. Når start ændres, bevares intervallets varighed.

Tidspunkter vises i browserens lokale tidszone og sendes til API'et som UTC. Bookingoversigten viser køretøj, interval, varighed og tildeling før bekræftelse. Et køretøj kan registreres direkte på siden og vælges automatisk. Login er fortsat påkrævet. Bekræftelse er kun tilgængelig med gyldige tider, et valgt køretøj og ledig kapacitet; selvvalg kræver også en ledig valgt plads. En konflikt opdaterer ledigheden og rydder pladsvalget, så brugeren kan prøve et andet tidsrum eller en anden plads. Den endelige bekræftelse viser plads og tider fra API'ets gemte reservation.

### Anbefaling af bedste plads

Ved køretøjsregistrering skal brugeren aktivt vælge **Ja – elbil** eller **Nej – ikke elbil** i feltet **Er køretøjet elektrisk?**; der er intet forhåndsvalg. Klassifikationen gemmes på køretøjet. Når en elbil vælges eller registreres, bliver standardpladser og ladepladser automatisk mulige uden ekstra afkrydsning. En ikke-elbil kan ikke booke en ladeplads. Ved skift af køretøj ryddes selvvalgt plads, og et ladekrav fjernes ved skift til en ikke-elbil.

Afkrydsningsfelterne **Jeg har brug for en handicapplads** og **Jeg har brug for en el-lader** angiver krav til pladsen. Ladekravet kan kun vælges for en elbil og begrænser valget til ladepladser; en elbil behøver altså ikke kræve opladning for at kunne benytte en ladeplads. Begge valgt kræver begge faciliteter på samme plads; en handicapplads og en separat ladeplads opfylder ikke kombinationen. Handicappladser er fortsat forbeholdt det tilsvarende behov. Filtreringen gælder både automatisk tildeling og selvvalg, og serveren håndhæver kravene ud fra det ejede køretøjs gemte klassifikation, ikke en elbil-påstand i booking-requesten. Ingen match giver en forklaring og ingen booking, aldrig en plads uden de krævede faciliteter. Der gemmes ikke medicinske oplysninger på brugerprofilen.

Ved **Automatisk** kan brugeren vælge **Foretrukken placering** nær en eksisterende plads. Algoritmen filtrerer først efter facilitetskrav og rangerer derefter efter Manhattan-afstand: `abs(row - foretrukkenRow) + abs(column - foretrukkenColumn)`, derefter laveste plads-ID ved samme afstand. Kun passende pladser, der er ledige i hele bookingintervallet, kan tildeles. Referencepladsen må gerne være reserveret eller have andre faciliteter; den angiver et ønsket område, ikke et krav om en bestemt plads. Uden præference bruges laveste passende ledige plads-ID.

Klienten viser og markerer et foreløbigt forslag. Serveren rangerer selv, låser én kandidat ad gangen og kontrollerer faciliteter og overlap igen før commit. Hvis den foretrukne kandidat er optaget, prøves den næste passende plads i rækkefølgen. Afviste kandidaters låse frigives før næste forsøg, så forskellige præferencer ikke holder kandidatlåse i modsat rækkefølge. Forslaget er derfor ikke en garanti; den gemte reservation er afgørende. Afstanden er kun afstand i gitteret, ikke målt gangafstand. Inventory gemmer `isAccessible` og `hasCharger`, men indeholder ikke indgange, priser eller køretøjsstørrelser.

De **første to** pladser efter stigende stabilt plads-ID markeres som handicappladser, og de **sidste fire** som ladepladser. Dette er rækkefølge, ikke hardkodede ID'er. Ved færre end seks pladser kan grupperne overlappe, så samme plads har begge faciliteter. På et 20-pladsers layout er de første to handicappladser, de midterste 14 standardpladser og de sidste fire ladepladser. Migrations-backfill og inventory-scriptet bruger samme regel; eksisterende reservationer flyttes eller slettes ikke.

| Kald | Beskrivelse |
| --- | --- |
| `GET /api/parking-spaces` | Offentlig, aktuel reservationsbaseret status med stabile ID'er og todimensionale `row`/`column`-koordinater |
| `GET /api/parking-spaces?start=...&end=...` | Status for det angivne tidsinterval |
| `GET /api/vehicles` | Den indloggede brugers køretøjer |
| `POST /api/vehicles` | Registrer eget køretøj med `{ "registrationNumber": "AB12345", "isElectric": true }`; eksplicit `true` eller `false` er påkrævet |
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

Det valgfrie felt `"preferredSpaceId": <eksisterende plads-ID>` angiver referenceplaceringen ved automatisk tildeling; udelad feltet eller brug `null` for ingen præference. Feltet accepteres ikke ved `manual`. Et ikke-positivt eller ukendt ID giver `400` med `errors.preferredSpaceId`, og ingen reservation oprettes. Der kræves ingen databasemigration for præferencen.

Booking-body accepterer også `"requiresAccessible": true/false` og `"requiresCharger": true/false` i begge tildelingsmåder. Begge har standardværdien `false`, hvis de udelades. `GET /api/parking-spaces` returnerer `isAccessible` og `hasCharger` for hver plads; `isAvailable` beskriver alene reservationsbaseret ledighed, ikke om pladsen matcher brugerens krav. Featurefelterne kræver migrationen `AddParkingSpaceFeatures` før deployment af den nye API-version.

Registrering og `GET /api/vehicles` returnerer også køretøjets `isElectric`. Manglende eller `null` klassifikation ved registrering giver `400` med `errors.isElectric`; `false` er et gyldigt, eksplicit svar. Ladekrav for en ikke-elbil giver `400` med `errors.requiresCharger`. Migrationen `AddVehicleElectricClassification` er påkrævet og sætter konservativt eksisterende køretøjer til ikke-elektriske uden at gætte ud fra nummerpladen. Eksisterende køretøjer og reservationer bevares; denne ændring introducerer ikke et køretøjsredigerings-endpoint.

Status er baseret på reservationer og er **ikke** en måling af fysisk belægning. Der er ingen sensorintegration. Oversigten opdateres ved ændrede kriterier, manuel opdatering og booking/annullering. Alle API-tider kræver en eksplicit tidszone og normaliseres til UTC. Statistikken tæller reservationer med start i rapportintervallet og grupperer efter UTC-startdato; `hasData: false` betyder intet datagrundlag, ikke en gyldig trend.

## Provisionering og deployment

1. Tag backup og anvend EF-migrations, herunder `AddParkingSpaceFeatures` og `AddVehicleElectricClassification`, før API-deployment. Migrationerne bevarer eksisterende ID'er, konti og reservationer. Eksisterende køretøjer klassificeres konservativt som ikke-elektriske.
2. Indlæs det faktiske parkeringshus i produktion med [operatørens inventory-script](ParkingRealm/Data/Provisioning/README.md). I Development migreres databasen ved opstart, og et tomt inventory får 20 eksempelpladser med de samme facilitetsregler. Produktion opretter ikke automatisk et layout.
3. Registrer en administratorkonto normalt og følg [administrator-provisionering](ParkingRealm/Data/Provisioning/Administrator.md). Den betroede operatørkommando `dotnet run --project ParkingRealm --no-launch-profile -- --grant-admin admin@example.com` tildeler rollen og afslutter uden at starte webserveren. Kontoen skal logge ind igen bagefter. Der er ingen offentlig rolletildeling.
4. Deploy API og React sammen. Kontrollér login, antiforgery, to konkurrerende bookinger, annullering og administratoradgang mod SQL Server.
5. Ved rollback fjernes bookingadgangen/UI først; behold de additive tabeller og reservationernes historik til genopretning. Drop ikke bookingdata automatisk.

## Tests

```powershell
dotnet test ParkingRealm.Tests      # bruger en midlertidig LocalDB-database, ParkingRealm_Tests
cd parking-realm; npm test
```