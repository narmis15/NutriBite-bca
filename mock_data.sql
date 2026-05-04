UPDATE VendorPayouts SET Status = 0 WHERE Status = 1 AND Id IN (7,8,9);
GO

INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (6, '2026-03-02 12:30:00', '2026-03-02 12:30:00', 'Delivered', 'Paid', 300, 270, 30, 2, 40, 15);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (420, '2026-03-05 14:15:00', '2026-03-05 14:15:00', 'Delivered', 'Paid', 450, 405, 45, 2, 40, 22.5);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (430, '2026-03-08 19:40:00', '2026-03-08 19:40:00', 'Delivered', 'Paid', 600, 540, 60, 3, 40, 30);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (500, '2026-03-11 11:20:00', '2026-03-11 11:20:00', 'Delivered', 'Paid', 800, 720, 80, 4, 40, 40);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (510, '2026-03-14 20:10:00', '2026-03-14 20:10:00', 'Delivered', 'Paid', 250, 225, 25, 4, 40, 12.5);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (6, '2026-03-15 13:45:00', '2026-03-15 13:45:00', 'Delivered', 'Paid', 500, 450, 50, 6, 40, 25);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (420, '2026-03-18 18:30:00', '2026-03-18 18:30:00', 'Delivered', 'Paid', 700, 630, 70, 7, 40, 35);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (430, '2026-03-20 09:15:00', '2026-03-20 09:15:00', 'Delivered', 'Paid', 900, 810, 90, 8, 40, 45);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (500, '2026-03-22 17:50:00', '2026-03-22 17:50:00', 'Delivered', 'Paid', 350, 315, 35, 9, 40, 17.5);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (510, '2026-03-25 15:20:00', '2026-03-25 15:20:00', 'Delivered', 'Paid', 550, 495, 55, 10, 40, 27.5);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (6, '2026-03-28 12:40:00', '2026-03-28 12:40:00', 'Delivered', 'Paid', 400, 360, 40, 3, 40, 20);
INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST) VALUES (420, '2026-03-30 21:05:00', '2026-03-30 21:05:00', 'Delivered', 'Paid', 650, 585, 65, 2, 40, 32.5);
GO

INSERT INTO Subscriptions (UserId, FoodId, VendorId, SubscriptionType, PricePerDelivery, StartDate, EndDate, Status, DeliveryTimePreference) VALUES (6, 1, 1, 'Monthly', 150, '2026-03-01 10:00:00', '2026-04-01 10:00:00', 'Active', 'Lunch');
INSERT INTO Subscriptions (UserId, FoodId, VendorId, SubscriptionType, PricePerDelivery, StartDate, EndDate, Status, DeliveryTimePreference) VALUES (430, 2, 2, 'Weekly', 120, '2026-03-10 14:30:00', '2026-03-17 14:30:00', 'Active', 'Dinner');
INSERT INTO Subscriptions (UserId, FoodId, VendorId, SubscriptionType, PricePerDelivery, StartDate, EndDate, Status, DeliveryTimePreference) VALUES (500, 1, 3, 'Monthly', 200, '2026-03-20 09:15:00', '2026-04-20 09:15:00', 'Active', 'Breakfast');
GO
