# Parking inventory provisioning

Run `ParkingSpaces.sql` using a trusted database operator connection after applying EF migrations. This is not a public API, and it must not be callable by ordinary application customers.

Supply the SQL parameter `@Inventory` as a JSON array containing the real garage's zero-based row/column coordinates. For interactive SQL Server Management Studio use, prepend a `DECLARE @Inventory nvarchar(max) = N'...';` statement containing your validated JSON. For example, `[{"row":0,"column":0},{"row":0,"column":1}]` describes two example positions only; replace it with the actual garage layout.

The script validates coordinates and rejects duplicate positions in the input. It adds missing positions atomically, is safe to rerun, and never changes existing IDs or removes spaces and reservations. Keep the approved inventory JSON with deployment configuration, not customer input. An empty array provisions nothing; a newly migrated database therefore has no bookable spaces until inventory is supplied.

Use a database backup and a separately reviewed migration for any future change that moves or removes existing physical spaces. Do not edit rows referenced by reservations ad hoc.
