# Proposal

## Why

ParkingRealm has no way for a person to get an account. UC1 "Opret bruger" lets an unauthenticated user create a profile so they can later use the parking system's features. Every other use case depends on a user existing, so this is the first one to build.

## What Changes

- Add a registration endpoint to the ASP.NET Core minimal API that validates the submitted details, creates the user and stores it in the database.
- The user supplies email and password (the Identity login credentials) plus first name, last name and phone number (user data). All five are required.
- The phone number must include a country code (for example `+45`).
- Set up the data layer: SQL Server (MSSQL) via Entity Framework Core with ASP.NET Core Identity, including the initial migration.
- Add a registration page to the React frontend with a form, client-side validation, and success and error feedback.
- Reject invalid or missing details, and never create a profile from them. Tell the user when creation fails, including on system or database errors.
- Allow the React dev server to call the API (CORS in development).

Out of scope:
- Cars. They are a separate entity linked to the user and come in a later use case. Registration neither asks for nor creates cars.
- Login, sessions/tokens, email confirmation, roles and password reset.

## Capabilities

### New Capabilities
- `user-registration`: An unauthenticated user can create a profile (email, password, first name, last name, phone number with country code) through the UI. Details are validated, valid ones are persisted, invalid ones are rejected, and the user is told whether creation succeeded or failed.

### Modified Capabilities
<!-- None: openspec/specs/ is empty. -->

## Impact

- **Backend** (`ParkingRealm/`): new NuGet packages (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`), a `DbContext`, a user type with first and last name, `Program.cs` wiring, a registration endpoint, an EF migration, and a connection string in `appsettings*.json`.
- **Frontend** (`parking-realm/`): new registration page and API client call in the Create React App project.
- **Infrastructure**: requires a reachable SQL Server instance for development (for example LocalDB or a container).
- **APIs**: introduces the first API contract, `POST` register, which later use cases (login, cars) will build on.
