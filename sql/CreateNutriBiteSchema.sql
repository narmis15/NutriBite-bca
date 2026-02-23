-- NutriBite schema additions for calorie calculation & meal planning
-- Safe: CREATE TABLE statements wrapped in IF NOT EXISTS checks.
-- Adds FK constraints dynamically only if a suitable Users table and PK column are found.
-- Run as a user with permission to create tables and constraints.

SET NOCOUNT ON;
GO

--------------------------------------------------------------------------------
-- Detect existing Users table and primary key column name (Id / UserId)
--------------------------------------------------------------------------------
DECLARE @UserTableSchema SYSNAME = NULL;
DECLARE @UserTableName SYSNAME = NULL;
DECLARE @UserPkColumn SYSNAME = NULL;

-- Candidate tables to check (common names observed in codebase)
IF EXISTS (SELECT 1 FROM sys.tables t WHERE t.name = 'Users' AND SCHEMA_NAME(t.schema_id) = 'dbo')
BEGIN
    SET @UserTableSchema = 'dbo'; SET @UserTableName = 'Users';
END
ELSE IF EXISTS (SELECT 1 FROM sys.tables t WHERE t.name = 'UserSignup' AND SCHEMA_NAME(t.schema_id) = 'dbo')
BEGIN
    SET @UserTableSchema = 'dbo'; SET @UserTableName = 'UserSignup';
END
ELSE IF EXISTS (SELECT 1 FROM sys.tables t WHERE t.name = 'AspNetUsers' AND SCHEMA_NAME(t.schema_id) = 'dbo')
BEGIN
    SET @UserTableSchema = 'dbo'; SET @UserTableName = 'AspNetUsers';
END

IF @UserTableName IS NOT NULL
BEGIN
    IF EXISTS(SELECT 1 FROM sys.columns c
              WHERE c.object_id = OBJECT_ID(QUOTENAME(@UserTableSchema) + '.' + QUOTENAME(@UserTableName))
              AND c.name = 'Id')
        SET @UserPkColumn = 'Id';
    ELSE IF EXISTS(SELECT 1 FROM sys.columns c
                   WHERE c.object_id = OBJECT_ID(QUOTENAME(@UserTableSchema) + '.' + QUOTENAME(@UserTableName))
                   AND c.name = 'UserId')
        SET @UserPkColumn = 'UserId';
    ELSE IF EXISTS(SELECT 1 FROM sys.columns c
                   WHERE c.object_id = OBJECT_ID(QUOTENAME(@UserTableSchema) + '.' + QUOTENAME(@UserTableName))
                   AND c.name = 'UserID')
        SET @UserPkColumn = 'UserID';
    -- if no suitable pk column found, we'll skip adding FKs to user table
END

--------------------------------------------------------------------------------
-- 1) UserHealthProfiles
--------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserHealthProfiles]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.UserHealthProfiles
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,                  -- FK added later if Users table detected
        Age TINYINT NULL,
        Gender NVARCHAR(20) NULL,             -- 'Male', 'Female', 'Other'
        HeightCm FLOAT NULL,
        WeightKg FLOAT NULL,
        ActivityLevel NVARCHAR(50) NULL,      -- 'Sedentary', 'Light', 'Moderate', 'Active'
        Goal NVARCHAR(50) NULL,               -- 'Maintain', 'WeightLoss', 'WeightGain'
        TargetCalories INT NULL,              -- Calculated target calories
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE())
    );
    -- one-to-one per user
    CREATE UNIQUE INDEX IX_UserHealthProfiles_UserId ON dbo.UserHealthProfiles(UserId);
END

--------------------------------------------------------------------------------
-- 2) HealthConditions & mapping UserConditions
--------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HealthConditions]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.HealthConditions
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsActive BIT NOT NULL DEFAULT(1),
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE())
    );
    CREATE INDEX IX_HealthConditions_Name ON dbo.HealthConditions(Name);
END

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserConditions]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.UserConditions
    (
        UserId INT NOT NULL,         -- FK to Users table if detected (added later)
        ConditionId INT NOT NULL,    -- FK to HealthConditions.Id
        Severity NVARCHAR(50) NULL,  -- e.g. 'Mild', 'Moderate', 'Severe'
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE()),
        CONSTRAINT PK_UserConditions PRIMARY KEY (UserId, ConditionId)
    );
    CREATE INDEX IX_UserConditions_ConditionId ON dbo.UserConditions(ConditionId);
END

