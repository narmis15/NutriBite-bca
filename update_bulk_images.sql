UPDATE [FoodDeliveryDB].[dbo].[BulkItems]
SET ImagePath = '/images/Meals/standard1.jpeg' WHERE Name LIKE '%Corporate%' AND (ImagePath IS NULL OR ImagePath = '' OR ImagePath LIKE '%https%');

UPDATE [FoodDeliveryDB].[dbo].[BulkItems]
SET ImagePath = '/images/Meals/Standard_Thali.jpeg' WHERE (ImagePath IS NULL OR ImagePath = '' OR ImagePath LIKE '%https%');
