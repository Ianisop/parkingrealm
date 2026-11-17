-- Supply @Inventory as a JSON array of row/column coordinates.
SET XACT_ABORT ON;

IF ISJSON(@Inventory) <> 1 OR LEFT(LTRIM(@Inventory), 1) <> '['
	THROW 50001, 'Inventory must be a JSON array.', 1;

DECLARE @Spaces TABLE ([Row] int, [Column] int);
INSERT INTO @Spaces ([Row], [Column])
SELECT [Row], [Column]
FROM OPENJSON(@Inventory) WITH ([Row] int '$.row', [Column] int '$.column');

IF EXISTS (SELECT 1 FROM @Spaces WHERE [Row] IS NULL OR [Column] IS NULL OR [Row] < 0 OR [Column] < 0)
	THROW 50002, 'Each coordinate must be a nonnegative integer.', 1;

IF EXISTS (SELECT 1 FROM @Spaces GROUP BY [Row], [Column] HAVING COUNT(*) > 1)
	THROW 50003, 'Inventory coordinates must be unique.', 1;

BEGIN TRANSACTION;
INSERT INTO [ParkingSpaces] ([Row], [Column])
SELECT s.[Row], s.[Column]
FROM @Spaces s
WHERE NOT EXISTS (
	SELECT 1 FROM [ParkingSpaces] existing WITH (UPDLOCK, HOLDLOCK)
	WHERE existing.[Row] = s.[Row] AND existing.[Column] = s.[Column]
);
COMMIT TRANSACTION;