--------------------------------------------------------------------------------
-- 3) FoodItems Master Table
--------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FoodItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.FoodItems
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(250) NOT NULL,
        MealCategory NVARCHAR(50) NOT NULL,   -- Breakfast, Lunch, Snack, Dinner
        DietType NVARCHAR(50) NOT NULL,       -- Veg, NonVeg, Jain
        CaloriesPer100g FLOAT NOT NULL DEFAULT(0),
        ProteinPer100g FLOAT NOT NULL DEFAULT(0),
        CarbsPer100g FLOAT NOT NULL DEFAULT(0),
        FatsPer100g FLOAT NOT NULL DEFAULT(0),
        FiberPer100g FLOAT NOT NULL DEFAULT(0),
        SodiumPer100g FLOAT NOT NULL DEFAULT(0),
        CalciumPer100g FLOAT NOT NULL DEFAULT(0),
        PricePer100g DECIMAL(10,2) NOT NULL DEFAULT(0.00),
        IsActive BIT NOT NULL DEFAULT(1),
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE())
    );
    CREATE INDEX IX_FoodItems_Name ON dbo.FoodItems(Name);
    CREATE INDEX IX_FoodItems_MealCategory ON dbo.FoodItems(MealCategory);
END

--------------------------------------------------------------------------------
-- 4) Meals Table
--------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MealsMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.MealsMaster
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        MealName NVARCHAR(250) NOT NULL,
        TargetAudience NVARCHAR(50) NOT NULL,    -- Student, Elderly, Corporate
        MealPlanType NVARCHAR(100) NULL,         -- Budget, HighProtein, DiabeticFriendly, WeightLoss...
        IsCustomizable BIT NOT NULL DEFAULT(0),
        BasePrice DECIMAL(10,2) NOT NULL DEFAULT(0.00),
        Description NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE())
    );
    CREATE INDEX IX_MealsMaster_TargetAudience ON dbo.MealsMaster(TargetAudience);
END

--------------------------------------------------------------------------------
-- 5) MealItems Mapping Table
--------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MealItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.MealItems
    (
        MealId INT NOT NULL,          -- FK to MealsMaster.Id
        FoodItemId INT NOT NULL,      -- FK to FoodItems.Id
        QuantityInGrams INT NOT NULL, -- grams used in the meal
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE()),
        CONSTRAINT PK_MealItems PRIMARY KEY (MealId, FoodItemId)
    );
    CREATE INDEX IX_MealItems_FoodItemId ON dbo.MealItems(FoodItemId);
END

--------------------------------------------------------------------------------
-- 6) Subscriptions Table
--------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Subscriptions]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.Subscriptions
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,                -- FK to Users table if detected
        PlanType NVARCHAR(100) NOT NULL,    -- e.g. 'Monthly', 'Quarterly', 'Yearly' or 'Basic/Premium'
        StartDate DATE NOT NULL,
        EndDate DATE NULL,
        DailyCalorieTarget INT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT('Active'), -- Active, Cancelled, Expired
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE())
    );
    CREATE INDEX IX_Subscriptions_UserId ON dbo.Subscriptions(UserId);
    CREATE INDEX IX_Subscriptions_Status ON dbo.Subscriptions(Status);
END

--------------------------------------------------------------------------------
-- 7) Orders & OrderItems (create only if not already present)
--------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.Orders
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NULL,                      -- FK to Users table if detected
        OrderDate DATETIME2 NOT NULL DEFAULT(GETDATE()),
        Status NVARCHAR(50) NOT NULL DEFAULT('Pending'), -- Pending, Confirmed, Delivered, Cancelled
        TotalAmount DECIMAL(10,2) NOT NULL DEFAULT(0.00),
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE())
    );
    CREATE INDEX IX_Orders_UserId ON dbo.Orders(UserId);
    CREATE INDEX IX_Orders_OrderDate ON dbo.Orders(OrderDate);
END

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.OrderItems
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OrderId INT NOT NULL,             -- FK to Orders.Id
        MealId INT NULL,                  -- FK to MealsMaster.Id (nullable if user ordered ad-hoc FoodItems)
        FoodItemId INT NULL,              -- optional direct food item FK
        QuantityInGrams INT NOT NULL,
        UnitPrice DECIMAL(10,2) NOT NULL DEFAULT(0.00),
        LineTotal AS (QuantityInGrams * UnitPrice / 100.0) PERSISTED, -- convenience column (price per 100g * grams / 100)
        CreatedAt DATETIME2 NOT NULL DEFAULT(GETDATE())
    );
    CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
    CREATE INDEX IX_OrderItems_MealId ON dbo.OrderItems(MealId);
END

