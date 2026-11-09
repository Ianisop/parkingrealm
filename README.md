# parkingrealm

ParkingRealm består af en ASP.NET Core minimal API (`ParkingRealm/`) med SQL Server via Entity Framework Core og Identity, og en React-frontend (`parking-realm/`).

## Forudsætninger

- .NET 10 SDK
- Node.js og npm
- SQL Server LocalDB (følger med Visual Studio) eller en anden SQL Server. Standardforbindelsen i `ParkingRealm/appsettings.Development.json` bruger `(localdb)\MSSQLLocalDB`.
- EF Core-værktøjet: `dotnet tool install --global dotnet-ef`

## Database

Databasen oprettes ud fra C#-modellerne med EF Core migrations. Kør fra roden af repoet:

```powershell
cd ParkingRealm
dotnet ef database update
```

Ny migration efter en modelændring: `dotnet ef migrations add <Navn> --output-dir Data/Migrations`.

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

## Tests

```powershell
dotnet test ParkingRealm.Tests      # bruger en midlertidig LocalDB-database, ParkingRealm_Tests
cd parking-realm; npm test
```