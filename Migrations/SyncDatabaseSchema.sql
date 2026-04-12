-- Sync database schema with recent model changes

-- 1. Update DailyCalorieEntry with MealType
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DailyCalorieEntry]') AND name = N'MealType')
BEGIN
    ALTER TABLE [DailyCalorieEntry] ADD [MealType] NVARCHAR(50) NOT NULL DEFAULT 'Other';
END

-- 2. Update OrderTable with TrackingProgress
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrderTable]') AND name = N'TrackingProgress')
BEGIN
    ALTER TABLE [OrderTable] ADD [TrackingProgress] INT NOT NULL DEFAULT 0;
END

-- 3. Update OrderTable with Version (if not already there)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrderTable]') AND name = N'Version')
BEGIN
    ALTER TABLE [OrderTable] ADD [Version] INT NOT NULL DEFAULT 1;
END

-- 4. Update Carttables with IsBulk
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Carttables]') AND name = N'IsBulk')
BEGIN
    ALTER TABLE [Carttables] ADD [IsBulk] BIT NOT NULL DEFAULT 0;
END

-- 5. Update OrderItems with BulkItemId
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrderItems]') AND name = N'BulkItemId')
BEGIN
    ALTER TABLE [OrderItems] ADD [BulkItemId] INT NULL;
END

PRINT 'Database schema sync completed successfully.';
GO