--------------------------------------------------------------------------------
-- Add foreign key constraints to detected user table and to internal tables
-- (We create them via ALTER TABLE using dynamic SQL only when appropriate)
--------------------------------------------------------------------------------
DECLARE @sql NVARCHAR(MAX);

-- Helper to add FK only if both tables/columns exist and FK not already present
CREATE TABLE #FkAddLog (Msg NVARCHAR(4000));
-- Add FK: UserHealthProfiles.UserId -> Users(PK)
IF @UserTableName IS NOT NULL AND @UserPkColumn IS NOT NULL
BEGIN
    -- UserHealthProfiles
    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserHealthProfiles]') AND type = 'U')
       AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[UserHealthProfiles]') AND name = 'UserId')
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[UserHealthProfiles]') AND fk.name = 'FK_UserHealthProfiles_Users')
    BEGIN
        SET @sql = N'ALTER TABLE dbo.UserHealthProfiles
                    ADD CONSTRAINT FK_UserHealthProfiles_Users FOREIGN KEY (UserId)
                    REFERENCES ' + QUOTENAME(@UserTableSchema) + '.' + QUOTENAME(@UserTableName) + '(' + QUOTENAME(@UserPkColumn) + ');';
        EXEC sp_executesql @sql;
        INSERT INTO #FkAddLog VALUES ('Added FK_UserHealthProfiles_Users');
    END

    -- UserConditions
    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserConditions]') AND type = 'U')
       AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[UserConditions]') AND name = 'UserId')
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[UserConditions]') AND fk.name = 'FK_UserConditions_Users')
    BEGIN
        SET @sql = N'ALTER TABLE dbo.UserConditions
                    ADD CONSTRAINT FK_UserConditions_Users FOREIGN KEY (UserId)
                    REFERENCES ' + QUOTENAME(@UserTableSchema) + '.' + QUOTENAME(@UserTableName) + '(' + QUOTENAME(@UserPkColumn) + ');';
        EXEC sp_executesql @sql;
        INSERT INTO #FkAddLog VALUES ('Added FK_UserConditions_Users');
    END

    -- Subscriptions
    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Subscriptions]') AND type = 'U')
       AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Subscriptions]') AND name = 'UserId')
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[Subscriptions]') AND fk.name = 'FK_Subscriptions_Users')
    BEGIN
        SET @sql = N'ALTER TABLE dbo.Subscriptions
                    ADD CONSTRAINT FK_Subscriptions_Users FOREIGN KEY (UserId)
                    REFERENCES ' + QUOTENAME(@UserTableSchema) + '.' + QUOTENAME(@UserTableName) + '(' + QUOTENAME(@UserPkColumn) + ');';
        EXEC sp_executesql @sql;
        INSERT INTO #FkAddLog VALUES ('Added FK_Subscriptions_Users');
    END

    -- Orders
    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND type = 'U')
       AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'UserId')
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[Orders]') AND fk.name = 'FK_Orders_Users')
    BEGIN
        SET @sql = N'ALTER TABLE dbo.Orders
                    ADD CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId)
                    REFERENCES ' + QUOTENAME(@UserTableSchema) + '.' + QUOTENAME(@UserTableName) + '(' + QUOTENAME(@UserPkColumn) + ');';
        EXEC sp_executesql @sql;
        INSERT INTO #FkAddLog VALUES ('Added FK_Orders_Users');
    END
END

-- Add FK: UserConditions.ConditionId -> HealthConditions.Id
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserConditions]') AND type = 'U')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[UserConditions]') AND name = 'ConditionId')
   AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HealthConditions]') AND type = 'U')
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[UserConditions]') AND fk.name = 'FK_UserConditions_HealthConditions')
BEGIN
    ALTER TABLE dbo.UserConditions
    ADD CONSTRAINT FK_UserConditions_HealthConditions FOREIGN KEY (ConditionId) REFERENCES dbo.HealthConditions(Id);
    INSERT INTO #FkAddLog VALUES ('Added FK_UserConditions_HealthConditions');
END

-- Add FK: MealItems -> MealsMaster, FoodItems
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MealItems]') AND type = 'U')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[MealItems]') AND fk.name = 'FK_MealItems_MealsMaster')
       AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MealsMaster]') AND type = 'U')
    BEGIN
        ALTER TABLE dbo.MealItems ADD CONSTRAINT FK_MealItems_MealsMaster FOREIGN KEY (MealId) REFERENCES dbo.MealsMaster(Id);
        INSERT INTO #FkAddLog VALUES ('Added FK_MealItems_MealsMaster');
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[MealItems]') AND fk.name = 'FK_MealItems_FoodItems')
       AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FoodItems]') AND type = 'U')
    BEGIN
        ALTER TABLE dbo.MealItems ADD CONSTRAINT FK_MealItems_FoodItems FOREIGN KEY (FoodItemId) REFERENCES dbo.FoodItems(Id);
        INSERT INTO #FkAddLog VALUES ('Added FK_MealItems_FoodItems');
    END
