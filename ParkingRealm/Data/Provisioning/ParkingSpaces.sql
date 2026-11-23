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
INSERT INTO [ParkingSpaces] ([Row], [Column], [IsAccessible], [HasCharger])
SELECT TOP (2147483647) s.[Row], s.[Column], 0, 0
FROM @Spaces s
WHERE NOT EXISTS (
	SELECT 1 FROM [ParkingSpaces] existing WITH (UPDLOCK, HOLDLOCK)
	WHERE existing.[Row] = s.[Row] AND existing.[Column] = s.[Column]
)
ORDER BY s.[Row], s.[Column];

WITH RankedSpaces AS (
	SELECT [Id],
		ROW_NUMBER() OVER (ORDER BY [Id]) AS [FirstPosition],
		ROW_NUMBER() OVER (ORDER BY [Id] DESC) AS [LastPosition]
	FROM [ParkingSpaces] WITH (UPDLOCK, HOLDLOCK)
)
UPDATE space
SET [IsAccessible] = CASE WHEN ranked.[FirstPosition] <= 2 THEN 1 ELSE 0 END,
	[HasCharger] = CASE WHEN ranked.[LastPosition] <= 4 THEN 1 ELSE 0 END
FROM [ParkingSpaces] space
INNER JOIN RankedSpaces ranked ON ranked.[Id] = space.[Id];
COMMIT TRANSACTION;
