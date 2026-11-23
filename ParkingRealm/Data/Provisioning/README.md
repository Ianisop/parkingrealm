# Parking inventory provisioning

## Development migration conflicts

Development startup applies EF migrations to the configured `DefaultConnection`; production migrations remain a deployment responsibility. SQL Server error 2714 (for example, `AspNetRoles` already exists) means a pending migration is trying to create an existing object. EF uses `__EFMigrationsHistory`, not the presence of application tables, to decide which migrations are pending.

Before changing anything, verify the effective connection (including user secrets and environment overrides) and inspect the target database using a trusted operator connection:

```sql
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
SELECT s.name AS SchemaName, o.name AS ObjectName, o.type_desc
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.name IN (N'AspNetRoles', N'__EFMigrationsHistory');
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
	SELECT MigrationId, ProductVersion
	FROM dbo.__EFMigrationsHistory
	ORDER BY MigrationId;
```

If existing data must be preserved, back up the database and compare its schema with every operation in the pending migrations, including columns, keys, indexes, and constraints. Restore valid migration history from a known matching backup, or have a database operator prepare a reviewed reconciliation. An existing `AspNetRoles` table alone does not establish that `InitialIdentity` (or any later migration) was fully applied. Do not insert migration-history rows blindly, swallow the exception, or replace migrations with `EnsureCreated`.

For disposable development data, point `DefaultConnection` at a new, unused database name and restart so migrations can build a consistent schema without deleting the old database. Accounts and bookings in the old database will not appear in the new database. Verify that configuration overrides do not still select the old database.

## Inventory

Run `ParkingSpaces.sql` using a trusted database operator connection after applying EF migrations. This is not a public API, and it must not be callable by ordinary application customers.

Supply the SQL parameter `@Inventory` as a JSON array containing the real garage's zero-based row/column coordinates. For interactive SQL Server Management Studio use, prepend a `DECLARE @Inventory nvarchar(max) = N'...';` statement containing your validated JSON. For example, `[{"row":0,"column":0},{"row":0,"column":1}]` describes two example positions only; replace it with the actual garage layout.

The script validates coordinates and rejects duplicate positions in the input. It adds missing positions atomically, is safe to rerun, and never changes existing IDs or removes spaces and reservations. Keep the approved inventory JSON with deployment configuration, not customer input. An empty array provisions nothing; a newly migrated database therefore has no bookable spaces until inventory is supplied.

Apply the `AddParkingSpaceFeatures` migration before running this script. New coordinates are inserted in row/column order. After provisioning, the first two spaces by ascending stable ID are marked `IsAccessible`, and the last four are marked `HasCharger`; these are positions in ID order, not hardcoded IDs. Existing IDs and reservations remain unchanged. An inventory with fewer than six spaces may have spaces with both flags. The migration applies the same classification to inventory already in the database. Development startup also classifies its 20 example spaces; production still requires operator-provided inventory.

Rerunning provisioning reapplies the classification to the entire inventory. Adding spaces can therefore change which four spaces are last and have chargers. Review the physical layout and existing bookings before changing inventory, and do not treat these flags as live charger-status readings. Accessible spaces require the accessibility checkbox. Charging spaces automatically become eligible for a registered electric vehicle; the charging checkbox narrows selection to chargers when charging is required. Non-electric vehicles cannot use charging spaces. Selecting both requirements still requires both flags on one space.

Use a database backup and a separately reviewed migration for any future change that moves or removes existing physical spaces. Do not edit rows referenced by reservations ad hoc.
