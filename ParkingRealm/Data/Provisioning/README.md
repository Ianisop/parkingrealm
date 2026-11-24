# Parking inventory provisioning

## Development schema recovery

Development uses the migration-managed LocalDB database `ParkingRealm_Development_Migrations`. The older local database named `ParkingRealm` can contain an incompatible schema with `Vehicles.UserId`, `Reservations.StartsAt`/`EndsAt`, and `ParkingSpaces.Label`/`Floor`. It is not interchangeable with this application's model.

If startup reports SQL error 2714 (`AspNetRoles` already exists), verify the effective connection string, including environment-variable and user-secret overrides, and compare `__EFMigrationsHistory` with the actual schema. Use the existing compatible development database; normal Development startup applies genuinely pending migrations. Changing a connection does not transfer accounts or bookings between databases.

Never drop a populated database, suppress migration errors, or insert migration-history rows just because tables exist. Back up legacy data and use a separately reviewed data migration if it must be transferred. Preserve extra tables and migration-history entries from other feature branches; switching branches is not a reason to run destructive rollback migrations.

Run `ParkingSpaces.sql` using a trusted database operator connection after applying EF migrations. This is not a public API, and it must not be callable by ordinary application customers.

Supply the SQL parameter `@Inventory` as a JSON array containing the real garage's zero-based row/column coordinates. For interactive SQL Server Management Studio use, prepend a `DECLARE @Inventory nvarchar(max) = N'...';` statement containing your validated JSON. For example, `[{"row":0,"column":0},{"row":0,"column":1}]` describes two example positions only; replace it with the actual garage layout.

The script validates coordinates and rejects duplicate positions in the input. It adds missing positions atomically, is safe to rerun, and never changes existing IDs or removes spaces and reservations. Keep the approved inventory JSON with deployment configuration, not customer input. An empty array provisions nothing; a newly migrated database therefore has no bookable spaces until inventory is supplied.

Apply the `AddParkingSpaceFeatures` migration before running this script. New coordinates are inserted in row/column order. After provisioning, the first two spaces by ascending stable ID are marked `IsAccessible`, and the last four are marked `HasCharger`; these are positions in ID order, not hardcoded IDs. Existing IDs and reservations remain unchanged. An inventory with fewer than six spaces may have spaces with both flags. The migration applies the same classification to inventory already in the database. Development startup also classifies its 20 example spaces; production still requires operator-provided inventory.

Rerunning provisioning reapplies the classification to the entire inventory. Adding spaces can therefore change which four spaces are last and have chargers. Review the physical layout and existing bookings before changing inventory, and do not treat these flags as live charger-status readings. Accessible spaces require the accessibility checkbox. Charging spaces automatically become eligible for a registered electric vehicle; the charging checkbox narrows selection to chargers when charging is required. Non-electric vehicles cannot use charging spaces. Selecting both requirements still requires both flags on one space.

Use a database backup and a separately reviewed migration for any future change that moves or removes existing physical spaces. Do not edit rows referenced by reservations ad hoc.