END

-- Add FK: OrderItems -> Orders, MealsMaster, FoodItems
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND type = 'U')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND fk.name = 'FK_OrderItems_Orders')
       AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND type = 'U')
    BEGIN
        ALTER TABLE dbo.OrderItems ADD CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id);
        INSERT INTO #FkAddLog VALUES ('Added FK_OrderItems_Orders');
    END

    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MealsMaster]') AND type = 'U')
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND fk.name = 'FK_OrderItems_MealsMaster')
    BEGIN
        ALTER TABLE dbo.OrderItems ADD CONSTRAINT FK_OrderItems_MealsMaster FOREIGN KEY (MealId) REFERENCES dbo.MealsMaster(Id);
        INSERT INTO #FkAddLog VALUES ('Added FK_OrderItems_MealsMaster');
    END

    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FoodItems]') AND type = 'U')
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[OrderItems]') AND fk.name = 'FK_OrderItems_FoodItems')
    BEGIN
        ALTER TABLE dbo.OrderItems ADD CONSTRAINT FK_OrderItems_FoodItems FOREIGN KEY (FoodItemId) REFERENCES dbo.FoodItems(Id);
        INSERT INTO #FkAddLog VALUES ('Added FK_OrderItems_FoodItems');
    END
END

-- Clean up
DROP TABLE #FkAddLog;

--------------------------------------------------------------------------------
-- Sample INSERTs for HealthConditions
--------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.HealthConditions WHERE Name = 'Diabetes')
    INSERT INTO dbo.HealthConditions (Name, Description) VALUES ('Diabetes', 'Diabetes mellitus - blood sugar regulation condition');

IF NOT EXISTS (SELECT 1 FROM dbo.HealthConditions WHERE Name = 'High Blood Pressure')
    INSERT INTO dbo.HealthConditions (Name, Description) VALUES ('High Blood Pressure', 'Hypertension');

IF NOT EXISTS (SELECT 1 FROM dbo.HealthConditions WHERE Name = 'Heart Condition')
    INSERT INTO dbo.HealthConditions (Name, Description) VALUES ('Heart Condition', 'Ischemic or other cardiac conditions');

IF NOT EXISTS (SELECT 1 FROM dbo.HealthConditions WHERE Name = 'Joint Pain')
    INSERT INTO dbo.HealthConditions (Name, Description) VALUES ('Joint Pain', 'Arthritis or other joint-related pain');

--------------------------------------------------------------------------------
-- Sample query: calculate total calories of a meal dynamically
-- Uses formula: SUM((QuantityInGrams / 100.0) * CaloriesPer100g)
-- Replace @MealId with the target meal id.
--------------------------------------------------------------------------------
-- Example usage:
-- DECLARE @MealId INT = 1;
-- SELECT m.Id AS MealId, m.MealName,
--        SUM( (mi.QuantityInGrams / 100.0) * fi.CaloriesPer100g ) AS TotalCalories
-- FROM dbo.MealsMaster m
-- JOIN dbo.MealItems mi ON mi.MealId = m.Id
-- JOIN dbo.FoodItems fi ON fi.Id = mi.FoodItemId
-- WHERE m.Id = @MealId
-- GROUP BY m.Id, m.MealName;



IF NOT EXISTS (SELECT 1 FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_MealCalories]'))
BEGIN
    EXEC('CREATE VIEW dbo.vw_MealCalories
    AS
    SELECT m.Id AS MealId, m.MealName,
           SUM( (mi.QuantityInGrams / 100.0) * fi.CaloriesPer100g ) AS TotalCalories,
           SUM( (mi.QuantityInGrams / 100.0) * fi.ProteinPer100g ) AS TotalProtein,
           SUM( (mi.QuantityInGrams / 100.0) * fi.CarbsPer100g ) AS TotalCarbs,
           SUM( (mi.QuantityInGrams / 100.0) * fi.FatsPer100g ) AS TotalFats
    FROM dbo.MealsMaster m
    JOIN dbo.MealItems mi ON mi.MealId = m.Id
    JOIN dbo.FoodItems fi ON fi.Id = mi.FoodItemId
    GROUP BY m.Id, m.MealName;');
END


-- Done
PRINT 'NutriBite schema creation script finished.';
GO