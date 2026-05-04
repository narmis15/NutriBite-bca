SET QUOTED_IDENTIFIER ON;
DECLARE @OrderIds TABLE (OrderId INT);
INSERT INTO @OrderIds SELECT OrderId FROM OrderTable WHERE PaymentStatus = 'Pending' OR Status = 'Pending Payment' OR Status = 'Pending';
DELETE FROM OrderItems WHERE OrderId IN (SELECT OrderId FROM @OrderIds);
DELETE FROM VendorPayouts WHERE OrderId IN (SELECT OrderId FROM @OrderIds);
DELETE FROM Payment WHERE OrderId IN (SELECT OrderId FROM @OrderIds);
DELETE FROM DailyCalorieEntry WHERE OrderId IN (SELECT OrderId FROM @OrderIds);
DELETE FROM OrderTable WHERE OrderId IN (SELECT OrderId FROM @OrderIds);
