-- Seed BulkItems data for NutriBite
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BulkItems')
BEGIN
    CREATE TABLE [dbo].[BulkItems] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(255) NOT NULL,
        [Category] NVARCHAR(100) NULL,
        [SubCategory] NVARCHAR(100) NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Price] DECIMAL(18,2) NOT NULL,
        [Weight] NVARCHAR(50) NULL,
        [IsVeg] BIT NULL,
        [MOQ] INT NULL,
        [ImagePath] NVARCHAR(255) NULL,
        [Status] NVARCHAR(50) NULL,
        [CreatedAt] DATETIME DEFAULT GETDATE(),
        [VendorId] INT NULL
    );
END
ELSE
BEGIN
    -- If table exists but VendorId column is missing, add it
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BulkItems') AND name = 'VendorId')
    BEGIN
        ALTER TABLE BulkItems ADD [VendorId] INT NULL;
    END
END

-- Clear existing demo data to avoid duplicates during setup
DELETE FROM BulkItems WHERE Status = 'Active';

-- Get a sample vendor ID
DECLARE @SampleVendorId INT;
SELECT TOP 1 @SampleVendorId = VendorId FROM VendorSignup WHERE IsApproved = 1;
IF @SampleVendorId IS NULL SET @SampleVendorId = 1;

-- 1. Bulk Meals
INSERT INTO BulkItems (Name, Category, Description, Price, IsVeg, MOQ, ImagePath, Status, VendorId)
VALUES 
('Office Thali Pack', 'Meals', 'Complete North Indian thali with 2 sabzi, dal, rice, and 4 rotis.', 180.00, 1, 10, '/images/bulk/bulk-meal1.webp', 'Active', @SampleVendorId),
('Executive Health Meal', 'Meals', 'Premium meal box with paneer, dal makhani, pulao and healthy fruit salad.', 250.00, 1, 5, '/images/bulk/bulk-meal1.webp', 'Active', @SampleVendorId),
('Chicken Curry Combo Bulk', 'Meals', 'Home-style chicken curry with steamed brown rice and salad.', 220.00, 0, 10, '/images/bulk/bulk-meal1.webp', 'Active', @SampleVendorId);

-- 2. Healthy Party Snacks
INSERT INTO BulkItems (Name, Category, Description, Price, IsVeg, MOQ, ImagePath, Status, VendorId)
VALUES 
('Hara Bhara Kabab Platter (50 pcs)', 'Snacks', 'Nutritious spinach and pea kababs served with mint chutney.', 450.00, 1, 1, '/images/bulk/harabharakabab.jpg', 'Active', @SampleVendorId),
('Whole Wheat Sandwich Box', 'Snacks', 'Mix of brown bread veg club and corn-spinach sandwiches.', 600.00, 1, 1, '/images/bulk/Brownbreadmayosandwich.jpg', 'Active', @SampleVendorId),
('Grilled Chicken Strips', 'Snacks', 'Tender grilled chicken strips with healthy herb dip.', 850.00, 0, 1, '/images/bulk/bulk-meal2.webp', 'Active', @SampleVendorId);

-- 3. Predefined Healthy Food Boxes
INSERT INTO BulkItems (Name, Category, Description, Price, IsVeg, MOQ, ImagePath, Status, VendorId)
VALUES 
('Nutri-Celebration Box', 'FoodBox_Predefined', 'Includes whole wheat paneer wrap, fruit bowl, fresh juice, and roasted makhana.', 350.00, 1, 15, '/images/bulk/fb1.jpg', 'Active', @SampleVendorId),
('Executive Seminar Box', 'FoodBox_Predefined', 'Healthy quinoa wrap, fruit bowl, and roasted nuts.', 280.00, 1, 20, '/images/bulk/fb2.jpg', 'Active', @SampleVendorId);

-- 4. Custom Healthy FoodBox Items
INSERT INTO BulkItems (Name, Category, SubCategory, Price, IsVeg, ImagePath, Status, VendorId)
VALUES 
('Fresh Orange Juice', 'FoodBox_Custom', 'Beverages', 60.00, 1, '/images/bulk/paperboat.jpg', 'Active', @SampleVendorId),
('Tender Coconut Water', 'FoodBox_Custom', 'Beverages', 80.00, 1, '/images/bulk/default.jpg', 'Active', @SampleVendorId),
('Date & Nut Energy Ball', 'FoodBox_Custom', 'Desserts', 120.00, 1, '/images/bulk/default.jpg', 'Active', @SampleVendorId),
('Fruit Skewers', 'FoodBox_Custom', 'Desserts', 90.00, 1, '/images/bulk/default.jpg', 'Active', @SampleVendorId),
('Avocado & Hummus Sandwich', 'FoodBox_Custom', 'Mains', 110.00, 1, '/images/bulk/Brownbreadmayosandwich.jpg', 'Active', @SampleVendorId),
('Grilled Chicken Wrap', 'FoodBox_Custom', 'Mains', 150.00, 0, '/images/bulk/bulk-meal3.webp', 'Active', @SampleVendorId);

PRINT 'BulkItems seeded successfully!';
